using System.Collections.Generic;

namespace UnityMCP.Editor
{
    internal static class MCPToolInputSchemaCatalog
    {
        internal static Dictionary<string, object> Get(string route)
        {
            switch (route)
            {
                case "_meta/tools":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("compact", "boolean", "Return compact descriptors. Defaults to true."),
                        MCPToolSchemaFactory.Prop("includeSchema", "boolean", "Include input schemas. Defaults to false."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Tool offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum tools returned. Built-in default is 50; capped at 200."),
                        MCPToolSchemaFactory.Prop("category", "string", "Optional exact category filter."),
                        MCPToolSchemaFactory.Prop("includeMetadataIssues", "boolean", "Include metadata audit diagnostics in detailed mode. Defaults to false.")
                    ));
                case "asset/list":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("folder", "string", "Folder to search. Defaults to Assets."),
                        MCPToolSchemaFactory.Prop("type", "string", "Optional Unity asset type filter."),
                        MCPToolSchemaFactory.Prop("search", "string", "Optional AssetDatabase search expression."),
                        MCPToolSchemaFactory.Prop("recursive", "boolean", "Include descendants. Defaults to true."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Result offset."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum assets. Defaults to 100; capped at 500.")));
                case "asset/import-settings/get":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Texture, model, or audio asset path below Assets/."),
                        MCPToolSchemaFactory.Prop("platform", "string", "Optional Unity platform override name such as Standalone, Android, or iPhone.")
                    ), "assetPath");
                case "asset/import-settings/set":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Texture, model, or audio asset path below Assets/."),
                        MCPToolSchemaFactory.Prop("settings", "object", "Semantic importer fields. Unsupported keys are rejected with the allowed field list."),
                        MCPToolSchemaFactory.Prop("platform", "string", "Optional Unity platform override name such as Standalone, Android, or iPhone."),
                        MCPToolSchemaFactory.Prop("platformSettings", "object", "Optional semantic TextureImporter or AudioImporter override settings for platform."),
                        MCPToolSchemaFactory.Prop("reimport", "boolean", "Save and reimport the asset after updating settings. Defaults to true."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate and return before/requested settings without modifying the importer.")
                    ), "assetPath", "settings");
                case "scene/workspace":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("action", "string", "Workspace action: list, open, close, or set-active. Defaults to list."),
                        MCPToolSchemaFactory.Prop("path", "string", "Scene asset path for open, close, or set-active."),
                        MCPToolSchemaFactory.Prop("name", "string", "Loaded scene name for close or set-active when path is omitted."),
                        MCPToolSchemaFactory.Prop("mode", "string", "Open mode: additive (default) or single."),
                        MCPToolSchemaFactory.Prop("saveModified", "boolean", "For single open, save every dirty loaded scene before replacement."),
                        MCPToolSchemaFactory.Prop("discardModified", "boolean", "For single open, explicitly allow replacement of dirty loaded scenes without saving."),
                        MCPToolSchemaFactory.Prop("save", "boolean", "For close, save a dirty scene before closing."),
                        MCPToolSchemaFactory.Prop("discardChanges", "boolean", "For close, explicitly discard dirty scene changes."),
                        MCPToolSchemaFactory.Prop("removeScene", "boolean", "For close, remove the scene from the workspace. Defaults to true.")
                    ));
                case "material/properties/get":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Material asset path below Assets/."),
                        MCPToolSchemaFactory.ArrayProp("propertyNames", "string", "Optional shader property names. Omit to page through declared shader properties."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Shader property offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum shader properties returned. Defaults to 100; capped at 500.")
                    ), "assetPath");
                case "material/properties/set":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Material asset path below Assets/."),
                        MCPToolSchemaFactory.Prop("properties", "object", "Shader property values keyed by declared shader property name. Texture values accept assetPath plus optional scale and offset."),
                        MCPToolSchemaFactory.Prop("keywords", "object", "Keyword changes with enable and disable string arrays."),
                        MCPToolSchemaFactory.Prop("shader", "string", "Optional replacement shader name."),
                        MCPToolSchemaFactory.Prop("renderQueue", "number", "Optional Material render queue."),
                        MCPToolSchemaFactory.Prop("enableInstancing", "boolean", "Optional GPU instancing flag."),
                        MCPToolSchemaFactory.Prop("doubleSidedGI", "boolean", "Optional double-sided global illumination flag."),
                        MCPToolSchemaFactory.Prop("globalIlluminationFlags", "string", "Optional MaterialGlobalIlluminationFlags value."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate and return requested changes without modifying the Material.")
                    ), "assetPath");
                case "shadergraph/info":
                case "shadergraph/get-nodes":
                case "shadergraph/get-edges":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Shader Graph asset path below Assets/.")
                    ), "path");
                case "shadergraph/get-properties":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Optional Shader or Shader Graph asset path below Assets/."),
                        MCPToolSchemaFactory.Prop("shaderName", "string", "Optional loaded shader name when path is omitted.")
                    ));
                case "shadergraph/set-node-property":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Shader Graph asset path below Assets/."),
                        MCPToolSchemaFactory.Prop("objectId", "string", "Serialized graph object ID returned by shadergraph/get-properties or shadergraph/get-nodes."),
                        MCPToolSchemaFactory.Prop("nodeId", "string", "Legacy alias for objectId."),
                        MCPToolSchemaFactory.Prop("propertyName", "string", "Existing top-level scalar field on the target graph object."),
                        MCPToolSchemaFactory.AnyJsonValueProp("value", "Replacement scalar value. Its JSON type must match the existing field.")
                    ), "path", "propertyName", "value");
                case "physics/raycast":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("dimension", "string", "Physics dimension: 2D or 3D. Defaults to Project Settings > Unity MCP > Tool Defaults (3D initially)."),
                        MCPToolSchemaFactory.Prop("origin", "object", "Ray origin with x/y/z. z is ignored for 2D."),
                        MCPToolSchemaFactory.Prop("direction", "object", "Ray direction with x/y/z. z is ignored for 2D."),
                        MCPToolSchemaFactory.Prop("maxDistance", "number", "Maximum ray distance. Defaults to infinity."),
                        MCPToolSchemaFactory.Prop("layerMask", "number", "Optional Physics or Physics2D layer mask."),
                        MCPToolSchemaFactory.Prop("all", "boolean", "Return multiple hits rather than only the closest hit."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum hits returned when all is true. Defaults to 100; capped at 500.")
                    ), "origin", "direction");
                case "physics/overlap-sphere":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("dimension", "string", "Physics dimension: 2D or 3D. Defaults to the Unity MCP project setting (3D initially). In 2D this performs an overlap circle."),
                        MCPToolSchemaFactory.Prop("center", "object", "Query center with x/y/z. z is ignored for 2D."),
                        MCPToolSchemaFactory.Prop("radius", "number", "Sphere or circle radius. Defaults to 1."),
                        MCPToolSchemaFactory.Prop("layerMask", "number", "Optional Physics or Physics2D layer mask."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum colliders returned. Defaults to 100; capped at 500.")
                    ), "center");
                case "physics/overlap-box":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("dimension", "string", "Physics dimension: 2D or 3D. Defaults to the Unity MCP project setting (3D initially)."),
                        MCPToolSchemaFactory.Prop("center", "object", "Query center with x/y/z. z is ignored for 2D."),
                        MCPToolSchemaFactory.Prop("halfExtents", "object", "Half extents with x/y/z. In 2D, x/y are doubled into box size."),
                        MCPToolSchemaFactory.Prop("angle", "number", "2D box rotation in degrees. Ignored for 3D."),
                        MCPToolSchemaFactory.Prop("layerMask", "number", "Optional Physics or Physics2D layer mask."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum colliders returned. Defaults to 100; capped at 500.")
                    ), "center", "halfExtents");
                case "search/scene":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("name", "string", "Optional case-insensitive GameObject name substring or regular expression."),
                        MCPToolSchemaFactory.Prop("regex", "boolean", "Interpret name as a regular expression with a bounded match timeout. Defaults to false."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Optional Component type name or full name that must exist on the GameObject."),
                        MCPToolSchemaFactory.Prop("tag", "string", "Optional exact Unity Tag."),
                        MCPToolSchemaFactory.Prop("layer", "string", "Optional Unity Layer name or numeric index."),
                        MCPToolSchemaFactory.Prop("shader", "string", "Optional case-insensitive shader-name substring used by a Renderer on the GameObject."),
                        MCPToolSchemaFactory.Prop("includeInactive", "boolean", "Include inactive GameObjects. Defaults to true."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Stable result offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum results. Defaults to 200; capped at 500.")));
                case "_meta/capabilities":
                case "queue/info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props());
                case "queue/status":
                case "queue/cancel":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("ticketId", "number", "Owned queue ticket identifier.")), "ticketId");
                case "asset/create-folder":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Folder path below Assets/."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate and report without creating folders.")), "path");
                case "asset/copy":
                {
                    var copyProperties = MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("sourcePath", "string", "Source asset path."),
                        MCPToolSchemaFactory.Prop("targetPath", "string", "Target asset path."));
                    var properties = MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("sourcePath", "string", "Source asset path for a single copy."),
                        MCPToolSchemaFactory.Prop("targetPath", "string", "Target asset path for a single copy."),
                        MCPToolSchemaFactory.Prop("overwrite", "boolean", "Replace existing targets with rollback snapshots. Defaults to false."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Preflight without copying. Defaults to false."));
                    properties["copies"] = new Dictionary<string, object>
                    {
                        { "type", "array" },
                        { "description", "Batch of sourcePath/targetPath copy requests." },
                        { "minItems", 1 },
                        { "items", MCPToolSchemaFactory.Schema(copyProperties, "sourcePath", "targetPath") },
                    };
                    var schema = MCPToolSchemaFactory.Schema(properties);
                    schema["oneOf"] = new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            { "required", new List<object> { "sourcePath", "targetPath" } },
                        },
                        new Dictionary<string, object>
                        {
                            { "required", new List<object> { "copies" } },
                        },
                    };
                    return schema;
                }
                case "asset/dependencies":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Asset whose references should be inspected."),
                        MCPToolSchemaFactory.Prop("direction", "string", "outgoing, incoming, or both. Defaults to both."),
                        MCPToolSchemaFactory.Prop("recursive", "boolean", "Use recursive dependency resolution. Defaults to true."),
                        MCPToolSchemaFactory.ArrayProp("searchRoots", "string", "Folders scanned for incoming references. Defaults to Assets."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Result offset."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum results. Defaults to 100; capped at 500.")), "path");
                case "asset/transaction":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.ArrayProp("operations", "object", "Ordered ensure-folder, copy, move, delete, or serialized-set operations."),
                        MCPToolSchemaFactory.ArrayProp("requiredAssets", "string", "Assets or folders that must exist after execution."),
                        MCPToolSchemaFactory.ArrayProp("referenceChecks", "object", "Postconditions containing assetPath and requiredDependencies."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Preflight all operations without mutation.")), "operations");
                case "uitoolkit/edit-uxml":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "UXML asset path below Assets/."),
                        MCPToolSchemaFactory.ArrayProp("operations", "object", "Ordered structural UXML edit operations."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Return the edit result without writing.")), "assetPath", "operations");
                case "uitoolkit/edit-uss":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "USS asset path below Assets/."),
                        MCPToolSchemaFactory.ArrayProp("operations", "object", "Ordered selector/declaration edit operations."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Return the edit result without writing.")), "assetPath", "operations");
                case "uitoolkit/authoring-transaction":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.ArrayProp("edits", "object", "Ordered edit objects with kind, assetPath, and operations."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate all edits without writing.")), "edits");
                case "packages/add":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("identifier", "string", "Registry package name, Git URL, local path, or tarball identifier.")),
                        "identifier");
                case "packages/list":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("offset", "number", "Result offset."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum packages. Defaults to 100; capped at 200.")));
                case "packages/remove":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("name", "string", "Installed package name to remove.")), "name");
                case "packages/search":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("query", "string", "Registry search query."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Result offset."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum returned packages. Defaults to 50; capped at 200.")),
                        "query");
                case "localization/status":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props());
                case "localization/locales":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("includePseudo", "boolean", "Include PseudoLocale assets. Defaults to true.")
                    ));
                case "localization/create-locale":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("code", "string", "Locale code, for example en-US or zh-CN."),
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Locale asset path under Assets ending in .asset."),
                        MCPToolSchemaFactory.Prop("name", "string", "Optional Locale display name."),
                        MCPToolSchemaFactory.Prop("addToProject", "boolean", "Register the Locale with Localization Settings. Defaults to true.")
                    ), "code", "assetPath");
                case "localization/set-selected-locale":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("locale", "string", "Registered Locale code to select.")
                    ), "locale");
                case "localization/collections":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("type", "string", "Optional collection type filter: string or asset."),
                        MCPToolSchemaFactory.Prop("nameContains", "string", "Optional case-insensitive collection name filter.")
                    ));
                case "localization/create-collection":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("name", "string", "Table Collection name."),
                        MCPToolSchemaFactory.Prop("type", "string", "Collection type: string or asset."),
                        MCPToolSchemaFactory.Prop("assetDirectory", "string", "Existing or new directory under Assets."),
                        MCPToolSchemaFactory.ArrayProp("locales", "string", "Optional Locale codes. Defaults to every registered Locale."),
                        MCPToolSchemaFactory.Prop("group", "string", "Optional Localization window group."),
                        MCPToolSchemaFactory.Prop("preload", "boolean", "Optional preload flag for all created tables.")
                    ), "name", "type", "assetDirectory");
                case "localization/entries":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("collection", "string", "Table Collection name or GUID."),
                        MCPToolSchemaFactory.Prop("type", "string", "Collection type: string or asset. Defaults to string."),
                        MCPToolSchemaFactory.Prop("locale", "string", "Optional Locale code filter."),
                        MCPToolSchemaFactory.Prop("keyContains", "string", "Optional case-insensitive key filter."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Filtered key offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum keys returned. Defaults to 100; capped at 500.")
                    ), "collection");
                case "localization/upsert-entry":
                    return MCPToolSchemaFactory.LocalizationUpsertEntriesSchema();
                case "localization/remove-entry":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("collection", "string", "Table Collection name or GUID."),
                        MCPToolSchemaFactory.Prop("type", "string", "Collection type: string or asset. Defaults to string."),
                        MCPToolSchemaFactory.Prop("key", "string", "Localization key to remove."),
                        MCPToolSchemaFactory.Prop("locale", "string", "Optional Locale code. Omit to remove the shared key from every table.")
                    ), "collection", "key");
                case "localization/validate":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("collection", "string", "Optional Table Collection name or GUID."),
                        MCPToolSchemaFactory.Prop("type", "string", "Optional collection type filter: string or asset."),
                        MCPToolSchemaFactory.Prop("includeEmpty", "boolean", "Report empty values as well as missing entries. Defaults to true."),
                        MCPToolSchemaFactory.Prop("maxIssues", "number", "Maximum issues returned. Defaults to 200; capped at 2000.")
                    ));
                case "localization/settings":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("initializeSynchronously", "boolean", "Optional Localization initialization mode."),
                        MCPToolSchemaFactory.Prop("projectLocale", "string", "Optional registered project Locale code."),
                        MCPToolSchemaFactory.Prop("selectedLocale", "string", "Optional registered selected Locale code.")
                    ));
                case "localization/variables":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("group", "string", "Optional case-insensitive persistent variable group filter."),
                        MCPToolSchemaFactory.Prop("nameContains", "string", "Optional case-insensitive variable name filter.")
                    ));
                case "localization/upsert-variable":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("group", "string", "Persistent variable group name."),
                        MCPToolSchemaFactory.Prop("name", "string", "Variable name inside the group."),
                        MCPToolSchemaFactory.Prop("type", "string", "Variable type: bool, int, long, float, double, string, or object."),
                        MCPToolSchemaFactory.AnyJsonValueProp("value", "Variable value. Object variables accept an Assets path."),
                        MCPToolSchemaFactory.Prop("groupAssetPath", "string", "Required asset path when creating a missing VariablesGroupAsset.")
                    ), "group", "name", "type", "value");
                case "localization/remove-variable":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("group", "string", "Persistent variable group name."),
                        MCPToolSchemaFactory.Prop("name", "string", "Variable name to remove.")
                    ), "group", "name");
                case "packages/update-git":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("name", "string", "Package name, e.g. com.example.package"),
                        MCPToolSchemaFactory.Prop("gitUrl", "string", "Optional Git URL. Defaults to the current manifest Git URL."),
                        MCPToolSchemaFactory.Prop("ref", "string", "Optional branch, tag, or commit. Defaults to main."),
                        MCPToolSchemaFactory.Prop("skipIfResolved", "boolean", "Skip Package Manager resolve when packages-lock already matches the requested Git commit. Defaults to true."),
                        MCPToolSchemaFactory.Prop("force", "boolean", "Force Package Manager resolve even when packages-lock already matches. Defaults to false.")
                    ), "name");
                case "packages/status":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("name", "string", "Optional package name. If omitted, returns all Git dependencies from the manifest."),
                        MCPToolSchemaFactory.Prop("includeResolved", "boolean", "Include Package Manager resolved package data when available. Defaults to false.")
                    ));
                case "packages/lint-metas":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("name", "string", "Installed package name to lint."),
                        MCPToolSchemaFactory.Prop("path", "string", "Absolute or project-relative package path to lint."),
                        MCPToolSchemaFactory.Prop("all", "boolean", "Lint all resolved package roots."),
                        MCPToolSchemaFactory.Prop("checkDirectories", "boolean", "Also require directory .meta files. Defaults to true."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum missing entries returned per package.")
                    ));
                case "wait/editor-idle":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("timeoutMs", "number", "Maximum wait time in milliseconds. Defaults to 30000."),
                        MCPToolSchemaFactory.Prop("stableFrames", "number", "Number of consecutive idle editor frames required. Defaults to 3."),
                        MCPToolSchemaFactory.Prop("stableMs", "number", "Minimum continuous idle time in milliseconds. Defaults to 500.")
                    ));
                case "mcp/health":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("includeRecentActions", "boolean", "Include recent and slow action details. Defaults to false so health checks remain compact."),
                        MCPToolSchemaFactory.Prop("recentCount", "number", "Number of recent MCP actions to return when includeRecentActions is true. Defaults to 20."),
                        MCPToolSchemaFactory.Prop("slowThresholdMs", "number", "Recent actions at or above this duration are listed as slow. Defaults to 1000.")
                    ));
                case "mcp/set-autostart":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("enabled", "boolean", "Whether this Unity Editor instance should auto-start the MCP bridge after reload.")
                    ), "enabled");
                case "jobs/list":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("jobType", "string", "Optional job type filter."),
                        MCPToolSchemaFactory.Prop("status", "string", "Optional status filter."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Result offset."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum jobs. Defaults to 50; capped at 200.")));
                case "jobs/get":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("jobId", "string", "Job identifier."),
                        MCPToolSchemaFactory.Prop("jobType", "string", "Optional job type disambiguator."),
                        MCPToolSchemaFactory.Prop("jobAccessToken", "string", "Capability token returned when a persistent job starts. Required after the originating MCP agent disconnects.")), "jobId");
                case "jobs/cancel":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("jobId", "string", "Persistent job identifier returned by its start route."),
                        MCPToolSchemaFactory.Prop("jobType", "string", "Optional job type disambiguator."),
                        MCPToolSchemaFactory.Prop("jobAccessToken", "string", "Capability token returned when a persistent job starts. Required after the originating MCP agent disconnects.")
                    ), "jobId");
                case "jobs/cleanup":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("jobId", "string", "Terminal persistent job identifier whose explicit cleanup contract should run."),
                        MCPToolSchemaFactory.Prop("jobAccessToken", "string", "Capability token returned when the persistent job started.")
                    ), "jobId");
                case "vfxgraph/info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "VisualEffectAsset path below Assets/."),
                        MCPToolSchemaFactory.Prop("maxObjects", "number", "Maximum semantic graph nodes returned. Defaults to 250; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxExposedProperties", "number", "Maximum exposed properties returned. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxConnections", "number", "Maximum connections among returned nodes and properties. Defaults to 500; capped at 2000."),
                        MCPToolSchemaFactory.Prop("maxSlotsPerNode", "number", "Maximum input and output slots per node when includeSlots is true. Defaults to 50; capped at 200."),
                        MCPToolSchemaFactory.Prop("maxProperties", "number", "Maximum visible serialized properties per graph object when includeSerialized is true. Defaults to 40; capped at 500."),
                        MCPToolSchemaFactory.Prop("includeSlots", "boolean", "Include typed input/output slot values for each node. Defaults to false."),
                        MCPToolSchemaFactory.Prop("includeSerialized", "boolean", "Include a recursively budgeted serialized graph diagnostic. Defaults to false.")
                    ), "assetPath");
                case "vfxgraph/transaction":
                    return MCPToolSchemaFactory.AssetGraphTransactionSchema("VFX Graph");
                case "audio-mixer/info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "AudioMixer asset path below Assets/."),
                        MCPToolSchemaFactory.Prop("maxGroups", "number", "Maximum groups returned. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxSnapshots", "number", "Maximum snapshots returned. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxEffects", "number", "Maximum detailed effects returned. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxChildrenPerGroup", "number", "Maximum child groups listed per group. Defaults to 50; capped at 200."),
                        MCPToolSchemaFactory.Prop("maxEffectsPerGroup", "number", "Maximum effect references listed per group. Defaults to 50; capped at 200."),
                        MCPToolSchemaFactory.Prop("maxParametersPerEffect", "number", "Maximum parameter definitions returned per effect. Defaults to 50; capped at 200."),
                        MCPToolSchemaFactory.Prop("maxExposedParameters", "number", "Maximum exposed parameters returned. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxObjects", "number", "Maximum mixer subassets in the optional serialized diagnostic. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxProperties", "number", "Maximum visible serialized properties per object when includeSerialized is true. Defaults to 40; capped at 500."),
                        MCPToolSchemaFactory.Prop("includeSerialized", "boolean", "Include a recursively budgeted serialized mixer diagnostic. Defaults to false.")
                    ), "assetPath");
                case "audio-mixer/transaction":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "AudioMixer asset path below Assets/."),
                        MCPToolSchemaFactory.ArrayProp("operations", "object", "Ordered semantic group, snapshot, effect, exposed-parameter, snapshot-value, rename, or set-property operations. Runtime exposed-parameter overrides must use a separate transaction."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate and describe the transaction without changing the mixer.")
                    ), "assetPath", "operations");
                case "build/profile":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("action", "string", "Build Profile action: info (default) or transaction."),
                        MCPToolSchemaFactory.ArrayProp("operations", "object", "For transaction, ordered set-active, set-scenes, set-scripting-defines, set-global-scenes, or set-property operations."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate and return current profiles plus requested operations without mutation."),
                        MCPToolSchemaFactory.Prop("includeAfter", "boolean", "Include a paginated post-transaction Build Profile snapshot. Defaults to false; operation results are returned regardless."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Build Profile offset for info or includeAfter. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum Build Profiles for info or includeAfter. Defaults to 50; capped at 200.")
                    ));
                case "addressables/info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("offset", "number", "Addressable entry offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum entries returned. Defaults to 100; capped at 500.")
                    ));
                case "addressables/transaction":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.ArrayProp("operations", "object", "Ordered create/remove/default-group, add/remove/rename-label, create-or-move-entry, set-address, set-label, or remove-entry operations."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate and describe the Addressables transaction without modifying settings.")
                    ), "operations");
                case "addressables/build":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props());
                case "timeline/info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "TimelineAsset path below Assets/."),
                        MCPToolSchemaFactory.Prop("maxTracks", "number", "Maximum tracks returned across the semantic hierarchy. Defaults to 250; capped at 1000."),
                        MCPToolSchemaFactory.Prop("maxClipsPerTrack", "number", "Maximum clips returned per track. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxMarkersPerTrack", "number", "Maximum markers returned per track. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxObjects", "number", "Maximum Timeline subassets returned. Defaults to 250; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxProperties", "number", "Maximum serialized properties per Timeline object when includeSerialized is true. Defaults to 60; capped at 500."),
                        MCPToolSchemaFactory.Prop("includeSerialized", "boolean", "Include a recursively budgeted serialized Timeline diagnostic. Defaults to false.")
                    ), "assetPath");
                case "timeline/transaction":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "TimelineAsset path below Assets/."),
                        MCPToolSchemaFactory.ArrayProp("operations", "object", "Ordered create-track, delete-track, rename-track, set-track-property, create-clip, delete-clip, or set-clip operations."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate and return the current Timeline plus requested operations without mutation."),
                        MCPToolSchemaFactory.Prop("includeAfter", "boolean", "Include a bounded post-transaction Timeline snapshot. Defaults to false; operation results are returned regardless."),
                        MCPToolSchemaFactory.Prop("maxTracks", "number", "Maximum tracks in includeAfter. Defaults to 250; capped at 1000."),
                        MCPToolSchemaFactory.Prop("maxClipsPerTrack", "number", "Maximum clips per track in includeAfter. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxMarkersPerTrack", "number", "Maximum markers per track in includeAfter. Defaults to 100; capped at 500.")
                    ), "assetPath", "operations");
                case "cinemachine/info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Optional prefab asset path. Omit to inspect loaded scenes."),
                        MCPToolSchemaFactory.Prop("includeProperties", "boolean", "Include bounded serialized properties for every Cinemachine component. Defaults to false."),
                        MCPToolSchemaFactory.Prop("maxProperties", "number", "Maximum serialized properties per component. Defaults to 60; capped at 200."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Cinemachine component offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum Cinemachine components returned. Defaults to 100; capped at 500.")
                    ));
                case "cinemachine/transaction":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Optional prefab asset path. Omit to edit loaded scene objects."),
                        MCPToolSchemaFactory.ArrayProp("operations", "object", "Ordered set-property, set-object-reference, or set-enabled operations. Select scene objects by scenePath plus GameObject path, and components or target components by type plus zero-based index."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Resolve and describe every operation without modifying scene or prefab data.")
                    ), "operations");
                case "instance/current":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props());
                case "instance/list":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("includeStale", "boolean", "Include registry entries whose editor process may no longer be running. Defaults to false.")
                    ));
                case "instance/resolve":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("projectPath", "string", "Unity project root path to resolve. Exact normalized path match."),
                        MCPToolSchemaFactory.Prop("projectName", "string", "Unity project name to resolve. Ambiguous names return an error."),
                        MCPToolSchemaFactory.Prop("port", "number", "MCP bridge port to resolve.")
                    ));
                case "instance/assert-project":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("expectedProjectPath", "string", "Expected Unity project root path."),
                        MCPToolSchemaFactory.Prop("expectedProjectName", "string", "Expected Unity project name.")
                    ));
                case "asset/export-unitypackage":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.ArrayProp("assetPaths", "string", "Unity asset paths to export, e.g. Assets/MyFolder or Assets/MyPrefab.prefab."),
                        MCPToolSchemaFactory.Prop("outputPath", "string", "Absolute path or project-root-relative path for the .unitypackage output."),
                        MCPToolSchemaFactory.Prop("includeDependencies", "boolean", "Include asset dependencies. Defaults to true."),
                        MCPToolSchemaFactory.Prop("recurse", "boolean", "Recursively export folder contents. Defaults to true."),
                        MCPToolSchemaFactory.Prop("overwrite", "boolean", "Replace an existing output file. Defaults to false."),
                        MCPToolSchemaFactory.Prop("interactive", "boolean", "Show Unity's export package UI. Defaults to false.")
                    ), "outputPath");
                case "asset/import-unitypackage":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("packagePath", "string", "Absolute path or project-root-relative path to a .unitypackage file. Import is always non-interactive.")
                    ), "packagePath");
                case "editor/play-mode":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("action", "string", "Target action: play, pause, resume, step, or stop. Defaults to play. Pause is idempotent; step advances one frame and remains paused."),
                        MCPToolSchemaFactory.Prop("timeoutMs", "number", "Maximum time to wait for the confirmed target state. Defaults to 10000."),
                        MCPToolSchemaFactory.Prop("stableFrames", "number", "Consecutive Editor updates that must confirm the target state. Defaults to 2.")
                    ));
                case "editor/execute-code":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("code", "string", "C# method body to execute. Return a value to serialize it."),
                        MCPToolSchemaFactory.ArrayProp("usings", "string", "Additional namespace imports for this call. Recurring imports can be configured in Project Settings > Unity MCP > Execute Code. UnityEngine.UIElements is included by default."),
                        MCPToolSchemaFactory.Prop("maxResultItems", "number", "Maximum serialized collection/object entries across the result. Defaults to 200; capped at 2000."),
                        MCPToolSchemaFactory.Prop("maxResultDepth", "number", "Maximum serialized result depth. Defaults to 8; capped at 16."),
                        MCPToolSchemaFactory.Prop("maxResultStringLength", "number", "Maximum characters per returned string. Defaults to 20000; capped at 200000."),
                        MCPToolSchemaFactory.EnumProp("unityStructFormat", "Unity value structs in the result: compact strings or structured typed objects. Defaults to compact.", "compact", "structured"),
                        MCPToolSchemaFactory.Prop("includeStackTrace", "boolean", "Include a full managed stack trace when executed code throws. Defaults to false."),
                        MCPToolSchemaFactory.Prop("idempotencyKey", "string", "Optional project-scoped key. Repeating the same key returns the existing persistent job instead of executing code again."),
                        MCPToolSchemaFactory.Prop("cleanupCode", "string", "Optional C# method body used only by jobs/cleanup to reverse temporary state created by this job.")
                    ), "code");
                case "profiler/enable":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("enabled", "boolean", "Enable or disable Profiler recording. Defaults to true."),
                        MCPToolSchemaFactory.Prop("deepProfiling", "boolean", "Optional deep profiling state.")
                    ));
                case "profiler/stats":
                case "profiler/memory":
                case "profiler/analyze":
                case "profiler/memory-status":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props());
                case "profiler/frame-data":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("frameIndex", "number", "Recorded Profiler frame index. Defaults to the latest frame."),
                        MCPToolSchemaFactory.Prop("threadIndex", "number", "Profiler thread index. Defaults to 0 for Main Thread."),
                        MCPToolSchemaFactory.Prop("maxItems", "number", "Maximum timing entries. Defaults to 30."),
                        MCPToolSchemaFactory.Prop("minTimeMs", "number", "Exclude nested timing entries below this total time.")
                    ));
                case "profiler/memory-breakdown":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("includeDetails", "boolean", "Include the largest assets in each category."),
                        MCPToolSchemaFactory.Prop("maxPerCategory", "number", "Maximum detailed assets per category. Defaults to 5.")
                    ));
                case "profiler/memory-top-assets":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("count", "number", "Maximum assets to return. Defaults to 20."),
                        MCPToolSchemaFactory.Prop("type", "string", "Optional asset type filter such as texture, mesh, audio, material, shader, animation, or font.")
                    ));
                case "profiler/memory-snapshot":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Optional output directory. Defaults to Unity's temporary cache MemorySnapshots folder."),
                        MCPToolSchemaFactory.Prop("timeoutMs", "number", "Maximum time to wait for snapshot completion. Defaults to 120000.")
                    ));
                case "profiler/memory-snapshot-status":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("jobId", "string", "Optional snapshot job ID. Defaults to the current job in this Editor session.")
                    ));
                case "scene/hierarchy":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("maxDepth", "number", "Maximum hierarchy depth to return. Defaults to 10."),
                        MCPToolSchemaFactory.Prop("maxNodes", "number", "Maximum hierarchy nodes to return. Defaults to 250; capped at 2000."),
                        MCPToolSchemaFactory.Prop("parentPath", "string", "Optional GameObject path used as the search root."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Optional component type name or full name. When set, returns compact flat matches instead of the full hierarchy."),
                        MCPToolSchemaFactory.Prop("nameContains", "string", "Optional case-insensitive GameObject name filter used with componentType."),
                        MCPToolSchemaFactory.Prop("pathContains", "string", "Optional case-insensitive hierarchy path filter used with componentType."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Component-filtered result offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum component-filtered matches. Defaults to min(maxNodes, 50); capped at 200.")
                    ));
                case "testing/list-tests":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("mode", "string", "Test mode: EditMode or PlayMode. Defaults to EditMode."),
                        MCPToolSchemaFactory.Prop("nameFilter", "string", "Optional case-insensitive test full-name filter."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Test result offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum tests to return. Defaults to 100; capped at 500.")
                    ));
                case "testing/run-tests":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("mode", "string", "Test mode: EditMode or PlayMode. Defaults to EditMode."),
                        MCPToolSchemaFactory.ArrayProp("testNames", "string", "Optional exact test full names."),
                        MCPToolSchemaFactory.ArrayProp("categories", "string", "Optional test categories. VM Unity MCP defaults to VMUnityMCP.PackageSmoke when testNames, categories, and groupNames are all omitted; pass VMUnityMCP.FullRegression for the full suite."),
                        MCPToolSchemaFactory.ArrayProp("assemblies", "string", "Optional test assembly names."),
                        MCPToolSchemaFactory.ArrayProp("groupNames", "string", "Optional Unity Test Runner group names."),
                        MCPToolSchemaFactory.Prop("clearStuck", "boolean", "Force-clear a previously stuck job before starting. Defaults to false.")
                    ));
                case "testing/get-job":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("jobId", "string", "Optional job ID. Defaults to the current or latest job."),
                        MCPToolSchemaFactory.Prop("includeDetails", "boolean", "Include paginated individual test results. Defaults to false."),
                        MCPToolSchemaFactory.Prop("includeFailedOnly", "boolean", "Include only failed or inconclusive test results."),
                        MCPToolSchemaFactory.Prop("includeStackTrace", "boolean", "Include test stack traces. Defaults to false."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Individual test result offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Individual test result limit. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("failureLimit", "number", "Maximum failures included in progress. Defaults to 20; capped at 100.")
                    ));
                case "testing/run-package-tests":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("packageName", "string", "Git package name. Defaults to com.vm233.unity-mcp."),
                        MCPToolSchemaFactory.Prop("mode", "string", "Test mode: EditMode or PlayMode. Defaults to EditMode."),
                        MCPToolSchemaFactory.ArrayProp("assemblies", "string", "Test assembly names. Defaults to the Unity MCP regression assembly for the Unity MCP package."),
                        MCPToolSchemaFactory.ArrayProp("testNames", "string", "Optional exact test full names."),
                        MCPToolSchemaFactory.ArrayProp("categories", "string", "Optional test categories."),
                        MCPToolSchemaFactory.ArrayProp("groupNames", "string", "Optional Unity Test Runner group names.")
                    ));
                case "testing/get-package-job":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("jobId", "string", "Optional package-test job ID. Defaults to the active or latest workflow."),
                        MCPToolSchemaFactory.Prop("jobAccessToken", "string", "Capability token returned when the package-test job started. Required after the originating MCP agent disconnects."),
                        MCPToolSchemaFactory.Prop("clear", "boolean", "Delete terminal workflow state after returning it. Defaults to false.")
                    ));
                case "scene/instantiate-prefab":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Prefab asset path to instantiate into the currently open scene."),
                        MCPToolSchemaFactory.Prop("name", "string", "Optional name for the created scene instance."),
                        MCPToolSchemaFactory.Prop("parent", "string", "Optional scene GameObject name used as the parent."),
                        MCPToolSchemaFactory.Prop("position", "object", "Optional world position object with x/y/z."),
                        MCPToolSchemaFactory.Prop("rotation", "object", "Optional world Euler rotation object with x/y/z.")
                    ), "prefabPath");
                case "prefab-asset/hierarchy":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to inspect."),
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Optional GameObject path used as the hierarchy root."),
                        MCPToolSchemaFactory.Prop("maxDepth", "number", "Maximum hierarchy depth to return. Defaults to 10."),
                        MCPToolSchemaFactory.Prop("maxNodes", "number", "Maximum hierarchy nodes to return. Defaults to 250; capped at 2000.")
                    ), "assetPath");
                case "prefab-asset/get-properties":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to inspect."),
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Component type name or full name.")
                    ), "assetPath", "componentType");
                case "prefab-asset/set-property":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Component type name or full name."),
                        MCPToolSchemaFactory.Prop("propertyName", "string", "Serialized property name or property path to set."),
                        MCPToolSchemaFactory.AnyJsonValueProp("value", "Serialized value to assign. Accepts primitive values, arrays, and objects. A primitive scalar may be wrapped as {value: ...} when the MCP client exposes this field as an object."),
                        MCPToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffContextLines", "number", "Context lines around prefab YAML changes. Defaults to 2."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMaxLines", "number", "Maximum diff lines returned. Defaults to 200."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "componentType", "propertyName", "value");
                case "prefab-asset/set-reference":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Component type name or full name. Optional when propertyName can identify the component."),
                        MCPToolSchemaFactory.Prop("propertyName", "string", "ObjectReference serialized property name or property path."),
                        MCPToolSchemaFactory.Prop("referenceAssetPath", "string", "Project asset path to assign. Ambiguous compatible objects require an exact subasset selector."),
                        MCPToolSchemaFactory.Prop("referenceSubAssetName", "string", "Optional exact object name within referenceAssetPath."),
                        MCPToolSchemaFactory.Prop("referenceSubAssetLocalId", "string", "Optional exact local file ID within referenceAssetPath, encoded as a decimal string."),
                        MCPToolSchemaFactory.Prop("referencePrefabPath", "string", "Path of a GameObject inside the same prefab to assign."),
                        MCPToolSchemaFactory.Prop("referenceComponentType", "string", "When using referencePrefabPath, assign this component instead of the GameObject."),
                        MCPToolSchemaFactory.Prop("clear", "boolean", "Clear the ObjectReference."),
                        MCPToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "propertyName");
                case "prefab-asset/instantiate-child-prefab":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Target prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("sourcePrefabPath", "string", "Prefab asset path to instantiate into the target prefab."),
                        MCPToolSchemaFactory.Prop("parentPrefabPath", "string", "Parent path inside the target prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("name", "string", "Optional name override for the created GameObject."),
                        MCPToolSchemaFactory.Prop("siblingIndex", "number", "Optional sibling index under the parent."),
                        MCPToolSchemaFactory.Prop("position", "object", "Optional local position object with x/y/z."),
                        MCPToolSchemaFactory.Prop("rotation", "object", "Optional local Euler rotation object with x/y/z."),
                        MCPToolSchemaFactory.Prop("scale", "object", "Optional local scale object with x/y/z.")
                    ), "assetPath", "sourcePrefabPath");
                case "prefab-asset/add-gameobject":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("parentPrefabPath", "string", "Parent path inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("name", "string", "Name of the new child GameObject."),
                        MCPToolSchemaFactory.Prop("primitiveType", "string", "Optional Unity PrimitiveType to create, e.g. Cube or Sphere."),
                        MCPToolSchemaFactory.Prop("layer", "string", "Optional Unity layer name or numeric index. Defaults to the parent GameObject's layer."),
                        MCPToolSchemaFactory.Prop("position", "object", "Optional local position object with x/y/z."),
                        MCPToolSchemaFactory.Prop("rotation", "object", "Optional local Euler rotation object with x/y/z."),
                        MCPToolSchemaFactory.Prop("scale", "object", "Optional local scale object with x/y/z."),
                        MCPToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "name");
                case "prefab-asset/add-component":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Component type name or full name."),
                        MCPToolSchemaFactory.JsonValueMapProp("properties", "Optional serialized property names/paths mapped to initial JSON values. Values are applied before the new component is saved."),
                        MCPToolSchemaFactory.Prop("waitForType", "boolean", "Wait for compilation/import until the component type is available. Defaults to true."),
                        MCPToolSchemaFactory.Prop("typeResolveTimeoutMs", "number", "Maximum type wait time in milliseconds. Defaults to 30000."),
                        MCPToolSchemaFactory.Prop("typeResolveStableMs", "number", "Continuous idle time after type resolution before editing. Defaults to 500."),
                        MCPToolSchemaFactory.Prop("refreshAssets", "boolean", "Call AssetDatabase.Refresh once before waiting. Defaults to true."),
                        MCPToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffContextLines", "number", "Context lines around prefab YAML changes. Defaults to 2."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMaxLines", "number", "Maximum diff lines returned. Defaults to 200."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary."),
                        MCPToolSchemaFactory.ArrayProp("prefabFileDiffIgnoreContains", "string", "Optional substrings used to hide noisy diff lines."),
                        MCPToolSchemaFactory.ArrayProp("prefabFileDiffIgnoreYamlProperties", "string", "Optional YAML property names used to hide noisy diff lines.")
                    ), "assetPath", "componentType");
                case "prefab-asset/configure-component":
                    return MCPToolSchemaFactory.PrefabAssetConfigureComponentSchema();
                case "prefab-asset/remove-component":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Component type name or full name."),
                        MCPToolSchemaFactory.Prop("componentIndex", "number", "Component index when multiple components of the same type exist. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "componentType");
                case "prefab-asset/move-component":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("sourcePrefabPath", "string", "Path of the source GameObject inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("targetPrefabPath", "string", "Path of the target GameObject inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Component type name or full name."),
                        MCPToolSchemaFactory.Prop("componentIndex", "number", "Component index on the source GameObject. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffContextLines", "number", "Context lines around prefab YAML changes. Defaults to 2."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMaxLines", "number", "Maximum diff lines returned. Defaults to 200."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "sourcePrefabPath", "targetPrefabPath", "componentType");
                case "prefab-asset/move-gameobject":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject to move inside the prefab."),
                        MCPToolSchemaFactory.Prop("newParentPrefabPath", "string", "New parent path inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("siblingIndex", "number", "Optional sibling index under the new parent."),
                        MCPToolSchemaFactory.Prop("worldPositionStays", "boolean", "Preserve world transform while reparenting. Defaults to false.")
                    ), "assetPath", "prefabPath");
                case "prefab-asset/remove-gameobject":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Path of the child GameObject to remove. Cannot be root."),
                        MCPToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "prefabPath");
                case "prefab-asset/find":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to search."),
                        MCPToolSchemaFactory.Prop("name", "string", "Exact GameObject name filter."),
                        MCPToolSchemaFactory.Prop("nameContains", "string", "Case-insensitive GameObject name contains filter."),
                        MCPToolSchemaFactory.Prop("pathContains", "string", "Case-insensitive prefab path contains filter."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Optional component type name or full name filter."),
                        MCPToolSchemaFactory.Prop("propertyName", "string", "Optional serialized property name/path to require on the component."),
                        MCPToolSchemaFactory.Prop("propertyValue", "string", "Optional serialized property value to match."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum returned matches. Defaults to 50.")
                    ), "assetPath");
                case "prefab-asset/transaction-edit":
                    return MCPToolSchemaFactory.PrefabAssetTransactionEditSchema();
                case "prefab-asset/cleanup-missing-overrides":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab Variant asset path to clean."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Report removable overrides without saving. Defaults to false."),
                        MCPToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the Unity MCP user preference (disabled initially)."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath");
                case "component/set-reference":
                    return MCPToolSchemaFactory.ComponentSetReferenceSchema();
                case "component/set-property":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("instanceId", "string", "Target scene GameObject instance id."),
                        MCPToolSchemaFactory.Prop("path", "string", "Target scene GameObject hierarchy path when instanceId is omitted."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Component type name or full name."),
                        MCPToolSchemaFactory.Prop("propertyName", "string", "Serialized property name, or inherited Behaviour property name such as enabled."),
                        MCPToolSchemaFactory.AnyJsonValueProp("value", "Property value. Accepts primitive values, arrays, and objects. A primitive scalar may be wrapped as {value: ...} when the MCP client exposes this field as an object.")
                    ), "componentType", "propertyName", "value");
                case "serialized-object/get":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("instanceId", "number", "Target Unity object instance id."),
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Target asset path if instanceId is omitted."),
                        MCPToolSchemaFactory.Prop("assetType", "string", "Optional asset type name/full name used when loading assetPath."),
                        MCPToolSchemaFactory.Prop("gameObjectPath", "string", "Scene GameObject path if instanceId and assetPath are omitted."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Optional component type to select from a GameObject target."),
                        MCPToolSchemaFactory.Prop("componentIndex", "number", "Component index when multiple components of the same type exist."),
                        MCPToolSchemaFactory.Prop("propertyPath", "string", "Optional serialized property path to read."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Visible property offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("maxProperties", "number", "Maximum properties to return when propertyPath is omitted. Defaults to 50; capped at 500."),
                        MCPToolSchemaFactory.Prop("includeChildren", "boolean", "Walk child properties. Defaults to false."),
                        MCPToolSchemaFactory.Prop("maxDepth", "number", "Maximum nested serialized value depth. Defaults to 3; capped at 8."),
                        MCPToolSchemaFactory.Prop("maxArrayElements", "number", "Maximum elements returned per serialized array. Defaults to 50; capped at 500.")
                    ));
                case "serialized-object/set":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("instanceId", "number", "Target Unity object instance id."),
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Target asset path if instanceId is omitted."),
                        MCPToolSchemaFactory.Prop("assetType", "string", "Optional asset type name/full name used when loading assetPath."),
                        MCPToolSchemaFactory.Prop("gameObjectPath", "string", "Scene GameObject path if instanceId and assetPath are omitted."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Optional component type to select from a GameObject target."),
                        MCPToolSchemaFactory.Prop("componentIndex", "number", "Component index when multiple components of the same type exist."),
                        MCPToolSchemaFactory.Prop("propertyPath", "string", "Serialized property path to write."),
                        MCPToolSchemaFactory.AnyJsonValueProp("value", "Serialized value. A primitive scalar may be wrapped as {value: ...} when the MCP client exposes this field as an object. ObjectReference supports assetPath, instanceId, or gameObject. SerializeReference objects may include '$managedReferenceType' as 'AssemblyName::Namespace.TypeName'.")
                    ), "propertyPath", "value");
                case "asset/rename":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Current asset path, e.g. Assets/Art/Old Name.png."),
                        MCPToolSchemaFactory.Prop("newName", "string", "New file or folder name. Do not include a directory path."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate and return expected paths without renaming.")
                    ));
                case "asset/import":
                    return MCPToolSchemaFactory.AssetImportSchema();
                case "asset/refresh":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.ArrayProp("assetPaths", "string", "Optional Unity asset paths to import. When supplied, only these paths are imported, with known dependencies before dependents. Omit to run a full synchronous AssetDatabase refresh and reconcile all external changes."),
                        MCPToolSchemaFactory.Prop("forceUpdate", "boolean", "Use ImportAssetOptions.ForceUpdate for full refreshes and non-compilation targeted assets. Compilation assets are always imported without ForceUpdate to avoid broad dependency reimports. Defaults to false."),
                        MCPToolSchemaFactory.Prop("saveAssets", "boolean", "Call AssetDatabase.SaveAssets after refresh/import. Defaults to false."),
                        MCPToolSchemaFactory.Prop("clearStuck", "boolean", "Replace a non-terminal refresh job left behind by an interrupted editor session. Defaults to false.")
                    ));
                case "asset/get-refresh-job":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("jobId", "string", "Optional refresh job ID. Defaults to the current or latest job."),
                        MCPToolSchemaFactory.Prop("refreshRequestId", "string", "Optional original asset/refresh request ID used to recover the matching persistent job after a transport timeout or domain reload."),
                        MCPToolSchemaFactory.Prop("clear", "boolean", "Clear the persisted job after a terminal result is read. Defaults to false."),
                        MCPToolSchemaFactory.Prop("timeoutMs", "number", "Maximum reload reconnection wait consumed by the MCP transport. Defaults to 300000ms.")
                    ));
                case "asset/move":
                    return MCPToolSchemaFactory.AssetMoveSchema();
                case "console/query":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("count", "number", "Maximum returned entries. Defaults to 50; capped at 200."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Filtered entry offset, counting from the newest match. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("type", "string", "Filter by all, error, warning, info, exception, or assert. Defaults to all."),
                        MCPToolSchemaFactory.Prop("messageContains", "string", "Case-insensitive message substring filter."),
                        MCPToolSchemaFactory.Prop("sourceContains", "string", "Case-insensitive source stack frame/path substring filter."),
                        MCPToolSchemaFactory.Prop("stackContains", "string", "Case-insensitive full stack substring filter."),
                        MCPToolSchemaFactory.Prop("since", "string", "Start time filter. Accepts ISO/local time, Unix seconds, or Unix milliseconds."),
                        MCPToolSchemaFactory.Prop("until", "string", "End time filter. Accepts ISO/local time, Unix seconds, or Unix milliseconds."),
                        MCPToolSchemaFactory.Prop("sinceSecondsAgo", "number", "Start time filter relative to now."),
                        MCPToolSchemaFactory.Prop("sinceLastPlay", "boolean", "Only include entries recorded after the latest Play transition."),
                        MCPToolSchemaFactory.Prop("includeStack", "boolean", "Include full stack traces. Defaults to false."),
                        MCPToolSchemaFactory.Prop("newestFirst", "boolean", "Return newest entries first. Defaults to false.")
                    ));
                case "debug/attach-unity":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("openWindow", "boolean", "Open Unity's Managed Debugger window. Defaults to false."),
                        MCPToolSchemaFactory.Prop("waitForAttach", "boolean", "Wait briefly for an external managed debugger to attach. Defaults to false."),
                        MCPToolSchemaFactory.Prop("timeoutMs", "number", "Attach wait timeout in milliseconds when waitForAttach is true. Defaults to 0.")
                    ));
                case "debug/set-breakpoint":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("file", "string", "Source file path for the requested breakpoint."),
                        MCPToolSchemaFactory.Prop("line", "number", "1-based source line for the requested breakpoint.")
                    ), "file", "line");
                case "debug/stack-trace":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("skipFrames", "number", "Number of MCP call frames to skip. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("maxFrames", "number", "Maximum stack frames to return. Defaults to 50.")
                    ));
                case "debug/variables":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("frameId", "number", "Paused debugger frame id.")
                    ), "frameId");
                case "debug/evaluate":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("expression", "string", "C# expression to evaluate in Unity Editor context. Wrapped as return <expression>; when code is omitted."),
                        MCPToolSchemaFactory.Prop("code", "string", "Full C# method body for editor-context evaluation.")
                    ));
                case "animation/transition-info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("controllerPath", "string", "AnimatorController asset path."),
                        MCPToolSchemaFactory.Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("sourceState", "string", "Optional source state name filter."),
                        MCPToolSchemaFactory.Prop("destinationState", "string", "Optional destination state, state machine, or Exit filter."),
                        MCPToolSchemaFactory.Prop("fromAnyState", "boolean", "When true, only inspect Any State transitions. When false, only inspect state transitions."),
                        MCPToolSchemaFactory.Prop("transitionIndex", "number", "Optional transition index under the source.")
                    ), "controllerPath");
                case "animation/update-state":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("controllerPath", "string", "AnimatorController asset path."),
                        MCPToolSchemaFactory.Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("stateName", "string", "State name to modify."),
                        MCPToolSchemaFactory.Prop("newStateName", "string", "Optional new state name."),
                        MCPToolSchemaFactory.Prop("motionPath", "string", "AnimationClip or Motion asset path to assign."),
                        MCPToolSchemaFactory.Prop("clearMotion", "boolean", "Clear the state's motion."),
                        MCPToolSchemaFactory.Prop("speed", "number", "State speed."),
                        MCPToolSchemaFactory.Prop("tag", "string", "State tag."),
                        MCPToolSchemaFactory.Prop("position", "object", "State graph position object with x/y."),
                        MCPToolSchemaFactory.Prop("isDefault", "boolean", "Set this state as the layer default state."),
                        MCPToolSchemaFactory.Prop("writeDefaultValues", "boolean", "State write default values flag."),
                        MCPToolSchemaFactory.Prop("mirror", "boolean", "State mirror flag."),
                        MCPToolSchemaFactory.Prop("iKOnFeet", "boolean", "State IK on feet flag."),
                        MCPToolSchemaFactory.Prop("cycleOffset", "number", "State cycle offset.")
                    ), "controllerPath", "stateName");
                case "animation/update-transition":
                    return MCPToolSchemaFactory.AnimationUpdateTransitionSchema();
                case "animation/connect-states":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("controllerPath", "string", "AnimatorController asset path."),
                        MCPToolSchemaFactory.Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                        MCPToolSchemaFactory.ArrayProp("stateNames", "string", "State names to connect pairwise."),
                        MCPToolSchemaFactory.Prop("skipExisting", "boolean", "Skip existing transitions. Defaults to true."),
                        MCPToolSchemaFactory.Prop("replaceExisting", "boolean", "Remove existing matching transitions before creating new ones."),
                        MCPToolSchemaFactory.Prop("hasExitTime", "boolean", "Transition has exit time applied to created transitions."),
                        MCPToolSchemaFactory.Prop("exitTime", "number", "Transition exit time applied to created transitions."),
                        MCPToolSchemaFactory.Prop("duration", "number", "Transition duration applied to created transitions."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Transition offset applied to created transitions."),
                        MCPToolSchemaFactory.Prop("hasFixedDuration", "boolean", "Fixed duration flag applied to created transitions."),
                        MCPToolSchemaFactory.ArrayProp("conditions", "object", "Conditions applied to every created transition.")
                    ), "controllerPath", "stateNames");
                case "animation/validate-controller":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("controllerPath", "string", "AnimatorController asset path."),
                        MCPToolSchemaFactory.Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                        MCPToolSchemaFactory.ArrayProp("requiredParameters", new Dictionary<string, object>
                        {
                            { "anyOf", new List<object>
                                {
                                    new Dictionary<string, object> { { "type", "string" } },
                                    new Dictionary<string, object> { { "type", "object" } },
                                }
                            },
                        }, "Strings or objects with name/parameterName and optional type/parameterType."),
                        MCPToolSchemaFactory.ArrayProp("requiredStates", "string", "State names that must exist."),
                        MCPToolSchemaFactory.Prop("requireMotion", "boolean", "Require every state in the layer to have a motion."),
                        MCPToolSchemaFactory.ArrayProp("requiredTransitions", "object", "Objects with source/sourceState, destination/destinationState, and optional conditionParameter."),
                        MCPToolSchemaFactory.Prop("requireFullMesh", "boolean", "Require all stateNames to have pairwise transitions."),
                        MCPToolSchemaFactory.ArrayProp("stateNames", "string", "States used by full mesh validation. Defaults to all layer states.")
                    ), "controllerPath");
                case "uitoolkit/audit-uss-styles":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.ArrayProp("paths", "string", "Optional Assets-relative USS files. Omit to audit every USS file in the effective roots."),
                        MCPToolSchemaFactory.ArrayProp("roots", "string", "Assets-relative roots used to index USS and UXML files. Defaults to the project audit settings, then Assets."),
                        MCPToolSchemaFactory.ArrayProp("runtimeSourceRoots", "string", "Assets-relative roots scanned for UI Toolkit runtime class API references. Defaults to the project audit settings, then Assets."),
                        MCPToolSchemaFactory.ArrayProp("excludePaths", "string", "Assets-relative files or folders excluded from indexing."),
                        MCPToolSchemaFactory.Prop("useProjectSettings", "boolean", "Use ProjectSettings/UnityMCPUIToolkitAudit.json as the default scope. Defaults to true."),
                        MCPToolSchemaFactory.Prop("pixelGridEnabled", "boolean", "Override the project pixel-grid audit switch. The project/default value is used when omitted."),
                        MCPToolSchemaFactory.Prop("pixelGridStep", "number", "Override the positive pixel-grid step, such as 3 or 4."),
                        MCPToolSchemaFactory.Prop("includeSuppressed", "boolean", "Include findings with a reasoned uss-audit suppression comment. Defaults to false."),
                        MCPToolSchemaFactory.Prop("logWarnings", "boolean", "Also write active findings to the Unity Console. Defaults to false."),
                        MCPToolSchemaFactory.Prop("runSelfTests", "boolean", "Run deterministic in-memory rule tests and return their result. Defaults to false."),
                        MCPToolSchemaFactory.Prop("maxIssues", "number", "Maximum returned findings. Defaults to 200; capped at 5000.")
                    ));
                case "uitoolkit/audit-uxml-layout":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.ArrayProp("paths", "string", "Optional Assets-relative UXML files. Omit to audit every UXML file in the effective roots."),
                        MCPToolSchemaFactory.ArrayProp("roots", "string", "Assets-relative roots used to index UXML and USS files. Defaults to the project audit settings, then Assets."),
                        MCPToolSchemaFactory.ArrayProp("runtimeSourceRoots", "string", "Assets-relative roots scanned for runtime UI element-name references. Defaults to the project audit settings, then Assets."),
                        MCPToolSchemaFactory.ArrayProp("excludePaths", "string", "Assets-relative files or folders excluded from indexing."),
                        MCPToolSchemaFactory.Prop("useProjectSettings", "boolean", "Use ProjectSettings/UnityMCPUIToolkitAudit.json as the default scope. Defaults to true."),
                        MCPToolSchemaFactory.Prop("pixelGridEnabled", "boolean", "Override the project pixel-grid audit switch. The project/default value is used when omitted."),
                        MCPToolSchemaFactory.Prop("pixelGridStep", "number", "Override the positive pixel-grid step, such as 3 or 4."),
                        MCPToolSchemaFactory.Prop("includeSuppressed", "boolean", "Include findings with a reasoned uxml-layout-audit suppression comment. Defaults to false."),
                        MCPToolSchemaFactory.Prop("logWarnings", "boolean", "Also write active findings to the Unity Console. Defaults to false."),
                        MCPToolSchemaFactory.Prop("runSelfTests", "boolean", "Run deterministic in-memory rule tests and return their result. Defaults to false."),
                        MCPToolSchemaFactory.Prop("maxIssues", "number", "Maximum returned findings. Defaults to 200; capped at 5000.")
                    ));
                case "uitoolkit/windows":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props());
                case "uitoolkit/tree":
                    return MCPToolSchemaFactory.EditorWindowSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("maxDepth", "number", "Maximum tree depth. Defaults to 8."),
                        MCPToolSchemaFactory.Prop("maxNodes", "number", "Maximum returned nodes. Defaults to 300."),
                        MCPToolSchemaFactory.Prop("includeStyle", "boolean", "Include inline and resolved style summaries.")
                    ));
                case "uitoolkit/query":
                    return MCPToolSchemaFactory.EditorWindowSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class name exact match."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match."),
                        MCPToolSchemaFactory.Prop("text", "string", "TextElement text contains match."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum returned elements. Defaults to 50."),
                        MCPToolSchemaFactory.Prop("includeStyle", "boolean", "Include inline and resolved style summaries.")
                    ));
                case "uitoolkit/style":
                    return MCPToolSchemaFactory.EditorWindowSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Element path from uitoolkit/tree or uitoolkit/query."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        MCPToolSchemaFactory.Prop("text", "string", "TextElement text contains match if path is omitted.")
                    ));
                case "uitoolkit/repaint":
                    return MCPToolSchemaFactory.EditorWindowSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Optional element path from uitoolkit/tree or uitoolkit/query.")
                    ));
                case "uitoolkit/asset-inspect":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("uxmlPath", "string", "UXML asset path, e.g. Assets/UI/HUD.uxml."),
                        MCPToolSchemaFactory.Prop("ussPath", "string", "Optional USS asset path. UXML Style src entries are also auto-resolved."),
                        MCPToolSchemaFactory.ArrayProp("ussPaths", "string", "Optional USS asset paths. UXML Style src entries are also auto-resolved."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match."),
                        MCPToolSchemaFactory.ArrayProp("names", "string", "VisualElement.name values to validate."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class exact match."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "Expected or filtered VisualElement type name."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Total result budget for elements and name matches. Defaults to 100."),
                        MCPToolSchemaFactory.Prop("includeUss", "boolean", "Parse USS files, keeping unconditional class defaults separate from contextual and pseudo-state rules. Defaults to true."),
                        MCPToolSchemaFactory.Prop("includeElements", "boolean", "Return the general elements collection. Defaults to false for names queries and true otherwise."),
                        MCPToolSchemaFactory.Prop("includeAllUssClasses", "boolean", "Return every parsed USS class. Targeted queries default to only classes used by returned elements.")
                    ));
                case "uitoolkit/runtime-documents":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("includeInactive", "boolean", "Include inactive scene UIDocuments. Defaults to true.")
                    ));
                case "uitoolkit/runtime-tree":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("maxDepth", "number", "Maximum tree depth. Defaults to 8."),
                        MCPToolSchemaFactory.Prop("maxNodes", "number", "Maximum returned nodes. Defaults to 300."),
                        MCPToolSchemaFactory.Prop("includeStyle", "boolean", "Include inline, resolved, and background style summaries.")
                    ));
                case "uitoolkit/runtime-query":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path from runtime-tree, e.g. root/0/1."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names, e.g. MainMap/RightControls."),
                        MCPToolSchemaFactory.ArrayProp("visualElementNames", "string", "VisualElementPath names array."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class name exact match."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match."),
                        MCPToolSchemaFactory.Prop("text", "string", "TextElement text contains match."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum returned elements. Defaults to 50."),
                        MCPToolSchemaFactory.Prop("includeStyle", "boolean", "Include inline, resolved, and background style summaries.")
                    ));
                case "uitoolkit/runtime-style":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path from runtime-tree, e.g. root/0/1."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        MCPToolSchemaFactory.ArrayProp("visualElementNames", "string", "VisualElementPath names array."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        MCPToolSchemaFactory.Prop("text", "string", "TextElement text contains match if path is omitted.")
                    ));
                case "uitoolkit/diagnose-runtime":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.ArrayProp("queries", "object", "Optional list of element queries. Each accepts path, visualElementPath, name, className, typeName, text, and pixelScale."),
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path if queries is omitted."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names if queries is omitted."),
                        MCPToolSchemaFactory.ArrayProp("visualElementNames", "string", "VisualElementPath names array if queries is omitted."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if queries is omitted."),
                        MCPToolSchemaFactory.Prop("pixelScale", "number", "Pixel grid scale used for pixel diagnostics. Defaults to 1.")
                    ));
                case "uitoolkit/visual-check":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.ArrayProp("checks", "object", "Visual checks. Supported type values: pixel-grid, background-scale, size."),
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path if checks is omitted."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names if checks is omitted."),
                        MCPToolSchemaFactory.Prop("pixelScale", "number", "Pixel grid scale. Defaults to 1."),
                        MCPToolSchemaFactory.Prop("expectedScale", "number", "Expected background image scale for background-scale checks."),
                        MCPToolSchemaFactory.Prop("width", "number", "Expected element width for size checks."),
                        MCPToolSchemaFactory.Prop("height", "number", "Expected element height for size checks."),
                        MCPToolSchemaFactory.Prop("tolerance", "number", "Allowed pixel delta. Defaults to 0.01.")
                    ));
                case "uitoolkit/locate-element":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("runtime", "boolean", "Locate a runtime UIDocument element when true; otherwise locate an EditorWindow UI Toolkit element. Defaults to false."),
                        MCPToolSchemaFactory.Prop("window", "string", "EditorWindow type/title. Runtime defaults to Game when capture uses it later."),
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path, e.g. root/0/1."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        MCPToolSchemaFactory.ArrayProp("visualElementNames", "string", "VisualElementPath names array."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        MCPToolSchemaFactory.Prop("text", "string", "TextElement text contains match if path is omitted."),
                        MCPToolSchemaFactory.Prop("pixelScale", "number", "Scale from UI points to captured pixels. Defaults to EditorGUIUtility.pixelsPerPoint."),
                        MCPToolSchemaFactory.Prop("padding", "number", "Extra crop padding in pixels. Defaults to 0.")
                    ));
                case "uitoolkit/capture-element":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("runtime", "boolean", "Capture a runtime UIDocument element when true; otherwise capture an EditorWindow UI Toolkit element. Defaults to false."),
                        MCPToolSchemaFactory.Prop("window", "string", "EditorWindow type/title to capture. Runtime defaults to Game, editor defaults to the focused/matched window."),
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path, e.g. root/0/1."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        MCPToolSchemaFactory.ArrayProp("visualElementNames", "string", "VisualElementPath names array."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        MCPToolSchemaFactory.Prop("text", "string", "TextElement text contains match if path is omitted."),
                        MCPToolSchemaFactory.Prop("outputPath", "string", "Output PNG path for the cropped element screenshot."),
                        MCPToolSchemaFactory.Prop("windowOutputPath", "string", "Output PNG path for the full containing window screenshot."),
                        MCPToolSchemaFactory.Prop("pixelScale", "number", "Scale from UI points to captured pixels. Defaults to EditorGUIUtility.pixelsPerPoint."),
                        MCPToolSchemaFactory.Prop("padding", "number", "Extra crop padding in pixels. Defaults to 0.")
                    ));
                case "uitoolkit/compare-element":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("runtime", "boolean", "Capture a runtime UIDocument element when true; otherwise capture an EditorWindow UI Toolkit element. Defaults to false."),
                        MCPToolSchemaFactory.Prop("window", "string", "EditorWindow type/title to capture. Runtime defaults to Game."),
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path, e.g. root/0/1."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        MCPToolSchemaFactory.Prop("referencePath", "string", "Reference PNG path."),
                        MCPToolSchemaFactory.Prop("actualPath", "string", "Output path for captured current element PNG."),
                        MCPToolSchemaFactory.Prop("diffOutputPath", "string", "Optional output path for diff PNG."),
                        MCPToolSchemaFactory.Prop("referenceRect", "object", "Optional comparison rect in reference image."),
                        MCPToolSchemaFactory.Prop("actualRect", "object", "Optional comparison rect in captured image."),
                        MCPToolSchemaFactory.Prop("tolerance", "number", "Allowed per-channel pixel delta. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("padding", "number", "Extra capture padding in pixels. Defaults to 0.")
                    ));
                case "uitoolkit/generated-children":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("runtime", "boolean", "Inspect a runtime UIDocument element when true; otherwise inspect an EditorWindow UI Toolkit element. Defaults to false."),
                        MCPToolSchemaFactory.Prop("window", "string", "EditorWindow type/title for editor inspection."),
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path, e.g. root/0/1."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        MCPToolSchemaFactory.Prop("maxDepth", "number", "Descendant depth to inspect. Defaults to 4."),
                        MCPToolSchemaFactory.Prop("includeAll", "boolean", "Return all descendants, not only generated-looking children. Defaults to false."),
                        MCPToolSchemaFactory.ArrayProp("forbiddenClassContains", "string", "Class substrings that should produce warnings when found."),
                        MCPToolSchemaFactory.ArrayProp("forbiddenTypeContains", "string", "Type-name substrings that should produce warnings when found.")
                    ));
                case "uitoolkit/resource-audit":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("runtime", "boolean", "Audit runtime UIDocument elements when true; otherwise audit EditorWindow UI Toolkit elements. Defaults to false."),
                        MCPToolSchemaFactory.Prop("window", "string", "EditorWindow type/title for editor audits."),
                        MCPToolSchemaFactory.ArrayProp("queries", "object", "Optional list of element queries. Each accepts path, visualElementPath, name, className, typeName, text, expectedBackgroundContains, forbiddenBackgroundContains, requireBackground."),
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path if queries is omitted."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names if queries is omitted."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if queries is omitted."),
                        MCPToolSchemaFactory.Prop("expectedBackgroundContains", "string", "Expected substring in resolved background asset path or name."),
                        MCPToolSchemaFactory.ArrayProp("forbiddenBackgroundContains", "string", "Substrings that must not appear in the resolved background asset path or name."),
                        MCPToolSchemaFactory.Prop("requireBackground", "boolean", "Warn if the target has no resolved background image."),
                        MCPToolSchemaFactory.Prop("warnHighlighted", "boolean", "Warn when a target appears to use a highlighted asset. Defaults to true."),
                        MCPToolSchemaFactory.Prop("maxDepth", "number", "Descendant depth to scan for background resources. Defaults to 3.")
                    ));
                case "uitoolkit/runtime-repaint":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Optional element tree path from runtime-tree."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Optional slash-separated VisualElementPath names."),
                        MCPToolSchemaFactory.ArrayProp("visualElementNames", "string", "Optional VisualElementPath names array.")
                    ));
                case "uitoolkit/refresh":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("refreshAssets", "boolean", "Call AssetDatabase.Refresh before repainting. Defaults to true."),
                        MCPToolSchemaFactory.Prop("forceSynchronousImport", "boolean", "Use ForceSynchronousImport. Defaults to true."),
                        MCPToolSchemaFactory.Prop("timeoutMs", "number", "Maximum wait time in milliseconds. Defaults to 10000."),
                        MCPToolSchemaFactory.Prop("stableFrames", "number", "Consecutive idle repaint frames required. Defaults to 2.")
                    ));
                case "uitoolkit/builder-preview":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("uxmlPath", "string", "UXML asset path to open in UI Builder."),
                        MCPToolSchemaFactory.Prop("waitFrames", "number", "Editor frames to wait before capturing. Defaults to 8."),
                        MCPToolSchemaFactory.Prop("stableFrames", "number", "Consecutive ready UI Builder frames required. Defaults to 2."),
                        MCPToolSchemaFactory.Prop("timeoutMs", "number", "Maximum time to wait for the requested document and canvas. Defaults to 10000."),
                        MCPToolSchemaFactory.Prop("capture", "boolean", "Capture the UI Builder window after opening. Defaults to true."),
                        MCPToolSchemaFactory.Prop("autoMatchGameView", "boolean", "Enable UI Builder Match Game View when visible document content overflows the configured canvas. Defaults to true."),
                        MCPToolSchemaFactory.Prop("requireContentFit", "boolean", "Fail the preview result when visible document content remains clipped by the canvas. Defaults to true."),
                        MCPToolSchemaFactory.Prop("screenshotPath", "string", "PNG path for the UI Builder screenshot. Defaults to the Unity MCP project screenshot directory."),
                        MCPToolSchemaFactory.Prop("maxDimension", "number", "Maximum screenshot dimension. Defaults to 8192."),
                        MCPToolSchemaFactory.Prop("zoom", "number", "Requested zoom, recorded for diagnostics. UI Builder has no stable public zoom API.")
                    ));
                case "uitoolkit/assert-layout":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.ArrayProp("assertions", "object", "Layout assertions. Supported types: edge-touch, same-edge, same-center, inside, size.")
                    ), "assertions");
                case "screenshot/game":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Output PNG path. Defaults to the Unity MCP project screenshot directory (Assets/Screenshots initially)."),
                        MCPToolSchemaFactory.Prop("superSize", "number", "Resolution multiplier. Defaults to 1."),
                        MCPToolSchemaFactory.Prop("waitFrames", "number", "Frames to wait before requesting a running capture. Ignored while paused. Defaults to 2."),
                        MCPToolSchemaFactory.Prop("stableFrames", "number", "Consecutive stable file-size frames required for a running capture. Ignored while paused. Defaults to 2."),
                        MCPToolSchemaFactory.Prop("timeoutMs", "number", "Maximum time to wait for a complete decodable PNG. Defaults to 10000."),
                        MCPToolSchemaFactory.Prop("editorOverlays", "string", "Game View Gizmos and Stats policy: suppress or preserve. Defaults to suppress; use preserve only when editor overlays are the evidence subject.")
                    ));
                case "screenshot/crop":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("sourcePath", "string", "Image path to crop."),
                        MCPToolSchemaFactory.Prop("rect", "object", "Crop rect with x, y, width, height."),
                        MCPToolSchemaFactory.Prop("outputPath", "string", "Output PNG path. Defaults next to source with _crop suffix."),
                        MCPToolSchemaFactory.Prop("originTopLeft", "boolean", "Treat rect x/y as top-left image coordinates. Defaults to true.")
                    ));
                case "screenshot/scene":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Output PNG path for file or both transport. Defaults to the Unity MCP project screenshot directory (Assets/Screenshots initially)."),
                        MCPToolSchemaFactory.Prop("width", "number", "Capture width in pixels. Defaults to 1920."),
                        MCPToolSchemaFactory.Prop("height", "number", "Capture height in pixels. Defaults to 1080."),
                        MCPToolSchemaFactory.Prop("transport", "string", "Output transport: file, base64, or both. Defaults to file.")
                    ));
                case "screenshot/editor-window":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("window", "string", "EditorWindow type full name, simple type name, or exact tab title."),
                        MCPToolSchemaFactory.Prop("typeOrTitle", "string", "Legacy alias for window."),
                        MCPToolSchemaFactory.Prop("path", "string", "Output PNG path. Defaults to the Unity MCP project screenshot directory (Assets/Screenshots initially)."),
                        MCPToolSchemaFactory.Prop("maxDimension", "number", "Maximum screenshot dimension. Defaults to 8192.")
                    ));
                case "graphics/asset-preview":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Asset path to preview, including prefab, material, mesh, or texture assets."),
                        MCPToolSchemaFactory.Prop("width", "number", "Requested preview width in pixels. Defaults to 256."),
                        MCPToolSchemaFactory.Prop("height", "number", "Requested preview height in pixels. Defaults to 256.")
                    ), "assetPath");
                case "gameview/info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props());
                case "gameview/set-resolution":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("width", "number", "Game View custom resolution width in pixels."),
                        MCPToolSchemaFactory.Prop("height", "number", "Game View custom resolution height in pixels."),
                        MCPToolSchemaFactory.Prop("label", "string", "Optional custom size label shown in the Game View size menu.")
                    ), "width", "height");
                case "gameview/set-scale":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("mode", "string", "Scale source: value or minimum. Defaults to value."),
                        MCPToolSchemaFactory.Prop("scale", "number", "Game View zoom scale when mode is value, e.g. 0.76 or 1."),
                        MCPToolSchemaFactory.Prop("fallbackScale", "number", "Fallback minimum scale used if Unity internals do not expose a valid one. Defaults to 0.76.")
                    ));
                case "graphics/image-alpha-bounds":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Texture2D asset path."),
                        MCPToolSchemaFactory.Prop("filePath", "string", "Absolute or project-relative PNG path if assetPath is omitted."),
                        MCPToolSchemaFactory.Prop("alphaThreshold", "number", "Alpha threshold. 0-1 or 0-255. Defaults to 0.01.")
                    ));
                case "graphics/rect-gap":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("firstRect", "object", "First rect with x, y, width, height."),
                        MCPToolSchemaFactory.Prop("secondRect", "object", "Second rect with x, y, width, height."),
                        MCPToolSchemaFactory.Prop("axis", "string", "x or y. Defaults to x."),
                        MCPToolSchemaFactory.Prop("firstEdge", "string", "First rect edge. Defaults to right for x, bottom for y."),
                        MCPToolSchemaFactory.Prop("secondEdge", "string", "Second rect edge. Defaults to left for x, top for y."),
                        MCPToolSchemaFactory.Prop("tolerance", "number", "Touch tolerance in pixels. Defaults to 0.5.")
                    ), "firstRect", "secondRect");
                case "graphics/annotate-rects":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("sourcePath", "string", "Image path to annotate."),
                        MCPToolSchemaFactory.Prop("outputPath", "string", "Output PNG path. Defaults next to source with _annotated suffix."),
                        MCPToolSchemaFactory.ArrayProp("rects", "object", "Rectangles to draw. Each has x, y, width, height, optional color and thickness."),
                        MCPToolSchemaFactory.Prop("originTopLeft", "boolean", "Treat rect x/y as top-left image coordinates. Defaults to true."),
                        MCPToolSchemaFactory.Prop("color", "string", "Default HTML color, e.g. #ff00ffff."),
                        MCPToolSchemaFactory.Prop("thickness", "number", "Default border thickness in pixels. Defaults to 2.")
                    ), "rects");
                case "graphics/compare-images":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("expectedPath", "string", "Reference image path."),
                        MCPToolSchemaFactory.Prop("actualPath", "string", "Current image path."),
                        MCPToolSchemaFactory.Prop("expectedRect", "object", "Optional reference crop rect with x, y, width, height."),
                        MCPToolSchemaFactory.Prop("actualRect", "object", "Optional current crop rect with x, y, width, height."),
                        MCPToolSchemaFactory.Prop("tolerance", "number", "Per-channel pixel tolerance, 0-255. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("maxSamples", "number", "Maximum differing pixel samples returned. Defaults to 20."),
                        MCPToolSchemaFactory.Prop("diffOutputPath", "string", "Optional PNG path to write a red-highlight diff image.")
                    ));
                case "sprite/sheet-info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("texturePath", "string", "Sprite sheet texture asset path.")
                    ));
                case "sprite/pixel-check":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Texture/Sprite asset path."),
                        MCPToolSchemaFactory.ArrayProp("assetPaths", "string", "Texture/Sprite asset paths."),
                        MCPToolSchemaFactory.Prop("folderPath", "string", "Folder to scan recursively for Texture2D assets."),
                        MCPToolSchemaFactory.Prop("dimensionsMultipleOf", "number", "Optional divisor required for texture width/height."),
                        MCPToolSchemaFactory.Prop("expectedScale", "number", "Optional UI scale used to check source dimensions after scaling."),
                        MCPToolSchemaFactory.Prop("tolerance", "number", "Allowed pixel delta. Defaults to 0.01."),
                        MCPToolSchemaFactory.Prop("requirePointFilter", "boolean", "Warn if FilterMode is not Point. Defaults to true."),
                        MCPToolSchemaFactory.Prop("requireNoCompression", "boolean", "Warn if default platform format is compressed. Defaults to true."),
                        MCPToolSchemaFactory.Prop("requireNoMipMaps", "boolean", "Warn if mip maps are enabled. Defaults to true.")
                    ));
                case "sprite/replace-and-slice":
                case "sprite/slice-sheet":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("texturePath", "string", "Sprite sheet texture asset path."),
                        MCPToolSchemaFactory.Prop("sourcePath", "string", "External image file to copy over texturePath. Required for replace-and-slice."),
                        MCPToolSchemaFactory.Prop("frameWidth", "number", "Frame width in pixels."),
                        MCPToolSchemaFactory.Prop("frameHeight", "number", "Frame height in pixels."),
                        MCPToolSchemaFactory.Prop("frameCount", "number", "Optional frame count. Defaults to the full grid."),
                        MCPToolSchemaFactory.Prop("baseName", "string", "Generated sprite name prefix. Defaults to texture file name."),
                        MCPToolSchemaFactory.Prop("columns", "number", "Grid column count. Defaults to textureWidth / frameWidth."),
                        MCPToolSchemaFactory.Prop("startX", "number", "Grid start x in pixels. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("startY", "number", "Grid start y in top-left pixels. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("pivotX", "number", "Optional normalized pivot x."),
                        MCPToolSchemaFactory.Prop("pivotY", "number", "Optional normalized pivot y."),
                        MCPToolSchemaFactory.Prop("preserveSpriteIDs", "boolean", "Preserve existing sprite IDs by generated name. Defaults to true.")
                    ), "texturePath", "frameWidth", "frameHeight");
                case "sprite/update-animation-clip":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("clipPath", "string", "AnimationClip asset path."),
                        MCPToolSchemaFactory.Prop("texturePath", "string", "Sprite sheet texture asset path."),
                        MCPToolSchemaFactory.Prop("bindingPath", "string", "Animation binding path to SpriteRenderer. Empty means the animated object itself."),
                        MCPToolSchemaFactory.Prop("frameRate", "number", "Animation frame rate. Defaults to the clip frame rate or 12."),
                        MCPToolSchemaFactory.ArrayProp("spriteNames", "string", "Optional exact sprite names to use."),
                        MCPToolSchemaFactory.Prop("loopTime", "boolean", "Whether the clip loops. Defaults to the current clip setting.")
                    ), "clipPath", "texturePath");
                case "sprite/replace-slice-update-clip":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("texturePath", "string", "Sprite sheet texture asset path."),
                        MCPToolSchemaFactory.Prop("sourcePath", "string", "External image file to copy over texturePath."),
                        MCPToolSchemaFactory.Prop("clipPath", "string", "Optional AnimationClip asset path to update after slicing."),
                        MCPToolSchemaFactory.Prop("frameWidth", "number", "Frame width in pixels."),
                        MCPToolSchemaFactory.Prop("frameHeight", "number", "Frame height in pixels."),
                        MCPToolSchemaFactory.Prop("frameCount", "number", "Optional frame count. Defaults to the full grid."),
                        MCPToolSchemaFactory.Prop("baseName", "string", "Generated sprite name prefix. Defaults to texture file name."),
                        MCPToolSchemaFactory.Prop("frameRate", "number", "Animation frame rate. Defaults to the clip frame rate or 12."),
                        MCPToolSchemaFactory.Prop("bindingPath", "string", "Animation binding path to SpriteRenderer.")
                    ), "texturePath", "sourcePath", "frameWidth", "frameHeight");
                case "texture/apply-sprite-preset":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Texture asset path."),
                        MCPToolSchemaFactory.Prop("referencePath", "string", "Optional texture asset whose importer settings are copied first."),
                        MCPToolSchemaFactory.Prop("preset", "string", "High-level preset. Supported: pixel-sprite. Preserves the current Single/Multiple mode."),
                        MCPToolSchemaFactory.Prop("pixelsPerUnit", "number", "Sprite pixels per unit."),
                        MCPToolSchemaFactory.Prop("filterMode", "string", "Texture FilterMode, e.g. Point."),
                        MCPToolSchemaFactory.Prop("textureCompression", "string", "TextureImporterCompression value."),
                        MCPToolSchemaFactory.Prop("defaultPlatformFormat", "string", "Default platform TextureImporterFormat, e.g. RGBA32."),
                        MCPToolSchemaFactory.Prop("defaultPlatformCompression", "string", "Default platform TextureImporterCompression."),
                        MCPToolSchemaFactory.Prop("readable", "boolean", "Texture is readable."),
                        MCPToolSchemaFactory.Prop("mipmapEnabled", "boolean", "Generate mipmaps."),
                        MCPToolSchemaFactory.Prop("alphaIsTransparency", "boolean", "Alpha is transparency."),
                        MCPToolSchemaFactory.Prop("pivot", "object", "Sprite pivot with x/y."),
                        MCPToolSchemaFactory.Prop("border", "object", "Sprite border. Accepts number, [left,bottom,right,top], or object with left/bottom/right/top.")
                    ), "path");
                case "texture/info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Texture asset path under Assets/.")
                    ), "path");
                case "texture/set-import":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Texture asset path under Assets/."),
                        MCPToolSchemaFactory.Prop("textureType", "string", "TextureImporterType, such as Default, Sprite, or NormalMap."),
                        MCPToolSchemaFactory.Prop("spriteMode", "string", "SpriteImportMode, such as Single or Multiple."),
                        MCPToolSchemaFactory.Prop("spritePixelsPerUnit", "number", "Sprite pixels per unit."),
                        MCPToolSchemaFactory.Prop("sRGB", "boolean", "Import as sRGB texture."),
                        MCPToolSchemaFactory.Prop("readable", "boolean", "Enable CPU read/write access."),
                        MCPToolSchemaFactory.Prop("mipmapEnabled", "boolean", "Generate mipmaps."),
                        MCPToolSchemaFactory.Prop("filterMode", "string", "Texture FilterMode."),
                        MCPToolSchemaFactory.Prop("wrapMode", "string", "TextureWrapMode."),
                        MCPToolSchemaFactory.Prop("maxTextureSize", "number", "Maximum imported texture size."),
                        MCPToolSchemaFactory.Prop("textureCompression", "string", "TextureImporterCompression value."),
                        MCPToolSchemaFactory.Prop("anisoLevel", "number", "Anisotropic filtering level."),
                        MCPToolSchemaFactory.Prop("alphaIsTransparency", "boolean", "Treat alpha as transparency."),
                        MCPToolSchemaFactory.Prop("npotScale", "string", "TextureImporterNPOTScale value.")
                    ), "path");
                case "texture/find-duplicates":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("folder", "string", "Single search folder under Assets/. Defaults to Assets."),
                        MCPToolSchemaFactory.ArrayProp("folders", "string", "Additional search folders under Assets/. Results are de-duplicated across folders."),
                        MCPToolSchemaFactory.Prop("mode", "string", "Comparison mode: decodedPixels (default) or fileBytes."),
                        MCPToolSchemaFactory.ArrayProp("extensions", "string", "Optional file extensions such as png, jpg, or jpeg. decodedPixels supports PNG/JPEG."),
                        MCPToolSchemaFactory.Prop("maxAssets", "number", "Maximum assets to fingerprint. Defaults to 10000; capped at 50000."),
                        MCPToolSchemaFactory.Prop("maxGroups", "number", "Maximum duplicate groups returned. Defaults to 100; capped at 2000.")
                    ));
                case "texture/import-image":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("sourcePath", "string", "Local image file path."),
                        MCPToolSchemaFactory.Prop("sourceUrl", "string", "Remote image URL."),
                        MCPToolSchemaFactory.Prop("targetPath", "string", "Target asset path inside Assets."),
                        MCPToolSchemaFactory.Prop("targetFolder", "string", "Target folder used with assetName."),
                        MCPToolSchemaFactory.Prop("assetName", "string", "Target file name used with targetFolder."),
                        MCPToolSchemaFactory.Prop("overwrite", "boolean", "Overwrite targetPath if content differs. Defaults to false."),
                        MCPToolSchemaFactory.Prop("dedupeByHash", "boolean", "Skip if the target folder already contains identical image bytes. Defaults to true."),
                        MCPToolSchemaFactory.Prop("applySpritePreset", "boolean", "Apply sprite import settings after import. Defaults to true."),
                        MCPToolSchemaFactory.Prop("preset", "string", "Preset passed to texture/apply-sprite-preset. Defaults to pixel-sprite.")
                    ));
                case "texture/check-import-settings":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Texture asset path to check."),
                        MCPToolSchemaFactory.ArrayProp("assetPaths", "string", "Texture asset paths to check."),
                        MCPToolSchemaFactory.Prop("folderPath", "string", "Folder to scan recursively for Texture2D assets."),
                        MCPToolSchemaFactory.Prop("referencePath", "string", "Optional texture asset whose importer settings are treated as expected."),
                        MCPToolSchemaFactory.Prop("preset", "string", "Optional high-level preset to check. Supported: pixel-sprite."),
                        MCPToolSchemaFactory.Prop("requirePixelSprite", "boolean", "Shortcut for preset=pixel-sprite. Defaults to true when referencePath is omitted."),
                        MCPToolSchemaFactory.Prop("includeMatching", "boolean", "Include passing comparisons in the returned comparisons list. Defaults to false.")
                    ));
                case "texture/check-ui-import-settings":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Texture asset path to check."),
                        MCPToolSchemaFactory.ArrayProp("assetPaths", "string", "Texture asset paths to check."),
                        MCPToolSchemaFactory.Prop("folderPath", "string", "Folder to scan recursively for Texture2D assets."),
                        MCPToolSchemaFactory.Prop("referencePath", "string", "Optional texture asset whose importer settings are treated as expected."),
                        MCPToolSchemaFactory.Prop("includeMatching", "boolean", "Include passing comparisons in the returned comparisons list. Defaults to false."),
                        MCPToolSchemaFactory.Prop("expectedWidth", "number", "Optional exact texture width check."),
                        MCPToolSchemaFactory.Prop("expectedHeight", "number", "Optional exact texture height check."),
                        MCPToolSchemaFactory.Prop("expectedBorder", "object", "Optional sprite border check. Accepts object with left/bottom/right/top or x/y/z/w."),
                        MCPToolSchemaFactory.Prop("maxTextureSize", "number", "Optional exact TextureImporter maxTextureSize check."),
                        MCPToolSchemaFactory.Prop("tolerance", "number", "Float tolerance for border/PPU checks. Defaults to 0.001.")
                    ));
                case "build/start":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("target", "string", "BuildTarget. Defaults to StandaloneWindows64."),
                        MCPToolSchemaFactory.Prop("outputPath", "string", "Player output executable path."),
                        MCPToolSchemaFactory.Prop("developmentBuild", "boolean", "Build with Development flag."),
                        MCPToolSchemaFactory.ArrayProp("scenes", "string", "Optional scene paths. Defaults to enabled Build Settings scenes."),
                        MCPToolSchemaFactory.Prop("overwrite", "boolean", "Delete existing exe and Data folder before build. Defaults to true."),
                        MCPToolSchemaFactory.Prop("run", "boolean", "Launch the built executable after a successful build. Defaults to true."),
                        MCPToolSchemaFactory.Prop("runSeconds", "number", "Seconds to let the executable run before sampling/termination. Defaults to 5."),
                        MCPToolSchemaFactory.Prop("terminateAfter", "boolean", "Kill the process after sampling. Defaults to true."),
                        MCPToolSchemaFactory.Prop("captureWindow", "boolean", "Capture the built player's main window on Windows. Defaults to false."),
                        MCPToolSchemaFactory.Prop("screenshotPath", "string", "PNG path for captureWindow output."),
                        MCPToolSchemaFactory.Prop("windowWaitMs", "number", "Milliseconds to wait for the main window. Defaults to 5000."),
                        MCPToolSchemaFactory.Prop("logTailLines", "number", "Player.log tail lines to return. Defaults to 120."),
                        MCPToolSchemaFactory.Prop("clearStuck", "boolean", "Replace a non-terminal build job left behind by an interrupted editor session. Defaults to false.")
                    ), "outputPath");
                case "build/get-job":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("jobId", "string", "Optional build job ID. Defaults to the current or latest job."),
                        MCPToolSchemaFactory.Prop("clear", "boolean", "Clear the persisted job after a terminal result is read. Defaults to false.")
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
    }
}
