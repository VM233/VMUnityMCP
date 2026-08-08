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

        private static string ExtractCategory(string path)
        {
            int slash = path.IndexOf('/');
            return slash > 0 ? path.Substring(0, slash) : path;
        }

        public static object GetRegisteredTools(bool compact = true,
            bool includeSchema = false, int offset = 0, int limit = 50, string category = null,
            bool includeMetadataIssues = false)
        {
            EnsureToolMetadataCache();
            IEnumerable<Dictionary<string, object>> query = _cachedTools;
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
                { "schemaVersion", MCPContractMetadata.ToolMetadataSchemaVersion },
            };
            if (!string.IsNullOrEmpty(category))
                result["category"] = category;
            if (offset > 0)
                result["offset"] = offset;
            if (nextOffset < tools.Count)
            {
                result["nextOffset"] = nextOffset;
                result["totalTools"] = tools.Count;
            }

            if (compact)
            {
                result["tools"] = page.Select(tool => ToCompactToolDescriptor(tool, includeSchema)).ToList();
                return result;
            }

            result["metadataSource"] = "MCPToolProfileCatalog";
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
                { "category", tool["category"] },
                { "moduleId", tool["moduleId"] },
                { "capability", tool["capability"] },
                { "operationKind", tool["operationKind"] },
                { "description", tool["description"] },
            };
            foreach (string key in new[]
                     {
                         "whenToUse", "notFor", "completionEvidence", "cleanupToolName",
                     })
            {
                if (tool.TryGetValue(key, out object value))
                    MCPContractMetadata.AddOptionalString(descriptor, key, value?.ToString());
            }
            foreach (string key in new[]
                     {
                         "aliases", "searchTerms", "preconditions", "errorCodes",
                     })
            {
                if (tool.TryGetValue(key, out object value))
                    MCPContractMetadata.AddOptionalList(descriptor, key,
                        value as System.Collections.IEnumerable);
            }
            if (tool.TryGetValue("tags", out object tags))
                MCPContractMetadata.SetTags(descriptor, tags as IEnumerable<string>);
            if (tool.TryGetValue("sideEffects", out object sideEffects))
                MCPContractMetadata.AddOptionalList(descriptor, "sideEffects", sideEffects as System.Collections.IEnumerable);
            if (tool.ContainsKey("projectToolName") &&
                tool.TryGetValue("errorCodes", out object errorCodes))
                MCPContractMetadata.AddOptionalList(descriptor, "errorCodes", errorCodes as System.Collections.IEnumerable);
            if (tool.TryGetValue("annotations", out object annotations) &&
                annotations is IDictionary<string, object> annotationDictionary &&
                annotationDictionary.Count > 0)
                descriptor["annotations"] = annotations;
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

        private static void EnsureRouteCache()
        {
            if (_cachedRoutes == null)
                _cachedRoutes = GetRegisteredRouteList();
        }

        private static List<string> GetRegisteredRouteList()
        {
            var routes = MCPRouteRegistry.BuiltInRoutes.ToList();
            routes.AddRange(MCPProjectToolCommands.GetDirectRoutePaths());
            return routes
                .Where(route => !string.IsNullOrEmpty(route))
                .Where(MCPRouteRegistry.IsCatalogRoute)
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
            string description = MCPToolDescriptionCatalog.Get(route);
            MCPToolProfile profile = GetToolProfile(route);
            Dictionary<string, object> inputSchema = AddTargetBindingSchema(
                MCPToolInputSchemaCatalog.Get(route), !profile.ReadOnly);
            MCPToolConfigurationPolicy.AnnotateInputSchema(route, inputSchema);
            var metadata = new Dictionary<string, object>
            {
                { "route", route },
                { "toolName", toolName },
                { "category", ExtractCategory(route) },
                { "moduleId", "unity." + ExtractCategory(route) },
                { "capability", MCPCapabilityRegistry.GetCapabilityName(route) },
                { "operationKind", ResolveOperationKind(profile) },
                { "whenToUse", description },
                { "searchTerms", BuildSearchTerms(route, toolName) },
                { "description", description },
                { "inputSchema", inputSchema },
                { "outputSchema", GetToolOutputSchema(route) },
                { "errorCodes", GetStandardErrorCodes(route) },
            };
            MCPContractMetadata.SetTags(metadata, MCPContractMetadata.BuildToolTags(
                readOnly: profile.ReadOnly,
                dangerous: profile.Dangerous,
                longRunning: profile.LongRunning,
                requiresPlayMode: profile.RequiresPlayMode));
            MCPContractMetadata.AddOptionalList(metadata, "sideEffects",
                MCPContractMetadata.BuildSideEffects(
                    null,
                    readOnly: profile.ReadOnly,
                    mutatesAssets: profile.MutatesAssets,
                    mutatesRuntime: profile.MutatesRuntime,
                     mayReloadDomain: profile.MayReloadDomain));
            if (profile.RequiresPlayMode)
                metadata["preconditions"] = new List<string> { "playMode" };
            Dictionary<string, object> annotations = profile.ToAnnotations();
            if (annotations.Count > 0)
                metadata["annotations"] = annotations;
            return metadata;
        }

        private static Dictionary<string, object> BuildProjectToolMetadata(string route,
            Dictionary<string, object> projectTool)
        {
            string projectToolName = projectTool["toolName"].ToString();
            string description = projectTool["description"].ToString();
            var inputSchema = (Dictionary<string, object>)projectTool["inputSchema"];

            string shortName = projectTool.TryGetValue("shortName", out object shortNameValue)
                ? shortNameValue.ToString()
                : "";
            string toolName = ProjectToolNameToToolName(projectToolName, shortName);
            var tags = MCPContractMetadata.ReadTags(projectTool);
            var sideEffectValues = projectTool.TryGetValue("sideEffects", out object declaredSideEffects)
                ? declaredSideEffects as System.Collections.IEnumerable
                : null;
            var sideEffects = MCPContractMetadata.BuildSideEffects(sideEffectValues);
            bool readOnly = tags.Contains(MCPContractMetadata.Tag.ReadOnly, StringComparer.Ordinal);
            bool mutatesAssets =
                sideEffects.Contains("writesAssets", StringComparer.Ordinal) ||
                sideEffects.Contains("writesScene", StringComparer.Ordinal);
            bool mutatesRuntime = sideEffects.Contains("changesRuntimeState", StringComparer.Ordinal);
            bool dangerous = tags.Contains(MCPContractMetadata.Tag.Dangerous, StringComparer.Ordinal);
            bool longRunning = tags.Contains(MCPContractMetadata.Tag.LongRunning, StringComparer.Ordinal);
            bool mayReloadDomain = sideEffects.Contains("reloadsDomain", StringComparer.Ordinal);
            bool requiresPlayMode =
                tags.Contains(MCPContractMetadata.Tag.RequiresPlayMode, StringComparer.Ordinal);
            var profile = new MCPToolProfile
            {
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
                (Dictionary<string, object>)projectTool["outputSchema"];
            var outputSchema = new Dictionary<string, object>
            {
                { "oneOf", new List<object>
                    {
                        businessOutputSchema,
                        CreatePersistentJobOutputSchema(),
                    }
                },
            };

            var metadata = new Dictionary<string, object>
            {
                { "route", route },
                { "toolName", toolName },
                { "category", "project-tools" },
                { "moduleId", projectTool["moduleId"].ToString() },
                { "capability", projectTool["capability"].ToString() },
                { "operationKind", projectTool["operationKind"].ToString() },
                { "description", description },
                { "inputSchema", inputSchema },
                { "outputSchema", outputSchema },
                { "projectToolName", projectToolName },
            };
            MCPContractMetadata.SetTags(metadata, tags);
            MCPContractMetadata.AddOptionalList(metadata, "sideEffects", sideEffects);
            if (projectTool.TryGetValue("errorCodes", out object errorCodes))
                MCPContractMetadata.AddOptionalList(metadata, "errorCodes",
                    errorCodes as System.Collections.IEnumerable);
            Dictionary<string, object> annotations = profile.ToAnnotations();
            if (annotations.Count > 0)
                metadata["annotations"] = annotations;
            if (projectTool.TryGetValue("cleanupToolName", out object cleanupToolName))
                MCPContractMetadata.AddOptionalString(metadata, "cleanupToolName", cleanupToolName?.ToString());
            if (projectTool.TryGetValue("source", out var source))
                MCPContractMetadata.AddOptionalString(metadata, "source", source?.ToString());
            CopyOptionalString(projectTool, metadata, "whenToUse");
            CopyOptionalString(projectTool, metadata, "notFor");
            CopyOptionalString(projectTool, metadata, "completionEvidence");
            CopyOptionalList(projectTool, metadata, "aliases");
            CopyOptionalList(projectTool, metadata, "searchTerms");
            CopyOptionalList(projectTool, metadata, "preconditions");
            return metadata;
        }

        private static void CopyOptionalString(Dictionary<string, object> source,
            Dictionary<string, object> destination, string key)
        {
            if (source.TryGetValue(key, out object value))
                MCPContractMetadata.AddOptionalString(destination, key, value?.ToString());
        }

        private static void CopyOptionalList(Dictionary<string, object> source,
            Dictionary<string, object> destination, string key)
        {
            if (source.TryGetValue(key, out object value))
                MCPContractMetadata.AddOptionalList(destination, key,
                    value as System.Collections.IEnumerable);
        }

        private static string ResolveOperationKind(MCPToolProfile profile)
        {
            if (profile.LongRunning)
                return "job";
            if (profile.ReadOnly)
                return "inspect";
            return "mutate";
        }

        private static List<string> BuildSearchTerms(string route, string toolName)
        {
            return new[] { route, toolName }
                .Where(value => string.IsNullOrWhiteSpace(value) == false)
                .SelectMany(value => value.Split(new[] { '/', '-', '_', '.' },
                    StringSplitOptions.RemoveEmptyEntries))
                .Select(value => value.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
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
                        { "description", "Opaque identifier of the persistent Job." },
                    }
                },
                { "jobAccessToken", new Dictionary<string, object>
                    {
                        { "type", "string" },
                        { "description", "Capability token used to access the Job after its originating agent disconnects." },
                    }
                },
                { "jobType", new Dictionary<string, object>
                    {
                        { "type", "string" },
                        { "description", "Durable Job owner type used to resume the correct workflow." },
                    }
                },
                { "operation", new Dictionary<string, object>
                    {
                        { "type", "string" },
                        { "description", "Originating execute-code or project-tool operation." },
                    }
                },
                { "status", new Dictionary<string, object>
                    {
                        { "type", "string" },
                        { "description", "Current persistent Job lifecycle state." },
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
                { "tags", new Dictionary<string, object>
                    {
                        { "type", "array" },
                        { "description", "Presence-only Job capabilities and positive lifecycle facts." },
                        { "items", new Dictionary<string, object> { { "type", "string" } } },
                        { "uniqueItems", true },
                    }
                },
                { "cleanupStatus", new Dictionary<string, object>
                    {
                        { "type", "string" },
                        { "description", "Current lifecycle state of the Job's explicit cleanup workflow." },
                        { "enum", new List<object>
                            {
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
                { "cleanupToken", new Dictionary<string, object>
                    {
                        { "type", "string" },
                        { "description", "Opaque tool-defined token consumed by the explicit cleanup contract." },
                    }
                },
                { "progress", new Dictionary<string, object>
                    {
                        { "type", "number" },
                        { "description", "Normalized Job progress from zero through one." },
                        { "minimum", 0 },
                        { "maximum", 1 },
                    }
                },
                { "statusMessage", new Dictionary<string, object>
                    {
                        { "type", "string" },
                        { "description", "Current human-readable progress or terminal-status summary." },
                    }
                },
                { "stepCount", new Dictionary<string, object>
                    {
                        { "type", "integer" },
                        { "description", "Number of incremental continuation steps executed by the Job." },
                        { "minimum", 1 },
                    }
                },
                { "nextRunAt", new Dictionary<string, object>
                    {
                        { "type", "string" },
                        { "description", "UTC timestamp after which the next incremental step may run." },
                    }
                },
                { "idempotencyKey", new Dictionary<string, object>
                    {
                        { "type", "string" },
                        { "description", "Caller key bound to the exact operation arguments for safe reuse." },
                    }
                },
                { "createdAt", new Dictionary<string, object>
                    {
                        { "type", "string" },
                        { "description", "UTC timestamp when the Job record was created." },
                    }
                },
                { "startedAt", new Dictionary<string, object>
                    {
                        { "type", "string" },
                        { "description", "UTC timestamp when execution first crossed the Job boundary." },
                    }
                },
                { "completedAt", new Dictionary<string, object>
                    {
                        { "type", "string" },
                        { "description", "UTC timestamp when the Job reached a terminal state." },
                    }
                },
                { "updatedAt", new Dictionary<string, object>
                    {
                        { "type", "string" },
                        { "description", "UTC timestamp of the latest persisted Job mutation." },
                    }
                },
                { "sideEffects", new Dictionary<string, object>
                    {
                        { "type", "array" },
                        { "description", "Exact side-effect categories declared by the originating operation." },
                        { "items", new Dictionary<string, object> { { "type", "string" } } },
                    }
                },
                { "result", new Dictionary<string, object>
                    {
                        { "description", "Validated terminal result produced by the originating operation." },
                    }
                },
                { "error", new Dictionary<string, object>
                    {
                        { "description", "Structured failure produced when the Job cannot complete." },
                    }
                },
                { "cleanupResult", new Dictionary<string, object>
                    {
                        { "description", "Terminal result produced by the explicit cleanup workflow." },
                    }
                },
                { "cleanupError", new Dictionary<string, object>
                    {
                        { "description", "Structured failure produced when explicit cleanup cannot complete." },
                    }
                },
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
                        "createdAt",
                        "updatedAt",
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
                KeyValuePair<string, object> bindingProperty = MCPToolSchemaFactory.Prop("expectedProjectPath", "string",
                    "Expected Unity project root; rejects cross-project mutation.");
                properties[bindingProperty.Key] = bindingProperty.Value;
            }
            if (!properties.ContainsKey("expectedProjectName"))
            {
                KeyValuePair<string, object> bindingProperty = MCPToolSchemaFactory.Prop("expectedProjectName", "string",
                    "Optional project name used as an additional target-binding check.");
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
                KeyValuePair<string, object> property = MCPToolSchemaFactory.Prop("runAsJob", "boolean",
                    "Run this invocation through the persistent project-tool job owner. Long-running tools always do this.");
                properties[property.Key] = property.Value;
            }
            if (!properties.ContainsKey("idempotencyKey"))
            {
                KeyValuePair<string, object> property = MCPToolSchemaFactory.Prop("idempotencyKey", "string",
                    "Optional project-scoped key used to reuse an existing persistent invocation.");
                properties[property.Key] = property.Value;
            }
            schema["properties"] = properties;
            return schema;
        }

        private static MCPToolProfile GetToolProfile(string route)
        {
            return MCPToolProfileCatalog.Get(route);
        }

        internal static bool IsRouteReadOnly(string route)
        {
            if (MCPProjectToolCommands.TryGetToolDetailForDirectRoute(route, out var projectTool))
                return MCPContractMetadata.HasTag(projectTool, MCPContractMetadata.Tag.ReadOnly);
            return GetToolProfile(route).ReadOnly;
        }

        internal static bool RouteMutatesAssets(string route)
        {
            if (MCPProjectToolCommands.TryGetToolDetailForDirectRoute(route, out var projectTool))
            {
                var sideEffects = projectTool.TryGetValue("sideEffects", out object value)
                    ? value as System.Collections.IEnumerable
                    : null;
                return MCPContractMetadata.HasString(sideEffects, "writesAssets") ||
                       MCPContractMetadata.HasString(sideEffects, "writesScene");
            }
            return GetToolProfile(route).MutatesAssets;
        }

        internal static bool RouteMutatesRuntime(string route)
        {
            if (MCPProjectToolCommands.TryGetToolDetailForDirectRoute(route, out var projectTool))
            {
                var sideEffects = projectTool.TryGetValue("sideEffects", out object value)
                    ? value as System.Collections.IEnumerable
                    : null;
                return MCPContractMetadata.HasString(sideEffects, "changesRuntimeState");
            }
            return GetToolProfile(route).MutatesRuntime;
        }

        internal static bool RouteIsDangerous(string route)
        {
            if (MCPProjectToolCommands.TryGetToolDetailForDirectRoute(route, out var projectTool))
                return MCPContractMetadata.HasTag(projectTool, MCPContractMetadata.Tag.Dangerous);
            return GetToolProfile(route).Dangerous;
        }

        internal static bool RouteRequiresTargetBinding(string route)
        {
            return !IsRouteReadOnly(route);
        }

        internal static bool RouteMayReloadDomain(string route)
        {
            if (MCPProjectToolCommands.TryGetToolDetailForDirectRoute(route, out var projectTool))
            {
                var sideEffects = projectTool.TryGetValue("sideEffects", out object value)
                    ? value as System.Collections.IEnumerable
                    : null;
                return MCPContractMetadata.HasString(sideEffects, "reloadsDomain");
            }
            return GetToolProfile(route).MayReloadDomain;
        }

        internal static bool RouteIsLongRunning(string route)
        {
            if (MCPProjectToolCommands.TryGetToolDetailForDirectRoute(route, out var projectTool))
                return MCPContractMetadata.HasTag(projectTool, MCPContractMetadata.Tag.LongRunning);
            return GetToolProfile(route).LongRunning;
        }

        private static List<Dictionary<string, object>> BuildMetadataIssues(List<Dictionary<string, object>> tools)
        {
            var issues = new List<Dictionary<string, object>>();
            foreach (var tool in tools)
            {
                string route = tool.TryGetValue("route", out var routeObj) ? routeObj?.ToString() : "";
                string description = tool.TryGetValue("description", out var descObj) ? descObj?.ToString() : "";
                if (description.StartsWith(
                        "Execute the canonical Unity MCP tool registered for route ",
                        StringComparison.Ordinal))
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


    }
}
