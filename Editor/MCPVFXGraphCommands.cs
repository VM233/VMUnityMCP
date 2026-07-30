using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class MCPVFXGraphCommands
    {
        private const string ResourceTypeName = "UnityEditor.VFX.VisualEffectResource";

        public static object Info(Dictionary<string, object> args)
        {
            if (!TryValidateTopLevelKeys(args,
                    new[]
                    {
                        "assetPath", "maxObjects", "maxProperties", "includeSlots",
                        "includeSerialized", "maxExposedProperties", "maxConnections",
                        "maxSlotsPerNode", "_agentId",
                    },
                    out object keyError))
                return keyError;
            if (!MCPCapabilityRegistry.IsCapabilityAvailable("vfxgraph"))
                return MCPResponse.Error(
                    "VFX Graph is not available. Install com.unity.visualeffectgraph.",
                    "capability_unavailable");
            string assetPath = GetString(args, "assetPath");
            if (!TryGetContents(assetPath, out object resource,
                    out List<UnityEngine.Object> contents, out object error))
                return error;

            int maxNodes = Math.Max(1, Math.Min(500,
                GetInt(args, "maxObjects", 250)));
            int maxExposedProperties = Math.Max(1, Math.Min(500,
                GetInt(args, "maxExposedProperties", 100)));
            int maxConnections = Math.Max(1, Math.Min(2000,
                GetInt(args, "maxConnections", 500)));
            int maxSlotsPerNode = Math.Max(1, Math.Min(200,
                GetInt(args, "maxSlotsPerNode", 50)));
            bool includeSlots = GetBool(args, "includeSlots", false);
            List<UnityEngine.Object> models = contents.Where(IsVfxModel).ToList();
            var ids = models.Select((model, index) => new { model, index })
                .ToDictionary(item => item.model,
                    item => StableId(item.model, item.index));
            List<UnityEngine.Object> allNodeModels = models.Where(IsNode).ToList();
            List<UnityEngine.Object> nodeModels = allNodeModels.Take(maxNodes).ToList();
            List<UnityEngine.Object> allExposedParameterModels = models.Where(IsParameter)
                .Where(parameter => GetProperty(parameter.GetType(), parameter, "exposed")
                                    is bool exposed && exposed)
                .ToList();
            List<UnityEngine.Object> exposedParameterModels = allExposedParameterModels
                .Take(maxExposedProperties).ToList();
            var returnedModelIds = new HashSet<string>(
                nodeModels.Concat(exposedParameterModels).Select(model => ids[model]),
                StringComparer.Ordinal);
            var nodes = nodeModels
                .Select(model => NodeSummary(model, ids, returnedModelIds,
                    includeSlots, maxSlotsPerNode))
                .ToList();
            var parameters = exposedParameterModels
                .Select(model => ParameterSummary(model, ids))
                .ToList();
            List<Dictionary<string, object>> allReturnedConnections =
                BuildConnections(nodeModels.Concat(exposedParameterModels), ids,
                    returnedModelIds);
            List<Dictionary<string, object>> connections = allReturnedConnections
                .Take(maxConnections).ToList();

            var response = new Dictionary<string, object>
            {
                { "success", true },
                { "assetPath", assetPath },
                { "resourceType", resource.GetType().FullName },
                { "graphType", contents.FirstOrDefault(item =>
                    item != null && item.GetType().Name == "VFXGraph")?.GetType().FullName ?? "" },
                { "modelCount", models.Count },
                { "nodeCount", allNodeModels.Count },
                { "returnedNodeCount", nodes.Count },
                { "nodes", nodes },
                { "nodesTruncated", allNodeModels.Count > nodes.Count },
                { "exposedPropertyCount", allExposedParameterModels.Count },
                { "returnedExposedPropertyCount", parameters.Count },
                { "exposedProperties", parameters },
                { "connectionsTruncated",
                    allReturnedConnections.Count > connections.Count },
                { "connections", connections },
            };

            if (GetBool(args, "includeSerialized", false))
            {
                response["serializedGraph"] = MCPAssetGraphUtility.InspectAsset(assetPath,
                    IsVfxObject, Math.Min(maxNodes, 100),
                    GetInt(args, "maxProperties", 40), contents);
            }
            return response;
        }

        public static object Transaction(Dictionary<string, object> args)
        {
            if (!TryValidateTopLevelKeys(args,
                    new[] { "assetPath", "operations", "dryRun", "_agentId" },
                    out object keyError))
                return keyError;
            if (!MCPCapabilityRegistry.IsCapabilityAvailable("vfxgraph"))
                return MCPResponse.Error(
                    "VFX Graph is not available. Install com.unity.visualeffectgraph.",
                    "capability_unavailable");
            string assetPath = GetString(args, "assetPath");
            if (!TryGetContents(assetPath, out object resource,
                    out List<UnityEngine.Object> contents, out object error))
                return error;
            return MCPAssetGraphUtility.ApplyTransaction(assetPath,
                GetList(args, "operations"), IsVfxObject, GetBool(args, "dryRun", false),
                "Unity MCP Edit VFX Graph", contents, () => InvokeNoArgs(resource, "WriteAsset"));
        }

        private static bool TryGetContents(string assetPath, out object resource,
            out List<UnityEngine.Object> contents, out object error)
        {
            resource = null;
            contents = null;
            if (string.IsNullOrEmpty(assetPath))
            {
                error = MCPResponse.Error("assetPath is required.", "invalid_arguments");
                return false;
            }
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null || !IsVfxObject(asset))
            {
                error = MCPResponse.Error($"VFX Graph asset '{assetPath}' was not found.",
                    "asset_not_found");
                return false;
            }

            Type resourceType = MCPAssetGraphUtility.FindType(ResourceTypeName);
            MethodInfo getResource = resourceType?.GetMethod("GetResourceAtPath",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(string) }, null);
            resource = getResource?.Invoke(null, new object[] { assetPath });
            if (resource == null)
            {
                error = MCPResponse.Error(
                    $"VFX Graph resource for '{assetPath}' was not available.",
                    "vfx_resource_unavailable");
                return false;
            }
            object rawContents = InvokeNoArgs(resource, "GetContents");
            contents = Enumerate(rawContents).OfType<UnityEngine.Object>()
                .Where(item => item != null).Distinct().ToList();
            if (!contents.Contains(asset))
                contents.Insert(0, asset);
            error = null;
            return true;
        }

        private static Dictionary<string, object> NodeSummary(UnityEngine.Object model,
            IReadOnlyDictionary<UnityEngine.Object, string> ids,
            ISet<string> returnedModelIds, bool includeSlots, int maxSlotsPerNode)
        {
            Type type = model.GetType();
            List<string> allChildren = Enumerate(GetProperty(type, model, "children"))
                .OfType<UnityEngine.Object>()
                .Where(ids.ContainsKey)
                .Select(child => ids[child])
                .ToList();
            List<string> children = allChildren.Where(returnedModelIds.Contains).ToList();
            object position = GetProperty(type, model, "position");
            var result = new Dictionary<string, object>
            {
                { "id", ids[model] },
                { "name", GetSemanticName(model) },
                { "type", type.FullName },
                { "kind", NodeKind(type) },
                { "position", Vector2Summary(position) },
                { "collapsed", GetProperty(type, model, "collapsed") ?? false },
                { "childCount", allChildren.Count },
                { "children", children },
                { "childrenTruncated", allChildren.Count > children.Count },
            };
            if (includeSlots)
            {
                result["inputs"] = SlotSummaries(model, "inputSlots", maxSlotsPerNode);
                result["outputs"] = SlotSummaries(model, "outputSlots", maxSlotsPerNode);
            }
            return result;
        }

        private static Dictionary<string, object> ParameterSummary(UnityEngine.Object parameter,
            IReadOnlyDictionary<UnityEngine.Object, string> ids)
        {
            Type type = parameter.GetType();
            return new Dictionary<string, object>
            {
                { "id", ids[parameter] },
                { "name", GetProperty(type, parameter, "exposedName")?.ToString() ??
                          GetSemanticName(parameter) },
                { "type", GetProperty(type, parameter, "type")?.ToString() ?? "" },
                { "exposed", GetProperty(type, parameter, "exposed") ?? false },
                { "category", GetProperty(type, parameter, "category")?.ToString() ?? "" },
                { "order", GetProperty(type, parameter, "order") ?? 0 },
                { "value", MCPAssetGraphUtility.SanitizeValue(
                    GetProperty(type, parameter, "value")) },
                { "position", Vector2Summary(GetProperty(type, parameter, "position")) },
            };
        }

        private static List<Dictionary<string, object>> SlotSummaries(
            UnityEngine.Object model, string propertyName, int maxSlots)
        {
            object slotsValue = GetProperty(model.GetType(), model, propertyName);
            return Enumerate(slotsValue).Take(maxSlots).Select(slot =>
            {
                object property = GetProperty(slot.GetType(), slot, "property");
                string name = GetMember(property, "name")?.ToString() ?? "";
                string valueType = GetMember(property, "type")?.ToString() ?? "";
                object linkCount = InvokeNoArgs(slot, "GetNbLinks");
                return new Dictionary<string, object>
                {
                    { "name", name },
                    { "type", valueType },
                    { "value", MCPAssetGraphUtility.SanitizeValue(
                        GetProperty(slot.GetType(), slot, "value")) },
                    { "linkCount", linkCount ?? 0 },
                };
            }).ToList();
        }

        private static List<Dictionary<string, object>> BuildConnections(
            IEnumerable<UnityEngine.Object> models,
            IReadOnlyDictionary<UnityEngine.Object, string> ids,
            ISet<string> allowedIds)
        {
            var connections = new List<Dictionary<string, object>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (UnityEngine.Object model in models)
            {
                foreach (object slot in Enumerate(GetProperty(model.GetType(), model,
                             "outputSlots")))
                {
                    string fromSlot = SlotName(slot);
                    foreach (object linked in Enumerate(GetFieldValue(slot, "m_LinkedSlots")))
                    {
                        object owner = GetProperty(linked.GetType(), linked, "owner");
                        if (!(owner is UnityEngine.Object target) ||
                            !ids.TryGetValue(target, out string targetId) ||
                            !allowedIds.Contains(targetId))
                            continue;
                        string key = ids[model] + "|" + fromSlot + "|" + targetId + "|" +
                                     SlotName(linked);
                        if (!seen.Add(key))
                            continue;
                        connections.Add(new Dictionary<string, object>
                        {
                            { "fromNodeId", ids[model] },
                            { "fromSlot", fromSlot },
                            { "toNodeId", targetId },
                            { "toSlot", SlotName(linked) },
                        });
                    }
                }
            }
            return connections;
        }

        private static bool TryValidateTopLevelKeys(Dictionary<string, object> values,
            IEnumerable<string> allowed, out object error)
        {
            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unknown = values?.Keys.FirstOrDefault(key =>
                !allowedSet.Contains(key));
            if (string.IsNullOrEmpty(unknown))
            {
                error = null;
                return true;
            }

            error = MCPResponse.Error(
                $"Unsupported argument '{unknown}'. Allowed arguments: " +
                string.Join(", ", allowedSet.Where(key => key != "_agentId")
                    .OrderBy(key => key)) + ".",
                "invalid_arguments");
            return false;
        }

        private static string SlotName(object slot)
        {
            object property = GetProperty(slot.GetType(), slot, "property");
            return GetMember(property, "name")?.ToString() ?? "";
        }

        private static object GetFieldValue(object target, string fieldName)
        {
            if (target == null)
                return null;
            Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                    return field.GetValue(target);
                type = type.BaseType;
            }
            return null;
        }

        private static object GetMember(object target, string name)
        {
            if (target == null)
                return null;
            Type type = target.GetType();
            PropertyInfo property = FindProperty(type, name);
            if (property != null)
                return property.GetValue(target);
            while (type != null)
            {
                FieldInfo field = type.GetField(name,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field.GetValue(target);
                type = type.BaseType;
            }
            return null;
        }

        private static object GetProperty(Type type, object target, string name)
        {
            if (target == null)
                return null;
            return FindProperty(type, name)?.GetValue(target);
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            while (type != null)
            {
                PropertyInfo property = type.GetProperties(
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .FirstOrDefault(candidate =>
                        string.Equals(candidate.Name, name, StringComparison.Ordinal) &&
                        candidate.GetIndexParameters().Length == 0);
                if (property != null)
                    return property;
                type = type.BaseType;
            }
            return null;
        }

        private static object InvokeNoArgs(object target, string methodName)
        {
            if (target == null)
                return null;
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            return method?.Invoke(target, null);
        }

        private static string StableId(UnityEngine.Object model, int index)
        {
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(model, out string _,
                       out long localId) && localId != 0
                ? localId.ToString()
                : "model:" + index;
        }

        private static string GetSemanticName(UnityEngine.Object model)
        {
            object semantic = GetProperty(model.GetType(), model, "name");
            string value = semantic?.ToString();
            return !string.IsNullOrEmpty(value) ? value : model.name ?? "";
        }

        private static object Vector2Summary(object value)
        {
            if (value is Vector2 vector)
            {
                return new Dictionary<string, object>
                {
                    { "x", vector.x },
                    { "y", vector.y },
                };
            }
            return null;
        }

        private static string NodeKind(Type type)
        {
            if (HasBaseType(type, "VFXContext")) return "context";
            if (HasBaseType(type, "VFXBlock")) return "block";
            if (HasBaseType(type, "VFXOperator")) return "operator";
            return "node";
        }

        private static bool IsNode(UnityEngine.Object value)
        {
            Type type = value?.GetType();
            return HasBaseType(type, "VFXContext") ||
                   HasBaseType(type, "VFXOperator") ||
                   HasBaseType(type, "VFXBlock");
        }

        private static bool HasBaseType(Type type, string typeName)
        {
            while (type != null)
            {
                if (string.Equals(type.Name, typeName, StringComparison.Ordinal))
                    return true;
                type = type.BaseType;
            }
            return false;
        }

        private static bool IsParameter(UnityEngine.Object value)
        {
            return value?.GetType().Name.Contains("VFXParameter") == true;
        }

        private static bool IsVfxModel(UnityEngine.Object value)
        {
            string type = value?.GetType().FullName ?? "";
            return type.StartsWith("UnityEditor.VFX.", StringComparison.Ordinal) &&
                   (HasProperty(value.GetType(), "children") ||
                    type.EndsWith(".VFXParameter", StringComparison.Ordinal));
        }

        private static bool HasProperty(Type type, string name)
        {
            return FindProperty(type, name) != null;
        }

        private static bool IsVfxObject(UnityEngine.Object value)
        {
            return MCPAssetGraphUtility.IsTypeOrNamespace(value,
                "UnityEngine.VFX.", "UnityEditor.VFX.");
        }

        private static IEnumerable<object> Enumerate(object value)
        {
            if (!(value is IEnumerable enumerable))
                yield break;
            foreach (object item in enumerable)
                yield return item;
        }

        private static string GetString(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : "";
        }

        private static int GetInt(Dictionary<string, object> values, string key, int defaultValue)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? Convert.ToInt32(value)
                : defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> values, string key,
            bool defaultValue)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? Convert.ToBoolean(value)
                : defaultValue;
        }

        private static List<object> GetList(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value)
                ? value as List<object>
                : null;
        }
    }
}
