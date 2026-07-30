using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor
{
    public static class MCPToolMetadata
    {
        private static List<string> _cachedRoutes;
        private static List<Dictionary<string, object>> _cachedTools;
        private static List<Dictionary<string, object>> _cachedFirstClassTools;

        private const string ExposureFirstClass = "first-class";
        private const string ExposureFallback = "fallback";
        private const string ExposureLazy = "lazy";

        private sealed class ToolProfile
        {
            public string Exposure;
            public bool Preferred;
            public bool ReadOnly;
            public bool MutatesAssets;
            public bool MutatesRuntime;
            public bool Dangerous;
            public bool LongRunning;
            public bool MayReloadDomain;
            public bool RequiresPlayMode;

            public static ToolProfile FirstClass(bool readOnly = false, bool mutatesAssets = false,
                bool mutatesRuntime = false,
                bool dangerous = false, bool longRunning = false, bool mayReloadDomain = false,
                bool requiresPlayMode = false)
            {
                return new ToolProfile
                {
                    Exposure = ExposureFirstClass,
                    Preferred = true,
                    ReadOnly = readOnly,
                    MutatesAssets = mutatesAssets,
                    MutatesRuntime = mutatesRuntime,
                    Dangerous = dangerous,
                    LongRunning = longRunning,
                    MayReloadDomain = mayReloadDomain,
                    RequiresPlayMode = requiresPlayMode,
                };
            }

            public static ToolProfile Fallback()
            {
                return new ToolProfile
                {
                    Exposure = ExposureFallback,
                    Preferred = false,
                    ReadOnly = false,
                    MutatesAssets = true,
                    MutatesRuntime = false,
                    Dangerous = true,
                    LongRunning = false,
                    MayReloadDomain = false,
                    RequiresPlayMode = false,
                };
            }

            public static ToolProfile Lazy()
            {
                return new ToolProfile
                {
                    Exposure = ExposureLazy,
                    Preferred = false,
                    ReadOnly = false,
                    MutatesAssets = false,
                    MutatesRuntime = false,
                    Dangerous = false,
                    LongRunning = false,
                    MayReloadDomain = false,
                    RequiresPlayMode = false,
                };
            }

            public ToolProfile Clone()
            {
                return new ToolProfile
                {
                    Exposure = Exposure,
                    Preferred = Preferred,
                    ReadOnly = ReadOnly,
                    MutatesAssets = MutatesAssets,
                    MutatesRuntime = MutatesRuntime,
                    Dangerous = Dangerous,
                    LongRunning = LongRunning,
                    MayReloadDomain = MayReloadDomain,
                    RequiresPlayMode = RequiresPlayMode,
                };
            }

            public Dictionary<string, object> ToAnnotations()
            {
                var annotations = new Dictionary<string, object>();
                if (ReadOnly)
                {
                    annotations["readOnlyHint"] = true;
                    annotations["idempotentHint"] = true;
                }
                if (Dangerous)
                    annotations["destructiveHint"] = true;
                return annotations;
            }
        }

        private static readonly HashSet<string> FirstClassRouteAllowlist =
            new HashSet<string>(StringComparer.Ordinal)
            {
                // Canonical metadata for server-core tools. These replace same-named
                // fallbacks without increasing the public tool count.
                "asset/import",
                "asset/list",
                "asset/refresh",
                "build/get-job",
                "build/start",
                "compilation/errors",
                "component/set-property",
                "editor/play-mode",
                "editor/execute-code",
                "queue/info",
                "search/scene",
                "scene/hierarchy",
                "screenshot/game",

                // Small release-managed surface for common structured workflows.
                "packages/list",
                "packages/update-git",
                "wait/editor-idle",
                "scene/instantiate-prefab",
                "serialized-object/get",
                "serialized-object/set",
                "component/set-reference",
                "prefab-asset/configure-component",
                "asset/get-refresh-job",
                "asset/rename",
                "asset/move",
                "console/query",
                "uitoolkit/runtime-query",
                "uitoolkit/refresh",
                "uitoolkit/edit-uxml",
                "uitoolkit/edit-uss",
                "testing/run-tests",
                "testing/get-job",
                "project-tools/list",
                "project-tools/get",
                "project-tools/execute",
                "jobs/get",
                "jobs/cancel",
                "jobs/cleanup",
                "asset/import-settings/get",
                "asset/import-settings/set",
                "scene/workspace",
                "material/properties/get",
                "material/properties/set",
            };

        private static readonly Dictionary<string, ToolProfile> ToolProfiles = BuildToolProfiles();

        private static Dictionary<string, ToolProfile> BuildToolProfiles()
        {
            var profiles = new Dictionary<string, ToolProfile>(StringComparer.Ordinal);

            AddProfile(profiles, ToolProfile.FirstClass(readOnly: true),
                "_meta/capabilities",
                "_meta/tools",
                "context",
                "context/*",
                "queue/info",
                "queue/status",
                "search/scene",
                "compilation/errors",
                "asset/list",
                "scene/hierarchy",
                "serialized-object/get",
                "prefab-asset/get-properties",
                "prefab-asset/hierarchy",
                "prefab-asset/find",
                "console/query",
                "uitoolkit/asset-inspect",
                "uitoolkit/runtime-documents",
                "uitoolkit/runtime-tree",
                "uitoolkit/runtime-query",
                "uitoolkit/runtime-style",
                "uitoolkit/locate-element",
                "uitoolkit/capture-element",
                "uitoolkit/compare-element",
                "localization/status",
                "localization/locales",
                "localization/collections",
                "localization/entries",
                "localization/validate",
                "localization/variables",
                "packages/list",
                "packages/info",
                "packages/status",
                "packages/lint-metas",
                "texture/info",
                "texture/find-duplicates",
                "animation/transition-info",
                "asset/dependencies",
                "asset/get-refresh-job",
                "asset/import-settings/get",
                "build/get-job",
                "jobs/list",
                "jobs/get",
                "material/properties/get",
                "audio-mixer/info",
                "vfxgraph/info",
                "addressables/info",
                "timeline/info",
                "cinemachine/info",
                "testing/get-job",
                "testing/get-package-job",
                "profiler/stats",
                "profiler/memory",
                "profiler/memory-status",
                "profiler/memory-snapshot-status",
                "project-tools/get",
                "project-tools/list");

            AddProfile(profiles, ToolProfile.FirstClass(readOnly: true, longRunning: true),
                "wait/editor-idle",
                "testing/list-tests",
                "uitoolkit/audit-uss-styles",
                "uitoolkit/audit-uxml-layout",
                "uitoolkit/builder-preview",
                "profiler/frame-data",
                "profiler/analyze",
                "profiler/memory-breakdown",
                "profiler/memory-top-assets");

            AddProfile(profiles, ToolProfile.FirstClass(readOnly: true, longRunning: true,
                    requiresPlayMode: true),
                "screenshot/game");

            AddProfile(profiles, ToolProfile.FirstClass(longRunning: true),
                "testing/run-tests",
                "build/start");

            AddProfile(profiles, ToolProfile.FirstClass(mutatesAssets: true),
                "serialized-object/set",
                "prefab-asset/add-component",
                "prefab-asset/add-gameobject",
                "prefab-asset/configure-component",
                "prefab-asset/instantiate-child-prefab",
                "prefab-asset/move-component",
                "prefab-asset/move-gameobject",
                "prefab-asset/remove-component",
                "prefab-asset/remove-gameobject",
                "prefab-asset/set-property",
                "prefab-asset/set-reference",
                "prefab-asset/transaction-edit",
                "prefab-asset/cleanup-missing-overrides",
                "asset/import",
                "asset/rename",
                "asset/move",
                "asset/export-unitypackage",
                "asset/create-folder",
                "asset/copy",
                "asset/transaction",
                "asset/import-settings/set",
                "material/properties/set",
                "vfxgraph/transaction",
                "timeline/transaction",
                "texture/apply-sprite-preset",
                "animation/update-state",
                "animation/update-transition",
                "animation/connect-states",
                "uitoolkit/runtime-repaint",
                "uitoolkit/assert-layout",
                "uitoolkit/edit-uxml",
                "uitoolkit/edit-uss",
                "uitoolkit/authoring-transaction",
                "localization/create-locale",
                "localization/create-collection",
                "localization/upsert-entry",
                "localization/remove-entry",
                "localization/settings",
                "localization/upsert-variable",
                "localization/remove-variable");

            AddProfile(profiles, ToolProfile.FirstClass(mutatesAssets: true, longRunning: true,
                    mayReloadDomain: true),
                "asset/refresh",
                "asset/import-unitypackage",
                "uitoolkit/refresh");

            AddProfile(profiles, ToolProfile.FirstClass(mutatesRuntime: true),
                "localization/set-selected-locale",
                "component/set-property",
                "component/set-reference",
                "scene/instantiate-prefab",
                "profiler/enable");

            AddProfile(profiles, ToolProfile.FirstClass(mutatesAssets: true,
                    mutatesRuntime: true, dangerous: true),
                "scene/workspace",
                "audio-mixer/transaction",
                "cinemachine/transaction");

            AddProfile(profiles, ToolProfile.FirstClass(mutatesRuntime: true, longRunning: true,
                    mayReloadDomain: true),
                "editor/play-mode");

            AddProfile(profiles, ToolProfile.FirstClass(mutatesAssets: true,
                    mutatesRuntime: true, dangerous: true, longRunning: true,
                    mayReloadDomain: true),
                "editor/execute-code");

            AddProfile(profiles, ToolProfile.FirstClass(mutatesRuntime: true, longRunning: true),
                "profiler/memory-snapshot");

            AddProfile(profiles, ToolProfile.FirstClass(),
                "queue/cancel",
                "jobs/cancel");

            AddProfile(profiles, ToolProfile.FirstClass(mutatesAssets: true,
                    mutatesRuntime: true, dangerous: true, longRunning: true),
                "jobs/cleanup");

            AddProfile(profiles, ToolProfile.FirstClass(mutatesAssets: true,
                    mayReloadDomain: true),
                "build/profile");

            AddProfile(profiles, ToolProfile.FirstClass(mutatesAssets: true),
                "addressables/transaction");

            AddProfile(profiles, ToolProfile.FirstClass(mutatesAssets: true,
                    longRunning: true),
                "addressables/build");

            AddProfile(profiles, ToolProfile.FirstClass(mutatesAssets: true, longRunning: true,
                    mayReloadDomain: true),
                "packages/update-git",
                "packages/add",
                "packages/remove",
                "testing/run-package-tests");

            AddProfile(profiles, ToolProfile.FirstClass(readOnly: true, longRunning: true),
                "packages/search");

            AddProfile(profiles, ToolProfile.Fallback(),
                "advanced/execute");

            AddProfile(profiles, ToolProfile.FirstClass(mutatesAssets: true, mutatesRuntime: true,
                    dangerous: true),
                "project-tools/execute");

            foreach (var pair in profiles)
            {
                if (string.Equals(pair.Value.Exposure, ExposureFirstClass, StringComparison.Ordinal) &&
                    !FirstClassRouteAllowlist.Contains(pair.Key))
                {
                    pair.Value.Exposure = ExposureLazy;
                    pair.Value.Preferred = false;
                }
            }

            return profiles;
        }

        private static void AddProfile(Dictionary<string, ToolProfile> profiles, ToolProfile profile,
            params string[] routes)
        {
            foreach (string route in routes)
                profiles[route] = profile.Clone();
        }

        private static string ExtractCategory(string path)
        {
            int slash = path.IndexOf('/');
            return slash > 0 ? path.Substring(0, slash) : path;
        }

        public static object GetRegisteredTools(bool firstClassOnly = true, bool compact = true,
            bool includeSchema = false, int offset = 0, int limit = 50, string category = null,
            bool includeMetadataIssues = false)
        {
            if (firstClassOnly)
                EnsureFirstClassToolMetadataCache();
            else
                EnsureToolMetadataCache();
            IEnumerable<Dictionary<string, object>> query = firstClassOnly ? _cachedFirstClassTools : _cachedTools;
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(tool => string.Equals(
                    tool.TryGetValue("category", out var value) ? value?.ToString() : "",
                    category, StringComparison.OrdinalIgnoreCase));
            }

            var tools = query.ToList();
            offset = Math.Max(0, offset);
            limit = Math.Max(1, Math.Min(limit, 200));
            var page = tools.Skip(offset).Take(limit).ToList();
            int nextOffset = offset + page.Count;
            var result = new Dictionary<string, object>
            {
                { "schemaVersion", 4 },
                { "compact", compact },
                { "firstClassOnly", firstClassOnly },
                { "includeSchema", includeSchema },
                { "offset", offset },
                { "limit", limit },
                { "returnedTools", page.Count },
                { "totalTools", tools.Count },
                { "hasMore", nextOffset < tools.Count },
                { "nextOffset", nextOffset < tools.Count ? (object)nextOffset : null },
            };
            if (!string.IsNullOrEmpty(category))
                result["category"] = category;

            if (compact)
            {
                result["tools"] = page.Select(tool => ToCompactToolDescriptor(tool, includeSchema)).ToList();
                return result;
            }

            result["metadataSource"] = "MCPToolMetadata.ToolProfiles";
            result["tools"] = page.Select(tool => ToDetailedToolDescriptor(tool, includeSchema)).ToList();
            if (includeMetadataIssues)
                result["metadataIssues"] = BuildMetadataIssues(page);
            return result;
        }

        private static Dictionary<string, object> ToCompactToolDescriptor(Dictionary<string, object> tool,
            bool includeSchema)
        {
            var descriptor = new Dictionary<string, object>
            {
                { "route", tool["route"] },
                { "toolName", tool["toolName"] },
                { "description", tool["description"] },
                { "annotations", tool["annotations"] },
                { "firstClass", IsFirstClassTool(tool) },
                { "exposure", tool["exposure"] }
            };
            if (includeSchema)
            {
                descriptor["inputSchema"] = tool["inputSchema"];
                descriptor["outputSchema"] = tool["outputSchema"];
            }
            if (tool.TryGetValue("projectToolName", out var projectToolName))
                descriptor["projectToolName"] = projectToolName;
            return descriptor;
        }

        private static Dictionary<string, object> ToDetailedToolDescriptor(Dictionary<string, object> tool,
            bool includeSchema)
        {
            var descriptor = tool.ToDictionary(pair => pair.Key, pair => pair.Value);
            if (!includeSchema)
            {
                descriptor.Remove("inputSchema");
                descriptor.Remove("outputSchema");
            }
            return descriptor;
        }

        private static void EnsureToolMetadataCache()
        {
            EnsureRouteCache();
            if (_cachedTools != null)
                return;

            _cachedTools = _cachedRoutes.Select(BuildToolMetadata).ToList();
        }

        private static void EnsureFirstClassToolMetadataCache()
        {
            EnsureRouteCache();
            if (_cachedFirstClassTools != null)
                return;

            _cachedFirstClassTools = _cachedRoutes
                .Select(BuildToolMetadata)
                .Where(IsFirstClassTool)
                .ToList();
        }

        private static void EnsureRouteCache()
        {
            if (_cachedRoutes == null)
                _cachedRoutes = GetRegisteredRouteList();
        }

        private static bool IsFirstClassTool(Dictionary<string, object> tool)
        {
            return string.Equals(tool.TryGetValue("exposure", out var exposure) ? exposure?.ToString() : "",
                "first-class", StringComparison.Ordinal);
        }

        private static List<string> GetRegisteredRouteList()
        {
            var routes = MCPRouteRegistry.BuiltInRoutes.ToList();
            routes.AddRange(MCPProjectToolCommands.GetDirectRoutePaths());
            return routes
                .Where(route => !string.IsNullOrEmpty(route))
                .Where(MCPCapabilityRegistry.IsRouteAvailable)
                .Distinct()
                .OrderBy(route => route)
                .ToList();
        }

        private static Dictionary<string, object> BuildToolMetadata(string route)
        {
            if (MCPProjectToolCommands.TryGetToolDetailForDirectRoute(route, out var projectTool))
                return BuildProjectToolMetadata(route, projectTool);

            string toolName = RouteToToolName(route);
            string description = GetToolDescription(route);
            ToolProfile profile = GetToolProfile(route);
            Dictionary<string, object> inputSchema = AddTargetBindingSchema(
                GetToolInputSchema(route), !profile.ReadOnly);
            MCPToolConfigurationPolicy.AnnotateInputSchema(route, inputSchema);
            bool isFirstClass = string.Equals(profile.Exposure, ExposureFirstClass, StringComparison.Ordinal);
            return new Dictionary<string, object>
            {
                { "route", route },
                { "toolName", toolName },
                { "category", ExtractCategory(route) },
                { "capability", MCPCapabilityRegistry.GetCapabilityName(route) },
                { "description", description },
                { "inputSchema", inputSchema },
                { "outputSchema", GetToolOutputSchema(route) },
                { "firstClass", isFirstClass },
                { "exposure", profile.Exposure },
                { "preferred", profile.Preferred },
                { "readOnly", profile.ReadOnly },
                { "mutatesAssets", profile.MutatesAssets },
                { "mutatesRuntime", profile.MutatesRuntime },
                { "dangerous", profile.Dangerous },
                { "longRunning", profile.LongRunning },
                { "mayReloadDomain", profile.MayReloadDomain },
                { "requiresPlayMode", profile.RequiresPlayMode },
                { "annotations", profile.ToAnnotations() },
                { "errorCodes", GetStandardErrorCodes(route) },
                { "fallbackRoute", isFirstClass ? "" : "advanced/execute" },
            };
        }

        private static Dictionary<string, object> BuildProjectToolMetadata(string route,
            Dictionary<string, object> projectTool)
        {
            var projectToolName = projectTool.TryGetValue("toolName", out var name) ? name?.ToString() : "";
            var description = projectTool.TryGetValue("description", out var desc) ? desc?.ToString() : "";
            var inputSchema = projectTool.TryGetValue("inputSchema", out var schema) &&
                              schema is Dictionary<string, object> schemaDictionary
                ? schemaDictionary
                : new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>() },
                    { "additionalProperties", true }
                };

            var shortName = projectTool.TryGetValue("shortName", out var shortNameValue)
                ? shortNameValue?.ToString()
                : "";
            string toolName = ProjectToolNameToToolName(projectToolName, shortName);
            bool explicitMutatesAssets = GetBool(projectTool, "mutatesAssets", false);
            bool mutatesRuntime = GetBool(projectTool, "mutatesRuntime", false);
            bool readOnly = GetBool(projectTool, "readOnly", false);
            bool mutatesAssets = explicitMutatesAssets;
            bool dangerous = GetBool(projectTool, "dangerous", false);
            bool longRunning = GetBool(projectTool, "longRunning", false);
            bool mayReloadDomain = GetBool(projectTool, "mayReloadDomain", false);
            bool requiresPlayMode = GetBool(projectTool, "requiresPlayMode", false);
            bool isFirstClass = GetBool(projectTool, "firstClass", false);
            var profile = new ToolProfile
            {
                Exposure = isFirstClass ? ExposureFirstClass : ExposureLazy,
                Preferred = isFirstClass,
                ReadOnly = readOnly,
                MutatesAssets = mutatesAssets,
                MutatesRuntime = mutatesRuntime,
                Dangerous = dangerous,
                LongRunning = longRunning,
                MayReloadDomain = mayReloadDomain,
                RequiresPlayMode = requiresPlayMode,
            };
            inputSchema = AddTargetBindingSchema(inputSchema, !profile.ReadOnly);
            inputSchema = AddProjectToolExecutionSchema(inputSchema);
            var businessOutputSchema =
                projectTool.TryGetValue("outputSchema", out object outputSchemaValue) &&
                outputSchemaValue is Dictionary<string, object> outputSchemaDictionary
                    ? outputSchemaDictionary
                    : new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "additionalProperties", true },
                    };
            var outputSchema = new Dictionary<string, object>
            {
                { "oneOf", new List<object>
                    {
                        businessOutputSchema,
                        CreatePersistentJobOutputSchema(),
                    }
                },
            };

            return new Dictionary<string, object>
            {
                { "route", route },
                { "toolName", toolName },
                { "category", "project-tools" },
                { "capability", "project" },
                { "description", string.IsNullOrEmpty(description) ? $"Project MCP tool: {projectToolName}" : description },
                { "inputSchema", inputSchema },
                { "outputSchema", outputSchema },
                { "projectToolName", projectToolName },
                { "firstClass", isFirstClass },
                { "exposure", profile.Exposure },
                { "preferred", profile.Preferred },
                { "readOnly", readOnly },
                { "mutatesAssets", mutatesAssets },
                { "mutatesRuntime", mutatesRuntime },
                { "dangerous", dangerous },
                { "longRunning", longRunning },
                { "mayReloadDomain", mayReloadDomain },
                { "requiresPlayMode", requiresPlayMode },
                { "sideEffects", projectTool.TryGetValue("sideEffects", out object sideEffects)
                    ? sideEffects
                    : new List<object>() },
                { "cleanupAvailable", GetBool(projectTool, "cleanupAvailable", false) },
                { "cleanupToolName", projectTool.TryGetValue("cleanupToolName", out object cleanupToolName)
                    ? cleanupToolName
                    : "" },
                { "incrementalJob", GetBool(projectTool, "incrementalJob", false) },
                { "errorCodes", projectTool.TryGetValue("errorCodes", out object errorCodes)
                    ? errorCodes
                    : GetStandardErrorCodes(route) },
                { "annotations", profile.ToAnnotations() },
                { "source", projectTool.TryGetValue("source", out var source) ? source : "" },
                { "fallbackRoute", isFirstClass ? "" : "project-tools/execute" },
            };
        }

        private static bool GetBool(Dictionary<string, object> dictionary, string key, bool fallback)
        {
            if (dictionary == null || !dictionary.TryGetValue(key, out var value) || value == null)
                return fallback;

            if (value is bool boolValue)
                return boolValue;

            return bool.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
        }

        private static Dictionary<string, object> CreateGenericOutputSchema()
        {
            return new Dictionary<string, object>
            {
                { "type", "object" },
                { "additionalProperties", true },
            };
        }

        private static Dictionary<string, object> GetToolOutputSchema(string route)
        {
            return route == "editor/execute-code" ||
                   route == "jobs/get" ||
                   route == "jobs/cancel" ||
                   route == "jobs/cleanup"
                ? CreatePersistentJobOutputSchema()
                : CreateGenericOutputSchema();
        }

        private static Dictionary<string, object> CreatePersistentJobOutputSchema()
        {
            var properties = new Dictionary<string, object>
            {
                { "jobId", new Dictionary<string, object>
                    {
                        { "type", "string" },
                        { "minLength", 1 },
                    }
                },
                { "jobAccessToken", new Dictionary<string, object>
                    {
                        { "type", "string" },
                    }
                },
                { "jobType", new Dictionary<string, object>
                    {
                        { "type", "string" },
                    }
                },
                { "operation", new Dictionary<string, object>
                    {
                        { "type", "string" },
                    }
                },
                { "status", new Dictionary<string, object>
                    {
                        { "type", "string" },
                        { "enum", new List<object>
                            {
                                "queued",
                                "running",
                                "succeeded",
                                "failed",
                                "canceled",
                                "interrupted",
                            }
                        },
                    }
                },
                { "cleanupStatus", new Dictionary<string, object>
                    {
                        { "type", "string" },
                        { "enum", new List<object>
                            {
                                "none",
                                "available",
                                "queued",
                                "running",
                                "succeeded",
                                "failed",
                                "canceled",
                                "interrupted",
                            }
                        },
                    }
                },
                { "cleanupAvailable", new Dictionary<string, object>
                    {
                        { "type", "boolean" },
                    }
                },
                { "cleanupDeclared", new Dictionary<string, object>
                    {
                        { "type", "boolean" },
                    }
                },
                { "cleanupToken", new Dictionary<string, object>
                    {
                        { "type", "string" },
                    }
                },
                { "cancellationRequested", new Dictionary<string, object>
                    {
                        { "type", "boolean" },
                    }
                },
                { "cancelMode", new Dictionary<string, object>
                    {
                        { "type", "string" },
                        { "enum", new List<object> { "beforeStart", "betweenSteps" } },
                    }
                },
                { "incremental", new Dictionary<string, object>
                    {
                        { "type", "boolean" },
                    }
                },
                { "progress", new Dictionary<string, object>
                    {
                        { "type", new List<object> { "number", "null" } },
                        { "minimum", 0 },
                        { "maximum", 1 },
                    }
                },
                { "statusMessage", new Dictionary<string, object>
                    {
                        { "type", "string" },
                    }
                },
                { "stepCount", new Dictionary<string, object>
                    {
                        { "type", "integer" },
                        { "minimum", 0 },
                    }
                },
                { "nextRunAt", new Dictionary<string, object> { { "type", "string" } } },
                { "idempotencyKey", new Dictionary<string, object> { { "type", "string" } } },
                { "createdAt", new Dictionary<string, object> { { "type", "string" } } },
                { "startedAt", new Dictionary<string, object> { { "type", "string" } } },
                { "completedAt", new Dictionary<string, object> { { "type", "string" } } },
                { "updatedAt", new Dictionary<string, object> { { "type", "string" } } },
                { "sideEffects", new Dictionary<string, object>
                    {
                        { "type", "array" },
                        { "items", new Dictionary<string, object> { { "type", "string" } } },
                    }
                },
                { "result", new Dictionary<string, object>() },
                { "error", new Dictionary<string, object>() },
                { "cleanupResult", new Dictionary<string, object>() },
                { "cleanupError", new Dictionary<string, object>() },
                { "statusRoute", new Dictionary<string, object>
                    {
                        { "const", "jobs/get" },
                    }
                },
                { "cancelRoute", new Dictionary<string, object>
                    {
                        { "const", "jobs/cancel" },
                    }
                },
                { "cleanupRoute", new Dictionary<string, object>
                    {
                        { "const", "jobs/cleanup" },
                    }
                },
                { "reused", new Dictionary<string, object> { { "type", "boolean" } } },
            };
            return new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", properties },
                { "required", new List<object>
                    {
                        "jobId",
                        "jobType",
                        "operation",
                        "status",
                        "cleanupStatus",
                        "cleanupAvailable",
                        "cleanupDeclared",
                        "cleanupToken",
                        "cancellationRequested",
                        "cancelMode",
                        "incremental",
                        "progress",
                        "statusMessage",
                        "stepCount",
                        "nextRunAt",
                        "idempotencyKey",
                        "createdAt",
                        "startedAt",
                        "completedAt",
                        "updatedAt",
                        "sideEffects",
                        "result",
                        "error",
                        "cleanupResult",
                        "cleanupError",
                        "statusRoute",
                        "cancelRoute",
                        "cleanupRoute",
                    }
                },
                { "additionalProperties", false },
            };
        }

        private static List<string> GetStandardErrorCodes(string route)
        {
            var codes = new List<string>
            {
                "invalid_arguments",
                "target_project_mismatch",
                "tool_execution_failed",
                "response_too_large",
            };
            if (route == "editor/execute-code" ||
                route == "project-tools/execute" ||
                route != null && route.StartsWith(MCPProjectToolCommands.DirectRoutePrefix,
                    StringComparison.Ordinal))
            {
                codes.Add("idempotency_conflict");
            }
            if (route != null && route.StartsWith("jobs/", StringComparison.Ordinal))
            {
                codes.AddRange(new[]
                {
                    "job_not_found",
                    "job_owner_mismatch",
                    "job_not_cancellable",
                    "job_not_cleanable",
                    "job_not_terminal",
                    "job_cleanup_token_missing",
                });
            }
            return codes.Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToList();
        }

        private static Dictionary<string, object> AddTargetBindingSchema(
            Dictionary<string, object> inputSchema, bool requiresTargetBinding)
        {
            if (!requiresTargetBinding)
                return inputSchema;

            var schema = inputSchema != null
                ? new Dictionary<string, object>(inputSchema)
                : new Dictionary<string, object> { { "type", "object" } };
            var properties = schema.TryGetValue("properties", out object propertiesValue) &&
                             propertiesValue is Dictionary<string, object> existingProperties
                ? new Dictionary<string, object>(existingProperties)
                : new Dictionary<string, object>();
            if (!properties.ContainsKey("expectedProjectPath"))
            {
                KeyValuePair<string, object> bindingProperty = Prop("expectedProjectPath", "string",
                    "Expected Unity project root path. The request is rejected before mutation if it reaches another project.");
                properties[bindingProperty.Key] = bindingProperty.Value;
            }
            if (!properties.ContainsKey("expectedProjectName"))
            {
                KeyValuePair<string, object> bindingProperty = Prop("expectedProjectName", "string",
                    "Optional expected Unity project name used with expectedProjectPath as an additional target-binding check.");
                properties[bindingProperty.Key] = bindingProperty.Value;
            }
            schema["properties"] = properties;
            return schema;
        }

        private static Dictionary<string, object> AddProjectToolExecutionSchema(
            Dictionary<string, object> inputSchema)
        {
            var schema = inputSchema != null
                ? new Dictionary<string, object>(inputSchema)
                : new Dictionary<string, object> { { "type", "object" } };
            var properties = schema.TryGetValue("properties", out object propertiesValue) &&
                             propertiesValue is Dictionary<string, object> existingProperties
                ? new Dictionary<string, object>(existingProperties)
                : new Dictionary<string, object>();
            if (!properties.ContainsKey("runAsJob"))
            {
                KeyValuePair<string, object> property = Prop("runAsJob", "boolean",
                    "Run this invocation through the persistent project-tool job owner. Long-running tools always do this.");
                properties[property.Key] = property.Value;
            }
            if (!properties.ContainsKey("idempotencyKey"))
            {
                KeyValuePair<string, object> property = Prop("idempotencyKey", "string",
                    "Optional project-scoped key used to reuse an existing persistent invocation.");
                properties[property.Key] = property.Value;
            }
            schema["properties"] = properties;
            return schema;
        }

        private static ToolProfile GetToolProfile(string route)
        {
            if (ToolProfiles.TryGetValue(route, out var profile))
                return profile;

            if (!string.IsNullOrEmpty(route))
            {
                int slashIndex = route.IndexOf('/');
                if (slashIndex > 0)
                {
                    string familyRoute = route.Substring(0, slashIndex + 1) + "*";
                    if (ToolProfiles.TryGetValue(familyRoute, out profile))
                        return profile;
                }
            }

            return ToolProfile.Lazy();
        }

        internal static bool IsRouteReadOnly(string route)
        {
            if (MCPProjectToolCommands.TryGetToolDetailForDirectRoute(route, out var projectTool))
                return GetBool(projectTool, "readOnly", false);
            return GetToolProfile(route).ReadOnly;
        }

        internal static bool RouteMutatesAssets(string route)
        {
            if (MCPProjectToolCommands.TryGetToolDetailForDirectRoute(route, out var projectTool))
                return GetBool(projectTool, "mutatesAssets", false);
            return GetToolProfile(route).MutatesAssets;
        }

        internal static bool RouteMutatesRuntime(string route)
        {
            if (MCPProjectToolCommands.TryGetToolDetailForDirectRoute(route, out var projectTool))
                return GetBool(projectTool, "mutatesRuntime", false);
            return GetToolProfile(route).MutatesRuntime;
        }

        internal static bool RouteIsDangerous(string route)
        {
            if (MCPProjectToolCommands.TryGetToolDetailForDirectRoute(route, out var projectTool))
                return GetBool(projectTool, "dangerous", false);
            return GetToolProfile(route).Dangerous;
        }

        internal static bool RouteRequiresTargetBinding(string route)
        {
            // Unknown/lazy routes are treated conservatively as writes. A route may skip
            // target binding only by declaring itself read-only in metadata.
            return !IsRouteReadOnly(route);
        }

        internal static bool RouteMayReloadDomain(string route)
        {
            if (MCPProjectToolCommands.TryGetToolDetailForDirectRoute(route, out var projectTool))
                return GetBool(projectTool, "mayReloadDomain", false);
            return GetToolProfile(route).MayReloadDomain;
        }

        internal static bool RouteIsLongRunning(string route)
        {
            if (MCPProjectToolCommands.TryGetToolDetailForDirectRoute(route, out var projectTool))
                return GetBool(projectTool, "longRunning", false);
            return GetToolProfile(route).LongRunning;
        }

        private static List<Dictionary<string, object>> BuildMetadataIssues(List<Dictionary<string, object>> tools)
        {
            var issues = new List<Dictionary<string, object>>();
            foreach (var tool in tools)
            {
                string route = tool.TryGetValue("route", out var routeObj) ? routeObj?.ToString() : "";
                string exposure = tool.TryGetValue("exposure", out var exposureObj) ? exposureObj?.ToString() : "";
                string description = tool.TryGetValue("description", out var descObj) ? descObj?.ToString() : "";
                bool hasProfile = ToolProfiles.ContainsKey(route) ||
                                  MCPProjectToolCommands.TryGetToolDetailForDirectRoute(route, out _);

                if (!hasProfile && string.Equals(exposure, ExposureFirstClass, StringComparison.Ordinal))
                {
                    issues.Add(new Dictionary<string, object>
                    {
                        { "route", route },
                        { "issue", "first_class_without_profile" },
                    });
                }

                if (description.StartsWith("Execute Unity MCP route ", StringComparison.Ordinal) ||
                    description.StartsWith("Lazy Unity route: ", StringComparison.Ordinal))
                {
                    issues.Add(new Dictionary<string, object>
                    {
                        { "route", route },
                        { "issue", "default_description" },
                    });
                }

                if (tool.TryGetValue("inputSchema", out var schemaObj) &&
                    schemaObj is Dictionary<string, object> schema)
                {
                    CollectSchemaIssues(route, schema, "$", false, issues);
                }
            }

            return issues;
        }

        private static void CollectSchemaIssues(
            string route,
            Dictionary<string, object> schema,
            string path,
            bool isProperty,
            List<Dictionary<string, object>> issues)
        {
            string type = schema.TryGetValue("type", out var typeObj) ? typeObj?.ToString() : "";
            if (isProperty &&
                (!schema.TryGetValue("description", out var descriptionObj) ||
                 string.IsNullOrWhiteSpace(descriptionObj?.ToString())))
            {
                issues.Add(new Dictionary<string, object>
                {
                    { "route", route },
                    { "issue", "property_without_description" },
                    { "path", path },
                });
            }

            if (string.Equals(type, "array", StringComparison.Ordinal) &&
                !schema.ContainsKey("items"))
            {
                issues.Add(new Dictionary<string, object>
                {
                    { "route", route },
                    { "issue", "array_without_items" },
                    { "path", path },
                });
            }

            if (schema.TryGetValue("properties", out var propertiesObj) &&
                propertiesObj is Dictionary<string, object> properties)
            {
                foreach (var property in properties)
                {
                    if (property.Value is Dictionary<string, object> propertySchema)
                        CollectSchemaIssues(route, propertySchema, path + "." + property.Key, true, issues);
                }
            }

            if (schema.TryGetValue("items", out var itemsObj) &&
                itemsObj is Dictionary<string, object> itemsSchema)
            {
                CollectSchemaIssues(route, itemsSchema, path + "[]", false, issues);
            }

            foreach (string keyword in new[] { "allOf", "anyOf", "oneOf" })
            {
                if (!schema.TryGetValue(keyword, out var variantsObj) ||
                    !(variantsObj is IEnumerable<object> variants))
                {
                    continue;
                }

                int index = 0;
                foreach (object variant in variants)
                {
                    if (variant is Dictionary<string, object> variantSchema)
                        CollectSchemaIssues(route, variantSchema, path + "." + keyword + "[" + index + "]", false, issues);
                    index++;
                }
            }
        }

        private static string RouteToToolName(string route)
        {
            switch (route)
            {
                case "build/start":
                    return "unity_build";
                case "compilation/errors":
                    return "unity_get_compilation_errors";
                case "editor/play-mode":
                    return "unity_play_mode";
                case "queue/status":
                    return "unity_queue_ticket_status";
            }
            return "unity_" + route.Replace("/", "_").Replace("-", "_");
        }

        internal static string ProjectToolNameToToolName(string projectToolName, string shortName = "")
        {
            var normalized = NormalizeProjectToolName(string.IsNullOrEmpty(shortName) ? projectToolName : shortName);

            if (string.IsNullOrEmpty(normalized))
                normalized = "tool";

            var tokens = normalized.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(CompactProjectToolToken)
                .ToArray();
            string compact = "unity_pt_" + string.Join("_", tokens);
            const int maxLength = 48;
            if (compact.Length <= maxLength)
                return compact;

            string hash = ComputeStableNameHash(normalized);
            int prefixLength = maxLength - hash.Length - 1;
            return compact.Substring(0, prefixLength).TrimEnd('_') + "_" + hash;
        }

        private static string NormalizeProjectToolName(string projectToolName)
        {
            return Regex.Replace(projectToolName ?? "", "[^A-Za-z0-9]+", "_")
                .Trim('_')
                .ToLowerInvariant();
        }

        private static string CompactProjectToolToken(string token)
        {
            switch (token)
            {
                case "vmframework": return "vmf";
                case "battleidle": return "battle";
                case "visual": return "ui";
                case "element": return "el";
                case "elements": return "els";
                case "property": return "prop";
                case "properties": return "props";
                case "configuration": return "config";
                case "configurations": return "configs";
                case "wrapper": return "wrap";
                case "wrappers": return "wraps";
                default: return token;
            }
        }

        private static string ComputeStableNameHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value)
                {
                    hash ^= character;
                    hash *= 16777619;
                }

                return hash.ToString("x8");
            }
        }

        private static string GetToolDescription(string route)
        {
            switch (route)
            {
                case "_meta/tools":
                    return "List the Unity bridge tool catalog. This internal discovery route is not exposed as a normal first-class tool.";
                case "asset/list":
                    return "List assets below a Unity project folder with bounded pagination and an optional type filter.";
                case "compilation/errors":
                    return "Read tracked Unity compilation errors and warnings with bounded pagination and a separate obsolete-API warning summary.";
                case "packages/info":
                    return "Read detailed Unity Package Manager metadata for one installed package.";
                case "packages/list":
                    return "List installed Unity packages with bounded pagination.";
                case "packages/update-git":
                    return "Update a Git-based Unity package and return the resolved packages-lock hash.";
                case "packages/status":
                    return "Read Package Manager manifest and lock status for one package or all Git packages.";
                case "advanced/execute":
                    return "Fallback generic entrypoint for routes that do not have a concrete tool yet. Prefer route-specific unity_* tools first.";
                case "packages/lint-metas":
                    return "Lint a Unity package root for missing .meta files.";
                case "wait/editor-idle":
                    return "Wait until the Unity Editor is idle after compilation, domain reload, package refresh, or asset import.";
                case "editor/play-mode":
                    return "Enter, pause, resume, step one frame, or stop Play Mode and return only after Unity confirms the requested state.";
                case "testing/list-tests":
                    return "List discoverable Unity tests with mode and name filters.";
                case "testing/run-tests":
                    return "Start a Unity Test Runner job and return a job ID for polling.";
                case "testing/get-job":
                    return "Poll a Unity Test Runner job, including progress, failures, and optional result details. EditMode tests can delay main-thread queue polling while they execute.";
                case "testing/run-package-tests":
                    return "Run tests from a Git package by temporarily enabling package testables, surviving domain reloads, and restoring manifest.json exactly.";
                case "testing/get-package-job":
                    return "Poll a persistent package test workflow through testable enablement, test execution, and exact manifest restoration.";
                case "profiler/enable":
                    return "Enable or disable the Unity Profiler and optional deep profiling.";
                case "profiler/stats":
                    return "Read current Unity rendering statistics such as batches, draw calls, triangles, and frame time.";
                case "profiler/memory":
                    return "Read current allocated, reserved, managed-heap, graphics-driver, and temporary allocator memory.";
                case "profiler/frame-data":
                    return "Read a paginated CPU timing hierarchy from a recorded Unity Profiler frame.";
                case "profiler/analyze":
                    return "Analyze current memory, rendering, and recorded Profiler frame data with optimization findings.";
                case "profiler/memory-status":
                    return "Read Memory Profiler availability and a quick current memory summary.";
                case "profiler/memory-breakdown":
                    return "Scan loaded assets and summarize runtime memory by asset category.";
                case "profiler/memory-top-assets":
                    return "List the largest loaded assets by runtime memory usage.";
                case "profiler/memory-snapshot":
                    return "Capture a Memory Profiler snapshot and wait for confirmed completion when com.unity.memoryprofiler is installed.";
                case "profiler/memory-snapshot-status":
                    return "Poll the current Memory Profiler snapshot job after a long capture outlives the initiating request.";
                case "mcp/health":
                    return "Inspect MCP bridge health, queue state, sessions, process memory, and recent slow requests.";
                case "mcp/set-autostart":
                    return "Enable or disable MCP bridge auto-start for this Unity Editor instance.";
                case "instance/current":
                    return "Return the current Unity Editor MCP instance identity, including project path and port.";
                case "instance/list":
                    return "List registered Unity Editor MCP instances across open Unity projects.";
                case "instance/resolve":
                    return "Resolve one Unity Editor MCP instance by project path, project name, or port.";
                case "instance/assert-project":
                    return "Assert that this MCP request reached the expected Unity project.";
                case "scene/hierarchy":
                    return "Read the active scene hierarchy, optionally returning compact matches filtered by component type.";
                case "scene/instantiate-prefab":
                    return "Instantiate a prefab asset into the currently open scene.";
                case "scene/workspace":
                    return "List loaded scenes, open a scene additively or singly, close a loaded scene with an explicit dirty-scene policy, or set the active scene.";
                case "prefab-asset/add-component":
                    return "Add and optionally initialize a component on a prefab asset, then verify its serialized state after saving. Waits for a newly compiled script type when needed.";
                case "prefab-asset/configure-component":
                    return "Ensure and configure one component on a prefab asset GameObject, including serialized properties and ObjectReferences, in one atomic save.";
                case "prefab-asset/add-gameobject":
                    return "Create a child GameObject inside a prefab asset with an explicit or parent-inherited Layer.";
                case "prefab-asset/instantiate-child-prefab":
                    return "Instantiate a prefab asset as a child inside another prefab asset.";
                case "prefab-asset/hierarchy":
                    return "Get the full hierarchy tree of a prefab asset directly from disk.";
                case "prefab-asset/get-properties":
                    return "Read serialized properties from a component on a GameObject inside a prefab asset.";
                case "prefab-asset/set-property":
                    return "Set a serialized property on a component inside a prefab asset.";
                case "prefab-asset/set-reference":
                    return "Set an ObjectReference property on a component inside a prefab asset.";
                case "prefab-asset/move-gameobject":
                    return "Move or reorder a GameObject inside a prefab asset.";
                case "prefab-asset/move-component":
                    return "Atomically move a component between GameObjects inside one prefab asset while preserving serialized data and remapping references to the moved component.";
                case "prefab-asset/remove-component":
                    return "Remove a component from a GameObject inside a prefab asset.";
                case "prefab-asset/remove-gameobject":
                    return "Remove a child GameObject from inside a prefab asset.";
                case "prefab-asset/find":
                    return "Find GameObjects inside a prefab asset by name/path, component type, and serialized property value.";
                case "prefab-asset/transaction-edit":
                    return "Apply ordered prefab edits in one transaction with configurable immediate or frame-batched execution.";
                case "prefab-asset/cleanup-missing-overrides":
                    return "Remove Prefab Variant property overrides whose serialized target field no longer exists.";
                case "component/set-reference":
                    return "Assign one or more component ObjectReference properties with configurable immediate or frame-batched execution.";
                case "component/set-property":
                    return "Set a serialized component property, including inherited Behaviour.enabled, on a scene GameObject.";
                case "serialized-object/get":
                    return "Read serialized properties from a scene object, component, or asset via SerializedObject.";
                case "serialized-object/set":
                    return "Set one serialized property on a scene object, component, or asset via SerializedObject. SerializeReference values use '$managedReferenceType' when their concrete type cannot be inferred.";
                case "asset/refresh":
                    return "Start a reload-safe AssetDatabase refresh job. Poll asset/get-refresh-job until it reaches a terminal state.";
                case "asset/get-refresh-job":
                    return "Poll the current or latest reload-safe AssetDatabase refresh job.";
                case "asset/import":
                    return "Preflight and import one or more external assets with shared TextureImporter defaults, image-content deduplication, configurable execution, per-item results, and rollback.";
                case "asset/import-settings/get":
                    return "Read semantic TextureImporter, ModelImporter, or AudioImporter settings without exposing Unity's internal serialized fields.";
                case "asset/import-settings/set":
                    return "Validate and update semantic TextureImporter, ModelImporter, or AudioImporter settings, optional platform overrides, and reimport behavior.";
                case "asset/rename":
                    return "Safely rename a Unity asset using AssetDatabase while preserving its .meta GUID and synchronizing Single Sprite internal names.";
                case "asset/move":
                    return "Preflight and move one or more Unity assets with configurable execution, GUID preservation, Single Sprite internal-name synchronization when filenames change, and rollback.";
                case "asset/export-unitypackage":
                    return "Export one or more Unity assets to a .unitypackage file using AssetDatabase.ExportPackage.";
                case "asset/import-unitypackage":
                    return "Start a reload-safe, non-interactive .unitypackage import. Poll jobs/get with the returned jobId and jobType until the AssetDatabase completion callback is confirmed.";
                case "asset/create-folder":
                    return "Create or ensure an Assets folder hierarchy through AssetDatabase, with dry-run support.";
                case "asset/copy":
                    return "Copy one or more Unity asset files with parent-folder creation, overwrite snapshots, and rollback.";
                case "asset/dependencies":
                    return "Read paginated outgoing dependencies and incoming references for an asset.";
                case "asset/transaction":
                    return "Apply folder, copy, move, delete, and serialized-property edits as one rollback-capable asset transaction.";
                case "console/query":
                    return "Query recent Unity Console entries with time, source, message, stack, and last-Play filters.";
                case "debug/attach-unity":
                    return "Inspect Unity managed debugger attachment state and return MCP debug capability boundaries.";
                case "debug/set-breakpoint":
                    return "Request a managed source breakpoint. Currently reports that this requires an external debugger adapter.";
                case "debug/stack-trace":
                    return "Return the current MCP request stack trace. Paused managed frames require an external debugger adapter.";
                case "debug/variables":
                    return "Request variables for a paused managed frame. Currently reports that this requires an external debugger adapter.";
                case "debug/evaluate":
                    return "Evaluate C# code in the Unity Editor context. Paused frame evaluation requires an external debugger adapter.";
                case "animation/transition-info":
                    return "Read full Animator transition details including conditions, exit time, duration, and offset.";
                case "animation/update-state":
                    return "Modify an existing Animator state, including motion, speed, tag, graph position, and default state.";
                case "animation/update-transition":
                    return "Modify an existing Animator transition, including settings and condition edits.";
                case "animation/connect-states":
                    return "Create transitions between every pair of the provided Animator states.";
                case "animation/validate-controller":
                    return "Validate Animator parameters, states, motions, required transitions, and pairwise state connections.";
                case "uitoolkit/audit-uss-styles":
                    return "Audit USS selectors that serve exactly one authored UXML element and declarations that repeat the same winning value already supplied by the loaded PanelSettings theme or another loaded stylesheet.";
                case "uitoolkit/audit-uxml-layout":
                    return "Audit authored UXML for tooltip attributes, unconsumed element names, fully fixed flex partitions, fixed cross-axis content wrappers inside single-axis ScrollViews, layout-only manually centered containers, removable single-child centering wrappers, visually inert centered-label stretching or growth, repeated inline layout variants, and inline declarations already owned by loaded USS defaults.";
                case "uitoolkit/windows":
                    return "List open Unity Editor windows with UI Toolkit root metadata.";
                case "uitoolkit/tree":
                    return "Read a UI Toolkit visual tree from an EditorWindow.";
                case "uitoolkit/query":
                    return "Query UI Toolkit elements by name, className, typeName, or text.";
                case "uitoolkit/style":
                    return "Read inline and resolved style for a UI Toolkit element.";
                case "uitoolkit/repaint":
                    return "Trigger repaint on a UI Toolkit EditorWindow or element.";
                case "uitoolkit/asset-inspect":
                    return "Inspect UXML and USS assets for VisualElement names, types, unconditional class defaults, contextual selectors, and pseudo-state rules.";
                case "uitoolkit/runtime-documents":
                    return "List runtime UIDocuments with root visual element metadata.";
                case "uitoolkit/runtime-tree":
                    return "Read a runtime UIDocument UI Toolkit visual tree.";
                case "uitoolkit/runtime-query":
                    return "Query runtime UIDocument UI Toolkit elements by VisualElementPath, name, class, type, or text.";
                case "uitoolkit/runtime-style":
                    return "Read inline, resolved, and background style data for a runtime UI Toolkit element.";
                case "uitoolkit/diagnose-runtime":
                    return "Diagnose runtime UI Toolkit elements with VisualElementPath lookup, style, parent/children, background, and pixel-grid data.";
                case "uitoolkit/visual-check":
                    return "Run runtime UI Toolkit visual checks such as pixel-grid, background scale, and expected size.";
                case "uitoolkit/locate-element":
                    return "Locate an Editor or runtime UI Toolkit element and return its VisualElementPath, world bounds, crop rect, and context.";
                case "uitoolkit/capture-element":
                    return "Capture an Editor or runtime UI Toolkit element by taking its containing window screenshot and cropping to the element bounds.";
                case "uitoolkit/compare-element":
                    return "Capture a UI Toolkit element and compare the cropped image against a reference image.";
                case "uitoolkit/generated-children":
                    return "Inspect generated UI Toolkit child elements such as arrows, checkmarks, scrollers, TabView internals, and unnamed unity-* subparts.";
                case "uitoolkit/resource-audit":
                    return "Audit UI Toolkit elements for resolved background assets, generated child visuals, highlighted-state misuse, and scale metadata.";
                case "uitoolkit/runtime-repaint":
                    return "Trigger repaint for a runtime UIDocument or one of its elements.";
                case "uitoolkit/refresh":
                    return "Refresh UI Toolkit assets, repaint runtime and Editor panels, and return after stable Editor frames.";
                case "uitoolkit/assert-layout":
                    return "Assert UI Toolkit runtime layout constraints such as edge touching, containment, and size.";
                case "uitoolkit/builder-preview":
                    return "Open a UXML asset in UI Builder, expand an undersized canvas through Match Game View, wait for the preview to settle, and optionally capture the window.";
                case "uitoolkit/edit-uxml":
                    return "Structurally edit UXML elements by VisualElementPath or authored name, then synchronously reimport the asset.";
                case "uitoolkit/edit-uss":
                    return "Add, remove, or update USS selectors and declarations, then synchronously reimport the asset.";
                case "uitoolkit/authoring-transaction":
                    return "Apply UXML and USS edits across multiple files with atomic file snapshots and rollback.";
                case "packages/add":
                    return "Add a Unity package by registry name, Git URL, local path, or tarball and wait for Package Manager completion.";
                case "packages/remove":
                    return "Remove a Unity package dependency and wait for Package Manager completion.";
                case "packages/search":
                    return "Search Unity Package Manager registry packages with bounded results.";
                case "screenshot/game":
                    return "Capture the current Game View during active or paused Play Mode, suppress and restore Game View Gizmos and Stats by default or preserve them when they are the evidence subject, fail without creating an image in Edit Mode, and return only after the PNG is fully written and decodable.";
                case "screenshot/crop":
                    return "Crop an existing screenshot or image file to a PNG.";
                case "screenshot/scene":
                    return "Capture the current Scene View once and return the PNG as a file, base64 payload, or both.";
                case "graphics/asset-preview":
                    return "Render Unity's asset preview for any supported asset type, including prefabs, as a base64 PNG.";
                case "gameview/info":
                    return "Read the Unity Editor Game View resolution, selected size, scale, and minimum scale.";
                case "gameview/set-resolution":
                    return "Set the Unity Editor Game View to a custom resolution.";
                case "gameview/set-scale":
                    return "Set the Unity Editor Game View zoom scale to an explicit value or the current minimum slider scale.";
                case "graphics/image-alpha-bounds":
                    return "Inspect a PNG or texture asset and return alpha-based visible pixel bounds.";
                case "graphics/rect-gap":
                    return "Measure the gap or overlap between two rectangles along an edge pair.";
                case "graphics/annotate-rects":
                    return "Draw rectangle overlays on a screenshot or image file for visual verification.";
                case "graphics/compare-images":
                    return "Compare two screenshots or image files, optionally within crop rects, and return pixel-difference bounds plus an optional diff image.";
                case "sprite/sheet-info":
                    return "Inspect a sliced sprite sheet and return texture and sprite metadata.";
                case "sprite/pixel-check":
                    return "Check Sprite/Texture import settings, dimensions, pivot, border, and pixel-art suitability.";
                case "sprite/replace-and-slice":
                    return "Replace a sprite sheet image file and slice it into numbered sprites.";
                case "sprite/slice-sheet":
                    return "Slice an existing sprite sheet into numbered sprites while preserving existing sprite IDs by name.";
                case "sprite/update-animation-clip":
                    return "Update an AnimationClip SpriteRenderer.m_Sprite object-reference curve from a sprite sheet.";
                case "sprite/replace-slice-update-clip":
                    return "Replace a sprite sheet, slice it, then update an AnimationClip from the generated sprites.";
                case "texture/apply-sprite-preset":
                    return "Apply high-level TextureImporter/Sprite settings such as pixel sprite preset, PPU, pivot, border, and reference settings.";
                case "texture/info":
                    return "Inspect a texture asset, runtime format and memory, and its TextureImporter settings, including sprite PPU, pivot, and border when applicable.";
                case "texture/set-import":
                    return "Set TextureImporter type and import settings, including Sprite and NormalMap configuration, then reimport once.";
                case "texture/find-duplicates":
                    return "Audit project image assets for duplicate file bytes or identical decoded RGBA pixels, even when PNG/JPEG encoding differs.";
                case "texture/import-image":
                    return "Import an external image from a URL or local path into Assets, optionally dedupe, then apply sprite import settings.";
                case "texture/check-import-settings":
                    return "Check TextureImporter settings against a reference texture or a pixel-sprite preset without modifying assets.";
                case "texture/check-ui-import-settings":
                    return "Check UI pixel-art image import settings, including pixel sprite defaults plus optional expected dimensions, border, and max texture size.";
                case "build/start":
                    return "Start a persistent Player build job, optionally run the executable, and return immediately with a job ID. Poll build/get-job for the final BuildReport; no post-build asset refresh is required.";
                case "build/get-job":
                    return "Poll the current or latest persistent Player build job and return its final BuildReport and optional run result.";
                case "build/profile":
                    return "Inspect or transactionally edit Unity 6 Build Profiles, active profile, scenes, scripting defines, and global build-scene settings.";
                case "jobs/list":
                    return "List paginated persistent Unity MCP job history owned by the current agent.";
                case "jobs/get":
                    return "Get one persistent Unity MCP job snapshot with owner enforcement.";
                case "jobs/cancel":
                    return "Request owner- or capability-token-checked cancellation of a persistent Unity MCP job and report the actual cancellation mode.";
                case "jobs/cleanup":
                    return "Run the explicit persisted cleanup contract of a terminal execute-code or project-tool job. Cleanup is itself durable and status is read through jobs/get.";
                case "material/properties/get":
                    return "Read a Material's shader, typed shader properties, textures, keywords, render queue, and instancing settings through Unity's public Material API.";
                case "material/properties/set":
                    return "Transactionally set typed Material shader properties, texture references and transforms, keywords, render queue, and instancing settings.";
                case "physics/raycast":
                    return "Raycast through Physics or Physics2D using one dimension-selectable contract, with deterministic bounded multi-hit results.";
                case "physics/overlap-sphere":
                    return "Run a 3D sphere or 2D circle overlap query with deterministic bounded collider results.";
                case "physics/overlap-box":
                    return "Run a 3D or 2D box overlap query with deterministic bounded collider results.";
                case "vfxgraph/info":
                    return "Inspect a VFX Graph's contexts, blocks, operators, exposed properties, and object-reference connections, with slots and bounded raw serialization available only when requested.";
                case "vfxgraph/transaction":
                    return "Apply a validated, undoable batch of VFX Graph node or exposed-property serialized edits.";
                case "audio-mixer/info":
                    return "Inspect an AudioMixer's groups, snapshots, effects, and exposed parameter values, with a bounded raw serialized diagnostic available only when requested.";
                case "audio-mixer/transaction":
                    return "Manage AudioMixer groups, snapshots, effects, exposed parameters and persistent snapshot values, or apply a separate batch of editor-session runtime overrides.";
                case "addressables/info":
                    return "List Addressables settings, groups, schemas, labels, and paginated entries when com.unity.addressables is installed.";
                case "addressables/transaction":
                    return "Transactionally manage Addressables groups, copied schemas, the default group, labels, entries, addresses, and entry-label assignments.";
                case "addressables/build":
                    return "Start a persistent Addressables content build job and return a job ID for jobs/get or jobs/cancel.";
                case "timeline/info":
                    return "Inspect a Timeline asset's tracks, clips, markers, and duration, with a bounded raw serialized diagnostic available only when requested.";
                case "timeline/transaction":
                    return "Apply an undoable Timeline transaction that creates, deletes, renames, or configures tracks and clips.";
                case "cinemachine/info":
                    return "Inspect Cinemachine cameras, brains, and extensions in loaded scenes or a prefab, with optional bounded serialized properties.";
                case "cinemachine/transaction":
                    return "Apply an undoable Cinemachine scene or prefab transaction for properties, object targets, and enabled state.";
                case "animation/set-object-reference-curve":
                    return "Set AnimationClip ObjectReference keyframes, such as SpriteRenderer.m_Sprite.";
                case "localization/status":
                    return "Inspect Unity Localization package, settings, locale, and table collection status.";
                case "localization/locales":
                    return "List project Locales registered with Unity Localization.";
                case "localization/create-locale":
                    return "Create a Locale asset and optionally register it with Localization Settings.";
                case "localization/set-selected-locale":
                    return "Set the currently selected Unity Localization Locale.";
                case "localization/collections":
                    return "List String and Asset Table Collections with their Locale tables.";
                case "localization/create-collection":
                    return "Create a String or Asset Table Collection for selected Locales.";
                case "localization/entries":
                    return "Read paginated String or Asset Table entries across Locale tables.";
                case "localization/upsert-entry":
                    return "Create or update one or more localized String, Smart String, or Asset Table entries with configurable execution.";
                case "localization/remove-entry":
                    return "Remove a localization entry from one Locale table or the entire collection.";
                case "localization/validate":
                    return "Find missing, empty, and duplicate localization entries across Locale tables.";
                case "localization/settings":
                    return "Read or update Localization Settings, project Locale, and selected Locale.";
                case "localization/variables":
                    return "List Smart String persistent variable groups and values.";
                case "localization/upsert-variable":
                    return "Create or update a Smart String persistent variable and optionally create its group asset.";
                case "localization/remove-variable":
                    return "Remove a Smart String persistent variable from a registered group.";
                case "project-tools/list":
                    return "List compact project-defined MCP tool summaries without parameter schemas.";
                case "project-tools/get":
                    return "Get the complete descriptor and input schema for one project-defined MCP tool.";
                case "project-tools/execute":
                    return "Execute a project-defined MCP tool after discovering it with project-tools/list and inspecting it with project-tools/get.";
                case "queue/info":
                    return "Inspect queue capacity, active work, and per-agent depth.";
                case "queue/status":
                    return "Read one owned queue ticket and its terminal result.";
                case "queue/cancel":
                    return "Cancel one owned queued request; executing Unity work is not preempted.";
                case "search/scene":
                    return "Search loaded scene GameObjects with composable name, component, tag, layer, and shader filters plus stable pagination.";
                case "_meta/capabilities":
                    return "List core and optional Unity MCP capabilities detected in this project.";
                default:
                    return $"Lazy Unity route: {route}";
            }
        }

        private static Dictionary<string, object> GetToolInputSchema(string route)
        {
            switch (route)
            {
                case "_meta/tools":
                    return Schema(Props(
                        Prop("firstClassOnly", "boolean", "Return only release-managed first-class tools. Defaults to true."),
                        Prop("compact", "boolean", "Return compact descriptors. Defaults to true."),
                        Prop("includeSchema", "boolean", "Include input schemas. Defaults to false."),
                        Prop("offset", "number", "Tool offset. Defaults to 0."),
                        Prop("limit", "number", "Maximum tools returned. Built-in default is 50; capped at 200."),
                        Prop("category", "string", "Optional exact category filter."),
                        Prop("includeMetadataIssues", "boolean", "Include metadata audit diagnostics in detailed mode. Defaults to false.")
                    ));
                case "asset/list":
                    return Schema(Props(
                        Prop("folder", "string", "Folder to search. Defaults to Assets."),
                        Prop("type", "string", "Optional Unity asset type filter."),
                        Prop("search", "string", "Optional AssetDatabase search expression."),
                        Prop("recursive", "boolean", "Include descendants. Defaults to true."),
                        Prop("offset", "number", "Result offset."),
                        Prop("limit", "number", "Maximum assets. Defaults to 100; capped at 500.")));
                case "asset/import-settings/get":
                    return Schema(Props(
                        Prop("assetPath", "string", "Texture, model, or audio asset path below Assets/."),
                        Prop("platform", "string", "Optional Unity platform override name such as Standalone, Android, or iPhone.")
                    ), "assetPath");
                case "asset/import-settings/set":
                    return Schema(Props(
                        Prop("assetPath", "string", "Texture, model, or audio asset path below Assets/."),
                        Prop("settings", "object", "Semantic importer fields. Unsupported keys are rejected with the allowed field list."),
                        Prop("platform", "string", "Optional Unity platform override name such as Standalone, Android, or iPhone."),
                        Prop("platformSettings", "object", "Optional semantic TextureImporter or AudioImporter override settings for platform."),
                        Prop("reimport", "boolean", "Save and reimport the asset after updating settings. Defaults to true."),
                        Prop("dryRun", "boolean", "Validate and return before/requested settings without modifying the importer.")
                    ), "assetPath", "settings");
                case "scene/workspace":
                    return Schema(Props(
                        Prop("action", "string", "Workspace action: list, open, close, or set-active. Defaults to list."),
                        Prop("path", "string", "Scene asset path for open, close, or set-active."),
                        Prop("name", "string", "Loaded scene name for close or set-active when path is omitted."),
                        Prop("mode", "string", "Open mode: additive (default) or single."),
                        Prop("saveModified", "boolean", "For single open, save every dirty loaded scene before replacement."),
                        Prop("discardModified", "boolean", "For single open, explicitly allow replacement of dirty loaded scenes without saving."),
                        Prop("save", "boolean", "For close, save a dirty scene before closing."),
                        Prop("discardChanges", "boolean", "For close, explicitly discard dirty scene changes."),
                        Prop("removeScene", "boolean", "For close, remove the scene from the workspace. Defaults to true.")
                    ));
                case "material/properties/get":
                    return Schema(Props(
                        Prop("assetPath", "string", "Material asset path below Assets/."),
                        ArrayProp("propertyNames", "string", "Optional shader property names. Omit to page through declared shader properties."),
                        Prop("offset", "number", "Shader property offset. Defaults to 0."),
                        Prop("limit", "number", "Maximum shader properties returned. Defaults to 100; capped at 500.")
                    ), "assetPath");
                case "material/properties/set":
                    return Schema(Props(
                        Prop("assetPath", "string", "Material asset path below Assets/."),
                        Prop("properties", "object", "Shader property values keyed by declared shader property name. Texture values accept assetPath plus optional scale and offset."),
                        Prop("keywords", "object", "Keyword changes with enable and disable string arrays."),
                        Prop("shader", "string", "Optional replacement shader name."),
                        Prop("renderQueue", "number", "Optional Material render queue."),
                        Prop("enableInstancing", "boolean", "Optional GPU instancing flag."),
                        Prop("doubleSidedGI", "boolean", "Optional double-sided global illumination flag."),
                        Prop("globalIlluminationFlags", "string", "Optional MaterialGlobalIlluminationFlags value."),
                        Prop("dryRun", "boolean", "Validate and return requested changes without modifying the Material.")
                    ), "assetPath");
                case "physics/raycast":
                    return Schema(Props(
                        Prop("dimension", "string", "Physics dimension: 2D or 3D. Defaults to Project Settings > Unity MCP > Tool Defaults (3D initially)."),
                        Prop("origin", "object", "Ray origin with x/y/z. z is ignored for 2D."),
                        Prop("direction", "object", "Ray direction with x/y/z. z is ignored for 2D."),
                        Prop("maxDistance", "number", "Maximum ray distance. Defaults to infinity."),
                        Prop("layerMask", "number", "Optional Physics or Physics2D layer mask."),
                        Prop("all", "boolean", "Return multiple hits rather than only the closest hit."),
                        Prop("maxResults", "number", "Maximum hits returned when all is true. Defaults to 100; capped at 500.")
                    ), "origin", "direction");
                case "physics/overlap-sphere":
                    return Schema(Props(
                        Prop("dimension", "string", "Physics dimension: 2D or 3D. Defaults to the Unity MCP project setting (3D initially). In 2D this performs an overlap circle."),
                        Prop("center", "object", "Query center with x/y/z. z is ignored for 2D."),
                        Prop("radius", "number", "Sphere or circle radius. Defaults to 1."),
                        Prop("layerMask", "number", "Optional Physics or Physics2D layer mask."),
                        Prop("maxResults", "number", "Maximum colliders returned. Defaults to 100; capped at 500.")
                    ), "center");
                case "physics/overlap-box":
                    return Schema(Props(
                        Prop("dimension", "string", "Physics dimension: 2D or 3D. Defaults to the Unity MCP project setting (3D initially)."),
                        Prop("center", "object", "Query center with x/y/z. z is ignored for 2D."),
                        Prop("halfExtents", "object", "Half extents with x/y/z. In 2D, x/y are doubled into box size."),
                        Prop("angle", "number", "2D box rotation in degrees. Ignored for 3D."),
                        Prop("layerMask", "number", "Optional Physics or Physics2D layer mask."),
                        Prop("maxResults", "number", "Maximum colliders returned. Defaults to 100; capped at 500.")
                    ), "center", "halfExtents");
                case "search/scene":
                    return Schema(Props(
                        Prop("name", "string", "Optional case-insensitive GameObject name substring or regular expression."),
                        Prop("regex", "boolean", "Interpret name as a regular expression with a bounded match timeout. Defaults to false."),
                        Prop("componentType", "string", "Optional Component type name or full name that must exist on the GameObject."),
                        Prop("tag", "string", "Optional exact Unity Tag."),
                        Prop("layer", "string", "Optional Unity Layer name or numeric index."),
                        Prop("shader", "string", "Optional case-insensitive shader-name substring used by a Renderer on the GameObject."),
                        Prop("includeInactive", "boolean", "Include inactive GameObjects. Defaults to true."),
                        Prop("offset", "number", "Stable result offset. Defaults to 0."),
                        Prop("limit", "number", "Maximum results. Defaults to 200; capped at 500.")));
                case "_meta/capabilities":
                case "queue/info":
                    return Schema(Props());
                case "queue/status":
                case "queue/cancel":
                    return Schema(Props(
                        Prop("ticketId", "number", "Owned queue ticket identifier.")), "ticketId");
                case "asset/create-folder":
                    return Schema(Props(
                        Prop("path", "string", "Folder path below Assets/."),
                        Prop("dryRun", "boolean", "Validate and report without creating folders.")), "path");
                case "asset/copy":
                    return Schema(Props(
                        Prop("sourcePath", "string", "Single source asset path."),
                        Prop("targetPath", "string", "Single target asset path."),
                        ArrayProp("copies", "object", "Optional batch of sourcePath/targetPath objects."),
                        Prop("overwrite", "boolean", "Replace existing targets with rollback snapshots."),
                        Prop("dryRun", "boolean", "Preflight without copying.")));
                case "asset/dependencies":
                    return Schema(Props(
                        Prop("path", "string", "Asset whose references should be inspected."),
                        Prop("direction", "string", "outgoing, incoming, or both. Defaults to both."),
                        Prop("recursive", "boolean", "Use recursive dependency resolution. Defaults to true."),
                        ArrayProp("searchRoots", "string", "Folders scanned for incoming references. Defaults to Assets."),
                        Prop("offset", "number", "Result offset."),
                        Prop("limit", "number", "Maximum results. Defaults to 100; capped at 500.")), "path");
                case "asset/transaction":
                    return Schema(Props(
                        ArrayProp("operations", "object", "Ordered ensure-folder, copy, move, delete, or serialized-set operations."),
                        ArrayProp("requiredAssets", "string", "Assets or folders that must exist after execution."),
                        ArrayProp("referenceChecks", "object", "Postconditions containing assetPath and requiredDependencies."),
                        Prop("dryRun", "boolean", "Preflight all operations without mutation.")), "operations");
                case "uitoolkit/edit-uxml":
                    return Schema(Props(
                        Prop("assetPath", "string", "UXML asset path below Assets/."),
                        ArrayProp("operations", "object", "Ordered structural UXML edit operations."),
                        Prop("dryRun", "boolean", "Return the edit result without writing.")), "assetPath", "operations");
                case "uitoolkit/edit-uss":
                    return Schema(Props(
                        Prop("assetPath", "string", "USS asset path below Assets/."),
                        ArrayProp("operations", "object", "Ordered selector/declaration edit operations."),
                        Prop("dryRun", "boolean", "Return the edit result without writing.")), "assetPath", "operations");
                case "uitoolkit/authoring-transaction":
                    return Schema(Props(
                        ArrayProp("edits", "object", "Ordered edit objects with kind, assetPath, and operations."),
                        Prop("dryRun", "boolean", "Validate all edits without writing.")), "edits");
                case "packages/add":
                    return Schema(Props(
                        Prop("identifier", "string", "Registry package name, Git URL, local path, or tarball identifier.")),
                        "identifier");
                case "packages/list":
                    return Schema(Props(
                        Prop("offset", "number", "Result offset."),
                        Prop("limit", "number", "Maximum packages. Defaults to 100; capped at 200.")));
                case "packages/remove":
                    return Schema(Props(
                        Prop("name", "string", "Installed package name to remove.")), "name");
                case "packages/search":
                    return Schema(Props(
                        Prop("query", "string", "Registry search query."),
                        Prop("offset", "number", "Result offset."),
                        Prop("limit", "number", "Maximum returned packages. Defaults to 50; capped at 200.")),
                        "query");
                case "advanced/execute":
                    return Schema(Props(
                        Prop("route", "string", "Unity route to execute, e.g. prefab-asset/transaction-edit or project-tools/execute."),
                        Prop("method", "string", "HTTP-like method used for the nested route. Defaults to POST."),
                        Prop("args", "object", "Arguments passed to the nested route."),
                        Prop("body", "string", "Optional raw JSON body. If provided, args are ignored."),
                        Prop("expectedProjectPath", "string", "Optional safety check. The request is rejected if it reaches a different Unity project.")
                    ), "route");
                case "localization/status":
                    return Schema(Props());
                case "localization/locales":
                    return Schema(Props(
                        Prop("includePseudo", "boolean", "Include PseudoLocale assets. Defaults to true.")
                    ));
                case "localization/create-locale":
                    return Schema(Props(
                        Prop("code", "string", "Locale code, for example en-US or zh-CN."),
                        Prop("assetPath", "string", "Locale asset path under Assets ending in .asset."),
                        Prop("name", "string", "Optional Locale display name."),
                        Prop("addToProject", "boolean", "Register the Locale with Localization Settings. Defaults to true.")
                    ), "code", "assetPath");
                case "localization/set-selected-locale":
                    return Schema(Props(
                        Prop("locale", "string", "Registered Locale code to select.")
                    ), "locale");
                case "localization/collections":
                    return Schema(Props(
                        Prop("type", "string", "Optional collection type filter: string or asset."),
                        Prop("nameContains", "string", "Optional case-insensitive collection name filter.")
                    ));
                case "localization/create-collection":
                    return Schema(Props(
                        Prop("name", "string", "Table Collection name."),
                        Prop("type", "string", "Collection type: string or asset."),
                        Prop("assetDirectory", "string", "Existing or new directory under Assets."),
                        ArrayProp("locales", "string", "Optional Locale codes. Defaults to every registered Locale."),
                        Prop("group", "string", "Optional Localization window group."),
                        Prop("preload", "boolean", "Optional preload flag for all created tables.")
                    ), "name", "type", "assetDirectory");
                case "localization/entries":
                    return Schema(Props(
                        Prop("collection", "string", "Table Collection name or GUID."),
                        Prop("type", "string", "Collection type: string or asset. Defaults to string."),
                        Prop("locale", "string", "Optional Locale code filter."),
                        Prop("keyContains", "string", "Optional case-insensitive key filter."),
                        Prop("offset", "number", "Filtered key offset. Defaults to 0."),
                        Prop("limit", "number", "Maximum keys returned. Defaults to 100; capped at 500.")
                    ), "collection");
                case "localization/upsert-entry":
                    return LocalizationUpsertEntriesSchema();
                case "localization/remove-entry":
                    return Schema(Props(
                        Prop("collection", "string", "Table Collection name or GUID."),
                        Prop("type", "string", "Collection type: string or asset. Defaults to string."),
                        Prop("key", "string", "Localization key to remove."),
                        Prop("locale", "string", "Optional Locale code. Omit to remove the shared key from every table.")
                    ), "collection", "key");
                case "localization/validate":
                    return Schema(Props(
                        Prop("collection", "string", "Optional Table Collection name or GUID."),
                        Prop("type", "string", "Optional collection type filter: string or asset."),
                        Prop("includeEmpty", "boolean", "Report empty values as well as missing entries. Defaults to true."),
                        Prop("maxIssues", "number", "Maximum issues returned. Defaults to 200; capped at 2000.")
                    ));
                case "localization/settings":
                    return Schema(Props(
                        Prop("initializeSynchronously", "boolean", "Optional Localization initialization mode."),
                        Prop("projectLocale", "string", "Optional registered project Locale code."),
                        Prop("selectedLocale", "string", "Optional registered selected Locale code.")
                    ));
                case "localization/variables":
                    return Schema(Props(
                        Prop("group", "string", "Optional case-insensitive persistent variable group filter."),
                        Prop("nameContains", "string", "Optional case-insensitive variable name filter.")
                    ));
                case "localization/upsert-variable":
                    return Schema(Props(
                        Prop("group", "string", "Persistent variable group name."),
                        Prop("name", "string", "Variable name inside the group."),
                        Prop("type", "string", "Variable type: bool, int, long, float, double, string, or object."),
                        AnyJsonValueProp("value", "Variable value. Object variables accept an Assets path."),
                        Prop("groupAssetPath", "string", "Required asset path when creating a missing VariablesGroupAsset.")
                    ), "group", "name", "type", "value");
                case "localization/remove-variable":
                    return Schema(Props(
                        Prop("group", "string", "Persistent variable group name."),
                        Prop("name", "string", "Variable name to remove.")
                    ), "group", "name");
                case "packages/update-git":
                    return Schema(Props(
                        Prop("name", "string", "Package name, e.g. com.example.package"),
                        Prop("gitUrl", "string", "Optional Git URL. Defaults to the current manifest Git URL."),
                        Prop("ref", "string", "Optional branch, tag, or commit. Defaults to main."),
                        Prop("skipIfResolved", "boolean", "Skip Package Manager resolve when packages-lock already matches the requested Git commit. Defaults to true."),
                        Prop("force", "boolean", "Force Package Manager resolve even when packages-lock already matches. Defaults to false.")
                    ), "name");
                case "packages/status":
                    return Schema(Props(
                        Prop("name", "string", "Optional package name. If omitted, returns all Git dependencies from the manifest."),
                        Prop("includeResolved", "boolean", "Include Package Manager resolved package data when available. Defaults to false.")
                    ));
                case "packages/lint-metas":
                    return Schema(Props(
                        Prop("name", "string", "Installed package name to lint."),
                        Prop("path", "string", "Absolute or project-relative package path to lint."),
                        Prop("all", "boolean", "Lint all resolved package roots."),
                        Prop("checkDirectories", "boolean", "Also require directory .meta files. Defaults to true."),
                        Prop("maxResults", "number", "Maximum missing entries returned per package.")
                    ));
                case "wait/editor-idle":
                    return Schema(Props(
                        Prop("timeoutMs", "number", "Maximum wait time in milliseconds. Defaults to 30000."),
                        Prop("stableFrames", "number", "Number of consecutive idle editor frames required. Defaults to 3."),
                        Prop("stableMs", "number", "Minimum continuous idle time in milliseconds. Defaults to 500.")
                    ));
                case "mcp/health":
                    return Schema(Props(
                        Prop("includeRecentActions", "boolean", "Include recent and slow action details. Defaults to false so health checks remain compact."),
                        Prop("recentCount", "number", "Number of recent MCP actions to return when includeRecentActions is true. Defaults to 20."),
                        Prop("slowThresholdMs", "number", "Recent actions at or above this duration are listed as slow. Defaults to 1000.")
                    ));
                case "mcp/set-autostart":
                    return Schema(Props(
                        Prop("enabled", "boolean", "Whether this Unity Editor instance should auto-start the MCP bridge after reload.")
                    ), "enabled");
                case "jobs/list":
                    return Schema(Props(
                        Prop("jobType", "string", "Optional job type filter."),
                        Prop("status", "string", "Optional status filter."),
                        Prop("offset", "number", "Result offset."),
                        Prop("limit", "number", "Maximum jobs. Defaults to 50; capped at 200.")));
                case "jobs/get":
                    return Schema(Props(
                        Prop("jobId", "string", "Job identifier."),
                        Prop("jobType", "string", "Optional job type disambiguator."),
                        Prop("jobAccessToken", "string", "Capability token returned when a persistent job starts. Required after the originating MCP agent disconnects.")), "jobId");
                case "jobs/cancel":
                    return Schema(Props(
                        Prop("jobId", "string", "Persistent job identifier returned by its start route."),
                        Prop("jobType", "string", "Optional job type disambiguator."),
                        Prop("jobAccessToken", "string", "Capability token returned when a persistent job starts. Required after the originating MCP agent disconnects.")
                    ), "jobId");
                case "jobs/cleanup":
                    return Schema(Props(
                        Prop("jobId", "string", "Terminal persistent job identifier whose explicit cleanup contract should run."),
                        Prop("jobAccessToken", "string", "Capability token returned when the persistent job started.")
                    ), "jobId");
                case "vfxgraph/info":
                    return Schema(Props(
                        Prop("assetPath", "string", "VisualEffectAsset path below Assets/."),
                        Prop("maxObjects", "number", "Maximum semantic graph nodes returned. Defaults to 250; capped at 500."),
                        Prop("maxExposedProperties", "number", "Maximum exposed properties returned. Defaults to 100; capped at 500."),
                        Prop("maxConnections", "number", "Maximum connections among returned nodes and properties. Defaults to 500; capped at 2000."),
                        Prop("maxSlotsPerNode", "number", "Maximum input and output slots per node when includeSlots is true. Defaults to 50; capped at 200."),
                        Prop("maxProperties", "number", "Maximum visible serialized properties per graph object when includeSerialized is true. Defaults to 40; capped at 500."),
                        Prop("includeSlots", "boolean", "Include typed input/output slot values for each node. Defaults to false."),
                        Prop("includeSerialized", "boolean", "Include a recursively budgeted serialized graph diagnostic. Defaults to false.")
                    ), "assetPath");
                case "vfxgraph/transaction":
                    return AssetGraphTransactionSchema("VFX Graph");
                case "audio-mixer/info":
                    return Schema(Props(
                        Prop("assetPath", "string", "AudioMixer asset path below Assets/."),
                        Prop("maxGroups", "number", "Maximum groups returned. Defaults to 100; capped at 500."),
                        Prop("maxSnapshots", "number", "Maximum snapshots returned. Defaults to 100; capped at 500."),
                        Prop("maxEffects", "number", "Maximum detailed effects returned. Defaults to 100; capped at 500."),
                        Prop("maxChildrenPerGroup", "number", "Maximum child groups listed per group. Defaults to 50; capped at 200."),
                        Prop("maxEffectsPerGroup", "number", "Maximum effect references listed per group. Defaults to 50; capped at 200."),
                        Prop("maxParametersPerEffect", "number", "Maximum parameter definitions returned per effect. Defaults to 50; capped at 200."),
                        Prop("maxExposedParameters", "number", "Maximum exposed parameters returned. Defaults to 100; capped at 500."),
                        Prop("maxObjects", "number", "Maximum mixer subassets in the optional serialized diagnostic. Defaults to 100; capped at 500."),
                        Prop("maxProperties", "number", "Maximum visible serialized properties per object when includeSerialized is true. Defaults to 40; capped at 500."),
                        Prop("includeSerialized", "boolean", "Include a recursively budgeted serialized mixer diagnostic. Defaults to false.")
                    ), "assetPath");
                case "audio-mixer/transaction":
                    return Schema(Props(
                        Prop("assetPath", "string", "AudioMixer asset path below Assets/."),
                        ArrayProp("operations", "object", "Ordered semantic group, snapshot, effect, exposed-parameter, snapshot-value, rename, or set-property operations. Runtime exposed-parameter overrides must use a separate transaction."),
                        Prop("dryRun", "boolean", "Validate and describe the transaction without changing the mixer.")
                    ), "assetPath", "operations");
                case "build/profile":
                    return Schema(Props(
                        Prop("action", "string", "Build Profile action: info (default) or transaction."),
                        ArrayProp("operations", "object", "For transaction, ordered set-active, set-scenes, set-scripting-defines, set-global-scenes, or set-property operations."),
                        Prop("dryRun", "boolean", "Validate and return current profiles plus requested operations without mutation."),
                        Prop("includeAfter", "boolean", "Include a paginated post-transaction Build Profile snapshot. Defaults to false; operation results are returned regardless."),
                        Prop("offset", "number", "Build Profile offset for info or includeAfter. Defaults to 0."),
                        Prop("limit", "number", "Maximum Build Profiles for info or includeAfter. Defaults to 50; capped at 200.")
                    ));
                case "addressables/info":
                    return Schema(Props(
                        Prop("offset", "number", "Addressable entry offset. Defaults to 0."),
                        Prop("limit", "number", "Maximum entries returned. Defaults to 100; capped at 500.")
                    ));
                case "addressables/transaction":
                    return Schema(Props(
                        ArrayProp("operations", "object", "Ordered create/remove/default-group, add/remove/rename-label, create-or-move-entry, set-address, set-label, or remove-entry operations."),
                        Prop("dryRun", "boolean", "Validate and describe the Addressables transaction without modifying settings.")
                    ), "operations");
                case "addressables/build":
                    return Schema(Props());
                case "timeline/info":
                    return Schema(Props(
                        Prop("assetPath", "string", "TimelineAsset path below Assets/."),
                        Prop("maxTracks", "number", "Maximum tracks returned across the semantic hierarchy. Defaults to 250; capped at 1000."),
                        Prop("maxClipsPerTrack", "number", "Maximum clips returned per track. Defaults to 100; capped at 500."),
                        Prop("maxMarkersPerTrack", "number", "Maximum markers returned per track. Defaults to 100; capped at 500."),
                        Prop("maxObjects", "number", "Maximum Timeline subassets returned. Defaults to 250; capped at 500."),
                        Prop("maxProperties", "number", "Maximum serialized properties per Timeline object when includeSerialized is true. Defaults to 60; capped at 500."),
                        Prop("includeSerialized", "boolean", "Include a recursively budgeted serialized Timeline diagnostic. Defaults to false.")
                    ), "assetPath");
                case "timeline/transaction":
                    return Schema(Props(
                        Prop("assetPath", "string", "TimelineAsset path below Assets/."),
                        ArrayProp("operations", "object", "Ordered create-track, delete-track, rename-track, set-track-property, create-clip, delete-clip, or set-clip operations."),
                        Prop("dryRun", "boolean", "Validate and return the current Timeline plus requested operations without mutation."),
                        Prop("includeAfter", "boolean", "Include a bounded post-transaction Timeline snapshot. Defaults to false; operation results are returned regardless."),
                        Prop("maxTracks", "number", "Maximum tracks in includeAfter. Defaults to 250; capped at 1000."),
                        Prop("maxClipsPerTrack", "number", "Maximum clips per track in includeAfter. Defaults to 100; capped at 500."),
                        Prop("maxMarkersPerTrack", "number", "Maximum markers per track in includeAfter. Defaults to 100; capped at 500.")
                    ), "assetPath", "operations");
                case "cinemachine/info":
                    return Schema(Props(
                        Prop("assetPath", "string", "Optional prefab asset path. Omit to inspect loaded scenes."),
                        Prop("includeProperties", "boolean", "Include bounded serialized properties for every Cinemachine component. Defaults to false."),
                        Prop("maxProperties", "number", "Maximum serialized properties per component. Defaults to 60; capped at 200."),
                        Prop("offset", "number", "Cinemachine component offset. Defaults to 0."),
                        Prop("limit", "number", "Maximum Cinemachine components returned. Defaults to 100; capped at 500.")
                    ));
                case "cinemachine/transaction":
                    return Schema(Props(
                        Prop("assetPath", "string", "Optional prefab asset path. Omit to edit loaded scene objects."),
                        ArrayProp("operations", "object", "Ordered set-property, set-object-reference, or set-enabled operations. Select scene objects by scenePath plus GameObject path, and components or target components by type plus zero-based index."),
                        Prop("dryRun", "boolean", "Resolve and describe every operation without modifying scene or prefab data.")
                    ), "operations");
                case "instance/current":
                    return Schema(Props());
                case "instance/list":
                    return Schema(Props(
                        Prop("includeStale", "boolean", "Include registry entries whose editor process may no longer be running. Defaults to false.")
                    ));
                case "instance/resolve":
                    return Schema(Props(
                        Prop("projectPath", "string", "Unity project root path to resolve. Exact normalized path match."),
                        Prop("projectName", "string", "Unity project name to resolve. Ambiguous names return an error."),
                        Prop("port", "number", "MCP bridge port to resolve.")
                    ));
                case "instance/assert-project":
                    return Schema(Props(
                        Prop("expectedProjectPath", "string", "Expected Unity project root path."),
                        Prop("expectedProjectName", "string", "Expected Unity project name.")
                    ));
                case "asset/export-unitypackage":
                    return Schema(Props(
                        ArrayProp("assetPaths", "string", "Unity asset paths to export, e.g. Assets/MyFolder or Assets/MyPrefab.prefab."),
                        Prop("outputPath", "string", "Absolute path or project-root-relative path for the .unitypackage output."),
                        Prop("includeDependencies", "boolean", "Include asset dependencies. Defaults to true."),
                        Prop("recurse", "boolean", "Recursively export folder contents. Defaults to true."),
                        Prop("overwrite", "boolean", "Replace an existing output file. Defaults to false."),
                        Prop("interactive", "boolean", "Show Unity's export package UI. Defaults to false.")
                    ), "outputPath");
                case "asset/import-unitypackage":
                    return Schema(Props(
                        Prop("packagePath", "string", "Absolute path or project-root-relative path to a .unitypackage file. Import is always non-interactive.")
                    ), "packagePath");
                case "editor/play-mode":
                    return Schema(Props(
                        Prop("action", "string", "Target action: play, pause, resume, step, or stop. Defaults to play. Pause is idempotent; step advances one frame and remains paused."),
                        Prop("timeoutMs", "number", "Maximum time to wait for the confirmed target state. Defaults to 10000."),
                        Prop("stableFrames", "number", "Consecutive Editor updates that must confirm the target state. Defaults to 2.")
                    ));
                case "editor/execute-code":
                    return Schema(Props(
                        Prop("code", "string", "C# method body to execute. Return a value to serialize it."),
                        ArrayProp("usings", "string", "Additional namespace imports for this call. Recurring imports can be configured in Project Settings > Unity MCP > Execute Code. UnityEngine.UIElements is included by default."),
                        Prop("maxResultItems", "number", "Maximum serialized collection/object entries across the result. Defaults to 200; capped at 2000."),
                        Prop("maxResultDepth", "number", "Maximum serialized result depth. Defaults to 8; capped at 16."),
                        Prop("maxResultStringLength", "number", "Maximum characters per returned string. Defaults to 20000; capped at 200000."),
                        EnumProp("unityStructFormat", "Unity value structs in the result: compact strings or structured typed objects. Defaults to compact.", "compact", "structured"),
                        Prop("includeStackTrace", "boolean", "Include a full managed stack trace when executed code throws. Defaults to false."),
                        Prop("idempotencyKey", "string", "Optional project-scoped key. Repeating the same key returns the existing persistent job instead of executing code again."),
                        Prop("cleanupCode", "string", "Optional C# method body used only by jobs/cleanup to reverse temporary state created by this job.")
                    ), "code");
                case "profiler/enable":
                    return Schema(Props(
                        Prop("enabled", "boolean", "Enable or disable Profiler recording. Defaults to true."),
                        Prop("deepProfiling", "boolean", "Optional deep profiling state.")
                    ));
                case "profiler/stats":
                case "profiler/memory":
                case "profiler/analyze":
                case "profiler/memory-status":
                    return Schema(Props());
                case "profiler/frame-data":
                    return Schema(Props(
                        Prop("frameIndex", "number", "Recorded Profiler frame index. Defaults to the latest frame."),
                        Prop("threadIndex", "number", "Profiler thread index. Defaults to 0 for Main Thread."),
                        Prop("maxItems", "number", "Maximum timing entries. Defaults to 30."),
                        Prop("minTimeMs", "number", "Exclude nested timing entries below this total time.")
                    ));
                case "profiler/memory-breakdown":
                    return Schema(Props(
                        Prop("includeDetails", "boolean", "Include the largest assets in each category."),
                        Prop("maxPerCategory", "number", "Maximum detailed assets per category. Defaults to 5.")
                    ));
                case "profiler/memory-top-assets":
                    return Schema(Props(
                        Prop("count", "number", "Maximum assets to return. Defaults to 20."),
                        Prop("type", "string", "Optional asset type filter such as texture, mesh, audio, material, shader, animation, or font.")
                    ));
                case "profiler/memory-snapshot":
                    return Schema(Props(
                        Prop("path", "string", "Optional output directory. Defaults to Unity's temporary cache MemorySnapshots folder."),
                        Prop("timeoutMs", "number", "Maximum time to wait for snapshot completion. Defaults to 120000.")
                    ));
                case "profiler/memory-snapshot-status":
                    return Schema(Props(
                        Prop("jobId", "string", "Optional snapshot job ID. Defaults to the current job in this Editor session.")
                    ));
                case "scene/hierarchy":
                    return Schema(Props(
                        Prop("maxDepth", "number", "Maximum hierarchy depth to return. Defaults to 10."),
                        Prop("maxNodes", "number", "Maximum hierarchy nodes to return. Defaults to 250; capped at 2000."),
                        Prop("parentPath", "string", "Optional GameObject path used as the search root."),
                        Prop("componentType", "string", "Optional component type name or full name. When set, returns compact flat matches instead of the full hierarchy."),
                        Prop("nameContains", "string", "Optional case-insensitive GameObject name filter used with componentType."),
                        Prop("pathContains", "string", "Optional case-insensitive hierarchy path filter used with componentType."),
                        Prop("offset", "number", "Component-filtered result offset. Defaults to 0."),
                        Prop("maxResults", "number", "Maximum component-filtered matches. Defaults to min(maxNodes, 50); capped at 200.")
                    ));
                case "testing/list-tests":
                    return Schema(Props(
                        Prop("mode", "string", "Test mode: EditMode or PlayMode. Defaults to EditMode."),
                        Prop("nameFilter", "string", "Optional case-insensitive test full-name filter."),
                        Prop("offset", "number", "Test result offset. Defaults to 0."),
                        Prop("maxResults", "number", "Maximum tests to return. Defaults to 100; capped at 500.")
                    ));
                case "testing/run-tests":
                    return Schema(Props(
                        Prop("mode", "string", "Test mode: EditMode or PlayMode. Defaults to EditMode."),
                        ArrayProp("testNames", "string", "Optional exact test full names."),
                        ArrayProp("categories", "string", "Optional test categories."),
                        ArrayProp("assemblies", "string", "Optional test assembly names."),
                        ArrayProp("groupNames", "string", "Optional Unity Test Runner group names."),
                        Prop("clearStuck", "boolean", "Force-clear a previously stuck job before starting. Defaults to false.")
                    ));
                case "testing/get-job":
                    return Schema(Props(
                        Prop("jobId", "string", "Optional job ID. Defaults to the current or latest job."),
                        Prop("includeDetails", "boolean", "Include paginated individual test results. Defaults to false."),
                        Prop("includeFailedOnly", "boolean", "Include only failed or inconclusive test results."),
                        Prop("includeStackTrace", "boolean", "Include test stack traces. Defaults to false."),
                        Prop("offset", "number", "Individual test result offset. Defaults to 0."),
                        Prop("limit", "number", "Individual test result limit. Defaults to 100; capped at 500."),
                        Prop("failureLimit", "number", "Maximum failures included in progress. Defaults to 20; capped at 100.")
                    ));
                case "testing/run-package-tests":
                    return Schema(Props(
                        Prop("packageName", "string", "Git package name. Defaults to com.vm233.unity-mcp."),
                        Prop("mode", "string", "Test mode: EditMode or PlayMode. Defaults to EditMode."),
                        ArrayProp("assemblies", "string", "Test assembly names. Defaults to the Unity MCP regression assembly for the Unity MCP package."),
                        ArrayProp("testNames", "string", "Optional exact test full names."),
                        ArrayProp("categories", "string", "Optional test categories."),
                        ArrayProp("groupNames", "string", "Optional Unity Test Runner group names.")
                    ));
                case "testing/get-package-job":
                    return Schema(Props(
                        Prop("workflowId", "string", "Optional package test workflow ID. Defaults to the active or latest workflow."),
                        Prop("clear", "boolean", "Delete terminal workflow state after returning it. Defaults to false.")
                    ));
                case "scene/instantiate-prefab":
                    return Schema(Props(
                        Prop("prefabPath", "string", "Prefab asset path to instantiate into the currently open scene."),
                        Prop("name", "string", "Optional name for the created scene instance."),
                        Prop("parent", "string", "Optional scene GameObject name used as the parent."),
                        Prop("position", "object", "Optional world position object with x/y/z."),
                        Prop("rotation", "object", "Optional world Euler rotation object with x/y/z.")
                    ), "prefabPath");
                case "prefab-asset/hierarchy":
                    return Schema(Props(
                        Prop("assetPath", "string", "Prefab asset path to inspect."),
                        Prop("prefabPath", "string", "Optional GameObject path used as the hierarchy root."),
                        Prop("maxDepth", "number", "Maximum hierarchy depth to return. Defaults to 10."),
                        Prop("maxNodes", "number", "Maximum hierarchy nodes to return. Defaults to 250; capped at 2000.")
                    ), "assetPath");
                case "prefab-asset/get-properties":
                    return Schema(Props(
                        Prop("assetPath", "string", "Prefab asset path to inspect."),
                        Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        Prop("componentType", "string", "Component type name or full name.")
                    ), "assetPath", "componentType");
                case "prefab-asset/set-property":
                    return Schema(Props(
                        Prop("assetPath", "string", "Prefab asset path to edit."),
                        Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        Prop("componentType", "string", "Component type name or full name."),
                        Prop("propertyName", "string", "Serialized property name or property path to set."),
                        AnyJsonValueProp("value", "Serialized value to assign. Accepts primitive values, arrays, and objects. A primitive scalar may be wrapped as {value: ...} when the MCP client exposes this field as an object."),
                        Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                        Prop("prefabFileDiffContextLines", "number", "Context lines around prefab YAML changes. Defaults to 2."),
                        Prop("prefabFileDiffMaxLines", "number", "Maximum diff lines returned. Defaults to 200."),
                        Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "componentType", "propertyName", "value");
                case "prefab-asset/set-reference":
                    return Schema(Props(
                        Prop("assetPath", "string", "Prefab asset path to edit."),
                        Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        Prop("componentType", "string", "Component type name or full name. Optional when propertyName can identify the component."),
                        Prop("propertyName", "string", "ObjectReference serialized property name or property path."),
                        Prop("referenceAssetPath", "string", "Project asset path to assign."),
                        Prop("referencePrefabPath", "string", "Path of a GameObject inside the same prefab to assign."),
                        Prop("referenceComponentType", "string", "When using referencePrefabPath, assign this component instead of the GameObject."),
                        Prop("clear", "boolean", "Clear the ObjectReference."),
                        Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                        Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "propertyName");
                case "prefab-asset/instantiate-child-prefab":
                    return Schema(Props(
                        Prop("assetPath", "string", "Target prefab asset path to edit."),
                        Prop("sourcePrefabPath", "string", "Prefab asset path to instantiate into the target prefab."),
                        Prop("parentPrefabPath", "string", "Parent path inside the target prefab. Empty means root."),
                        Prop("name", "string", "Optional name override for the created GameObject."),
                        Prop("siblingIndex", "number", "Optional sibling index under the parent."),
                        Prop("position", "object", "Optional local position object with x/y/z."),
                        Prop("rotation", "object", "Optional local Euler rotation object with x/y/z."),
                        Prop("scale", "object", "Optional local scale object with x/y/z.")
                    ), "assetPath", "sourcePrefabPath");
                case "prefab-asset/add-gameobject":
                    return Schema(Props(
                        Prop("assetPath", "string", "Prefab asset path to edit."),
                        Prop("parentPrefabPath", "string", "Parent path inside the prefab. Empty means root."),
                        Prop("name", "string", "Name of the new child GameObject."),
                        Prop("primitiveType", "string", "Optional Unity PrimitiveType to create, e.g. Cube or Sphere."),
                        Prop("layer", "string", "Optional Unity layer name or numeric index. Defaults to the parent GameObject's layer."),
                        Prop("position", "object", "Optional local position object with x/y/z."),
                        Prop("rotation", "object", "Optional local Euler rotation object with x/y/z."),
                        Prop("scale", "object", "Optional local scale object with x/y/z."),
                        Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                        Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "name");
                case "prefab-asset/add-component":
                    return Schema(Props(
                        Prop("assetPath", "string", "Prefab asset path to edit."),
                        Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        Prop("componentType", "string", "Component type name or full name."),
                        JsonValueMapProp("properties", "Optional serialized property names/paths mapped to initial JSON values. Values are applied before the new component is saved."),
                        Prop("waitForType", "boolean", "Wait for compilation/import until the component type is available. Defaults to true."),
                        Prop("typeResolveTimeoutMs", "number", "Maximum type wait time in milliseconds. Defaults to 30000."),
                        Prop("typeResolveStableMs", "number", "Continuous idle time after type resolution before editing. Defaults to 500."),
                        Prop("refreshAssets", "boolean", "Call AssetDatabase.Refresh once before waiting. Defaults to true."),
                        Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                        Prop("prefabFileDiffContextLines", "number", "Context lines around prefab YAML changes. Defaults to 2."),
                        Prop("prefabFileDiffMaxLines", "number", "Maximum diff lines returned. Defaults to 200."),
                        Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary."),
                        ArrayProp("prefabFileDiffIgnoreContains", "string", "Optional substrings used to hide noisy diff lines."),
                        ArrayProp("prefabFileDiffIgnoreYamlProperties", "string", "Optional YAML property names used to hide noisy diff lines.")
                    ), "assetPath", "componentType");
                case "prefab-asset/configure-component":
                    return PrefabAssetConfigureComponentSchema();
                case "prefab-asset/remove-component":
                    return Schema(Props(
                        Prop("assetPath", "string", "Prefab asset path to edit."),
                        Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        Prop("componentType", "string", "Component type name or full name."),
                        Prop("componentIndex", "number", "Component index when multiple components of the same type exist. Defaults to 0."),
                        Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                        Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "componentType");
                case "prefab-asset/move-component":
                    return Schema(Props(
                        Prop("assetPath", "string", "Prefab asset path to edit."),
                        Prop("sourcePrefabPath", "string", "Path of the source GameObject inside the prefab. Empty means root."),
                        Prop("targetPrefabPath", "string", "Path of the target GameObject inside the prefab. Empty means root."),
                        Prop("componentType", "string", "Component type name or full name."),
                        Prop("componentIndex", "number", "Component index on the source GameObject. Defaults to 0."),
                        Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                        Prop("prefabFileDiffContextLines", "number", "Context lines around prefab YAML changes. Defaults to 2."),
                        Prop("prefabFileDiffMaxLines", "number", "Maximum diff lines returned. Defaults to 200."),
                        Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "sourcePrefabPath", "targetPrefabPath", "componentType");
                case "prefab-asset/move-gameobject":
                    return Schema(Props(
                        Prop("assetPath", "string", "Prefab asset path to edit."),
                        Prop("prefabPath", "string", "Path of the GameObject to move inside the prefab."),
                        Prop("newParentPrefabPath", "string", "New parent path inside the prefab. Empty means root."),
                        Prop("siblingIndex", "number", "Optional sibling index under the new parent."),
                        Prop("worldPositionStays", "boolean", "Preserve world transform while reparenting. Defaults to false.")
                    ), "assetPath", "prefabPath");
                case "prefab-asset/remove-gameobject":
                    return Schema(Props(
                        Prop("assetPath", "string", "Prefab asset path to edit."),
                        Prop("prefabPath", "string", "Path of the child GameObject to remove. Cannot be root."),
                        Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                        Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "prefabPath");
                case "prefab-asset/find":
                    return Schema(Props(
                        Prop("assetPath", "string", "Prefab asset path to search."),
                        Prop("name", "string", "Exact GameObject name filter."),
                        Prop("nameContains", "string", "Case-insensitive GameObject name contains filter."),
                        Prop("pathContains", "string", "Case-insensitive prefab path contains filter."),
                        Prop("componentType", "string", "Optional component type name or full name filter."),
                        Prop("propertyName", "string", "Optional serialized property name/path to require on the component."),
                        Prop("propertyValue", "string", "Optional serialized property value to match."),
                        Prop("maxResults", "number", "Maximum returned matches. Defaults to 50.")
                    ), "assetPath");
                case "prefab-asset/transaction-edit":
                    return PrefabAssetTransactionEditSchema();
                case "prefab-asset/cleanup-missing-overrides":
                    return Schema(Props(
                        Prop("assetPath", "string", "Prefab Variant asset path to clean."),
                        Prop("dryRun", "boolean", "Report removable overrides without saving. Defaults to false."),
                        Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                        Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath");
                case "component/set-reference":
                    return ComponentSetReferenceSchema();
                case "component/set-property":
                    return Schema(Props(
                        Prop("instanceId", "string", "Target scene GameObject instance id."),
                        Prop("path", "string", "Target scene GameObject hierarchy path when instanceId is omitted."),
                        Prop("componentType", "string", "Component type name or full name."),
                        Prop("propertyName", "string", "Serialized property name, or inherited Behaviour property name such as enabled."),
                        AnyJsonValueProp("value", "Property value. Accepts primitive values, arrays, and objects. A primitive scalar may be wrapped as {value: ...} when the MCP client exposes this field as an object.")
                    ), "componentType", "propertyName", "value");
                case "serialized-object/get":
                    return Schema(Props(
                        Prop("instanceId", "number", "Target Unity object instance id."),
                        Prop("assetPath", "string", "Target asset path if instanceId is omitted."),
                        Prop("assetType", "string", "Optional asset type name/full name used when loading assetPath."),
                        Prop("gameObjectPath", "string", "Scene GameObject path if instanceId and assetPath are omitted."),
                        Prop("componentType", "string", "Optional component type to select from a GameObject target."),
                        Prop("componentIndex", "number", "Component index when multiple components of the same type exist."),
                        Prop("propertyPath", "string", "Optional serialized property path to read."),
                        Prop("offset", "number", "Visible property offset. Defaults to 0."),
                        Prop("maxProperties", "number", "Maximum properties to return when propertyPath is omitted. Defaults to 50; capped at 500."),
                        Prop("includeChildren", "boolean", "Walk child properties. Defaults to false."),
                        Prop("maxDepth", "number", "Maximum nested serialized value depth. Defaults to 3; capped at 8."),
                        Prop("maxArrayElements", "number", "Maximum elements returned per serialized array. Defaults to 50; capped at 500.")
                    ));
                case "serialized-object/set":
                    return Schema(Props(
                        Prop("instanceId", "number", "Target Unity object instance id."),
                        Prop("assetPath", "string", "Target asset path if instanceId is omitted."),
                        Prop("assetType", "string", "Optional asset type name/full name used when loading assetPath."),
                        Prop("gameObjectPath", "string", "Scene GameObject path if instanceId and assetPath are omitted."),
                        Prop("componentType", "string", "Optional component type to select from a GameObject target."),
                        Prop("componentIndex", "number", "Component index when multiple components of the same type exist."),
                        Prop("propertyPath", "string", "Serialized property path to write."),
                        AnyJsonValueProp("value", "Serialized value. A primitive scalar may be wrapped as {value: ...} when the MCP client exposes this field as an object. ObjectReference supports assetPath, instanceId, or gameObject. SerializeReference objects may include '$managedReferenceType' as 'AssemblyName::Namespace.TypeName'.")
                    ), "propertyPath", "value");
                case "asset/rename":
                    return Schema(Props(
                        Prop("path", "string", "Current asset path, e.g. Assets/Art/Old Name.png."),
                        Prop("newName", "string", "New file or folder name. Do not include a directory path."),
                        Prop("dryRun", "boolean", "Validate and return expected paths without renaming.")
                    ));
                case "asset/import":
                    return AssetImportSchema();
                case "asset/refresh":
                    return Schema(Props(
                        ArrayProp("assetPaths", "string", "Optional Unity asset paths to import. When supplied, only these paths are imported, with known dependencies before dependents. Omit to run a full synchronous AssetDatabase refresh and reconcile all external changes."),
                        Prop("forceUpdate", "boolean", "Use ImportAssetOptions.ForceUpdate for full refreshes and non-compilation targeted assets. Compilation assets are always imported without ForceUpdate to avoid broad dependency reimports. Defaults to false."),
                        Prop("saveAssets", "boolean", "Call AssetDatabase.SaveAssets after refresh/import. Defaults to false."),
                        Prop("clearStuck", "boolean", "Replace a non-terminal refresh job left behind by an interrupted editor session. Defaults to false.")
                    ));
                case "asset/get-refresh-job":
                    return Schema(Props(
                        Prop("jobId", "string", "Optional refresh job ID. Defaults to the current or latest job."),
                        Prop("refreshRequestId", "string", "Optional original asset/refresh request ID used to recover the matching persistent job after a transport timeout or domain reload."),
                        Prop("clear", "boolean", "Clear the persisted job after a terminal result is read. Defaults to false."),
                        Prop("timeoutMs", "number", "Maximum reload reconnection wait consumed by the MCP transport. Defaults to 300000ms.")
                    ));
                case "asset/move":
                    return AssetMoveSchema();
                case "console/query":
                    return Schema(Props(
                        Prop("count", "number", "Maximum returned entries. Defaults to 50; capped at 200."),
                        Prop("offset", "number", "Filtered entry offset, counting from the newest match. Defaults to 0."),
                        Prop("type", "string", "Filter by all, error, warning, info, exception, or assert. Defaults to all."),
                        Prop("messageContains", "string", "Case-insensitive message substring filter."),
                        Prop("sourceContains", "string", "Case-insensitive source stack frame/path substring filter."),
                        Prop("stackContains", "string", "Case-insensitive full stack substring filter."),
                        Prop("since", "string", "Start time filter. Accepts ISO/local time, Unix seconds, or Unix milliseconds."),
                        Prop("until", "string", "End time filter. Accepts ISO/local time, Unix seconds, or Unix milliseconds."),
                        Prop("sinceSecondsAgo", "number", "Start time filter relative to now."),
                        Prop("sinceLastPlay", "boolean", "Only include entries recorded after the latest Play transition."),
                        Prop("includeStack", "boolean", "Include full stack traces. Defaults to false."),
                        Prop("newestFirst", "boolean", "Return newest entries first. Defaults to false.")
                    ));
                case "debug/attach-unity":
                    return Schema(Props(
                        Prop("openWindow", "boolean", "Open Unity's Managed Debugger window. Defaults to false."),
                        Prop("waitForAttach", "boolean", "Wait briefly for an external managed debugger to attach. Defaults to false."),
                        Prop("timeoutMs", "number", "Attach wait timeout in milliseconds when waitForAttach is true. Defaults to 0.")
                    ));
                case "debug/set-breakpoint":
                    return Schema(Props(
                        Prop("file", "string", "Source file path for the requested breakpoint."),
                        Prop("line", "number", "1-based source line for the requested breakpoint.")
                    ), "file", "line");
                case "debug/stack-trace":
                    return Schema(Props(
                        Prop("skipFrames", "number", "Number of MCP call frames to skip. Defaults to 0."),
                        Prop("maxFrames", "number", "Maximum stack frames to return. Defaults to 50.")
                    ));
                case "debug/variables":
                    return Schema(Props(
                        Prop("frameId", "number", "Paused debugger frame id.")
                    ), "frameId");
                case "debug/evaluate":
                    return Schema(Props(
                        Prop("expression", "string", "C# expression to evaluate in Unity Editor context. Wrapped as return <expression>; when code is omitted."),
                        Prop("code", "string", "Full C# method body for editor-context evaluation.")
                    ));
                case "animation/transition-info":
                    return Schema(Props(
                        Prop("controllerPath", "string", "AnimatorController asset path."),
                        Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                        Prop("sourceState", "string", "Optional source state name filter."),
                        Prop("destinationState", "string", "Optional destination state, state machine, or Exit filter."),
                        Prop("fromAnyState", "boolean", "When true, only inspect Any State transitions. When false, only inspect state transitions."),
                        Prop("transitionIndex", "number", "Optional transition index under the source.")
                    ), "controllerPath");
                case "animation/update-state":
                    return Schema(Props(
                        Prop("controllerPath", "string", "AnimatorController asset path."),
                        Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                        Prop("stateName", "string", "State name to modify."),
                        Prop("newStateName", "string", "Optional new state name."),
                        Prop("motionPath", "string", "AnimationClip or Motion asset path to assign."),
                        Prop("clearMotion", "boolean", "Clear the state's motion."),
                        Prop("speed", "number", "State speed."),
                        Prop("tag", "string", "State tag."),
                        Prop("position", "object", "State graph position object with x/y."),
                        Prop("isDefault", "boolean", "Set this state as the layer default state."),
                        Prop("writeDefaultValues", "boolean", "State write default values flag."),
                        Prop("mirror", "boolean", "State mirror flag."),
                        Prop("iKOnFeet", "boolean", "State IK on feet flag."),
                        Prop("cycleOffset", "number", "State cycle offset.")
                    ), "controllerPath", "stateName");
                case "animation/update-transition":
                    return AnimationUpdateTransitionSchema();
                case "animation/connect-states":
                    return Schema(Props(
                        Prop("controllerPath", "string", "AnimatorController asset path."),
                        Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                        ArrayProp("stateNames", "string", "State names to connect pairwise."),
                        Prop("skipExisting", "boolean", "Skip existing transitions. Defaults to true."),
                        Prop("replaceExisting", "boolean", "Remove existing matching transitions before creating new ones."),
                        Prop("hasExitTime", "boolean", "Transition has exit time applied to created transitions."),
                        Prop("exitTime", "number", "Transition exit time applied to created transitions."),
                        Prop("duration", "number", "Transition duration applied to created transitions."),
                        Prop("offset", "number", "Transition offset applied to created transitions."),
                        Prop("hasFixedDuration", "boolean", "Fixed duration flag applied to created transitions."),
                        ArrayProp("conditions", "object", "Conditions applied to every created transition.")
                    ), "controllerPath", "stateNames");
                case "animation/validate-controller":
                    return Schema(Props(
                        Prop("controllerPath", "string", "AnimatorController asset path."),
                        Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                        ArrayProp("requiredParameters", new Dictionary<string, object>
                        {
                            { "anyOf", new List<object>
                                {
                                    new Dictionary<string, object> { { "type", "string" } },
                                    new Dictionary<string, object> { { "type", "object" } },
                                }
                            },
                        }, "Strings or objects with name/parameterName and optional type/parameterType."),
                        ArrayProp("requiredStates", "string", "State names that must exist."),
                        Prop("requireMotion", "boolean", "Require every state in the layer to have a motion."),
                        ArrayProp("requiredTransitions", "object", "Objects with source/sourceState, destination/destinationState, and optional conditionParameter."),
                        Prop("requireFullMesh", "boolean", "Require all stateNames to have pairwise transitions."),
                        ArrayProp("stateNames", "string", "States used by full mesh validation. Defaults to all layer states.")
                    ), "controllerPath");
                case "project-tools/list":
                    return Schema(Props(
                        Prop("offset", "number", "Result offset."),
                        Prop("limit", "number", "Maximum project tools. Defaults to 100; capped at 200.")));
                case "project-tools/get":
                    return Schema(Props(
                        Prop("toolName", "string", "Exact project tool name from project-tools/list.")
                    ), "toolName");
                case "project-tools/execute":
                    return Schema(Props(
                        Prop("toolName", "string", "Project tool name from project-tools/list."),
                        Prop("args", "object", "Arguments passed to the project tool as Dictionary<string, object>."),
                        Prop("runAsJob", "boolean", "Run a normally synchronous project tool through the persistent job owner. Long-running tools always use a job."),
                        Prop("idempotencyKey", "string", "Optional project-scoped idempotency key for persistent execution.")
                    ), "toolName");
                case "uitoolkit/audit-uss-styles":
                    return Schema(Props(
                        ArrayProp("paths", "string", "Optional Assets-relative USS files. Omit to audit every USS file in the effective roots."),
                        ArrayProp("roots", "string", "Assets-relative roots used to index USS and UXML files. Defaults to the project audit settings, then Assets."),
                        ArrayProp("runtimeSourceRoots", "string", "Assets-relative roots scanned for UI Toolkit runtime class API references. Defaults to the project audit settings, then Assets."),
                        ArrayProp("excludePaths", "string", "Assets-relative files or folders excluded from indexing."),
                        Prop("useProjectSettings", "boolean", "Use ProjectSettings/UnityMCPUIToolkitAudit.json as the default scope. Defaults to true."),
                        Prop("pixelGridEnabled", "boolean", "Override the project pixel-grid audit switch. The project/default value is used when omitted."),
                        Prop("pixelGridStep", "number", "Override the positive pixel-grid step, such as 3 or 4."),
                        Prop("includeSuppressed", "boolean", "Include findings with a reasoned uss-audit suppression comment. Defaults to false."),
                        Prop("logWarnings", "boolean", "Also write active findings to the Unity Console. Defaults to false."),
                        Prop("runSelfTests", "boolean", "Run deterministic in-memory rule tests and return their result. Defaults to false."),
                        Prop("maxIssues", "number", "Maximum returned findings. Defaults to 200; capped at 5000.")
                    ));
                case "uitoolkit/audit-uxml-layout":
                    return Schema(Props(
                        ArrayProp("paths", "string", "Optional Assets-relative UXML files. Omit to audit every UXML file in the effective roots."),
                        ArrayProp("roots", "string", "Assets-relative roots used to index UXML and USS files. Defaults to the project audit settings, then Assets."),
                        ArrayProp("runtimeSourceRoots", "string", "Assets-relative roots scanned for runtime UI element-name references. Defaults to the project audit settings, then Assets."),
                        ArrayProp("excludePaths", "string", "Assets-relative files or folders excluded from indexing."),
                        Prop("useProjectSettings", "boolean", "Use ProjectSettings/UnityMCPUIToolkitAudit.json as the default scope. Defaults to true."),
                        Prop("pixelGridEnabled", "boolean", "Override the project pixel-grid audit switch. The project/default value is used when omitted."),
                        Prop("pixelGridStep", "number", "Override the positive pixel-grid step, such as 3 or 4."),
                        Prop("includeSuppressed", "boolean", "Include findings with a reasoned uxml-layout-audit suppression comment. Defaults to false."),
                        Prop("logWarnings", "boolean", "Also write active findings to the Unity Console. Defaults to false."),
                        Prop("runSelfTests", "boolean", "Run deterministic in-memory rule tests and return their result. Defaults to false."),
                        Prop("maxIssues", "number", "Maximum returned findings. Defaults to 200; capped at 5000.")
                    ));
                case "uitoolkit/windows":
                    return Schema(Props());
                case "uitoolkit/tree":
                    return EditorWindowSchema(Props(
                        Prop("maxDepth", "number", "Maximum tree depth. Defaults to 8."),
                        Prop("maxNodes", "number", "Maximum returned nodes. Defaults to 300."),
                        Prop("includeStyle", "boolean", "Include inline and resolved style summaries.")
                    ));
                case "uitoolkit/query":
                    return EditorWindowSchema(Props(
                        Prop("name", "string", "VisualElement.name exact match."),
                        Prop("className", "string", "USS class name exact match."),
                        Prop("typeName", "string", "VisualElement type name contains match."),
                        Prop("text", "string", "TextElement text contains match."),
                        Prop("maxResults", "number", "Maximum returned elements. Defaults to 50."),
                        Prop("includeStyle", "boolean", "Include inline and resolved style summaries.")
                    ));
                case "uitoolkit/style":
                    return EditorWindowSchema(Props(
                        Prop("path", "string", "Element path from uitoolkit/tree or uitoolkit/query."),
                        Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        Prop("className", "string", "USS class name exact match if path is omitted."),
                        Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        Prop("text", "string", "TextElement text contains match if path is omitted.")
                    ));
                case "uitoolkit/repaint":
                    return EditorWindowSchema(Props(
                        Prop("path", "string", "Optional element path from uitoolkit/tree or uitoolkit/query.")
                    ));
                case "uitoolkit/asset-inspect":
                    return Schema(Props(
                        Prop("uxmlPath", "string", "UXML asset path, e.g. Assets/UI/HUD.uxml."),
                        Prop("ussPath", "string", "Optional USS asset path. UXML Style src entries are also auto-resolved."),
                        ArrayProp("ussPaths", "string", "Optional USS asset paths. UXML Style src entries are also auto-resolved."),
                        Prop("name", "string", "VisualElement.name exact match."),
                        ArrayProp("names", "string", "VisualElement.name values to validate."),
                        Prop("className", "string", "USS class exact match."),
                        Prop("typeName", "string", "Expected or filtered VisualElement type name."),
                        Prop("maxResults", "number", "Total result budget for elements and name matches. Defaults to 100."),
                        Prop("includeUss", "boolean", "Parse USS files, keeping unconditional class defaults separate from contextual and pseudo-state rules. Defaults to true."),
                        Prop("includeElements", "boolean", "Return the general elements collection. Defaults to false for names queries and true otherwise."),
                        Prop("includeAllUssClasses", "boolean", "Return every parsed USS class. Targeted queries default to only classes used by returned elements.")
                    ));
                case "uitoolkit/runtime-documents":
                    return Schema(Props(
                        Prop("includeInactive", "boolean", "Include inactive scene UIDocuments. Defaults to true.")
                    ));
                case "uitoolkit/runtime-tree":
                    return RuntimeUIDocumentSchema(Props(
                        Prop("maxDepth", "number", "Maximum tree depth. Defaults to 8."),
                        Prop("maxNodes", "number", "Maximum returned nodes. Defaults to 300."),
                        Prop("includeStyle", "boolean", "Include inline, resolved, and background style summaries.")
                    ));
                case "uitoolkit/runtime-query":
                    return RuntimeUIDocumentSchema(Props(
                        Prop("path", "string", "Element tree path from runtime-tree, e.g. root/0/1."),
                        Prop("visualElementPath", "string", "Slash-separated VisualElementPath names, e.g. MainMap/RightControls."),
                        ArrayProp("visualElementNames", "string", "VisualElementPath names array."),
                        Prop("name", "string", "VisualElement.name exact match."),
                        Prop("className", "string", "USS class name exact match."),
                        Prop("typeName", "string", "VisualElement type name contains match."),
                        Prop("text", "string", "TextElement text contains match."),
                        Prop("maxResults", "number", "Maximum returned elements. Defaults to 50."),
                        Prop("includeStyle", "boolean", "Include inline, resolved, and background style summaries.")
                    ));
                case "uitoolkit/runtime-style":
                    return RuntimeUIDocumentSchema(Props(
                        Prop("path", "string", "Element tree path from runtime-tree, e.g. root/0/1."),
                        Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        ArrayProp("visualElementNames", "string", "VisualElementPath names array."),
                        Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        Prop("className", "string", "USS class name exact match if path is omitted."),
                        Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        Prop("text", "string", "TextElement text contains match if path is omitted.")
                    ));
                case "uitoolkit/diagnose-runtime":
                    return RuntimeUIDocumentSchema(Props(
                        ArrayProp("queries", "object", "Optional list of element queries. Each accepts path, visualElementPath, name, className, typeName, text, and pixelScale."),
                        Prop("path", "string", "Element tree path if queries is omitted."),
                        Prop("visualElementPath", "string", "Slash-separated VisualElementPath names if queries is omitted."),
                        ArrayProp("visualElementNames", "string", "VisualElementPath names array if queries is omitted."),
                        Prop("name", "string", "VisualElement.name exact match if queries is omitted."),
                        Prop("pixelScale", "number", "Pixel grid scale used for pixel diagnostics. Defaults to 1.")
                    ));
                case "uitoolkit/visual-check":
                    return RuntimeUIDocumentSchema(Props(
                        ArrayProp("checks", "object", "Visual checks. Supported type values: pixel-grid, background-scale, size."),
                        Prop("path", "string", "Element tree path if checks is omitted."),
                        Prop("visualElementPath", "string", "Slash-separated VisualElementPath names if checks is omitted."),
                        Prop("pixelScale", "number", "Pixel grid scale. Defaults to 1."),
                        Prop("expectedScale", "number", "Expected background image scale for background-scale checks."),
                        Prop("width", "number", "Expected element width for size checks."),
                        Prop("height", "number", "Expected element height for size checks."),
                        Prop("tolerance", "number", "Allowed pixel delta. Defaults to 0.01.")
                    ));
                case "uitoolkit/locate-element":
                    return RuntimeUIDocumentSchema(Props(
                        Prop("runtime", "boolean", "Locate a runtime UIDocument element when true; otherwise locate an EditorWindow UI Toolkit element. Defaults to false."),
                        Prop("window", "string", "EditorWindow type/title. Runtime defaults to Game when capture uses it later."),
                        Prop("path", "string", "Element tree path, e.g. root/0/1."),
                        Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        ArrayProp("visualElementNames", "string", "VisualElementPath names array."),
                        Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        Prop("className", "string", "USS class name exact match if path is omitted."),
                        Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        Prop("text", "string", "TextElement text contains match if path is omitted."),
                        Prop("pixelScale", "number", "Scale from UI points to captured pixels. Defaults to EditorGUIUtility.pixelsPerPoint."),
                        Prop("padding", "number", "Extra crop padding in pixels. Defaults to 0.")
                    ));
                case "uitoolkit/capture-element":
                    return RuntimeUIDocumentSchema(Props(
                        Prop("runtime", "boolean", "Capture a runtime UIDocument element when true; otherwise capture an EditorWindow UI Toolkit element. Defaults to false."),
                        Prop("window", "string", "EditorWindow type/title to capture. Runtime defaults to Game, editor defaults to the focused/matched window."),
                        Prop("path", "string", "Element tree path, e.g. root/0/1."),
                        Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        ArrayProp("visualElementNames", "string", "VisualElementPath names array."),
                        Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        Prop("className", "string", "USS class name exact match if path is omitted."),
                        Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        Prop("text", "string", "TextElement text contains match if path is omitted."),
                        Prop("outputPath", "string", "Output PNG path for the cropped element screenshot."),
                        Prop("windowOutputPath", "string", "Output PNG path for the full containing window screenshot."),
                        Prop("pixelScale", "number", "Scale from UI points to captured pixels. Defaults to EditorGUIUtility.pixelsPerPoint."),
                        Prop("padding", "number", "Extra crop padding in pixels. Defaults to 0.")
                    ));
                case "uitoolkit/compare-element":
                    return RuntimeUIDocumentSchema(Props(
                        Prop("runtime", "boolean", "Capture a runtime UIDocument element when true; otherwise capture an EditorWindow UI Toolkit element. Defaults to false."),
                        Prop("window", "string", "EditorWindow type/title to capture. Runtime defaults to Game."),
                        Prop("path", "string", "Element tree path, e.g. root/0/1."),
                        Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        Prop("className", "string", "USS class name exact match if path is omitted."),
                        Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        Prop("referencePath", "string", "Reference PNG path."),
                        Prop("actualPath", "string", "Output path for captured current element PNG."),
                        Prop("diffOutputPath", "string", "Optional output path for diff PNG."),
                        Prop("referenceRect", "object", "Optional comparison rect in reference image."),
                        Prop("actualRect", "object", "Optional comparison rect in captured image."),
                        Prop("tolerance", "number", "Allowed per-channel pixel delta. Defaults to 0."),
                        Prop("padding", "number", "Extra capture padding in pixels. Defaults to 0.")
                    ));
                case "uitoolkit/generated-children":
                    return RuntimeUIDocumentSchema(Props(
                        Prop("runtime", "boolean", "Inspect a runtime UIDocument element when true; otherwise inspect an EditorWindow UI Toolkit element. Defaults to false."),
                        Prop("window", "string", "EditorWindow type/title for editor inspection."),
                        Prop("path", "string", "Element tree path, e.g. root/0/1."),
                        Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        Prop("className", "string", "USS class name exact match if path is omitted."),
                        Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        Prop("maxDepth", "number", "Descendant depth to inspect. Defaults to 4."),
                        Prop("includeAll", "boolean", "Return all descendants, not only generated-looking children. Defaults to false."),
                        ArrayProp("forbiddenClassContains", "string", "Class substrings that should produce warnings when found."),
                        ArrayProp("forbiddenTypeContains", "string", "Type-name substrings that should produce warnings when found.")
                    ));
                case "uitoolkit/resource-audit":
                    return RuntimeUIDocumentSchema(Props(
                        Prop("runtime", "boolean", "Audit runtime UIDocument elements when true; otherwise audit EditorWindow UI Toolkit elements. Defaults to false."),
                        Prop("window", "string", "EditorWindow type/title for editor audits."),
                        ArrayProp("queries", "object", "Optional list of element queries. Each accepts path, visualElementPath, name, className, typeName, text, expectedBackgroundContains, forbiddenBackgroundContains, requireBackground."),
                        Prop("path", "string", "Element tree path if queries is omitted."),
                        Prop("visualElementPath", "string", "Slash-separated VisualElementPath names if queries is omitted."),
                        Prop("name", "string", "VisualElement.name exact match if queries is omitted."),
                        Prop("expectedBackgroundContains", "string", "Expected substring in resolved background asset path or name."),
                        ArrayProp("forbiddenBackgroundContains", "string", "Substrings that must not appear in the resolved background asset path or name."),
                        Prop("requireBackground", "boolean", "Warn if the target has no resolved background image."),
                        Prop("warnHighlighted", "boolean", "Warn when a target appears to use a highlighted asset. Defaults to true."),
                        Prop("maxDepth", "number", "Descendant depth to scan for background resources. Defaults to 3.")
                    ));
                case "uitoolkit/runtime-repaint":
                    return RuntimeUIDocumentSchema(Props(
                        Prop("path", "string", "Optional element tree path from runtime-tree."),
                        Prop("visualElementPath", "string", "Optional slash-separated VisualElementPath names."),
                        ArrayProp("visualElementNames", "string", "Optional VisualElementPath names array.")
                    ));
                case "uitoolkit/refresh":
                    return Schema(Props(
                        Prop("refreshAssets", "boolean", "Call AssetDatabase.Refresh before repainting. Defaults to true."),
                        Prop("forceSynchronousImport", "boolean", "Use ForceSynchronousImport. Defaults to true."),
                        Prop("timeoutMs", "number", "Maximum wait time in milliseconds. Defaults to 10000."),
                        Prop("stableFrames", "number", "Consecutive idle repaint frames required. Defaults to 2.")
                    ));
                case "uitoolkit/builder-preview":
                    return Schema(Props(
                        Prop("uxmlPath", "string", "UXML asset path to open in UI Builder."),
                        Prop("waitFrames", "number", "Editor frames to wait before capturing. Defaults to 8."),
                        Prop("stableFrames", "number", "Consecutive ready UI Builder frames required. Defaults to 2."),
                        Prop("timeoutMs", "number", "Maximum time to wait for the requested document and canvas. Defaults to 10000."),
                        Prop("capture", "boolean", "Capture the UI Builder window after opening. Defaults to true."),
                        Prop("autoMatchGameView", "boolean", "Enable UI Builder Match Game View when visible document content overflows the configured canvas. Defaults to true."),
                        Prop("requireContentFit", "boolean", "Fail the preview result when visible document content remains clipped by the canvas. Defaults to true."),
                        Prop("screenshotPath", "string", "PNG path for the UI Builder screenshot. Defaults to the Unity MCP project screenshot directory."),
                        Prop("maxDimension", "number", "Maximum screenshot dimension. Defaults to 8192."),
                        Prop("zoom", "number", "Requested zoom, recorded for diagnostics. UI Builder has no stable public zoom API.")
                    ));
                case "uitoolkit/assert-layout":
                    return RuntimeUIDocumentSchema(Props(
                        ArrayProp("assertions", "object", "Layout assertions. Supported types: edge-touch, same-edge, same-center, inside, size.")
                    ), "assertions");
                case "screenshot/game":
                    return Schema(Props(
                        Prop("path", "string", "Output PNG path. Defaults to the Unity MCP project screenshot directory (Assets/Screenshots initially)."),
                        Prop("superSize", "number", "Resolution multiplier. Defaults to 1."),
                        Prop("waitFrames", "number", "Frames to wait before requesting a running capture. Ignored while paused. Defaults to 2."),
                        Prop("stableFrames", "number", "Consecutive stable file-size frames required for a running capture. Ignored while paused. Defaults to 2."),
                        Prop("timeoutMs", "number", "Maximum time to wait for a complete decodable PNG. Defaults to 10000."),
                        Prop("editorOverlays", "string", "Game View Gizmos and Stats policy: suppress or preserve. Defaults to suppress; use preserve only when editor overlays are the evidence subject.")
                    ));
                case "screenshot/crop":
                    return Schema(Props(
                        Prop("sourcePath", "string", "Image path to crop."),
                        Prop("rect", "object", "Crop rect with x, y, width, height."),
                        Prop("outputPath", "string", "Output PNG path. Defaults next to source with _crop suffix."),
                        Prop("originTopLeft", "boolean", "Treat rect x/y as top-left image coordinates. Defaults to true.")
                    ));
                case "screenshot/scene":
                    return Schema(Props(
                        Prop("path", "string", "Output PNG path for file or both transport. Defaults to the Unity MCP project screenshot directory (Assets/Screenshots initially)."),
                        Prop("width", "number", "Capture width in pixels. Defaults to 1920."),
                        Prop("height", "number", "Capture height in pixels. Defaults to 1080."),
                        Prop("transport", "string", "Output transport: file, base64, or both. Defaults to file.")
                    ));
                case "screenshot/editor-window":
                    return Schema(Props(
                        Prop("window", "string", "EditorWindow type full name, simple type name, or exact tab title."),
                        Prop("typeOrTitle", "string", "Legacy alias for window."),
                        Prop("path", "string", "Output PNG path. Defaults to the Unity MCP project screenshot directory (Assets/Screenshots initially)."),
                        Prop("maxDimension", "number", "Maximum screenshot dimension. Defaults to 8192.")
                    ));
                case "graphics/asset-preview":
                    return Schema(Props(
                        Prop("assetPath", "string", "Asset path to preview, including prefab, material, mesh, or texture assets."),
                        Prop("width", "number", "Requested preview width in pixels. Defaults to 256."),
                        Prop("height", "number", "Requested preview height in pixels. Defaults to 256.")
                    ), "assetPath");
                case "gameview/info":
                    return Schema(Props());
                case "gameview/set-resolution":
                    return Schema(Props(
                        Prop("width", "number", "Game View custom resolution width in pixels."),
                        Prop("height", "number", "Game View custom resolution height in pixels."),
                        Prop("label", "string", "Optional custom size label shown in the Game View size menu.")
                    ), "width", "height");
                case "gameview/set-scale":
                    return Schema(Props(
                        Prop("mode", "string", "Scale source: value or minimum. Defaults to value."),
                        Prop("scale", "number", "Game View zoom scale when mode is value, e.g. 0.76 or 1."),
                        Prop("fallbackScale", "number", "Fallback minimum scale used if Unity internals do not expose a valid one. Defaults to 0.76.")
                    ));
                case "graphics/image-alpha-bounds":
                    return Schema(Props(
                        Prop("assetPath", "string", "Texture2D asset path."),
                        Prop("filePath", "string", "Absolute or project-relative PNG path if assetPath is omitted."),
                        Prop("alphaThreshold", "number", "Alpha threshold. 0-1 or 0-255. Defaults to 0.01.")
                    ));
                case "graphics/rect-gap":
                    return Schema(Props(
                        Prop("firstRect", "object", "First rect with x, y, width, height."),
                        Prop("secondRect", "object", "Second rect with x, y, width, height."),
                        Prop("axis", "string", "x or y. Defaults to x."),
                        Prop("firstEdge", "string", "First rect edge. Defaults to right for x, bottom for y."),
                        Prop("secondEdge", "string", "Second rect edge. Defaults to left for x, top for y."),
                        Prop("tolerance", "number", "Touch tolerance in pixels. Defaults to 0.5.")
                    ), "firstRect", "secondRect");
                case "graphics/annotate-rects":
                    return Schema(Props(
                        Prop("sourcePath", "string", "Image path to annotate."),
                        Prop("outputPath", "string", "Output PNG path. Defaults next to source with _annotated suffix."),
                        ArrayProp("rects", "object", "Rectangles to draw. Each has x, y, width, height, optional color and thickness."),
                        Prop("originTopLeft", "boolean", "Treat rect x/y as top-left image coordinates. Defaults to true."),
                        Prop("color", "string", "Default HTML color, e.g. #ff00ffff."),
                        Prop("thickness", "number", "Default border thickness in pixels. Defaults to 2.")
                    ), "rects");
                case "graphics/compare-images":
                    return Schema(Props(
                        Prop("expectedPath", "string", "Reference image path."),
                        Prop("actualPath", "string", "Current image path."),
                        Prop("expectedRect", "object", "Optional reference crop rect with x, y, width, height."),
                        Prop("actualRect", "object", "Optional current crop rect with x, y, width, height."),
                        Prop("tolerance", "number", "Per-channel pixel tolerance, 0-255. Defaults to 0."),
                        Prop("maxSamples", "number", "Maximum differing pixel samples returned. Defaults to 20."),
                        Prop("diffOutputPath", "string", "Optional PNG path to write a red-highlight diff image.")
                    ));
                case "sprite/sheet-info":
                    return Schema(Props(
                        Prop("texturePath", "string", "Sprite sheet texture asset path.")
                    ));
                case "sprite/pixel-check":
                    return Schema(Props(
                        Prop("assetPath", "string", "Texture/Sprite asset path."),
                        ArrayProp("assetPaths", "string", "Texture/Sprite asset paths."),
                        Prop("folderPath", "string", "Folder to scan recursively for Texture2D assets."),
                        Prop("dimensionsMultipleOf", "number", "Optional divisor required for texture width/height."),
                        Prop("expectedScale", "number", "Optional UI scale used to check source dimensions after scaling."),
                        Prop("tolerance", "number", "Allowed pixel delta. Defaults to 0.01."),
                        Prop("requirePointFilter", "boolean", "Warn if FilterMode is not Point. Defaults to true."),
                        Prop("requireNoCompression", "boolean", "Warn if default platform format is compressed. Defaults to true."),
                        Prop("requireNoMipMaps", "boolean", "Warn if mip maps are enabled. Defaults to true.")
                    ));
                case "sprite/replace-and-slice":
                case "sprite/slice-sheet":
                    return Schema(Props(
                        Prop("texturePath", "string", "Sprite sheet texture asset path."),
                        Prop("sourcePath", "string", "External image file to copy over texturePath. Required for replace-and-slice."),
                        Prop("frameWidth", "number", "Frame width in pixels."),
                        Prop("frameHeight", "number", "Frame height in pixels."),
                        Prop("frameCount", "number", "Optional frame count. Defaults to the full grid."),
                        Prop("baseName", "string", "Generated sprite name prefix. Defaults to texture file name."),
                        Prop("columns", "number", "Grid column count. Defaults to textureWidth / frameWidth."),
                        Prop("startX", "number", "Grid start x in pixels. Defaults to 0."),
                        Prop("startY", "number", "Grid start y in top-left pixels. Defaults to 0."),
                        Prop("pivotX", "number", "Optional normalized pivot x."),
                        Prop("pivotY", "number", "Optional normalized pivot y."),
                        Prop("preserveSpriteIDs", "boolean", "Preserve existing sprite IDs by generated name. Defaults to true.")
                    ), "texturePath", "frameWidth", "frameHeight");
                case "sprite/update-animation-clip":
                    return Schema(Props(
                        Prop("clipPath", "string", "AnimationClip asset path."),
                        Prop("texturePath", "string", "Sprite sheet texture asset path."),
                        Prop("bindingPath", "string", "Animation binding path to SpriteRenderer. Empty means the animated object itself."),
                        Prop("frameRate", "number", "Animation frame rate. Defaults to the clip frame rate or 12."),
                        ArrayProp("spriteNames", "string", "Optional exact sprite names to use."),
                        Prop("loopTime", "boolean", "Whether the clip loops. Defaults to the current clip setting.")
                    ), "clipPath", "texturePath");
                case "sprite/replace-slice-update-clip":
                    return Schema(Props(
                        Prop("texturePath", "string", "Sprite sheet texture asset path."),
                        Prop("sourcePath", "string", "External image file to copy over texturePath."),
                        Prop("clipPath", "string", "Optional AnimationClip asset path to update after slicing."),
                        Prop("frameWidth", "number", "Frame width in pixels."),
                        Prop("frameHeight", "number", "Frame height in pixels."),
                        Prop("frameCount", "number", "Optional frame count. Defaults to the full grid."),
                        Prop("baseName", "string", "Generated sprite name prefix. Defaults to texture file name."),
                        Prop("frameRate", "number", "Animation frame rate. Defaults to the clip frame rate or 12."),
                        Prop("bindingPath", "string", "Animation binding path to SpriteRenderer.")
                    ), "texturePath", "sourcePath", "frameWidth", "frameHeight");
                case "texture/apply-sprite-preset":
                    return Schema(Props(
                        Prop("path", "string", "Texture asset path."),
                        Prop("referencePath", "string", "Optional texture asset whose importer settings are copied first."),
                        Prop("preset", "string", "High-level preset. Supported: pixel-sprite."),
                        Prop("pixelsPerUnit", "number", "Sprite pixels per unit."),
                        Prop("filterMode", "string", "Texture FilterMode, e.g. Point."),
                        Prop("textureCompression", "string", "TextureImporterCompression value."),
                        Prop("defaultPlatformFormat", "string", "Default platform TextureImporterFormat, e.g. RGBA32."),
                        Prop("defaultPlatformCompression", "string", "Default platform TextureImporterCompression."),
                        Prop("readable", "boolean", "Texture is readable."),
                        Prop("mipmapEnabled", "boolean", "Generate mipmaps."),
                        Prop("alphaIsTransparency", "boolean", "Alpha is transparency."),
                        Prop("pivot", "object", "Sprite pivot with x/y."),
                        Prop("border", "object", "Sprite border. Accepts number, [left,bottom,right,top], or object with left/bottom/right/top.")
                    ), "path");
                case "texture/info":
                    return Schema(Props(
                        Prop("path", "string", "Texture asset path under Assets/.")
                    ), "path");
                case "texture/set-import":
                    return Schema(Props(
                        Prop("path", "string", "Texture asset path under Assets/."),
                        Prop("textureType", "string", "TextureImporterType, such as Default, Sprite, or NormalMap."),
                        Prop("spriteMode", "string", "SpriteImportMode, such as Single or Multiple."),
                        Prop("spritePixelsPerUnit", "number", "Sprite pixels per unit."),
                        Prop("sRGB", "boolean", "Import as sRGB texture."),
                        Prop("readable", "boolean", "Enable CPU read/write access."),
                        Prop("mipmapEnabled", "boolean", "Generate mipmaps."),
                        Prop("filterMode", "string", "Texture FilterMode."),
                        Prop("wrapMode", "string", "TextureWrapMode."),
                        Prop("maxTextureSize", "number", "Maximum imported texture size."),
                        Prop("textureCompression", "string", "TextureImporterCompression value."),
                        Prop("anisoLevel", "number", "Anisotropic filtering level."),
                        Prop("alphaIsTransparency", "boolean", "Treat alpha as transparency."),
                        Prop("npotScale", "string", "TextureImporterNPOTScale value.")
                    ), "path");
                case "texture/find-duplicates":
                    return Schema(Props(
                        Prop("folder", "string", "Single search folder under Assets/. Defaults to Assets."),
                        ArrayProp("folders", "string", "Additional search folders under Assets/. Results are de-duplicated across folders."),
                        Prop("mode", "string", "Comparison mode: decodedPixels (default) or fileBytes."),
                        ArrayProp("extensions", "string", "Optional file extensions such as png, jpg, or jpeg. decodedPixels supports PNG/JPEG."),
                        Prop("maxAssets", "number", "Maximum assets to fingerprint. Defaults to 10000; capped at 50000."),
                        Prop("maxGroups", "number", "Maximum duplicate groups returned. Defaults to 100; capped at 2000.")
                    ));
                case "texture/import-image":
                    return Schema(Props(
                        Prop("sourcePath", "string", "Local image file path."),
                        Prop("sourceUrl", "string", "Remote image URL."),
                        Prop("targetPath", "string", "Target asset path inside Assets."),
                        Prop("targetFolder", "string", "Target folder used with assetName."),
                        Prop("assetName", "string", "Target file name used with targetFolder."),
                        Prop("overwrite", "boolean", "Overwrite targetPath if content differs. Defaults to false."),
                        Prop("dedupeByHash", "boolean", "Skip if the target folder already contains identical image bytes. Defaults to true."),
                        Prop("applySpritePreset", "boolean", "Apply sprite import settings after import. Defaults to true."),
                        Prop("preset", "string", "Preset passed to texture/apply-sprite-preset. Defaults to pixel-sprite.")
                    ));
                case "texture/check-import-settings":
                    return Schema(Props(
                        Prop("assetPath", "string", "Texture asset path to check."),
                        ArrayProp("assetPaths", "string", "Texture asset paths to check."),
                        Prop("folderPath", "string", "Folder to scan recursively for Texture2D assets."),
                        Prop("referencePath", "string", "Optional texture asset whose importer settings are treated as expected."),
                        Prop("preset", "string", "Optional high-level preset to check. Supported: pixel-sprite."),
                        Prop("requirePixelSprite", "boolean", "Shortcut for preset=pixel-sprite. Defaults to true when referencePath is omitted."),
                        Prop("includeMatching", "boolean", "Include passing comparisons in the returned comparisons list. Defaults to false.")
                    ));
                case "texture/check-ui-import-settings":
                    return Schema(Props(
                        Prop("assetPath", "string", "Texture asset path to check."),
                        ArrayProp("assetPaths", "string", "Texture asset paths to check."),
                        Prop("folderPath", "string", "Folder to scan recursively for Texture2D assets."),
                        Prop("referencePath", "string", "Optional texture asset whose importer settings are treated as expected."),
                        Prop("includeMatching", "boolean", "Include passing comparisons in the returned comparisons list. Defaults to false."),
                        Prop("expectedWidth", "number", "Optional exact texture width check."),
                        Prop("expectedHeight", "number", "Optional exact texture height check."),
                        Prop("expectedBorder", "object", "Optional sprite border check. Accepts object with left/bottom/right/top or x/y/z/w."),
                        Prop("maxTextureSize", "number", "Optional exact TextureImporter maxTextureSize check."),
                        Prop("tolerance", "number", "Float tolerance for border/PPU checks. Defaults to 0.001.")
                    ));
                case "build/start":
                    return Schema(Props(
                        Prop("target", "string", "BuildTarget. Defaults to StandaloneWindows64."),
                        Prop("outputPath", "string", "Player output executable path."),
                        Prop("developmentBuild", "boolean", "Build with Development flag."),
                        ArrayProp("scenes", "string", "Optional scene paths. Defaults to enabled Build Settings scenes."),
                        Prop("overwrite", "boolean", "Delete existing exe and Data folder before build. Defaults to true."),
                        Prop("run", "boolean", "Launch the built executable after a successful build. Defaults to true."),
                        Prop("runSeconds", "number", "Seconds to let the executable run before sampling/termination. Defaults to 5."),
                        Prop("terminateAfter", "boolean", "Kill the process after sampling. Defaults to true."),
                        Prop("captureWindow", "boolean", "Capture the built player's main window on Windows. Defaults to false."),
                        Prop("screenshotPath", "string", "PNG path for captureWindow output."),
                        Prop("windowWaitMs", "number", "Milliseconds to wait for the main window. Defaults to 5000."),
                        Prop("logTailLines", "number", "Player.log tail lines to return. Defaults to 120."),
                        Prop("clearStuck", "boolean", "Replace a non-terminal build job left behind by an interrupted editor session. Defaults to false.")
                    ), "outputPath");
                case "build/get-job":
                    return Schema(Props(
                        Prop("jobId", "string", "Optional build job ID. Defaults to the current or latest job."),
                        Prop("clear", "boolean", "Clear the persisted job after a terminal result is read. Defaults to false.")
                    ));
                default:
                    return new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>() },
                        { "additionalProperties", true }
                    };
            }
        }

        private static Dictionary<string, object> AssetGraphTransactionSchema(string assetKind)
        {
            return Schema(Props(
                Prop("assetPath", "string", $"{assetKind} asset path below Assets/."),
                ArrayProp("operations", "object",
                    "Ordered rename or set-property operations. Target each subasset by localId or by type plus targetName."),
                Prop("dryRun", "boolean",
                    $"Validate and describe the {assetKind} transaction without modifying the asset.")
            ), "assetPath", "operations");
        }

        private static Dictionary<string, object> ExecutionSchema(bool includeContinueOnError = true)
        {
            var properties = Props(
                Prop("operationsPerFrame", "number", "Maximum operations processed in one editor frame. Defaults to 25."),
                Prop("frameBudgetMs", "number", "Soft per-frame execution budget in milliseconds. Defaults to 8."),
                Prop("timeoutMs", "number", "Maximum total execution time in milliseconds. Defaults to 90000."));
            properties["mode"] = new Dictionary<string, object>
            {
                { "type", "string" },
                { "description", "Execution mode. auto batches multi-operation requests, immediate runs in one frame, and batched yields across frames." },
                { "enum", new List<object> { "auto", "immediate", "batched" } },
            };
            if (includeContinueOnError)
                properties["continueOnError"] = Prop("continueOnError", "boolean",
                    "Continue processing later operations after one fails. Defaults to false.").Value;
            var schema = Schema(properties);
            schema["description"] = "Optional batching, frame-budget, timeout, and failure-continuation settings for this operation.";
            return schema;
        }

        private static Dictionary<string, object> ComponentSetReferenceSchema()
        {
            var referenceProperties = Props(
                Prop("path", "string", "Target scene GameObject path or name."),
                Prop("instanceId", "string", "Target scene GameObject instance ID."),
                Prop("componentType", "string", "Component containing the property."),
                Prop("propertyName", "string", "ObjectReference property to assign."),
                Prop("assetPath", "string", "Asset path to assign."),
                Prop("referenceGameObject", "string", "Scene GameObject path or name to assign."),
                Prop("referenceComponentType", "string", "Component type on the referenced GameObject."),
                Prop("referenceInstanceId", "number", "Unity object instance ID to assign."),
                Prop("clear", "boolean", "Clear the reference."));
            var properties = Props(
                Prop("path", "string", "Default target GameObject inherited by reference items."),
                Prop("instanceId", "string", "Default target instance ID inherited by reference items."),
                Prop("componentType", "string", "Default component type inherited by reference items."));
            properties["execution"] = ExecutionSchema();
            properties["references"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Reference assignments. Every item requires propertyName and one reference source or clear=true." },
                { "items", Schema(referenceProperties, "propertyName") },
            };
            return Schema(properties, "references");
        }

        private static Dictionary<string, object> AnimationUpdateTransitionSchema()
        {
            var conditionProperties = Props(
                Prop("parameter", "string", "Animator parameter name."),
                Prop("mode", "string", "AnimatorConditionMode value such as If, IfNot, Greater, Less, Equals, or NotEqual."),
                Prop("threshold", "number", "Condition threshold. Trigger and bool conditions normally use 0."));
            var updateConditionProperties = new Dictionary<string, object>(conditionProperties)
            {
                ["index"] = Prop("index", "number", "Zero-based condition index to update.").Value,
            };

            var properties = Props(
                Prop("controllerPath", "string", "AnimatorController asset path."),
                Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                Prop("sourceState", "string", "Source state name. Required unless fromAnyState is true."),
                Prop("destinationState", "string", "Destination state, state machine, or Exit filter."),
                Prop("fromAnyState", "boolean", "Modify an Any State transition."),
                Prop("transitionIndex", "number", "Optional transition index under the source."),
                Prop("hasExitTime", "boolean", "Transition has exit time."),
                Prop("exitTime", "number", "Transition exit time."),
                Prop("duration", "number", "Transition duration."),
                Prop("offset", "number", "Transition offset."),
                Prop("hasFixedDuration", "boolean", "Use fixed duration."),
                Prop("interruptionSource", "string", "TransitionInterruptionSource value."),
                Prop("orderedInterruption", "boolean", "Ordered interruption flag."),
                Prop("canTransitionToSelf", "boolean", "Any State can transition to self flag."));
            properties["conditions"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Replace all conditions with condition objects." },
                { "items", Schema(conditionProperties, "parameter") },
            };
            properties["addConditions"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Append condition objects." },
                { "items", Schema(conditionProperties, "parameter") },
            };
            properties["updateConditions"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Update condition objects by zero-based index." },
                { "items", Schema(updateConditionProperties, "index") },
            };
            properties["removeConditionIndexes"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Remove conditions by zero-based index." },
                { "items", new Dictionary<string, object> { { "type", "number" } } },
            };

            return Schema(properties, "controllerPath");
        }

        private static Dictionary<string, object> PrefabAssetConfigureComponentSchema()
        {
            var referenceProperties = Props(
                Prop("propertyName", "string", "ObjectReference serialized property name or path."),
                Prop("referenceAssetPath", "string", "Project asset path to assign."),
                Prop("referencePrefabPath", "string", "Path of a GameObject inside the same prefab to assign."),
                Prop("referenceComponentType", "string", "When using referencePrefabPath, assign this component instead of the GameObject."),
                Prop("referenceComponentIndex", "number", "Component index on referencePrefabPath when multiple components of the same type exist. Defaults to 0."),
                Prop("clear", "boolean", "Clear the ObjectReference."));
            var properties = Props(
                Prop("assetPath", "string", "Prefab asset path to edit."),
                Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                Prop("componentType", "string", "Component type name or full name."),
                Prop("componentIndex", "number", "Component index when multiple components of the same type exist. Defaults to 0."),
                Prop("addIfMissing", "boolean", "Add the component when componentIndex equals the current component count. Defaults to true."),
                JsonValueMapProp("properties", "Serialized property names/paths mapped to JSON values."),
                Prop("waitForTypes", "boolean", "Wait for referenced component types before editing. Defaults to true."),
                Prop("typeResolveTimeoutMs", "number", "Maximum type wait time in milliseconds. Defaults to 30000."),
                Prop("typeResolveStableMs", "number", "Continuous idle time after type resolution before editing. Defaults to 500."),
                Prop("refreshAssets", "boolean", "Schedule AssetDatabase.Refresh only when a referenced component type is missing. Defaults to true."),
                Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary."));
            properties["references"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "ObjectReference assignments applied to the configured component." },
                { "items", Schema(referenceProperties, "propertyName") },
            };
            return Schema(properties, "assetPath", "componentType");
        }

        private static Dictionary<string, object> PrefabAssetTransactionEditSchema()
        {
            var properties = Props(
                Prop("assetPath", "string", "Prefab asset path to edit."),
                Prop("waitForTypes", "boolean", "Wait for all referenced component types before editing. Defaults to true."),
                Prop("typeResolveTimeoutMs", "number", "Maximum type wait time in milliseconds. Defaults to 30000."),
                Prop("typeResolveStableMs", "number", "Continuous idle time after type resolution before editing. Defaults to 500."),
                Prop("refreshAssets", "boolean", "When referenced component types are missing, return a retryable response and schedule AssetDatabase.Refresh after the response. The refresh is skipped when all types are already loaded. Defaults to true."),
                Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                Prop("prefabFileDiffContextLines", "number", "Context lines around prefab YAML changes. Defaults to 2."),
                Prop("prefabFileDiffMaxLines", "number", "Maximum diff lines returned. Defaults to 200."),
                Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary."),
                ArrayProp("prefabFileDiffIgnoreContains", "string", "Optional substrings used to hide noisy diff lines."),
                ArrayProp("prefabFileDiffIgnoreYamlProperties", "string", "Optional YAML property names used to hide noisy diff lines.")
            );
            properties["execution"] = ExecutionSchema(includeContinueOnError: false);

            properties["operations"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Ordered prefab edits. Each item uses type plus the fields accepted by the matching prefab-asset route. addGameObject accepts an optional layer name or numeric index and otherwise inherits its parent's layer." },
                { "items", new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>
                            {
                                { "type", new Dictionary<string, object>
                                    {
                                        { "type", "string" },
                                        { "description", "Prefab edit operation kind. The remaining fields are interpreted by the selected operation." },
                                        { "enum", new List<object>
                                            {
                                                "addComponent", "configureComponent", "setProperty", "setReference", "addGameObject",
                                                "instantiatePrefab", "removeComponent", "removeGameObject", "moveGameObject",
                                                "arrayInsert", "arrayRemove", "arraySet", "arrayClear"
                                            }
                                        }
                                    }
                            }
                        }
                        },
                        { "required", new List<object> { "type" } },
                        { "additionalProperties", true }
                    }
                }
            };

            return Schema(properties, "assetPath", "operations");
        }

        private static Dictionary<string, object> AssetMoveSchema()
        {
            var moveProperties = Props(
                Prop("path", "string", "Current asset path."),
                Prop("destinationPath", "string", "Destination asset path, or an existing folder path to keep the same file name."),
                Prop("destinationFolder", "string", "Existing folder path to keep the same file name.")
            );

            var properties = Props(
                Prop("dryRun", "boolean", "Validate every move and return expected paths without moving."));
            properties["execution"] = ExecutionSchema();
            properties["moves"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Move requests. Every item needs path and either destinationPath or destinationFolder. Duplicate sources and targets are rejected before execution." },
                { "items", Schema(moveProperties) }
            };

            return Schema(properties, "moves");
        }

        private static Dictionary<string, object> AssetImportSchema()
        {
            var settingProperties = Props(
                Prop("overwrite", "boolean", "Replace an existing destination asset while preserving and restoring it if the batch rolls back. Defaults to false."),
                Prop("dedupeMode", "string", "Duplicate comparison: decodedPixels, fileBytes, or none. PNG/JPEG defaults to decodedPixels; other files default to none."),
                Prop("dedupeScope", "string", "Existing-asset search scope: assets (default), destinationFolder, or searchPath."),
                Prop("dedupeSearchPath", "string", "Folder under Assets/ used when dedupeScope is searchPath."),
                Prop("onDuplicate", "string", "Duplicate handling: skip (default), error, or report. report imports while returning duplicate metadata."),
                Prop("textureType", "string", "TextureImporterType such as Sprite or Default."),
                Prop("spriteMode", "string", "Sprite import mode: Single, Multiple, Polygon, or None."),
                Prop("pixelsPerUnit", "number", "Sprite pixels per unit."),
                Prop("filterMode", "string", "Texture filter mode: Point, Bilinear, or Trilinear."),
                Prop("isReadable", "boolean", "Enable CPU texture reads."),
                Prop("compression", "string", "Compression: uncompressed, low, normal, or high."),
                Prop("alphaIsTransparency", "boolean", "Treat alpha as transparency."),
                Prop("meshType", "string", "Sprite mesh type: FullRect or Tight."),
                Prop("mipmapEnabled", "boolean", "Generate mipmaps."));
            settingProperties["spriteSlice"] = new Dictionary<string, object>
            {
                { "type", "object" },
                { "description", "Optional explicit fixed-grid sprite slicing applied after import. Use this for sparse animation frames instead of Unity automatic slicing." },
                { "properties", Props(
                    Prop("frameWidth", "number", "Required width of each grid frame in pixels."),
                    Prop("frameHeight", "number", "Required height of each grid frame in pixels."),
                    Prop("frameCount", "number", "Optional number of frames. Defaults to every full grid cell."),
                    Prop("baseName", "string", "Generated sprite-name prefix. Defaults to the imported file name."),
                    Prop("columns", "number", "Optional grid column count. Defaults to all full columns."),
                    Prop("startX", "number", "Grid start x in pixels. Defaults to 0."),
                    Prop("startY", "number", "Grid start y in top-left pixels. Defaults to 0."),
                    Prop("pivotX", "number", "Optional normalized pivot x. Must be supplied with pivotY."),
                    Prop("pivotY", "number", "Optional normalized pivot y. Must be supplied with pivotX."),
                    Prop("preserveSpriteIDs", "boolean", "Preserve existing sprite IDs by generated name when replacing an asset. Defaults to true.")
                ) },
                { "required", new List<string> { "frameWidth", "frameHeight" } }
            };
            var importProperties = new Dictionary<string, object>(settingProperties)
            {
                ["sourcePath"] = Prop("sourcePath", "string", "Absolute external source file path.").Value,
                ["destinationPath"] = Prop("destinationPath", "string", "Destination Unity asset path under Assets/.").Value,
            };
            var properties = Props(
                Prop("dryRun", "boolean", "Validate every source, destination, collision, and importer setting without importing."));
            properties["defaults"] = new Dictionary<string, object>
            {
                { "type", "object" },
                { "description", "Shared overwrite, duplicate detection, and TextureImporter settings inherited by every import item. Item fields override these defaults." },
                { "properties", settingProperties },
            };
            properties["execution"] = ExecutionSchema();
            properties["imports"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Up to 500 import requests. Every item requires sourcePath and destinationPath. The full batch is preflighted before files are changed." },
                { "items", Schema(importProperties, "sourcePath", "destinationPath") },
                { "maxItems", 500 },
            };
            return Schema(properties, "imports");
        }

        private static Dictionary<string, object> LocalizationUpsertEntriesSchema()
        {
            var entryProperties = Props(
                Prop("key", "string", "Shared localization key."),
                Prop("locale", "string", "Target Locale code."),
                Prop("value", "string", "String or Smart String value when type is string."),
                Prop("smart", "boolean", "Optional Smart String flag when type is string."),
                Prop("assetPath", "string", "Asset path when type is asset."),
                Prop("subAssetName", "string", "Optional exact sub-asset name at assetPath."));

            var properties = Props(
                Prop("collection", "string", "Table Collection name or GUID."),
                Prop("type", "string", "Collection type: string or asset. Defaults to string."),
                Prop("createTables", "boolean", "Create missing Locale tables. Defaults to true."));
            properties["execution"] = ExecutionSchema();
            properties["entries"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Up to 500 Locale entry writes. The entire request is validated before changes are made." },
                { "items", Schema(entryProperties, "key", "locale") },
            };

            return Schema(properties, "collection", "entries");
        }

        private static Dictionary<string, object> EditorWindowSchema(Dictionary<string, object> extraProps)
        {
            var props = Props(
                Prop("instanceId", "number", "EditorWindow instance id from uitoolkit/windows."),
                Prop("window", "string", "Window title, type name, full type name, or instance id."),
                Prop("windowType", "string", "EditorWindow type name or full type name."),
                Prop("title", "string", "EditorWindow title text.")
            );

            foreach (var pair in extraProps)
                props[pair.Key] = pair.Value;

            return Schema(props);
        }

        private static Dictionary<string, object> RuntimeUIDocumentSchema(Dictionary<string, object> extraProps, params string[] required)
        {
            var props = Props(
                Prop("documentInstanceId", "number", "UIDocument instance id from uitoolkit/runtime-documents."),
                Prop("gameObjectPath", "string", "Scene GameObject path that owns the UIDocument."),
                Prop("gameObjectName", "string", "Scene GameObject name that owns the UIDocument."),
                Prop("documentName", "string", "UIDocument component name."),
                Prop("includeInactive", "boolean", "Include inactive scene UIDocuments. Defaults to true.")
            );

            foreach (var pair in extraProps)
                props[pair.Key] = pair.Value;

            return Schema(props, required);
        }

        private static Dictionary<string, object> Schema(Dictionary<string, object> properties, params string[] required)
        {
            var schema = new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", properties },
            };

            if (required != null && required.Length > 0)
                schema["required"] = required.ToList();

            return schema;
        }

        private static Dictionary<string, object> Props(params KeyValuePair<string, object>[] properties)
        {
            var result = new Dictionary<string, object>();
            foreach (var pair in properties)
                result[pair.Key] = pair.Value;
            return result;
        }

        private static KeyValuePair<string, object> Prop(string name, string type, string description)
        {
            return new KeyValuePair<string, object>(name, new Dictionary<string, object>
            {
                { "type", type },
                { "description", description },
            });
        }

        private static KeyValuePair<string, object> ArrayProp(
            string name,
            string itemType,
            string description)
        {
            return ArrayProp(name, new Dictionary<string, object>
            {
                { "type", itemType },
            }, description);
        }

        private static KeyValuePair<string, object> ArrayProp(
            string name,
            Dictionary<string, object> itemSchema,
            string description)
        {
            return new KeyValuePair<string, object>(name, new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", description },
                { "items", itemSchema },
            });
        }

        private static KeyValuePair<string, object> AnyJsonValueProp(string name, string description)
        {
            return new KeyValuePair<string, object>(name, new Dictionary<string, object>
            {
                { "description", description },
            });
        }

        private static KeyValuePair<string, object> JsonValueMapProp(
            string name, string description)
        {
            return new KeyValuePair<string, object>(name, new Dictionary<string, object>
            {
                { "type", "object" },
                { "description", description },
                { "additionalProperties", true },
            });
        }

    }
}
