using System.Collections.Generic;
using System.Linq;

namespace UnityMCP.Editor
{
    internal static class MCPToolSchemaFactory
    {
        internal static Dictionary<string, object> AssetGraphTransactionSchema(string assetKind)
        {
            return Schema(Props(
                Prop("assetPath", "string", $"{assetKind} asset path below Assets/."),
                ArrayProp("operations", "object",
                    "Ordered rename or set-property operations. Target each subasset by localId or by type plus targetName."),
                Prop("dryRun", "boolean",
                    $"Validate and describe the {assetKind} transaction without modifying the asset.")
            ), "assetPath", "operations");
        }

        internal static Dictionary<string, object> ExecutionSchema(bool includeContinueOnError = true)
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

        internal static Dictionary<string, object> ComponentSetReferenceSchema()
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

        internal static Dictionary<string, object> AnimationUpdateTransitionSchema()
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

        internal static Dictionary<string, object> PrefabAssetConfigureComponentSchema()
        {
            var referenceProperties = Props(
                Prop("propertyName", "string", "ObjectReference serialized property name or path."),
                Prop("referenceAssetPath", "string", "Project asset path to assign. Ambiguous compatible objects require an exact subasset selector."),
                Prop("referenceSubAssetName", "string", "Optional exact object name within referenceAssetPath."),
                Prop("referenceSubAssetLocalId", "string", "Optional exact local file ID within referenceAssetPath, encoded as a decimal string."),
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
                Prop("createPathIfMissing", "boolean", "Create missing prefabPath GameObjects before configuring the component. New children inherit their parent layer. Defaults to false."),
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

        internal static Dictionary<string, object> PrefabAssetTransactionEditSchema()
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

        internal static Dictionary<string, object> AssetMoveSchema()
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

        internal static Dictionary<string, object> AssetImportSchema()
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

        internal static Dictionary<string, object> LocalizationUpsertEntriesSchema()
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

        internal static Dictionary<string, object> EditorWindowSchema(Dictionary<string, object> extraProps)
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

        internal static Dictionary<string, object> RuntimeUIDocumentSchema(Dictionary<string, object> extraProps, params string[] required)
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

        internal static Dictionary<string, object> Schema(Dictionary<string, object> properties, params string[] required)
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

        internal static Dictionary<string, object> Props(params KeyValuePair<string, object>[] properties)
        {
            var result = new Dictionary<string, object>();
            foreach (var pair in properties)
                result[pair.Key] = pair.Value;
            return result;
        }

        internal static KeyValuePair<string, object> Prop(string name, string type, string description)
        {
            return new KeyValuePair<string, object>(name, new Dictionary<string, object>
            {
                { "type", type },
                { "description", description },
            });
        }

        internal static KeyValuePair<string, object> EnumProp(string name,
            string description, params string[] values)
        {
            return new KeyValuePair<string, object>(name,
                new Dictionary<string, object>
                {
                    { "type", "string" },
                    { "description", description },
                    { "enum", values.Cast<object>().ToList() },
                });
        }

        internal static KeyValuePair<string, object> ArrayProp(
            string name,
            string itemType,
            string description)
        {
            return ArrayProp(name, new Dictionary<string, object>
            {
                { "type", itemType },
            }, description);
        }

        internal static KeyValuePair<string, object> ArrayProp(
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

        internal static KeyValuePair<string, object> AnyJsonValueProp(string name, string description)
        {
            return new KeyValuePair<string, object>(name, new Dictionary<string, object>
            {
                { "description", description },
            });
        }

        internal static KeyValuePair<string, object> JsonValueMapProp(
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
