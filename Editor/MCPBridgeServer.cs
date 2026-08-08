using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// HTTP server that runs inside the Unity Editor, enabling external MCP tools
    /// to control the editor via REST API calls.
    ///
    /// Commands use the asynchronous ticket queue:
    /// POST /api/queue/submit → poll GET /api/queue/status.
    /// </summary>
    [InitializeOnLoad]
    public static class MCPBridgeServer
    {
        private static HttpListener _listener;
        private static Thread _listenerThread;
        private static volatile bool _isRunning;

        /// <summary>
        /// The actual port this server is running on.
        /// Resolved at startup via auto-selection or manual override.
        /// </summary>
        private static int _activePort;

        /// <summary>The port this server is currently bound to (0 if not running).</summary>
        public static int ActivePort => _isRunning ? _activePort : 0;

        internal static IEnumerable<string> DeferredRouteNames => MCPDeferredRouteRegistry.RouteNames;

        // SessionState key to persist running state across domain reloads (Play Mode, recompile)
        private const string WasRunningKey = "UnityMCP_WasRunningBeforeReload";

        // Keep MCP work from monopolizing the first Editor update after a compile/domain reload.
        // Individual Unity API calls are not preemptible, so execute at most one queued request
        // per update and wait for the asset pipeline to remain idle briefly before resuming.
        internal const int MaxRequestsPerEditorUpdate = 1;
        internal const double PostReloadProcessingDelaySeconds = 0.5;
        private static double _requestProcessingNotBefore;
        private static volatile bool _queueReady;
        private static volatile string _busyReason;
        private const int MaxRequestBodyBytes = 2 * 1024 * 1024;

        // ─── Manual-port restart retry (unity-mcp-server issue #10) ───
        // Right after a domain reload the configured manual port can be briefly
        // unbindable while the previous listener's socket is released. Auto-port
        // mode survives this (it probes and falls back); manual mode had neither
        // probe nor retry and failed permanently. Retry the SAME port instead.
        private const int MaxManualPortRetries = 10;
        private const double ManualPortRetryDelaySeconds = 0.5;
        private static int _manualPortRetryCount;
        private static double _manualPortRetryAt;
        private static bool _manualPortRetryPending;

        /// <summary>
        /// Whether the MCP bridge may auto-start in this Editor. False on MPPM
        /// Virtual Players when StartOnVirtualPlayers is disabled (issue #21) —
        /// manual start is unaffected.
        /// </summary>
        private static bool AutoStartAllowed =>
            MCPSettingsManager.AutoStart &&
            (MCPSettingsManager.StartOnVirtualPlayers || !MCPScenarioCommands.IsVirtualPlayer());

        static MCPBridgeServer()
        {
            // Skip batch-mode Unity subprocesses (AssetImportWorker, CLI builds, etc.).
            // These are short-lived, don't need MCP access, and would otherwise claim
            // ports in the 7890-7899 range and exhaust availability for real editors.
            if (Application.isBatchMode) return;

            _requestProcessingNotBefore = EditorApplication.timeSinceStartup + PostReloadProcessingDelaySeconds;

            // Restart if: auto-start is allowed (respects the Virtual Player setting)
            // OR the server was running before a domain reload.
            bool wasRunning = SessionState.GetBool(WasRunningKey, false);
            if (AutoStartAllowed || wasRunning)
            {
                Start();
                SessionState.SetBool(WasRunningKey, false);
            }
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.quitting += OnQuitting;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        /// <summary>
        /// Handle Play Mode transitions to ensure the server stays alive.
        /// Unity triggers a domain reload when entering/exiting Play Mode,
        /// which is handled by the assembly reload callbacks and the SessionState flag.
        /// This callback provides additional resilience for edge cases.
        /// </summary>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                if (!_isRunning && (AutoStartAllowed || SessionState.GetBool(WasRunningKey, false)))
                {
                    Debug.Log("[MCP Bridge] Restarting server after Play Mode transition...");
                    Start();
                    SessionState.SetBool(WasRunningKey, false);
                }
            }
        }

        private static void OnBeforeAssemblyReload()
        {
            _queueReady = false;
            if (_isRunning)
            {
                // Persist that we were running, so we restart after reload
                SessionState.SetBool(WasRunningKey, true);
                MCPRequestQueue.PrepareForDomainReload();
                MCPInstanceRegistry.MarkReloading();
                Stop(false);
            }
        }

        private static void OnQuitting()
        {
            Stop();
        }

        /// <summary>Whether the server is currently running.</summary>
        public static bool IsRunning => _isRunning;

        internal static void SetBusyReason(string reason)
        {
            _busyReason = string.IsNullOrWhiteSpace(reason) ? null : reason;
        }

        public static void Start()
        {
            if (_isRunning) return;

            // Batch-mode subprocesses (AssetImportWorker, etc.) must never start the server.
            if (Application.isBatchMode) return;

            _queueReady = false;
            _requestProcessingNotBefore =
                EditorApplication.timeSinceStartup + PostReloadProcessingDelaySeconds;

            // Ensure console log capture is active before anything else
            MCPConsoleCommands.EnsureListening();

            // Clean up stale entries before selecting a port
            MCPInstanceRegistry.CleanupStaleEntries();

            // Resolve port: use manual override if set, otherwise auto-select
            int port;
            if (MCPSettingsManager.UseManualPort)
            {
                port = MCPSettingsManager.Port;
            }
            else
            {
                port = MCPInstanceRegistry.FindAvailablePort();
                if (port < 0)
                {
                    // No port available in the auto-select range -> give up cleanly.
                    // Without this guard the old retry logic would spin forever.
                    Debug.LogError(
                        $"[AB-UMCP] No available port in range {MCPInstanceRegistry.PortRangeStart}-{MCPInstanceRegistry.PortRangeEnd}. " +
                        "Close other Unity instances or set a manual port in MCP settings.");
                    return;
                }
            }

            HttpListener candidateListener = null;
            Thread candidateThread = null;
            try
            {
                candidateListener = new HttpListener();
                candidateListener.Prefixes.Add($"http://127.0.0.1:{port}/");
                candidateListener.Start();

                var boundListener = candidateListener;
                candidateThread = new Thread(() => ListenLoop(boundListener))
                {
                    IsBackground = true,
                    Name = "AB Unity MCP Server"
                };

                _listener = candidateListener;
                _listenerThread = candidateThread;
                _activePort = port;
                _isRunning = true;

                // Register and cache instance identity on the main thread before the
                // listener can serve the infrastructure ping endpoint.
                MCPInstanceRegistry.Register(port);
                candidateThread.Start();

                // Update the settings only after the complete listener transaction succeeds.
                MCPSettingsManager.Port = port;

                // Successful bind — clear any pending manual-port retry state.
                _manualPortRetryCount = 0;
                _manualPortRetryPending = false;

                Debug.Log($"[AB-UMCP] Server started on port {port}");
            }
            catch (Exception ex)
            {
                RollBackFailedStart(candidateListener, candidateThread, port);

                if (MCPSettingsManager.UseManualPort)
                {
                    // Manual port: do NOT fall back to another port — the user
                    // explicitly chose this one. The port is usually only briefly
                    // unavailable (socket release after a domain reload), so retry
                    // the SAME port a few times before giving up (issue #10).
                    if (_manualPortRetryCount < MaxManualPortRetries)
                    {
                        _manualPortRetryCount++;
                        Debug.LogWarning(
                            $"[AB-UMCP] Port {port} not yet available ({ex.Message}). " +
                            $"Retry {_manualPortRetryCount}/{MaxManualPortRetries} in {ManualPortRetryDelaySeconds:0.0}s...");
                        _manualPortRetryAt = EditorApplication.timeSinceStartup + ManualPortRetryDelaySeconds;
                        _manualPortRetryPending = true;
                    }
                    else
                    {
                        _manualPortRetryCount = 0;
                        Debug.LogError(
                            $"[AB-UMCP] Failed to start on port {port} after {MaxManualPortRetries} retries: {ex.Message}. " +
                            "Choose a different manual port in MCP settings, or switch to automatic port selection.");
                    }
                }
                else
                {
                    Debug.LogError($"[AB-UMCP] Failed to start on port {port}: {ex.Message}");

                    // Auto-port mode: fall back to another free port.
                    // Retry only if another port is actually free — the previous
                    // implementation retried whenever port < PortRangeEnd which
                    // caused an infinite loop when FindAvailablePort kept returning
                    // the same unavailable default port.
                    int nextPort = MCPInstanceRegistry.FindAvailablePort();
                    if (nextPort < 0 || nextPort == port)
                    {
                        Debug.LogError(
                            "[AB-UMCP] No alternative port available. Giving up to avoid a retry loop.");
                        return;
                    }

                    Debug.Log($"[AB-UMCP] Trying next available port {nextPort}...");
                    EditorApplication.delayCall += Start;
                }
            }
        }

        public static void Stop(bool unregisterInstance = true)
        {
            _queueReady = false;
            _isRunning = false;
            _activePort = 0;

            HttpListener listener = _listener;
            Thread listenerThread = _listenerThread;
            _listener = null;
            _listenerThread = null;

            // Cancel any pending manual-port restart retry.
            _manualPortRetryPending = false;
            _manualPortRetryCount = 0;

            CloseListener(listener, listenerThread);

            if (unregisterInstance)
                MCPInstanceRegistry.Unregister();

            Debug.Log("[AB-UMCP] Server stopped");
        }

        private static void RollBackFailedStart(HttpListener listener, Thread listenerThread, int port)
        {
            _queueReady = false;
            _isRunning = false;
            _activePort = 0;
            if (ReferenceEquals(_listener, listener))
                _listener = null;
            if (ReferenceEquals(_listenerThread, listenerThread))
                _listenerThread = null;

            CloseListener(listener, listenerThread);
            if (MCPInstanceRegistry.RegisteredPort == port)
                MCPInstanceRegistry.Unregister();
        }

        private static void CloseListener(HttpListener listener, Thread listenerThread)
        {
            if (listener != null)
            {
                try
                {
                    listener.Stop();
                }
                catch (ObjectDisposedException)
                {
                    // Already closed by another lifecycle edge.
                }
                catch (HttpListenerException)
                {
                    // Listener shutdown can race a blocked GetContext call.
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AB-UMCP] Failed to stop HTTP listener cleanly: {ex.Message}");
                }

                try
                {
                    listener.Close();
                }
                catch (ObjectDisposedException)
                {
                    // Close is intentionally idempotent.
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AB-UMCP] Failed to dispose HTTP listener cleanly: {ex.Message}");
                }
            }

            if (listenerThread == null || listenerThread == Thread.CurrentThread || !listenerThread.IsAlive)
                return;

            try
            {
                if (!listenerThread.Join(1000))
                    Debug.LogWarning("[AB-UMCP] HTTP listener thread did not stop within 1 second.");
            }
            catch (ThreadStateException ex)
            {
                Debug.LogWarning($"[AB-UMCP] Failed to join HTTP listener thread: {ex.Message}");
            }
        }

        // ─── EditorApplication.update — processes the ticket queue on the main thread ───

        private static void OnEditorUpdate()
        {
            // 0. Manual-port restart retry (issue #10): the manual port can be
            //    briefly unbindable after a domain reload — retry on a short delay.
            if (_manualPortRetryPending && !_isRunning &&
                EditorApplication.timeSinceStartup >= _manualPortRetryAt)
            {
                _manualPortRetryPending = false;
                Start();
            }

            double now = EditorApplication.timeSinceStartup;
            if (!_isRunning || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                _queueReady = false;
                _requestProcessingNotBefore = now + PostReloadProcessingDelaySeconds;
                return;
            }

            if (now < _requestProcessingNotBefore)
            {
                _queueReady = false;
                return;
            }

            // Limit main-thread MCP work to one request per Editor update. Backlogged
            // clients are still served fairly by MCPRequestQueue across later frames.
            _queueReady = true;
            MCPRequestQueue.ProcessNextRequests(MaxRequestsPerEditorUpdate);
        }

        // ─── HTTP Listener ───

        private static void ListenLoop(HttpListener listener)
        {
            while (_isRunning && ReferenceEquals(_listener, listener))
            {
                try
                {
                    var context = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
                }
                catch (HttpListenerException) when (!_isRunning || !ReferenceEquals(_listener, listener))
                {
                    break;
                }
                catch (ThreadAbortException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    if (_isRunning && ReferenceEquals(_listener, listener))
                        Debug.LogError($"[AB-UMCP] Listener error: {ex.Message}");
                }
            }
        }

        // ─── Request Handler ───

        private static void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                string path = request.Url.AbsolutePath.TrimStart('/');
                if (!path.StartsWith("api/"))
                {
                    SendJson(response, 404, MCPResponse.Error("Not found.", "not_found"));
                    return;
                }

                string apiPath = path.Substring(4); // Remove "api/"
                string origin = request.Headers["Origin"];
                if (!string.IsNullOrEmpty(origin))
                {
                    SendJson(response, 403, MCPResponse.Error(
                        "Browser-originated requests are not accepted by the Unity MCP bridge.",
                        "forbidden_origin"));
                    return;
                }

                if (!IsHttpMethodAllowed(apiPath, request.HttpMethod))
                {
                    SendJson(response, 405, MCPResponse.Error(
                        $"HTTP {request.HttpMethod} is not allowed for '{apiPath}'.",
                        "method_not_allowed"));
                    return;
                }

                if (request.ContentLength64 > MaxRequestBodyBytes)
                {
                    SendJson(response, 413, MCPResponse.Error(
                        "Request body exceeded the Unity MCP bridge limit.",
                        "request_too_large", false, new Dictionary<string, object>
                        {
                            { "actualBytes", request.ContentLength64 },
                            { "limitBytes", MaxRequestBodyBytes },
                        }));
                    return;
                }

                string body = "";
                if (request.HasEntityBody)
                {
                    if (!TryReadRequestBody(request, out body, out int bodyBytes))
                    {
                        SendJson(response, 413, MCPResponse.Error(
                            "Request body exceeded the Unity MCP bridge limit.",
                            "request_too_large", false, new Dictionary<string, object>
                            {
                                { "actualBytes", bodyBytes },
                                { "limitBytes", MaxRequestBodyBytes },
                            }));
                        return;
                    }
                }

                string agentId = request.Headers["X-Agent-Id"] ?? "anonymous";
                var requestArgs = ParseJson(body);
                AddExpectedProjectHeaders(request, requestArgs);
                requestArgs["_agentId"] = agentId;
                string requestId = request.Headers["Idempotency-Key"] ?? request.Headers["X-Request-Id"];
                if (string.IsNullOrEmpty(requestId))
                    requestId = GetArgumentString(requestArgs, "requestId");
                if (!string.IsNullOrEmpty(requestId))
                    requestArgs["_requestId"] = requestId;
                body = MiniJson.Serialize(requestArgs);
                if (TryBuildProjectMismatchResponse(apiPath, requestArgs, out var projectMismatch))
                {
                    SendJson(response, 409, projectMismatch);
                    return;
                }

                // Instance discovery needs one non-command liveness endpoint before a
                // client has selected a target. All executable routes still use queue/submit.
                if (apiPath == "ping")
                {
                    SendJson(response, 200, BuildPingResponse());
                    return;
                }

                // ═══ Queue endpoints (async, non-blocking) ═══
                if (apiPath == "queue/submit")
                {
                    if (!_queueReady)
                    {
                        SendJson(response, 503, MCPResponse.Error(
                            "Unity Editor is still compiling, importing, or warming up its main-thread queue. Retry this submission.",
                            "editor_warming_up",
                            true,
                            new Dictionary<string, object>
                            {
                                { "queueReady", false },
                            }));
                        return;
                    }
                    HandleQueueSubmit(response, agentId, body);
                    return;
                }
                if (apiPath == "queue/status")
                {
                    HandleQueueStatus(response, request, requestArgs, agentId);
                    return;
                }
                if (apiPath == "queue/cancel")
                {
                    HandleQueueCancel(response, agentId, requestArgs);
                    return;
                }
                if (apiPath == "queue/info")
                {
                    SendJson(response, 200, BuildQueueInfoResponse());
                    return;
                }

                SendJson(response, 404, MCPResponse.Error(
                    "Direct command endpoints are not supported. Submit commands through queue/submit.",
                    "queue_submit_required"));
            }
            catch (FormatException ex)
            {
                SendJson(response, 400, MCPResponse.Error(ex.Message, "invalid_json"));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SendJson(response, 500, MCPResponse.Error(ex.Message, "bridge_internal_error"));
            }
        }

        private static bool TryReadRequestBody(
            HttpListenerRequest request,
            out string body,
            out int bodyBytes)
        {
            body = "";
            bodyBytes = 0;
            var builder = new StringBuilder();
            var buffer = new char[8192];
            using (var reader = new StreamReader(
                       request.InputStream,
                       request.ContentEncoding,
                       true,
                       buffer.Length,
                       leaveOpen: true))
            {
                int charactersRead;
                while ((charactersRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    bodyBytes += Encoding.UTF8.GetByteCount(buffer, 0, charactersRead);
                    if (bodyBytes > MaxRequestBodyBytes ||
                        builder.Length + charactersRead > MaxRequestBodyBytes)
                    {
                        return false;
                    }
                    builder.Append(buffer, 0, charactersRead);
                }
            }

            body = builder.ToString();
            return true;
        }

        private static bool IsHttpMethodAllowed(string apiPath, string method)
        {
            if (string.IsNullOrEmpty(apiPath) || string.IsNullOrEmpty(method))
                return false;

            switch (apiPath)
            {
                case "ping":
                case "queue/status":
                case "queue/info":
                    return string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);
                case "queue/submit":
                case "queue/cancel":
                    return string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase);
                default:
                    // Unknown/direct command paths still reach the normal structured
                    // queue_submit_required response instead of being mislabeled as 405.
                    return true;
            }
        }

        // ─── Queue Submit (async) ───

        private static void HandleQueueSubmit(HttpListenerResponse response, string agentId, string body)
        {
            try
            {
                var args = ParseJson(body);
                string apiPath = args.ContainsKey("apiPath") ? args["apiPath"].ToString() : "";
                string innerBody = args.ContainsKey("body") ? args["body"].ToString() : "";

                if (string.IsNullOrEmpty(apiPath))
                {
                    SendJson(response, 400, MCPResponse.Error(
                        "apiPath is required in the request body.",
                        "invalid_arguments"));
                    return;
                }

                var innerArgs = ParseJson(innerBody);
                innerArgs["_agentId"] = agentId;
                CopyArgumentIfMissing(args, innerArgs, "expectedProjectPath");
                CopyArgumentIfMissing(args, innerArgs, "expectedProjectName");
                if (TryBuildProjectMismatchResponse(apiPath, innerArgs, out var projectMismatch))
                {
                    SendJson(response, 409, projectMismatch);
                    return;
                }

                string requestId = GetArgumentString(args, "_requestId");
                if (string.IsNullOrEmpty(requestId))
                    requestId = GetArgumentString(args, "requestId");
                if (string.IsNullOrEmpty(requestId))
                    requestId = GetArgumentString(innerArgs, "requestId");
                if (!string.IsNullOrEmpty(requestId))
                    innerArgs["_requestId"] = requestId;
                string requestKey = string.IsNullOrEmpty(requestId)
                    ? null
                    : agentId + "|" + apiPath + "|" + requestId;

                // Deferred handlers execute directly from the queue callback. Consume their
                // transport envelope here after validation and request-key derivation. Normal
                // persistent routes must retain it until RouteRequest performs its own
                // main-thread target validation.
                RemoveTransportArgumentsForDirectQueueDispatch(apiPath, innerArgs);
                innerBody = MiniJson.Serialize(innerArgs);

                MCPRequestQueue.RequestTicket ticket;
                bool reused = false;
                if (apiPath == "wait/editor-idle")
                {
                    ticket = MCPRequestQueue.SubmitResumableEditorIdleWait(agentId, innerArgs, out reused);
                }
                else if (MCPDeferredRouteRegistry.TryGet(apiPath, out var deferredHandler))
                {
                    ticket = MCPRequestQueue.SubmitPersistentDeferredRequest(agentId, apiPath,
                        (resolve, progress) =>
                        {
                            var deferredArguments = ParseJson(innerBody);
                            MCPToolConfigurationPolicy.ApplyDefaults(
                                apiPath, deferredArguments);
                            deferredHandler(
                                deferredArguments, resolve, progress);
                        },
                        innerBody, requestKey, out reused);
                }
                else
                {
                    ticket = MCPRequestQueue.SubmitPersistentRequest(agentId, apiPath, "POST", innerBody,
                        requestKey, out reused);
                }

                // Return immediately with ticket info
                SendJson(response, 202, new Dictionary<string, object>
                {
                    { "ticketId",      ticket.TicketId },
                    { "status",        ticket.Status.ToString() },
                    { "queuePosition", ticket.QueuePosition },
                    { "agentId",       agentId },
                    { "reused",        reused },
                });
            }
            catch (FormatException ex)
            {
                SendJson(response, 400, MCPResponse.Error(ex.Message, "invalid_json"));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SendJson(response, 500, MCPResponse.Error(
                    $"Queue submit failed: {ex.Message}", "queue_submit_failed"));
            }
        }

        // ─── Queue Status (polling) ───

        private static void HandleQueueStatus(HttpListenerResponse response, HttpListenerRequest request,
            Dictionary<string, object> args, string agentId)
        {
            string ticketIdStr = request.QueryString["ticketId"];
            if (string.IsNullOrEmpty(ticketIdStr))
                ticketIdStr = GetArgumentString(args, "ticketId");
            if (string.IsNullOrEmpty(ticketIdStr) || !long.TryParse(ticketIdStr, out long ticketId))
            {
                SendJson(response, 400, MCPResponse.Error(
                    "ticketId must be supplied as a valid integer query parameter.",
                    "invalid_arguments"));
                return;
            }

            var status = MCPRequestQueue.GetTicketStatus(ticketId, agentId, true);
            if (status == null)
            {
                SendJson(response, 404, MCPResponse.Error(
                    $"Ticket {ticketId} was not found or has expired.",
                    "ticket_not_found"));
                return;
            }

            SendJson(response, 200, status);
        }

        private static void HandleQueueCancel(HttpListenerResponse response, string agentId,
            Dictionary<string, object> args)
        {
            if (args == null || !args.TryGetValue("ticketId", out object value) || value == null ||
                !long.TryParse(value.ToString(), out long ticketId))
            {
                SendJson(response, 400, MCPResponse.Error("ticketId is required.", "invalid_arguments"));
                return;
            }
            object result = MCPRequestQueue.CancelTicket(ticketId, agentId);
            SendJson(response, MCPResponse.TryGetError(result, out _, out _, out _) ? 409 : 200, result);
        }

        // ─── Route Request (runs on main thread) ───

        private static string ExtractCategory(string path)
        {
            int slash = path.IndexOf('/');
            return slash > 0 ? path.Substring(0, slash) : path;
        }

        private static string GetArgumentString(Dictionary<string, object> args, string key)
        {
            if (args == null || args.TryGetValue(key, out var value) == false || value == null)
                return "";
            return value.ToString();
        }

        private static void CopyArgumentIfMissing(Dictionary<string, object> source,
            Dictionary<string, object> destination, string key)
        {
            if (source == null || destination == null || destination.ContainsKey(key) ||
                !source.TryGetValue(key, out object value) || value == null)
                return;
            destination[key] = value;
        }

        private static void RemoveConsumedTransportArguments(string route,
            Dictionary<string, object> args)
        {
            if (args == null)
                return;

            string normalizedRoute = (route ?? "").Trim('/');
            bool routeOwnsTargetBinding =
                string.Equals(normalizedRoute, "instance/assert-project", StringComparison.Ordinal);
            if (!routeOwnsTargetBinding)
            {
                args.Remove("expectedProjectPath");
                args.Remove("expectedProjectName");
            }

            if (!RouteConsumesRequestId(normalizedRoute))
                args.Remove("_requestId");
        }

        private static void RemoveTransportArgumentsForDirectQueueDispatch(string route,
            Dictionary<string, object> args)
        {
            string normalizedRoute = (route ?? "").Trim('/');
            if (string.Equals(normalizedRoute, "wait/editor-idle", StringComparison.Ordinal) ||
                MCPDeferredRouteRegistry.Contains(normalizedRoute))
                RemoveConsumedTransportArguments(normalizedRoute, args);
        }

        private static bool RouteConsumesRequestId(string normalizedRoute)
        {
            return string.Equals(normalizedRoute, "asset/refresh", StringComparison.Ordinal) ||
                   string.Equals(normalizedRoute, "asset/import-unitypackage", StringComparison.Ordinal) ||
                   normalizedRoute.StartsWith(MCPProjectToolCommands.DirectRoutePrefix,
                       StringComparison.Ordinal);
        }

        private static void AddExpectedProjectHeaders(HttpListenerRequest request, Dictionary<string, object> args)
        {
            if (request == null || args == null)
                return;

            if (args.ContainsKey("expectedProjectPath") == false)
            {
                string expectedProjectPath = request.Headers["X-UnityMCP-Expected-Project-Path"];

                if (!string.IsNullOrEmpty(expectedProjectPath))
                    args["expectedProjectPath"] = expectedProjectPath;
            }

            if (args.ContainsKey("expectedProjectName") == false)
            {
                string expectedProjectName = request.Headers["X-UnityMCP-Expected-Project-Name"];

                if (!string.IsNullOrEmpty(expectedProjectName))
                    args["expectedProjectName"] = expectedProjectName;
            }
        }

        private static bool TryBuildProjectMismatchResponse(string route, Dictionary<string, object> args,
            out object response)
        {
            response = null;
            if (ShouldSkipProjectValidation(route))
                return false;

            string expectedProjectPath = MCPInstanceCommands.GetExpectedProjectPath(args);
            string expectedProjectName = GetArgumentString(args, "expectedProjectName");

            if (string.IsNullOrEmpty(expectedProjectPath) && string.IsNullOrEmpty(expectedProjectName))
            {
                if (!MCPToolMetadata.RouteRequiresTargetBinding(route))
                    return false;

                response = MCPResponse.Error(
                    "Mutating requests must bind to a Unity project by expectedProjectPath or expectedProjectName.",
                    "target_project_required", false, new Dictionary<string, object>
                {
                    { "route", route },
                    { "actualProjectPath", MCPInstanceRegistry.CurrentProjectPath },
                    { "actualProjectName", MCPInstanceRegistry.CurrentProjectName },
                    { "actualPort", ActivePort },
                    { "currentInstance", MCPInstanceRegistry.GetCurrentInstanceInfo() }
                });
                return true;
            }

            response = MCPInstanceCommands.BuildProjectMismatch(expectedProjectPath, expectedProjectName, route);
            return response != null;
        }

        private static bool ShouldSkipProjectValidation(string route)
        {
            if (string.IsNullOrEmpty(route))
                return true;

            route = route.Trim('/');
            if (route.StartsWith("_meta/", StringComparison.Ordinal))
                return true;

            switch (route)
            {
                case "ping":
                case "queue/status":
                case "queue/info":
                case "queue/cancel":
                // The outer transport envelope cannot be classified until
                // HandleQueueSubmit parses apiPath. The inner route is validated
                // immediately afterwards, so writes still require project binding.
                case "queue/submit":
                case "instance/current":
                case "instance/list":
                case "instance/resolve":
                case "instance/assert-project":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Route API requests to the appropriate handler.
        /// NOTE: This entire method runs on the main thread through
        /// MCPRequestQueue.ProcessNextRequests, so all Unity APIs work correctly.
        /// </summary>
        private static object RouteRequest(string path, string method, string body)
        {
            var configuredArguments = ParseJson(body);
            MCPToolConfigurationPolicy.ApplyDefaults(path, configuredArguments);
            body = MiniJson.Serialize(configuredArguments);

            // ─── Meta endpoints (no category check) ───
            if (path == "_meta/tools")
            {
                var args = configuredArguments;
                object value;
                bool compact = !args.TryGetValue("compact", out value) ||
                               value == null || Convert.ToBoolean(value);
                bool includeSchema = args.TryGetValue("includeSchema", out value) &&
                                     value != null && Convert.ToBoolean(value);
                bool includeMetadataIssues =
                    args.TryGetValue("includeMetadataIssues", out value) &&
                    value != null && Convert.ToBoolean(value);
                int offset = args.TryGetValue("offset", out value) && value != null
                    ? Convert.ToInt32(value)
                    : 0;
                int limit = args.TryGetValue("limit", out value) && value != null
                    ? Convert.ToInt32(value)
                    : 50;
                string metadataCategory = args.TryGetValue("category", out value) ? value?.ToString() : null;
                return MCPToolMetadata.GetRegisteredTools(compact, includeSchema,
                    offset, limit, metadataCategory, includeMetadataIssues);
            }
            if (path == "_meta/capabilities")
            {
                return MCPCapabilityRegistry.GetCapabilities();
            }

            if (TryBuildProjectMismatchResponse(path, ParseJson(body), out var projectMismatch))
            {
                return projectMismatch;
            }

            RemoveConsumedTransportArguments(path, configuredArguments);
            body = MiniJson.Serialize(configuredArguments);

            // Project context reads EditorPrefs and project paths, so it must run through
            // this main-thread router instead of directly on the HTTP listener thread.
            if (path == "context")
            {
                return MCPContextManager.GetContextResponse();
            }
            if (path.StartsWith("context/", StringComparison.Ordinal))
            {
                string contextCategory = path.Substring("context/".Length);
                return MCPContextManager.GetContextResponse(contextCategory);
            }

            // Check if category is enabled
            string category = ExtractCategory(path);
            if (category != "ping" && category != "agents" && category != "queue"
                && !MCPSettingsManager.IsCategoryEnabled(category))
            {
                return new { error = $"Category '{category}' is currently disabled. Enable it in Window > AB Unity MCP." };
            }

            if (MCPProjectToolCommands.TryExecuteDirectRoute(path, ParseJson(body), out var projectToolResult))
            {
                return projectToolResult;
            }

            if (!MCPRouteRegistry.ContainsBuiltInRoute(path))
            {
                return MCPResponse.Error($"Unknown MCP route '{path}'.", "unknown_route");
            }

            return MCPBuiltInRouteDispatcher.Dispatch(path, configuredArguments);
        }

        // ─── Helpers ───

        internal static Dictionary<string, object> BuildPingResponse()
        {
            var response = MCPInstanceRegistry.GetCurrentInstanceInfo();
            response["status"] = "ok";
            AddQueueAvailability(response);
            return response;
        }

        private static Dictionary<string, object> BuildQueueInfoResponse()
        {
            var response = MCPRequestQueue.GetQueueInfo();
            AddQueueAvailability(response);
            return response;
        }

        private static void AddQueueAvailability(Dictionary<string, object> response)
        {
            response["queueReady"] = _queueReady;
            string busyReason = _busyReason;
            if (!string.IsNullOrEmpty(busyReason))
                response["busyReason"] = busyReason;
        }

        private static Dictionary<string, object> ParseJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new Dictionary<string, object>();

            var parsed = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (parsed == null)
                throw new FormatException("Request body must be a JSON object.");
            return parsed;
        }

        // Response size limits (bytes) — prevents oversized payloads from crashing the MCP stdio pipe
        private const int ResponseSoftLimitBytes = 512 * 1024;
        private const int ResponseHardLimitBytes = 2 * 1024 * 1024;

        internal static void SendJson(HttpListenerResponse response, int statusCode, object data)
        {
            data = PrepareJsonResponseForTransport(statusCode, data);
            string json = MiniJson.Serialize(data);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            // Size validation — protect against Write EOF on large projects
            if (buffer.Length > ResponseHardLimitBytes)
            {
                Debug.LogWarning($"[AB-UMCP] Response too large ({buffer.Length / (1024 * 1024)}MB), replacing with error. Use pagination parameters.");
                var errorData = MCPResponse.Error(
                    "Response exceeded size limit. Use pagination parameters (maxNodes, limit, maxResults) to request smaller chunks.",
                    "response_too_large", false, new Dictionary<string, object>
                    {
                        { "actualBytes", buffer.Length },
                        { "limitBytes", ResponseHardLimitBytes },
                    });
                data = MCPResponse.CompactForTransport(errorData);
                json = MiniJson.Serialize(data);
                buffer = Encoding.UTF8.GetBytes(json);
                statusCode = 413; // Payload Too Large
            }
            else if (buffer.Length > ResponseSoftLimitBytes)
            {
                Debug.LogWarning($"[AB-UMCP] Large response ({buffer.Length / (1024 * 1024)}MB). Consider using pagination parameters.");
            }

            try
            {
                response.StatusCode = statusCode;
                response.ContentType = "application/json";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (HttpListenerException)
            {
                // The client disconnected while the response was being written.
            }
            catch (IOException)
            {
                // The response stream was closed by the remote client.
            }
            catch (ObjectDisposedException)
            {
                // Bridge shutdown disposed this in-flight response.
            }
            finally
            {
                try
                {
                    response.OutputStream.Close();
                }
                catch (HttpListenerException) { }
                catch (IOException) { }
                catch (ObjectDisposedException) { }
            }
        }

        internal static object PrepareJsonResponseForTransport(int statusCode,
            object data)
        {
            if (statusCode >= 400 ||
                MCPResponse.TryGetError(data, out _, out _, out _))
            {
                data = MCPResponse.NormalizeError(data,
                    statusCode == 408 ? "timeout" : "error",
                    statusCode == 408);
            }

            return MCPResponse.CompactForTransport(data);
        }

        internal static object ExecutePersistedRoute(string path, string method, string body)
        {
            return RouteRequest(path, string.IsNullOrEmpty(method) ? "POST" : method, body ?? "");
        }

        internal static bool IsDeferredRoute(string path)
        {
            return MCPDeferredRouteRegistry.Contains(path);
        }

        internal static void ExecutePersistedDeferredRoute(string path, string body, Action<object> resolve,
            Action<object> progress)
        {
            if (!MCPDeferredRouteRegistry.TryGet(path, out var handler))
            {
                resolve(MCPResponse.Error($"Deferred route was not found: '{path}'.", "route_not_found"));
                return;
            }
            handler(ParseJson(body), resolve, progress);
        }

        private static string GetProjectPath()
        {
            string dataPath = Application.dataPath;
            return dataPath.Substring(0, dataPath.Length - "/Assets".Length);
        }
    }

}
