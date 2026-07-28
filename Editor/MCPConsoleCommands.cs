using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    public static class MCPConsoleCommands
    {
        // Store log messages via Application.logMessageReceivedThreaded
        private static readonly List<LogEntry> _logEntries = new List<LogEntry>();
        private static bool _isListening = false;
        private static bool _playModeHooked = false;
        private static DateTime _lastPlayStartedAt = DateTime.MinValue;
        private const int MaxEntries = 1000;

        private struct LogEntry
        {
            public string message;
            public string stackTrace;
            public LogType type;
            public DateTime timestamp;
        }

        // ─── Compilation error buffer (independent of console log) ───
        // Populated via CompilationPipeline.assemblyCompilationFinished.
        // Cleared automatically at the start of each new compilation cycle.
        // Not affected by console Clear().
        private static readonly List<CompilationError> _compilationErrors = new List<CompilationError>();
        private static readonly HashSet<string> DeprecatedDiagnosticCodes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CS0612",
                "CS0618",
                "CS0619",
                "CS0672",
                "CS0809",
            };
        private static readonly Regex DiagnosticCodePattern =
            new Regex(@"\b(?:CS|SYSLIB)\d{4}\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private const string CompilationSessionStateKey = "UnityMCP.CompilationDiagnostics.v1";
        private const int MaxCompilationEntries = 1000;
        private static bool _compilationHooked = false;

        private struct CompilationError
        {
            public string file;
            public int line;
            public int column;
            public string message;
            public string severity; // "error" or "warning"
            public string code;
            public bool isDeprecated;
            public string assembly;
            public DateTime timestamp;
        }

        // Static constructor — runs at editor load thanks to [InitializeOnLoad]
        static MCPConsoleCommands()
        {
            EnsureListening();
            RestoreCompilationDiagnostics();
            EnsureCompilationHook();
            EnsurePlayModeHook();
        }

        /// <summary>
        /// Start capturing console messages. Safe to call multiple times.
        /// Called automatically at editor load AND when the bridge server starts.
        /// </summary>
        public static void EnsureListening()
        {
            if (_isListening) return;
            // Use logMessageReceivedThreaded to capture messages from ALL threads,
            // not just the main thread. This catches async compilation errors,
            // background job failures, etc.
            Application.logMessageReceivedThreaded += OnLogMessage;
            _isListening = true;
        }

        public static void EnsurePlayModeHook()
        {
            if (_playModeHooked) return;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            if (EditorApplication.isPlaying && _lastPlayStartedAt == DateTime.MinValue)
                _lastPlayStartedAt = DateTime.Now;
            _playModeHooked = true;
        }

        /// <summary>
        /// Hook into CompilationPipeline to capture compiler messages (errors/warnings)
        /// independently from the console log buffer. Safe to call multiple times.
        /// </summary>
        public static void EnsureCompilationHook()
        {
            if (_compilationHooked) return;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            _compilationHooked = true;
        }

        private static void OnCompilationStarted(object context)
        {
            // Fresh compilation cycle — clear previous results
            lock (_compilationErrors) { _compilationErrors.Clear(); }
            PersistCompilationDiagnostics();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
                _lastPlayStartedAt = DateTime.Now;
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            // Extract assembly name from path (e.g. "Library/ScriptAssemblies/Assembly-CSharp.dll" → "Assembly-CSharp")
            string asmName = System.IO.Path.GetFileNameWithoutExtension(assemblyPath);

            lock (_compilationErrors)
            {
                foreach (var msg in messages)
                {
                    // Only capture errors and warnings, skip info
                    if (msg.type != CompilerMessageType.Error && msg.type != CompilerMessageType.Warning)
                        continue;

                    string message = msg.message ?? "";
                    string severity = msg.type == CompilerMessageType.Error ? "error" : "warning";
                    string code = ExtractDiagnosticCode(message);
                    _compilationErrors.Add(new CompilationError
                    {
                        file = msg.file ?? "",
                        line = msg.line,
                        column = msg.column,
                        message = message,
                        severity = severity,
                        code = code,
                        isDeprecated = IsDeprecatedDiagnostic(severity, code, message),
                        assembly = asmName,
                        timestamp = DateTime.Now,
                    });
                }

                if (_compilationErrors.Count > MaxCompilationEntries)
                {
                    _compilationErrors.RemoveRange(0, _compilationErrors.Count - MaxCompilationEntries);
                }
            }

            PersistCompilationDiagnostics();
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            lock (_logEntries)
            {
                _logEntries.Add(new LogEntry
                {
                    message = message,
                    stackTrace = stackTrace,
                    type = type,
                    timestamp = DateTime.Now,
                });

                // Keep max entries capped
                if (_logEntries.Count > MaxEntries)
                    _logEntries.RemoveRange(0, _logEntries.Count - MaxEntries);
            }
        }

        public static object Query(Dictionary<string, object> args)
        {
            EnsureListening();
            EnsurePlayModeHook();

            int count = Math.Max(1, Math.Min(GetInt(args, "count", 50), 200));
            int offset = Math.Max(0, GetInt(args, "offset", 0));
            string typeFilter = GetString(args, "type", "all").ToLowerInvariant();
            string messageContains = GetString(args, "messageContains", "");
            string sourceContains = GetString(args, "sourceContains", "");
            string stackContains = GetString(args, "stackContains", "");
            bool includeStack = GetBool(args, "includeStack", false);
            bool sinceLastPlay = GetBool(args, "sinceLastPlay", false);
            bool newestFirst = GetBool(args, "newestFirst", false);

            DateTime? since = GetDateTime(args, "since");
            DateTime? until = GetDateTime(args, "until");
            double secondsAgo = GetDouble(args, "sinceSecondsAgo", -1d);
            if (secondsAgo >= 0d)
                since = MaxDateTime(since, DateTime.Now.AddSeconds(-secondsAgo));
            if (sinceLastPlay && _lastPlayStartedAt != DateTime.MinValue)
                since = MaxDateTime(since, _lastPlayStartedAt);

            var matchingEntries = new List<Dictionary<string, object>>();
            lock (_logEntries)
            {
                for (int i = _logEntries.Count - 1; i >= 0; i--)
                {
                    var entry = _logEntries[i];
                    if (!MatchesLogType(entry.type, typeFilter))
                        continue;
                    if (since.HasValue && entry.timestamp < since.Value)
                        continue;
                    if (until.HasValue && entry.timestamp > until.Value)
                        continue;
                    if (!ContainsIgnoreCase(entry.message, messageContains))
                        continue;
                    if (!ContainsIgnoreCase(entry.stackTrace, stackContains))
                        continue;

                    string source = ExtractSource(entry.stackTrace);
                    if (!ContainsIgnoreCase(source, sourceContains))
                        continue;

                    var result = new Dictionary<string, object>
                    {
                        { "message", entry.message },
                        { "type", entry.type.ToString().ToLowerInvariant() },
                        { "timestamp", entry.timestamp.ToString("o") },
                        { "source", source },
                    };

                    if (includeStack)
                        result["stackTrace"] = entry.stackTrace ?? "";

                    matchingEntries.Add(result);
                }
            }

            int totalMatches = matchingEntries.Count;
            var entries = matchingEntries.Skip(offset).Take(count).ToList();
            if (!newestFirst)
                entries.Reverse();
            int nextOffset = offset + entries.Count;

            return new Dictionary<string, object>
            {
                { "count", entries.Count },
                { "entries", entries },
                { "offset", offset },
                { "limit", count },
                { "totalMatches", totalMatches },
                { "hasMore", nextOffset < totalMatches },
                { "nextOffset", nextOffset < totalMatches ? (object)nextOffset : null },
                { "truncated", nextOffset < totalMatches },
                { "lastPlayStartedAt", _lastPlayStartedAt == DateTime.MinValue ? "" : _lastPlayStartedAt.ToString("o") },
                { "sinceLastPlay", sinceLastPlay },
            };
        }

        /// <summary>
        /// Get compilation errors/warnings captured via CompilationPipeline.
        /// Independent of the console log buffer — not affected by Clear().
        /// </summary>
        public static object GetCompilationErrors(Dictionary<string, object> args)
        {
            EnsureCompilationHook();

            int count = Math.Max(1, Math.Min(GetInt(args, "count", 50), 200));
            string severityFilter = NormalizeCompilationSeverity(GetString(args, "severity", "all"));
            List<CompilationError> snapshot = GetCompilationSnapshot();
            var entries = snapshot
                .Where(entry => severityFilter == "all" || entry.severity == severityFilter)
                .Reverse()
                .Take(count)
                .Reverse()
                .Select(BuildCompilationEntry)
                .ToList();
            int entryTotal = snapshot.Count(entry =>
                severityFilter == "all" || entry.severity == severityFilter);

            var response = BuildCompilationDiagnosticsSummary(snapshot, count);
            response["entries"] = entries;
            if (entries.Count < entryTotal)
                response["entryTotal"] = entryTotal;
            return response;
        }

        public static Dictionary<string, object> GetCompilationDiagnosticsSummary(int deprecatedWarningLimit = 50)
        {
            EnsureCompilationHook();
            int limit = Math.Max(1, Math.Min(deprecatedWarningLimit, 200));
            return BuildCompilationDiagnosticsSummary(GetCompilationSnapshot(), limit);
        }

        public static object Clear()
        {
            EnsureListening();
            lock (_logEntries) { _logEntries.Clear(); }
            return new { success = true, message = "Console log buffer cleared" };
        }

        private static Dictionary<string, object> BuildCompilationDiagnosticsSummary(
            List<CompilationError> snapshot, int deprecatedWarningLimit)
        {
            int errorCount = snapshot.Count(entry => entry.severity == "error");
            int warningCount = snapshot.Count(entry => entry.severity == "warning");
            int deprecatedWarningCount = snapshot.Count(entry =>
                entry.severity == "warning" && entry.isDeprecated);
            var deprecatedWarnings = snapshot
                .Where(entry => entry.severity == "warning" && entry.isDeprecated)
                .Reverse()
                .Take(deprecatedWarningLimit)
                .Reverse()
                .Select(BuildCompilationEntry)
                .ToList();

            var result = new Dictionary<string, object>
            {
                { "isCompiling", EditorApplication.isCompiling },
                { "counts", new Dictionary<string, object>
                    {
                        { "errors", errorCount },
                        { "warnings", warningCount },
                    }
                },
                { "deprecatedWarnings", deprecatedWarnings },
            };

            if (deprecatedWarnings.Count < deprecatedWarningCount)
                result["deprecatedWarningTotal"] = deprecatedWarningCount;

            return result;
        }

        private static Dictionary<string, object> BuildCompilationEntry(CompilationError entry)
        {
            return new Dictionary<string, object>
            {
                { "file", entry.file },
                { "line", entry.line },
                { "column", entry.column },
                { "message", entry.message },
                { "severity", entry.severity },
                { "code", entry.code },
                { "isDeprecated", entry.isDeprecated },
                { "assembly", entry.assembly },
                { "timestamp", entry.timestamp.ToString("HH:mm:ss.fff") },
            };
        }

        private static List<CompilationError> GetCompilationSnapshot()
        {
            lock (_compilationErrors)
            {
                return new List<CompilationError>(_compilationErrors);
            }
        }

        private static string NormalizeCompilationSeverity(string value)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? "all" : value.Trim().ToLowerInvariant();
            return normalized == "error" || normalized == "warning" ? normalized : "all";
        }

        private static string ExtractDiagnosticCode(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "";

            Match match = DiagnosticCodePattern.Match(message);
            return match.Success ? match.Value.ToUpperInvariant() : "";
        }

        private static bool IsDeprecatedDiagnostic(string severity, string code, string message)
        {
            if (!string.Equals(severity, "warning", StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.IsNullOrEmpty(code) && DeprecatedDiagnosticCodes.Contains(code))
                return true;

            return ContainsIgnoreCase(message, "obsolete") || ContainsIgnoreCase(message, "deprecated");
        }

        private static void PersistCompilationDiagnostics()
        {
            try
            {
                List<object> serializedEntries;
                lock (_compilationErrors)
                {
                    serializedEntries = _compilationErrors.Select(entry => (object)new Dictionary<string, object>
                    {
                        { "file", entry.file },
                        { "line", entry.line },
                        { "column", entry.column },
                        { "message", entry.message },
                        { "severity", entry.severity },
                        { "code", entry.code },
                        { "isDeprecated", entry.isDeprecated },
                        { "assembly", entry.assembly },
                        { "timestamp", entry.timestamp.ToString("O") },
                    }).ToList();
                }

                SessionState.SetString(CompilationSessionStateKey, MiniJson.Serialize(serializedEntries));
            }
            catch
            {
                // Compilation diagnostics must never interfere with Unity's compilation callback.
            }
        }

        private static void RestoreCompilationDiagnostics()
        {
            try
            {
                string serialized = SessionState.GetString(CompilationSessionStateKey, "");
                if (string.IsNullOrEmpty(serialized) ||
                    MiniJson.Deserialize(serialized) is not List<object> serializedEntries)
                {
                    return;
                }

                var restored = new List<CompilationError>();
                foreach (object serializedEntry in serializedEntries)
                {
                    if (serializedEntry is not Dictionary<string, object> entry)
                        continue;

                    string message = GetString(entry, "message", "");
                    string severity = NormalizeCompilationSeverity(GetString(entry, "severity", "warning"));
                    if (severity == "all")
                        severity = "warning";
                    string code = GetString(entry, "code", "");
                    if (string.IsNullOrEmpty(code))
                        code = ExtractDiagnosticCode(message);
                    if (!DateTime.TryParse(GetString(entry, "timestamp", ""), out DateTime timestamp))
                        timestamp = DateTime.Now;

                    restored.Add(new CompilationError
                    {
                        file = GetString(entry, "file", ""),
                        line = GetInt(entry, "line", 0),
                        column = GetInt(entry, "column", 0),
                        message = message,
                        severity = severity,
                        code = code,
                        isDeprecated = GetBool(entry, "isDeprecated",
                            IsDeprecatedDiagnostic(severity, code, message)),
                        assembly = GetString(entry, "assembly", ""),
                        timestamp = timestamp,
                    });
                }

                lock (_compilationErrors)
                {
                    _compilationErrors.Clear();
                    _compilationErrors.AddRange(restored.Skip(Math.Max(0, restored.Count - MaxCompilationEntries)));
                }
            }
            catch
            {
                // Ignore stale or malformed SessionState from an older package version.
            }
        }

        private static bool MatchesLogType(LogType type, string typeFilter)
        {
            if (string.IsNullOrEmpty(typeFilter) || typeFilter == "all")
                return true;
            if (typeFilter == "error")
                return type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            if (typeFilter == "warning")
                return type == LogType.Warning;
            if (typeFilter == "info")
                return type == LogType.Log;
            if (typeFilter == "exception")
                return type == LogType.Exception;
            if (typeFilter == "assert")
                return type == LogType.Assert;

            return string.Equals(type.ToString(), typeFilter, StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractSource(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
                return "";

            var lines = stackTrace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                int atIndex = line.IndexOf(" (at ", StringComparison.Ordinal);
                if (atIndex >= 0)
                    return line.Substring(atIndex + 5).TrimEnd(')');
            }

            return lines.Length > 0 ? lines[0].Trim() : "";
        }

        private static bool ContainsIgnoreCase(string value, string filter)
        {
            return string.IsNullOrEmpty(filter) ||
                   (value != null && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string GetString(Dictionary<string, object> args, string key, string defaultValue)
        {
            return args != null && args.ContainsKey(key) && args[key] != null
                ? args[key].ToString()
                : defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return defaultValue;
            if (args[key] is bool value)
                return value;

            return bool.TryParse(args[key].ToString(), out bool parsed) ? parsed : defaultValue;
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return defaultValue;

            return int.TryParse(args[key].ToString(), out int value) ? value : defaultValue;
        }

        private static double GetDouble(Dictionary<string, object> args, string key, double defaultValue)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return defaultValue;

            return double.TryParse(args[key].ToString(), out double value) ? value : defaultValue;
        }

        private static DateTime? GetDateTime(Dictionary<string, object> args, string key)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return null;

            string value = args[key].ToString();
            if (double.TryParse(value, out double numeric))
            {
                long unixValue = Convert.ToInt64(numeric);
                if (unixValue > 100000000000)
                    return DateTimeOffset.FromUnixTimeMilliseconds(unixValue).LocalDateTime;
                if (unixValue > 1000000000)
                    return DateTimeOffset.FromUnixTimeSeconds(unixValue).LocalDateTime;
            }

            return DateTime.TryParse(value, out DateTime parsed) ? parsed : null;
        }

        private static DateTime? MaxDateTime(DateTime? current, DateTime candidate)
        {
            return !current.HasValue || candidate > current.Value ? candidate : current;
        }
    }
}
