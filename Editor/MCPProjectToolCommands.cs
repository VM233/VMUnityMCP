using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class MCPProjectToolAttribute : Attribute
    {
        public string ToolName { get; }

        public string Description { get; set; }

        public string ShortName { get; set; }

        public string InputSchemaJson { get; set; }

        public bool ReadOnly { get; set; }

        public bool MutatesAssets { get; set; }

        public bool MutatesRuntime { get; set; }

        public bool Dangerous { get; set; }

        public bool LongRunning { get; set; }

        public bool MayReloadDomain { get; set; }

        public bool RequiresPlayMode { get; set; }

        public bool FirstClass { get; set; }

        public MCPProjectToolAttribute(string toolName)
        {
            ToolName = toolName;
        }
    }

    public interface IMCPProjectTool
    {
        object Execute(Dictionary<string, object> args);
    }

    public static class MCPProjectToolCommands
    {
        public const string DirectRoutePrefix = "project-tools/call/";
        private static List<ProjectToolDescriptor> _cachedProjectTools;

        private static readonly string[] ProjectBindingArgumentNames =
        {
            "expectedProjectPath",
            "expectedProjectName",
            "targetProjectPath",
            "targetProjectName",
            "unityProjectPath",
            "unityProjectName",
        };

        public static object List(Dictionary<string, object> args)
        {
            var allTools = GetToolSummaries(false);
            int offset = Math.Max(0, GetInt(args, "offset", 0));
            int limit = Math.Max(1, Math.Min(200, GetInt(args, "limit", 100)));
            var tools = allTools.Skip(offset).Take(limit).ToList();
            int nextOffset = offset + tools.Count;

            return new Dictionary<string, object>
            {
                { "tools", tools },
                { "returnedTools", tools.Count },
                { "totalTools", allTools.Count },
                { "offset", offset },
                { "limit", limit },
                { "hasMore", nextOffset < allTools.Count },
                { "nextOffset", nextOffset < allTools.Count ? (object)nextOffset : null }
            };
        }

        public static object Get(Dictionary<string, object> args)
        {
            string toolName = GetString(args, "toolName")?.Trim();
            if (string.IsNullOrEmpty(toolName))
                return MCPResponse.Error("toolName is required", "invalid_arguments");

            var matches = FindTools(toolName);
            if (matches.Count == 0)
            {
                return MCPResponse.Error($"Project tool '{toolName}' was not found.", "project_tool_not_found",
                    false, new Dictionary<string, object>
                    {
                        { "listRoute", "project-tools/list" }
                    });
            }

            if (matches.Count > 1)
            {
                return MCPResponse.Error($"Project tool '{toolName}' is registered more than once.",
                    "duplicate_project_tool", false, new Dictionary<string, object>
                    {
                        { "matches", matches.Select(tool => tool.ToSummaryDictionary()).ToList() }
                    });
            }

            return new Dictionary<string, object>
            {
                { "tool", matches[0].ToDetailDictionary() }
            };
        }

        public static List<Dictionary<string, object>> GetToolSummaries(bool validOnly)
        {
            return DiscoverTools()
                .Where(tool => validOnly == false || string.IsNullOrEmpty(tool.ValidationError))
                .OrderBy(tool => tool.ToolName, StringComparer.OrdinalIgnoreCase)
                .Select(tool => tool.ToSummaryDictionary())
                .ToList();
        }

        public static List<Dictionary<string, object>> GetToolDetails(bool validOnly)
        {
            return DiscoverTools()
                .Where(tool => validOnly == false || string.IsNullOrEmpty(tool.ValidationError))
                .OrderBy(tool => tool.ToolName, StringComparer.OrdinalIgnoreCase)
                .Select(tool => tool.ToDetailDictionary())
                .ToList();
        }

        public static List<string> GetDirectRoutePaths()
        {
            return DiscoverTools()
                .Where(tool => string.IsNullOrEmpty(tool.ValidationError) && tool.FirstClass)
                .GroupBy(tool => tool.ToolName, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() == 1)
                .Select(group => group.Single())
                .OrderBy(tool => tool.ToolName, StringComparer.OrdinalIgnoreCase)
                .Select(tool => GetDirectRoute(tool.ToolName))
                .ToList();
        }

        public static bool TryGetToolDetailForDirectRoute(string path, out Dictionary<string, object> tool)
        {
            tool = null;

            if (TryGetToolNameFromDirectRoute(path, out var toolName) == false)
                return false;

            var matches = FindTools(toolName);
            if (matches.Count != 1)
                return false;

            var descriptor = matches[0];
            if (!descriptor.FirstClass ||
                string.IsNullOrEmpty(descriptor.ValidationError) == false)
                return false;

            tool = descriptor.ToDetailDictionary();
            return true;
        }

        public static bool TryExecuteDirectRoute(string path, Dictionary<string, object> args, out object result)
        {
            result = null;

            if (TryGetToolNameFromDirectRoute(path, out var toolName) == false)
                return false;

            if (string.IsNullOrEmpty(toolName))
            {
                result = new { error = "Project tool route is missing a tool name." };
                return true;
            }

            var matches = FindTools(toolName);
            if (matches.Count != 1)
                return false;

            var descriptor = matches[0];
            if (!descriptor.FirstClass ||
                string.IsNullOrEmpty(descriptor.ValidationError) == false)
                return false;

            result = ExecuteTool(toolName, args ?? new Dictionary<string, object>());
            return true;
        }

        public static string GetDirectRoute(string toolName)
        {
            return DirectRoutePrefix + (toolName ?? "").TrimStart('/');
        }

        public static object Execute(Dictionary<string, object> args)
        {
            string toolName = GetString(args, "toolName");

            if (string.IsNullOrEmpty(toolName))
                return MCPResponse.Error("toolName is required", "invalid_arguments");

            var toolArgs = GetDictionary(args, "args")
                ?? new Dictionary<string, object>();
            foreach (string contextKey in new[] { "_agentId", "_requestId" })
            {
                if (!toolArgs.ContainsKey(contextKey) && args.TryGetValue(contextKey, out object contextValue))
                    toolArgs[contextKey] = contextValue;
            }

            return ExecuteTool(toolName, toolArgs);
        }

        private static object ExecuteTool(string toolName, Dictionary<string, object> toolArgs)
        {
            toolArgs = RemoveProjectBindingArguments(toolArgs);

            var matches = FindTools(toolName);

            if (matches.Count == 0)
            {
                return MCPResponse.Error($"Project tool '{toolName}' was not found.", "project_tool_not_found",
                    false, new Dictionary<string, object>
                    {
                        { "listRoute", "project-tools/list" }
                    });
            }

            if (matches.Count > 1)
            {
                return MCPResponse.Error($"Project tool '{toolName}' is registered more than once.",
                    "duplicate_project_tool", false, new Dictionary<string, object>
                    {
                        { "matches", matches.Select(tool => tool.ToSummaryDictionary()).ToList() }
                    });
            }

            var descriptor = matches[0];
            if (!string.IsNullOrEmpty(descriptor.ValidationError))
                return MCPResponse.Error(descriptor.ValidationError, "invalid_project_tool", false,
                    new Dictionary<string, object>
                    {
                        { "tool", descriptor.ToSummaryDictionary() },
                        { "detailsRoute", "project-tools/get" }
                    });

            if (!descriptor.TryValidateArguments(toolArgs, out var argumentError))
            {
                return MCPResponse.Error(argumentError, "invalid_arguments", false,
                    new Dictionary<string, object> { { "toolName", descriptor.ToolName } });
            }

            try
            {
                object result = descriptor.Invoke(toolArgs);
                return MCPResponse.Success(result, new Dictionary<string, object>
                {
                    { "toolName", descriptor.ToolName },
                });
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                Debug.LogException(inner);
                return MCPResponse.Error(inner.Message, "project_tool_exception", false,
                    new Dictionary<string, object>
                    {
                        { "toolName", descriptor.ToolName }
                    });
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return MCPResponse.Error(ex.Message, "project_tool_exception", false,
                    new Dictionary<string, object>
                    {
                        { "toolName", descriptor.ToolName }
                    });
            }
        }

        private static Dictionary<string, object> RemoveProjectBindingArguments(
            Dictionary<string, object> toolArgs)
        {
            var businessArguments = toolArgs != null
                ? new Dictionary<string, object>(toolArgs)
                : new Dictionary<string, object>();

            foreach (string argumentName in ProjectBindingArgumentNames)
                businessArguments.Remove(argumentName);

            return businessArguments;
        }

        private static List<ProjectToolDescriptor> FindTools(string toolName)
        {
            return DiscoverTools()
                .Where(tool => string.Equals(tool.ToolName, toolName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static bool TryGetToolNameFromDirectRoute(string path, out string toolName)
        {
            toolName = null;

            if (string.IsNullOrEmpty(path) || path.StartsWith(DirectRoutePrefix, StringComparison.Ordinal) == false)
                return false;

            var encodedToolName = path.Substring(DirectRoutePrefix.Length);
            toolName = Uri.UnescapeDataString(encodedToolName);
            return true;
        }

        private static List<ProjectToolDescriptor> DiscoverTools()
        {
            if (_cachedProjectTools != null)
                return _cachedProjectTools;

            var tools = new List<ProjectToolDescriptor>();

            foreach (var method in TypeCache.GetMethodsWithAttribute<MCPProjectToolAttribute>())
            {
                var attribute = method.GetCustomAttribute<MCPProjectToolAttribute>(false);
                if (attribute != null)
                    tools.Add(ProjectToolDescriptor.FromMethod(attribute, method));
            }

            foreach (var type in TypeCache.GetTypesWithAttribute<MCPProjectToolAttribute>())
            {
                var attribute = type.GetCustomAttribute<MCPProjectToolAttribute>(false);
                if (attribute != null)
                    tools.Add(ProjectToolDescriptor.FromType(attribute, type));
            }

            _cachedProjectTools = tools;
            return _cachedProjectTools;
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            if (args == null || !args.TryGetValue(key, out object value) || value == null)
                return null;

            return value.ToString();
        }

        private static int GetInt(Dictionary<string, object> args, string key, int fallback)
        {
            if (args == null || !args.TryGetValue(key, out object value) || value == null)
                return fallback;

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> args, string key)
        {
            if (args == null || !args.TryGetValue(key, out object value) || value == null)
                return null;

            return value as Dictionary<string, object>;
        }

        private sealed class ProjectToolDescriptor
        {
            public string ToolName;
            public string Description;
            public string ShortName;
            public string Source;
            public string ValidationError;
            public string ExposureWarning;
            public Dictionary<string, object> InputSchema;
            public bool ReadOnly;
            public bool MutatesAssets;
            public bool MutatesRuntime;
            public bool Dangerous;
            public bool LongRunning;
            public bool MayReloadDomain;
            public bool RequiresPlayMode;
            public bool FirstClass;

            private MethodInfo method;
            private Type type;

            public static ProjectToolDescriptor FromMethod(MCPProjectToolAttribute attribute, MethodInfo method)
            {
                var descriptor = new ProjectToolDescriptor
                {
                    ToolName = attribute.ToolName,
                    ShortName = attribute.ShortName ?? "",
                    Description = attribute.Description ?? "",
                    Source = method.DeclaringType.FullName + "." + method.Name,
                    ReadOnly = attribute.ReadOnly,
                    MutatesAssets = attribute.MutatesAssets,
                    MutatesRuntime = attribute.MutatesRuntime,
                    Dangerous = attribute.Dangerous,
                    LongRunning = attribute.LongRunning,
                    MayReloadDomain = attribute.MayReloadDomain,
                    RequiresPlayMode = attribute.RequiresPlayMode,
                    FirstClass = attribute.FirstClass,
                    method = method
                };

                descriptor.ValidationError = descriptor.ValidateMethod();
                descriptor.ValidationError = CombineValidationErrors(descriptor.ValidationError,
                    descriptor.ValidateOperationProfile());
                descriptor.ValidationError = CombineValidationErrors(descriptor.ValidationError,
                    descriptor.TrySetInputSchema(attribute.InputSchemaJson));
                descriptor.ApplyFirstClassMetadataGate();
                return descriptor;
            }

            public static ProjectToolDescriptor FromType(MCPProjectToolAttribute attribute, Type type)
            {
                var descriptor = new ProjectToolDescriptor
                {
                    ToolName = attribute.ToolName,
                    ShortName = attribute.ShortName ?? "",
                    Description = attribute.Description ?? "",
                    Source = type.FullName,
                    ReadOnly = attribute.ReadOnly,
                    MutatesAssets = attribute.MutatesAssets,
                    MutatesRuntime = attribute.MutatesRuntime,
                    Dangerous = attribute.Dangerous,
                    LongRunning = attribute.LongRunning,
                    MayReloadDomain = attribute.MayReloadDomain,
                    RequiresPlayMode = attribute.RequiresPlayMode,
                    FirstClass = attribute.FirstClass,
                    type = type
                };

                descriptor.ValidationError = descriptor.ValidateType();
                descriptor.ValidationError = CombineValidationErrors(descriptor.ValidationError,
                    descriptor.ValidateOperationProfile());
                descriptor.ValidationError = CombineValidationErrors(descriptor.ValidationError,
                    descriptor.TrySetInputSchema(attribute.InputSchemaJson));
                descriptor.ApplyFirstClassMetadataGate();
                return descriptor;
            }

            public object Invoke(Dictionary<string, object> args)
            {
                if (method != null)
                {
                    var parameters = method.GetParameters();
                    object result = parameters.Length == 0
                        ? method.Invoke(null, null)
                        : method.Invoke(null, new object[] { args });

                    return method.ReturnType == typeof(void) ? "ok" : result;
                }

                var instance = Activator.CreateInstance(type) as IMCPProjectTool;
                object typeResult = instance.Execute(args);
                return typeResult;
            }

            public Dictionary<string, object> ToSummaryDictionary()
            {
                var summary = new Dictionary<string, object>
                {
                    { "toolName", ToolName },
                    { "description", Description },
                    { "readOnly", ReadOnly },
                    { "mutatesAssets", MutatesAssets },
                    { "mutatesRuntime", MutatesRuntime },
                    { "dangerous", Dangerous },
                    { "longRunning", LongRunning },
                    { "mayReloadDomain", MayReloadDomain },
                    { "requiresPlayMode", RequiresPlayMode },
                    { "firstClass", FirstClass },
                    { "valid", string.IsNullOrEmpty(ValidationError) }
                };
                if (!string.IsNullOrEmpty(ExposureWarning))
                    summary["exposureWarning"] = ExposureWarning;
                return summary;
            }

            public Dictionary<string, object> ToDetailDictionary()
            {
                var descriptor = new Dictionary<string, object>
                {
                    { "toolName", ToolName },
                    { "shortName", ShortName },
                    { "description", Description },
                    { "source", Source },
                    { "executeRoute", "project-tools/execute" },
                    { "inputSchema", InputSchema ?? CreateDefaultInputSchema() },
                    { "readOnly", ReadOnly },
                    { "mutatesAssets", MutatesAssets },
                    { "mutatesRuntime", MutatesRuntime },
                    { "dangerous", Dangerous },
                    { "longRunning", LongRunning },
                    { "mayReloadDomain", MayReloadDomain },
                    { "requiresPlayMode", RequiresPlayMode },
                    { "firstClass", FirstClass },
                    { "enforcesInputSchema", true },
                    { "valid", string.IsNullOrEmpty(ValidationError) },
                    { "validationError", ValidationError ?? "" }
                };
                if (!string.IsNullOrEmpty(ExposureWarning))
                    descriptor["exposureWarning"] = ExposureWarning;
                if (FirstClass)
                    descriptor["directRoute"] = GetDirectRoute(ToolName);
                return descriptor;
            }

            private string TrySetInputSchema(string inputSchemaJson)
            {
                if (string.IsNullOrEmpty(inputSchemaJson))
                {
                    InputSchema = CreateDefaultInputSchema();
                    return null;
                }

                try
                {
                    InputSchema = MiniJson.Deserialize(inputSchemaJson) as Dictionary<string, object>;
                    if (InputSchema == null)
                        return "InputSchemaJson must deserialize to a JSON object.";

                    return ValidateInputSchema(InputSchema);
                }
                catch (Exception ex)
                {
                    InputSchema = CreateDefaultInputSchema();
                    return $"InputSchemaJson is invalid JSON: {ex.Message}";
                }
            }

            public bool TryValidateArguments(Dictionary<string, object> args, out string error)
            {
                args = args ?? new Dictionary<string, object>();
                var schema = InputSchema ?? CreateDefaultInputSchema();
                var errors = new List<string>();
                ValidateValueAgainstSchema(args, schema, "$", errors, true);

                if (errors.Count == 0)
                {
                    error = null;
                    return true;
                }

                error = string.Join(" ", errors);
                return false;
            }

            private string ValidateMethod()
            {
                if (string.IsNullOrEmpty(ToolName))
                    return "MCPProjectToolAttribute toolName cannot be empty.";

                if (!method.IsStatic)
                    return $"Project tool method '{Source}' must be static.";

                var parameters = method.GetParameters();
                if (parameters.Length > 1)
                    return $"Project tool method '{Source}' must accept zero parameters or one Dictionary<string, object> parameter.";

                if (parameters.Length == 1 && parameters[0].ParameterType != typeof(Dictionary<string, object>))
                    return $"Project tool method '{Source}' parameter must be Dictionary<string, object>.";

                return null;
            }

            private string ValidateType()
            {
                if (string.IsNullOrEmpty(ToolName))
                    return "MCPProjectToolAttribute toolName cannot be empty.";

                if (!typeof(IMCPProjectTool).IsAssignableFrom(type))
                    return $"Project tool type '{Source}' must implement IMCPProjectTool.";

                if (type.IsAbstract)
                    return $"Project tool type '{Source}' cannot be abstract.";

                if (type.GetConstructor(Type.EmptyTypes) == null)
                    return $"Project tool type '{Source}' must have a public parameterless constructor.";

                return null;
            }

            private string ValidateOperationProfile()
            {
                int operationKinds = (ReadOnly ? 1 : 0) + (MutatesAssets ? 1 : 0) + (MutatesRuntime ? 1 : 0);
                if (operationKinds > 1)
                    return $"Project tool '{ToolName}' declares conflicting operation kinds.";

                if (FirstClass && operationKinds == 0)
                    return $"First-class project tool '{ToolName}' must explicitly declare ReadOnly, MutatesAssets, or MutatesRuntime.";

                return null;
            }

            private void ApplyFirstClassMetadataGate()
            {
                if (!FirstClass || !string.IsNullOrEmpty(ValidationError))
                    return;

                var issues = new List<string>();
                if (string.IsNullOrWhiteSpace(Description))
                    issues.Add("description is required");
                CollectFirstClassSchemaIssues(InputSchema ?? CreateDefaultInputSchema(), "$", issues);
                if (issues.Count == 0)
                    return;

                FirstClass = false;
                ExposureWarning =
                    $"Project tool '{ToolName}' was kept in the three-stage catalog but not exposed directly: " +
                    string.Join("; ", issues.Distinct().Take(8)) +
                    (issues.Count > 8 ? "; additional schema issues omitted" : "") + ".";
            }

            private static Dictionary<string, object> CreateDefaultInputSchema()
            {
                return new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>() },
                    { "additionalProperties", true }
                };
            }

            private static string ValidateInputSchema(Dictionary<string, object> schema)
            {
                if (schema.TryGetValue("type", out var type) && type != null &&
                    type.ToString() != "object")
                    return "InputSchemaJson root type must be object.";

                var properties = GetSchemaProperties(schema);
                if (properties == null)
                    return "InputSchemaJson properties must be a JSON object.";

                var required = GetRequiredProperties(schema);
                foreach (string requiredName in required)
                {
                    if (!properties.ContainsKey(requiredName))
                        return $"InputSchemaJson required property '{requiredName}' is not declared in properties.";
                }

                var schemaErrors = new List<string>();
                ValidateSchemaNode(schema, "$", schemaErrors);
                if (schemaErrors.Count > 0)
                    return string.Join(" ", schemaErrors);

                return null;
            }

            private static void ValidateSchemaNode(Dictionary<string, object> schema, string path,
                List<string> errors)
            {
                if (schema == null)
                    return;

                foreach (string typeName in GetAllowedTypes(
                             schema.TryGetValue("type", out object rawType) ? rawType : null))
                {
                    if (typeName != "string" && typeName != "number" && typeName != "integer" &&
                        typeName != "boolean" && typeName != "object" && typeName != "array" &&
                        typeName != "null")
                        errors.Add($"{path} declares unsupported schema type '{typeName}'.");
                }

                var properties = GetSchemaProperties(schema);
                if (properties == null)
                {
                    errors.Add($"{path}.properties must be an object.");
                    return;
                }

                foreach (var pair in properties)
                {
                    if (!(pair.Value is Dictionary<string, object> propertySchema))
                    {
                        errors.Add($"{path}.{pair.Key} schema must be an object.");
                        continue;
                    }
                    ValidateSchemaNode(propertySchema, path + "." + pair.Key, errors);
                }

                if (schema.TryGetValue("items", out object items) && items != null)
                {
                    if (items is Dictionary<string, object> itemSchema)
                        ValidateSchemaNode(itemSchema, path + "[]", errors);
                    else
                        errors.Add($"{path}.items must be an object.");
                }

                foreach (string keyword in new[] { "allOf", "anyOf", "oneOf" })
                {
                    if (!schema.TryGetValue(keyword, out object variantsValue))
                        continue;
                    if (!(variantsValue is IList variants) || variants.Count == 0)
                    {
                        errors.Add($"{path}.{keyword} must be a non-empty array of schemas.");
                        continue;
                    }

                    for (int index = 0; index < variants.Count; index++)
                    {
                        if (variants[index] is Dictionary<string, object> variantSchema)
                            ValidateSchemaNode(variantSchema, $"{path}.{keyword}[{index}]", errors);
                        else
                            errors.Add($"{path}.{keyword}[{index}] must be an object.");
                    }
                }

                if (schema.TryGetValue("pattern", out object pattern) && pattern != null)
                {
                    try
                    {
                        _ = new Regex(pattern.ToString());
                    }
                    catch (ArgumentException ex)
                    {
                        errors.Add($"{path}.pattern is invalid: {ex.Message}");
                    }
                }
            }

            private static void CollectFirstClassSchemaIssues(Dictionary<string, object> schema,
                string path, List<string> issues)
            {
                if (schema == null)
                    return;

                var properties = GetSchemaProperties(schema);
                if (properties != null)
                {
                    foreach (var pair in properties)
                    {
                        if (!(pair.Value is Dictionary<string, object> propertySchema))
                            continue;
                        if (!propertySchema.TryGetValue("description", out object description) ||
                            string.IsNullOrWhiteSpace(description?.ToString()))
                            issues.Add($"{path}.{pair.Key} needs a description");
                        CollectFirstClassSchemaIssues(propertySchema, path + "." + pair.Key, issues);
                    }
                }

                var types = GetAllowedTypes(schema.TryGetValue("type", out object type) ? type : null);
                if (types.Contains("array") && !schema.ContainsKey("items"))
                    issues.Add($"{path} needs an items schema");
                if (schema.TryGetValue("items", out object items) &&
                    items is Dictionary<string, object> itemSchema)
                    CollectFirstClassSchemaIssues(itemSchema, path + "[]", issues);

                foreach (string keyword in new[] { "allOf", "anyOf", "oneOf" })
                {
                    if (!schema.TryGetValue(keyword, out object variantsValue) ||
                        !(variantsValue is IList variants))
                        continue;
                    for (int index = 0; index < variants.Count; index++)
                    {
                        if (variants[index] is Dictionary<string, object> variantSchema)
                            CollectFirstClassSchemaIssues(
                                variantSchema, $"{path}.{keyword}[{index}]", issues);
                    }
                }
            }

            private static void ValidateValueAgainstSchema(object value,
                Dictionary<string, object> schema, string path, List<string> errors,
                bool allowInternalProperties = false)
            {
                if (schema == null)
                    return;

                ValidateSchemaCombinators(value, schema, path, errors);

                if (!MatchesSchemaType(value, schema, out string typeError))
                {
                    errors.Add($"{path} {typeError}");
                    return;
                }

                if (schema.TryGetValue("enum", out object enumValue) && enumValue is IList allowedValues)
                {
                    bool matched = allowedValues.Cast<object>().Any(allowed => ValuesEqual(value, allowed));
                    if (!matched)
                        errors.Add($"{path} must be one of [{string.Join(", ", allowedValues.Cast<object>())}].");
                }

                if (value is string stringValue)
                {
                    if (TryGetDouble(schema, "minLength", out double minLength) &&
                        stringValue.Length < minLength)
                        errors.Add($"{path} must contain at least {minLength:0} characters.");
                    if (TryGetDouble(schema, "maxLength", out double maxLength) &&
                        stringValue.Length > maxLength)
                        errors.Add($"{path} must contain at most {maxLength:0} characters.");
                    if (schema.TryGetValue("pattern", out object pattern) && pattern != null &&
                        !MatchesPattern(stringValue, pattern.ToString(), out bool timedOut))
                    {
                        errors.Add(timedOut
                            ? $"{path} pattern validation exceeded the 100 ms match budget."
                            : $"{path} must match pattern '{pattern}'.");
                    }
                }

                if (IsNumber(value))
                {
                    double numericValue = Convert.ToDouble(value);
                    if (TryGetDouble(schema, "minimum", out double minimum) && numericValue < minimum)
                        errors.Add($"{path} must be greater than or equal to {minimum}.");
                    if (TryGetDouble(schema, "maximum", out double maximum) && numericValue > maximum)
                        errors.Add($"{path} must be less than or equal to {maximum}.");
                }

                if (value is IDictionary dictionary)
                {
                    var properties = GetSchemaProperties(schema) ?? new Dictionary<string, object>();
                    foreach (string requiredName in GetRequiredProperties(schema))
                    {
                        if (!dictionary.Contains(requiredName))
                            errors.Add($"{path}.{requiredName} is required.");
                    }

                    foreach (DictionaryEntry pair in dictionary)
                    {
                        string key = pair.Key?.ToString() ?? "";
                        if (allowInternalProperties && key.StartsWith("_", StringComparison.Ordinal))
                            continue;
                        if (!properties.TryGetValue(key, out object propertySchemaValue))
                        {
                            if (IsAdditionalPropertiesFalse(schema))
                                errors.Add($"{path}.{key} is not allowed.");
                            else if (schema.TryGetValue("additionalProperties",
                                         out object additionalSchemaValue) &&
                                     additionalSchemaValue is Dictionary<string, object> additionalSchema)
                                ValidateValueAgainstSchema(
                                    pair.Value, additionalSchema, path + "." + key, errors, false);
                            continue;
                        }
                        if (propertySchemaValue is Dictionary<string, object> propertySchema)
                            ValidateValueAgainstSchema(pair.Value, propertySchema, path + "." + key,
                                errors, false);
                    }
                }

                if (value is IList list && !(value is string))
                {
                    if (TryGetDouble(schema, "minItems", out double minItems) && list.Count < minItems)
                        errors.Add($"{path} must contain at least {minItems:0} items.");
                    if (TryGetDouble(schema, "maxItems", out double maxItems) && list.Count > maxItems)
                        errors.Add($"{path} must contain at most {maxItems:0} items.");
                    if (schema.TryGetValue("items", out object itemSchemaValue) &&
                        itemSchemaValue is Dictionary<string, object> itemSchema)
                    {
                        for (int index = 0; index < list.Count; index++)
                            ValidateValueAgainstSchema(list[index], itemSchema, $"{path}[{index}]",
                                errors, false);
                    }
                }
            }

            private static void ValidateSchemaCombinators(
                object value,
                Dictionary<string, object> schema,
                string path,
                List<string> errors)
            {
                if (schema.TryGetValue("allOf", out object allOfValue) && allOfValue is IList allOf)
                {
                    for (int index = 0; index < allOf.Count; index++)
                    {
                        if (allOf[index] is Dictionary<string, object> variant)
                            ValidateValueAgainstSchema(value, variant, path, errors, false);
                    }
                }

                ValidateAlternativeSchemas(value, schema, path, "anyOf", false, errors);
                ValidateAlternativeSchemas(value, schema, path, "oneOf", true, errors);
            }

            private static void ValidateAlternativeSchemas(
                object value,
                Dictionary<string, object> schema,
                string path,
                string keyword,
                bool requireExactlyOne,
                List<string> errors)
            {
                if (!schema.TryGetValue(keyword, out object variantsValue) ||
                    !(variantsValue is IList variants))
                    return;

                int matches = 0;
                foreach (object variantValue in variants)
                {
                    if (!(variantValue is Dictionary<string, object> variant))
                        continue;
                    var variantErrors = new List<string>();
                    ValidateValueAgainstSchema(value, variant, path, variantErrors, false);
                    if (variantErrors.Count == 0)
                        matches++;
                }

                bool valid = requireExactlyOne ? matches == 1 : matches > 0;
                if (!valid)
                {
                    errors.Add(requireExactlyOne
                        ? $"{path} must match exactly one schema in oneOf."
                        : $"{path} must match at least one schema in anyOf.");
                }
            }

            private static Dictionary<string, object> GetSchemaProperties(Dictionary<string, object> schema)
            {
                if (!schema.TryGetValue("properties", out var propertiesObj) || propertiesObj == null)
                    return new Dictionary<string, object>();

                return propertiesObj as Dictionary<string, object>;
            }

            private static List<string> GetRequiredProperties(Dictionary<string, object> schema)
            {
                if (!schema.TryGetValue("required", out var requiredObj) || requiredObj == null)
                    return new List<string>();

                var list = requiredObj as IList;
                if (list == null)
                    return new List<string>();

                return list.Cast<object>()
                    .Where(item => item != null)
                    .Select(item => item.ToString())
                    .Where(item => !string.IsNullOrEmpty(item))
                    .ToList();
            }

            private static bool IsAdditionalPropertiesFalse(Dictionary<string, object> schema)
            {
                return schema.TryGetValue("additionalProperties", out var value) &&
                       value is bool boolValue &&
                       boolValue == false;
            }

            private static bool MatchesSchemaType(object value, Dictionary<string, object> propertySchema,
                out string error)
            {
                error = null;
                if (!propertySchema.TryGetValue("type", out var typeObj) || typeObj == null)
                    return true;

                var allowedTypes = GetAllowedTypes(typeObj);
                if (allowedTypes.Count == 0)
                    return true;
                if (value == null)
                {
                    if (allowedTypes.Contains("null"))
                        return true;
                    error = $"must be {string.Join(" or ", allowedTypes)}.";
                    return false;
                }

                foreach (string allowedType in allowedTypes)
                {
                    if (MatchesType(value, allowedType))
                        return true;
                }

                error = $"must be {string.Join(" or ", allowedTypes)}.";
                return false;
            }

            private static List<string> GetAllowedTypes(object typeObj)
            {
                if (typeObj is string typeString)
                    return new List<string> { typeString };

                var list = typeObj as IList;
                if (list == null)
                    return new List<string>();

                return list.Cast<object>()
                    .Where(item => item != null)
                    .Select(item => item.ToString())
                    .Where(item => !string.IsNullOrEmpty(item))
                    .ToList();
            }

            private static bool MatchesType(object value, string type)
            {
                switch (type)
                {
                    case "string":
                        return value is string;
                    case "number":
                        return IsNumber(value);
                    case "integer":
                        return value is byte || value is sbyte || value is short || value is ushort ||
                               value is int || value is uint || value is long || value is ulong;
                    case "boolean":
                        return value is bool;
                    case "object":
                        return value is IDictionary;
                    case "array":
                        return value is IList && !(value is string);
                    case "null":
                        return value == null;
                    default:
                        return true;
                }
            }

            private static bool IsNumber(object value)
            {
                return value is byte || value is sbyte || value is short || value is ushort ||
                       value is int || value is uint || value is long || value is ulong ||
                        value is float || value is double || value is decimal;
            }

            private static bool MatchesPattern(string value, string pattern, out bool timedOut)
            {
                timedOut = false;
                try
                {
                    return Regex.IsMatch(value, pattern, RegexOptions.None,
                        TimeSpan.FromMilliseconds(100));
                }
                catch (RegexMatchTimeoutException)
                {
                    timedOut = true;
                    return false;
                }
            }

            private static bool TryGetDouble(Dictionary<string, object> dictionary, string key,
                out double value)
            {
                value = 0;
                return dictionary != null && dictionary.TryGetValue(key, out object raw) &&
                       raw != null && double.TryParse(raw.ToString(),
                           System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out value);
            }

            private static bool ValuesEqual(object left, object right)
            {
                if (ReferenceEquals(left, right))
                    return true;
                if (left == null || right == null)
                    return false;
                if (IsNumber(left) && IsNumber(right))
                    return Math.Abs(Convert.ToDouble(left) - Convert.ToDouble(right)) < 0.0000001;
                if (left is string leftString && right is string rightString)
                    return string.Equals(leftString, rightString, StringComparison.Ordinal);
                if (left is bool leftBoolean && right is bool rightBoolean)
                    return leftBoolean == rightBoolean;
                return left.Equals(right);
            }

            private static string CombineValidationErrors(string first, string second)
            {
                if (string.IsNullOrEmpty(first))
                    return second;

                if (string.IsNullOrEmpty(second))
                    return first;

                return first + " " + second;
            }
        }
    }
}
