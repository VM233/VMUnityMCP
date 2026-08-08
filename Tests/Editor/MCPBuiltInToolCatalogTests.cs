using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [Category(MCPPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class MCPBuiltInToolCatalogTests
    {
        [Test]
        public void ToolMetadata_ExposesPersistentPlayerBuildAndAssetRefreshJobs()
        {
            var tools = GetAllCatalogTools(compact: false, includeSchema: true);

            foreach (string route in new[]
                     {
                         "build/start",
                         "build/get-job",
                         "asset/refresh",
                         "asset/get-refresh-job",
                     })
            {
                var tool = tools.Single(item => item["route"].ToString() == route);
                Assert.That(tool["moduleId"], Is.EqualTo("unity." + route.Split('/')[0]), route);
                Assert.That(tool["operationKind"], Is.Not.Empty, route);
                Assert.That(tool["inputSchema"], Is.InstanceOf<Dictionary<string, object>>(), route);
            }

            var refreshTool = tools.Single(item => item["route"].ToString() == "asset/refresh");
            var refreshSchema = RequireDictionary(refreshTool["inputSchema"]);
            var refreshProperties = RequireDictionary(refreshSchema["properties"]);
            Assert.That(refreshProperties.ContainsKey("assetPaths"), Is.True);
            Assert.That(refreshProperties.ContainsKey("reconcileExternalChanges"), Is.False);
            Assert.That(refreshProperties.ContainsKey("expectedProjectPath"), Is.True);

            var refreshJobTool = tools.Single(item => item["route"].ToString() == "asset/get-refresh-job");
            var refreshJobSchema = RequireDictionary(refreshJobTool["inputSchema"]);
            var refreshJobProperties = RequireDictionary(refreshJobSchema["properties"]);
            Assert.That(refreshJobProperties.ContainsKey("timeoutMs"), Is.True);

            foreach (var tool in tools.Where(item => !HasTag(item, "readOnly")))
            {
                var schema = RequireDictionary(tool["inputSchema"]);
                var properties = RequireDictionary(schema["properties"]);
                Assert.That(properties.ContainsKey("expectedProjectPath"), Is.True,
                    tool["route"].ToString());
            }
        }

        [Test]
        public void ToolMetadata_ExposesPlayModeAndProfilerRoutesInCanonicalCatalog()
        {
            var toolsResult = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: false, includeSchema: true, limit: 200,
                category: "editor"));
            var tools = (List<Dictionary<string, object>>)toolsResult["tools"];

            var playModeTool = tools.Single(item => item["route"].ToString() == "editor/play-mode");
            Assert.That(playModeTool["toolName"], Is.EqualTo("unity_play_mode"));
            Assert.That(playModeTool["moduleId"], Is.EqualTo("unity.editor"));
            var playModeSchema = RequireDictionary(playModeTool["inputSchema"]);
            var playModeProperties = RequireDictionary(playModeSchema["properties"]);
            Assert.That(playModeProperties.Keys, Does.Contain("action"));
            Assert.That(playModeProperties.Keys, Does.Contain("timeoutMs"));
            Assert.That(playModeProperties.Keys, Does.Contain("expectedProjectPath"));

            var profilerToolsResult = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: false, includeSchema: true, limit: 200,
                category: "profiler"));
            var profilerTools = (List<Dictionary<string, object>>)profilerToolsResult["tools"];
            Assert.That(profilerTools, Is.Not.Empty);
            Assert.That(profilerTools.All(tool =>
                tool["moduleId"].ToString() == "unity.profiler"), Is.True);
            Assert.That(profilerTools.All(tool =>
                tool["inputSchema"] is Dictionary<string, object>), Is.True);

            var snapshotStatusTool = profilerTools.Single(item =>
                item["route"].ToString() == "profiler/memory-snapshot-status");
            Assert.That(HasTag(snapshotStatusTool, "readOnly"), Is.True);
            var snapshotStatusSchema = RequireDictionary(snapshotStatusTool["inputSchema"]);
            var snapshotStatusProperties = RequireDictionary(snapshotStatusSchema["properties"]);
            Assert.That(snapshotStatusProperties.Keys, Does.Contain("jobId"));
        }

        [Test]
        public void UIToolkitStaticAudits_AreCanonicalReadOnlyLongRunningTools()
        {
            var toolsResult = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: false, includeSchema: true, limit: 200,
                category: "uitoolkit"));
            var tools = (List<Dictionary<string, object>>)toolsResult["tools"];

            foreach (string route in new[]
                     {
                         "uitoolkit/audit-uss-styles",
                         "uitoolkit/audit-uxml-layout",
                     })
            {
                var tool = tools.Single(item => item["route"].ToString() == route);
                Assert.That(HasTag(tool, "readOnly"), Is.True, route);
                Assert.That(HasTag(tool, "longRunning"), Is.True, route);
                Assert.That(tool["moduleId"], Is.EqualTo("unity.uitoolkit"), route);
                Assert.That(tool["toolName"],
                    Is.EqualTo("unity_" + route.Replace('/', '_').Replace('-', '_')), route);

                var schema = RequireDictionary(tool["inputSchema"]);
                var properties = RequireDictionary(schema["properties"]);
                Assert.That(properties.Keys,
                    Is.SupersetOf(new[]
                    {
                        "paths", "roots", "runtimeSourceRoots", "excludePaths",
                        "useProjectSettings", "includeSuppressed", "runSelfTests",
                        "pixelGridEnabled", "pixelGridStep", "maxIssues",
                    }), route);
            }
        }

        [Test]
        public void ConsolidatedToolMetadata_ExposesVariantParametersOnCanonicalRoutes()
        {
            Dictionary<string, object> Properties(string route)
            {
                string category = route.Substring(0, route.IndexOf('/'));
                var toolsResult = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                    compact: false, includeSchema: true, limit: 200,
                    category: category));
                var tools = (List<Dictionary<string, object>>)toolsResult["tools"];
                var tool = tools.Single(item => item["route"].ToString() == route);
                var schema = RequireDictionary(tool["inputSchema"]);
                return RequireDictionary(schema["properties"]);
            }

            Assert.That(Properties("gameview/set-scale").Keys,
                Is.SupersetOf(new[] { "mode", "scale", "fallbackScale" }));
            Assert.That(Properties("screenshot/scene").Keys,
                Is.SupersetOf(new[] { "path", "width", "height", "transport" }));
            Assert.That(Properties("uitoolkit/refresh").Keys,
                Is.SupersetOf(new[] { "refreshAssets", "timeoutMs", "stableFrames" }));
            Assert.That(Properties("editor/play-mode").Keys,
                Does.Contain("action"));
        }

        [Test]
        public void ToolMetadata_DeclaresGameViewCaptureRequiresPlayMode()
        {
            var screenshotTools = (List<Dictionary<string, object>>)RequireDictionary(
                MCPToolMetadata.GetRegisteredTools(
                    compact: false,
                    includeSchema: true,
                    limit: 200,
                    category: "screenshot"))["tools"];
            var screenshot = screenshotTools.Single(tool =>
                tool["route"].ToString() == "screenshot/game");

            Assert.That(HasTag(screenshot, "requiresPlayMode"), Is.True);
            Assert.That(screenshot["description"].ToString(),
                Does.Contain("suppress and restore Game View Gizmos and Stats by default"));
            var inputSchema =
                (Dictionary<string, object>)screenshot["inputSchema"];
            var properties =
                (Dictionary<string, object>)inputSchema["properties"];
            Assert.That(properties.ContainsKey("editorOverlays"), Is.True);
        }

        [Test]
        public void CanonicalCatalog_IncludesCoreAndAvailableOptionalPackageTools()
        {
            var tools = GetAllCatalogTools(compact: false, includeSchema: true);
            var catalogRoutes = tools
                .Select(tool => tool["route"].ToString()).ToHashSet(StringComparer.Ordinal);
            foreach (string route in new[]
                     {
                         "editor/execute-code",
                         "jobs/get",
                         "jobs/cancel",
                         "jobs/cleanup",
                         "asset/import-settings/get", "asset/import-settings/set",
                         "scene/workspace",
                         "material/properties/get", "material/properties/set",
                     })
            {
                Assert.That(catalogRoutes, Does.Contain(route), route);
            }

            var catalogTools = tools.ToDictionary(tool => tool["route"].ToString());
            var executeCodeSchema = RequireDictionary(
                catalogTools["editor/execute-code"]["inputSchema"]);
            var executeCodeProperties = RequireDictionary(
                executeCodeSchema["properties"]);
            foreach (string property in new[]
                     {
                         "unityStructFormat",
                         "idempotencyKey",
                         "cleanupCode",
                     })
            {
                Assert.That(executeCodeProperties.ContainsKey(property),
                    Is.True, property);
            }
            var executeCodeOutput = RequireDictionary(
                catalogTools["editor/execute-code"]["outputSchema"]);
            var executeCodeOutputProperties = RequireDictionary(
                executeCodeOutput["properties"]);
            Assert.That(executeCodeOutputProperties.ContainsKey("jobId"),
                Is.True);
            Assert.That(executeCodeOutputProperties.ContainsKey("cleanupToken"),
                Is.True);
            Assert.That(executeCodeOutputProperties.ContainsKey("tags"), Is.True);
            foreach (var entry in executeCodeOutputProperties)
            {
                var propertySchema = RequireDictionary(entry.Value);
                Assert.That(propertySchema.TryGetValue("description", out object description),
                    Is.True, entry.Key);
                Assert.That(description?.ToString(), Is.Not.Empty, entry.Key);
            }
            foreach (string retiredField in new[]
                     {
                         "cleanupAvailable", "cleanupDeclared",
                         "cancellationRequested", "cancelMode", "incremental",
                         "statusRoute", "cancelRoute", "cleanupRoute", "reused",
                     })
            {
                Assert.That(executeCodeOutputProperties.ContainsKey(retiredField),
                    Is.False, retiredField);
            }

            foreach (string route in new[]
                     {
                         "vfxgraph/info", "audio-mixer/info", "build/profile",
                         "addressables/info", "timeline/info", "cinemachine/info",
                     })
            {
                if (!catalogTools.TryGetValue(route, out Dictionary<string, object> tool))
                    continue;
                Assert.That(tool["moduleId"], Is.EqualTo("unity." + route.Split('/')[0]), route);
            }
        }

        [Test]
        public void ToolMetadata_DefaultIsCompactPaginatedAndSchemaFree()
        {
            var result = RequireDictionary(MCPToolMetadata.GetRegisteredTools());
            Assert.That(System.Convert.ToInt32(result["schemaVersion"]), Is.EqualTo(7));
            Assert.That(result.ContainsKey("compact"), Is.False);
            Assert.That(result.ContainsKey("firstClassOnly"), Is.False);
            Assert.That(result.ContainsKey("includeSchema"), Is.False);
            Assert.That(result.ContainsKey("returnedTools"), Is.False);
            Assert.That(result.ContainsKey("hasMore"), Is.False);
            Assert.That(result.ContainsKey("routes"), Is.False);
            Assert.That(result.ContainsKey("mcpTools"), Is.False);
            Assert.That(MiniJson.Serialize(result).Length, Is.LessThan(100000));

            var tools = (List<Dictionary<string, object>>)result["tools"];
            Assert.That(tools.All(tool => !tool.ContainsKey("inputSchema")), Is.True);
        }

        [Test]
        public void ToolMetadata_DetailedPageUsesOneSchemaKey()
        {
            var result = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: false, includeSchema: true, limit: 5));
            var tools = (List<Dictionary<string, object>>)result["tools"];
            Assert.That(tools, Is.Not.Empty);
            Assert.That(tools.All(tool => tool.ContainsKey("inputSchema")), Is.True);
            Assert.That(tools.All(tool => !tool.ContainsKey("input_schema")), Is.True);
            Assert.That(result.ContainsKey("firstClassTools"), Is.False);
            Assert.That(result.ContainsKey("fallbackTools"), Is.False);
            Assert.That(result.ContainsKey("metadataIssues"), Is.False);
        }

        [Test]
        [Category(MCPPackageTestCommands.DefaultPackageSmokeCategory)]
        public void ToolMetadata_CanonicalCatalogHasNoDefaultDescriptionsOrSchemaGaps()
        {
            var issues = new List<Dictionary<string, object>>();
            var tools = GetAllCatalogTools(compact: false, includeSchema: true,
                metadataIssues: issues);

            Assert.That(issues, Is.Empty,
                string.Join(Environment.NewLine, issues.Select(MiniJson.Serialize)));
            Assert.That(tools.Any(tool => tool["route"].ToString() == "_meta/tools"), Is.False);
            Assert.That(tools.Any(tool => tool["route"].ToString() == "_meta/capabilities"), Is.False);
            Assert.That(tools.Any(tool => tool["route"].ToString() == "search/scene"), Is.True);
            Assert.That(tools.Any(tool => tool["route"].ToString() == "search/by-name"), Is.False);
            Assert.That(tools.Any(tool => tool["route"].ToString() == "search/by-component"), Is.False);
            var builtInRoutes = tools
                .Select(tool => tool["route"].ToString())
                .Where(route => !route.StartsWith("project-tools/call/", StringComparison.Ordinal))
                .ToArray();
            var registeredRoutes = GetBuiltInRoutes();
            Assert.That(builtInRoutes, Is.Not.Empty);
            Assert.That(builtInRoutes.All(route => registeredRoutes.Contains(route)), Is.True);
            foreach (string retiredRoute in new[]
                     {
                         "advanced/execute", "project-tools/list", "project-tools/get",
                         "project-tools/execute",
                     })
            {
                Assert.That(builtInRoutes, Does.Not.Contain(retiredRoute), retiredRoute);
            }
        }

        [Test]
        [Category(MCPPackageTestCommands.DefaultPackageSmokeCategory)]
        public void ToolMetadata_UsesOnlyTheCurrentMetadataContract()
        {
            var method = typeof(MCPToolMetadata).GetMethod(nameof(MCPToolMetadata.GetRegisteredTools),
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.GetParameters().Select(parameter => parameter.Name),
                Does.Not.Contain("includeCollections"));
            Assert.That(typeof(MCPToolMetadata).GetMethod("GetRegisteredRoutes",
                BindingFlags.Static | BindingFlags.Public), Is.Null);

            var result = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: false, includeSchema: true, limit: 20));
            foreach (string retiredCollection in new[]
                     {
                         "routes", "mcpTools", "firstClassTools", "fallbackTools", "categories"
                     })
            {
                Assert.That(result.ContainsKey(retiredCollection), Is.False, retiredCollection);
            }

            var tools = (List<Dictionary<string, object>>)result["tools"];
            Assert.That(tools.All(tool => !tool.ContainsKey("name")), Is.True);
            Assert.That(tools.All(tool => !tool.ContainsKey("legacyToolName")), Is.True);
            string[] retiredBooleanMetadata =
            {
                "readOnly", "mutatesAssets", "mutatesRuntime", "dangerous",
                "longRunning", "mayReloadDomain", "requiresPlayMode",
                "firstClass", "preferred", "cleanupAvailable", "incrementalJob",
                "hasOutputSchema", "enforcesInputSchema", "enforcesOutputSchema", "valid",
            };
            foreach (Dictionary<string, object> tool in tools)
            {
                Assert.That(retiredBooleanMetadata.All(key => !tool.ContainsKey(key)),
                    Is.True, tool["route"].ToString());
                if (tool.TryGetValue("tags", out object tags))
                {
                    Assert.That(tags, Is.InstanceOf<IList>());
                    Assert.That(((IList)tags).Cast<object>().All(tag =>
                        !string.IsNullOrWhiteSpace(tag?.ToString())), Is.True);
                }
            }

            foreach (Dictionary<string, object> tool in
                     MCPProjectToolCommands.GetToolDetails(validOnly: false))
            {
                Assert.That(retiredBooleanMetadata.All(key => !tool.ContainsKey(key)),
                    Is.True, tool["toolName"].ToString());
            }

            var routes = GetBuiltInRoutes();
            Assert.That(routes, Does.Not.Contain("_meta/routes"));
            Assert.That(routes.Any(route => route.StartsWith("amplify/", StringComparison.Ordinal)),
                Is.False);
            Assert.That(routes.Any(route => route.StartsWith("uma/", StringComparison.Ordinal)),
                Is.False);
            foreach (string retiredRoute in new[]
                     {
                         "prefab/set-object-reference", "prefab/duplicate",
                         "prefab/set-active", "prefab/reparent"
                     })
            {
                Assert.That(routes, Does.Not.Contain(retiredRoute), retiredRoute);
            }
            foreach (string currentRoute in new[]
                     {
                         "gameobject/duplicate", "gameobject/set-active", "gameobject/reparent"
                     })
            {
                Assert.That(routes, Does.Contain(currentRoute), currentRoute);
            }
            Assert.That(MCPSettingsManager.GetAllCategoryNames(), Does.Not.Contain("amplify"));
            Assert.That(MCPSettingsManager.GetAllCategoryNames(), Does.Not.Contain("uma"));

            var capabilities = RequireDictionary(MCPCapabilityRegistry.GetCapabilities());
            var optional = (List<Dictionary<string, object>>)capabilities["optional"];
            Assert.That(optional.Select(capability => capability["name"].ToString()),
                Does.Not.Contain("uma"));
        }

        [Test]
        public void ToolMetadata_CanonicalSchemasUseCanonicalFieldsAndTrueAnnotations()
        {
            var tools = GetAllCatalogTools(compact: true, includeSchema: true);
            string json = MiniJson.Serialize(tools);

            Assert.That(json, Does.Not.Contain("Alias for"));
            Assert.That(json, Does.Not.Contain("\"uidocumentInstanceId\""));
            foreach (var tool in tools)
            {
                if (!tool.TryGetValue("annotations", out object annotationsValue))
                    continue;
                var annotations = RequireDictionary(annotationsValue);
                Assert.That(annotations.ContainsKey("title"), Is.False);
                Assert.That(annotations.Values.OfType<bool>().All(value => value), Is.True);
            }
        }

        [Test]
        public void ToolMetadata_ExposesPrefabConfigureComponentInCanonicalCatalog()
        {
            var result = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: true, includeSchema: true, limit: 200,
                category: "prefab-asset"));
            var tools = (List<Dictionary<string, object>>)result["tools"];
            var tool = tools.Single(item =>
                item["route"].ToString() == "prefab-asset/configure-component");

            Assert.That(tool["toolName"], Is.EqualTo("unity_prefab_asset_configure_component"));
            Assert.That(tool["moduleId"], Is.EqualTo("unity.prefab-asset"));
            Assert.That(tool["operationKind"], Is.EqualTo("mutate"));
            var schema = RequireDictionary(tool["inputSchema"]);
            CollectionAssert.AreEquivalent(new[] { "assetPath", "componentType" },
                (List<string>)schema["required"]);
            var properties = RequireDictionary(schema["properties"]);
            Assert.That(properties.Keys, Does.Contain("properties"));
            Assert.That(properties.Keys, Does.Contain("references"));
            Assert.That(properties.Keys, Does.Contain("createPathIfMissing"));
            Assert.That(properties.Keys, Does.Contain("expectedProjectPath"));
            var references = RequireDictionary(properties["references"]);
            var referenceItems = RequireDictionary(references["items"]);
            var referenceProperties = RequireDictionary(referenceItems["properties"]);
            Assert.That(referenceProperties.Keys, Does.Contain("referenceSubAssetName"));
            Assert.That(referenceProperties.Keys, Does.Contain("referenceSubAssetLocalId"));
        }

        [Test]
        public void ToolMetadata_ExposesComposableAssetAuthoringRoutesInCanonicalCatalog()
        {
            var tools = GetAllCatalogTools(compact: true, includeSchema: true);
            string[] routes =
            {
                "asset/create-folder",
                "asset/copy",
                "prefab/create-variant",
                "prefab-asset/hierarchy",
                "prefab-asset/transaction-edit",
                "localization/upsert-entry",
                "localization/remove-entry",
            };

            foreach (string route in routes)
            {
                var tool = tools.Single(item => item["route"].ToString() == route);
                Assert.That(tool["moduleId"], Is.EqualTo("unity." + route.Split('/')[0]), route);
                Assert.That(tool.ContainsKey("inputSchema"), Is.True, route);
                Assert.That(tool.ContainsKey("outputSchema"), Is.True, route);
            }
        }

        [Test]
        public void ToolMetadata_PrefabAddGameObjectExposesLayer()
        {
            var result = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: true, includeSchema: true, limit: 200,
                category: "prefab-asset"));
            var tools = (List<Dictionary<string, object>>)result["tools"];
            var tool = tools.Single(item => item["route"].ToString() == "prefab-asset/add-gameobject");

            Assert.That(tool["moduleId"], Is.EqualTo("unity.prefab-asset"));
            var schema = RequireDictionary(tool["inputSchema"]);
            var properties = RequireDictionary(schema["properties"]);
            Assert.That(properties.Keys, Does.Contain("layer"));
            var layer = RequireDictionary(properties["layer"]);
            Assert.That(layer["type"], Is.EqualTo("string"));
            Assert.That(layer["description"].ToString(), Does.Contain("parent GameObject's layer"));
        }

        [Test]
        public void ToolMetadata_PrefabAddComponentExposesInitialSerializedProperties()
        {
            var result = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: true, includeSchema: true, limit: 200,
                category: "prefab-asset"));
            var tools = (List<Dictionary<string, object>>)result["tools"];
            var tool = tools.Single(item =>
                item["route"].ToString() == "prefab-asset/add-component");

            Assert.That(tool["moduleId"], Is.EqualTo("unity.prefab-asset"));
            Assert.That(tool["description"].ToString(),
                Does.Contain("optionally initialize").And.Contain("serialized state"));

            var schema = RequireDictionary(tool["inputSchema"]);
            var properties = RequireDictionary(schema["properties"]);
            Assert.That(properties.Keys, Does.Contain("properties"));
            var initialProperties = RequireDictionary(properties["properties"]);
            Assert.That(initialProperties["type"], Is.EqualTo("object"));
            Assert.That(initialProperties["additionalProperties"], Is.EqualTo(true));
            Assert.That(initialProperties["description"].ToString(),
                Does.Contain("before the new component is saved"));
        }

        [Test]
        public void ToolMetadata_ExposesAssetImportAndTextureToolsInCanonicalCatalog()
        {
            var assetResult = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: true, includeSchema: true, limit: 200,
                category: "asset"));
            var textureResult = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: true, includeSchema: true, limit: 200,
                category: "texture"));
            var tools = ((List<Dictionary<string, object>>)assetResult["tools"])
                .Concat((List<Dictionary<string, object>>)textureResult["tools"])
                .ToList();
            var toolsByRoute = tools.ToDictionary(tool => tool["route"].ToString());

            foreach (var expected in new[]
                     {
                         (Route: "asset/import", ToolName: "unity_asset_import"),
                         (Route: "texture/apply-sprite-preset", ToolName: "unity_texture_apply_sprite_preset"),
                         (Route: "texture/info", ToolName: "unity_texture_info"),
                         (Route: "texture/find-duplicates", ToolName: "unity_texture_find_duplicates"),
                     })
            {
                Assert.That(toolsByRoute.ContainsKey(expected.Route), Is.True,
                    $"{expected.Route} must publish discoverable metadata.");
                var tool = toolsByRoute[expected.Route];
                Assert.That(tool["toolName"], Is.EqualTo(expected.ToolName));
                Assert.That(tool["moduleId"], Is.EqualTo("unity." + expected.Route.Split('/')[0]));

                var schema = RequireDictionary(tool["inputSchema"]);
                var properties = RequireDictionary(schema["properties"]);
                Assert.That(properties, Is.Not.Empty, $"{expected.Route} must publish a concrete input schema.");
            }

            var textureInfoAnnotations = RequireDictionary(toolsByRoute["texture/info"]["annotations"]);
            Assert.That(textureInfoAnnotations["readOnlyHint"], Is.EqualTo(true));
            var duplicateFinderAnnotations = RequireDictionary(
                toolsByRoute["texture/find-duplicates"]["annotations"]);
            Assert.That(duplicateFinderAnnotations["readOnlyHint"], Is.EqualTo(true));

            var assetImportSchema = RequireDictionary(toolsByRoute["asset/import"]["inputSchema"]);
            var assetImportProperties = RequireDictionary(assetImportSchema["properties"]);
            Assert.That(assetImportProperties.Keys,
                Is.EquivalentTo(new[]
                {
                    "dryRun", "defaults", "execution", "imports",
                    "expectedProjectPath", "expectedProjectName"
                }));
            Assert.That(assetImportProperties.ContainsKey("sourcePath"), Is.False);
            Assert.That(assetImportProperties.ContainsKey("destinationPath"), Is.False);
            var defaultsSchema = RequireDictionary(assetImportProperties["defaults"]);
            var defaultProperties = RequireDictionary(defaultsSchema["properties"]);
            Assert.That(defaultProperties.Keys, Does.Contain("dedupeMode"));
            Assert.That(defaultProperties.Keys, Does.Contain("dedupeScope"));
            Assert.That(defaultProperties.Keys, Does.Contain("dedupeSearchPath"));
            Assert.That(defaultProperties.Keys, Does.Contain("onDuplicate"));
            Assert.That(defaultProperties.Keys, Does.Contain("spriteSlice"));
            var spriteSliceSchema = RequireDictionary(defaultProperties["spriteSlice"]);
            var spriteSliceProperties = RequireDictionary(spriteSliceSchema["properties"]);
            Assert.That(spriteSliceProperties.Keys, Does.Contain("frameWidth"));
            Assert.That(spriteSliceProperties.Keys, Does.Contain("frameHeight"));
        }

        [Test]
        public void ToolMetadata_ExposesUnityPackageImportInCanonicalCatalog()
        {
            var result = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: false, includeSchema: true, limit: 200,
                category: "asset"));
            var tools = (List<Dictionary<string, object>>)result["tools"];
            var tool = tools.Single(item => item["route"].ToString() == "asset/import-unitypackage");

            Assert.That(tool["toolName"], Is.EqualTo("unity_asset_import_unitypackage"));
            Assert.That(tool["moduleId"], Is.EqualTo("unity.asset"));
            Assert.That(HasSideEffect(tool, "writesAssets"), Is.True);
            Assert.That(HasTag(tool, "longRunning"), Is.True);
            Assert.That(HasSideEffect(tool, "reloadsDomain"), Is.True);

            var schema = RequireDictionary(tool["inputSchema"]);
            CollectionAssert.AreEquivalent(new[] { "packagePath" }, (List<string>)schema["required"]);
            var properties = RequireDictionary(schema["properties"]);
            Assert.That(properties.Keys, Does.Contain("packagePath"));
            Assert.That(properties.Keys, Does.Contain("expectedProjectPath"));
            Assert.That(properties.Keys, Does.Not.Contain("interactive"));
        }

        [Test]
        public void ToolMetadata_ExposesAnimatorEditingToolsInCanonicalCatalog()
        {
            var result = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: true, includeSchema: true, limit: 200,
                category: "animation"));
            var tools = (List<Dictionary<string, object>>)result["tools"];
            var toolsByRoute = tools.ToDictionary(tool => tool["route"].ToString());

            foreach (var expected in new[]
                     {
                         (Route: "animation/transition-info", ToolName: "unity_animation_transition_info"),
                         (Route: "animation/update-state", ToolName: "unity_animation_update_state"),
                         (Route: "animation/update-transition", ToolName: "unity_animation_update_transition"),
                         (Route: "animation/connect-states", ToolName: "unity_animation_connect_states"),
                     })
            {
                Assert.That(toolsByRoute.ContainsKey(expected.Route), Is.True,
                    $"{expected.Route} must publish discoverable metadata.");
                var tool = toolsByRoute[expected.Route];
                Assert.That(tool["toolName"], Is.EqualTo(expected.ToolName));
                Assert.That(tool["moduleId"], Is.EqualTo("unity.animation"));

                var schema = RequireDictionary(tool["inputSchema"]);
                var properties = RequireDictionary(schema["properties"]);
                Assert.That(properties, Is.Not.Empty, $"{expected.Route} must publish a concrete input schema.");
            }

            var transitionInfoAnnotations = RequireDictionary(
                toolsByRoute["animation/transition-info"]["annotations"]);
            Assert.That(transitionInfoAnnotations["readOnlyHint"], Is.EqualTo(true));

            foreach (string route in new[]
                     {
                         "animation/update-state",
                         "animation/update-transition",
                         "animation/connect-states",
                     })
            {
                Assert.That(
                    !toolsByRoute[route].TryGetValue("annotations", out object annotationsValue) ||
                    !RequireDictionary(annotationsValue).ContainsKey("readOnlyHint"),
                    Is.True, route);
            }

            var updateTransitionSchema = RequireDictionary(
                toolsByRoute["animation/update-transition"]["inputSchema"]);
            var updateTransitionProperties = RequireDictionary(updateTransitionSchema["properties"]);
            foreach (string propertyName in new[] { "conditions", "addConditions" })
            {
                var arraySchema = RequireDictionary(updateTransitionProperties[propertyName]);
                Assert.That(arraySchema["type"], Is.EqualTo("array"));
                var itemSchema = RequireDictionary(arraySchema["items"]);
                Assert.That(itemSchema["type"], Is.EqualTo("object"));
                var itemProperties = RequireDictionary(itemSchema["properties"]);
                CollectionAssert.IsSubsetOf(new[] { "parameter", "mode", "threshold" }, itemProperties.Keys);
                CollectionAssert.Contains((List<string>)itemSchema["required"], "parameter");
            }

            var updateConditionsSchema = RequireDictionary(updateTransitionProperties["updateConditions"]);
            var updateConditionItemSchema = RequireDictionary(updateConditionsSchema["items"]);
            var updateConditionItemProperties = RequireDictionary(updateConditionItemSchema["properties"]);
            CollectionAssert.IsSubsetOf(new[] { "index", "parameter", "mode", "threshold" },
                updateConditionItemProperties.Keys);
            CollectionAssert.Contains((List<string>)updateConditionItemSchema["required"], "index");

            var removeIndexesSchema = RequireDictionary(updateTransitionProperties["removeConditionIndexes"]);
            Assert.That(RequireDictionary(removeIndexesSchema["items"])["type"], Is.EqualTo("number"));
        }

        [Test]
        public void ToolMetadata_ValueSchemasAcceptPrimitiveNumbers()
        {
            foreach (string route in new[]
                     {
                         "prefab-asset/set-property",
                         "serialized-object/set",
                         "component/set-property",
                         "localization/upsert-variable",
                     })
            {
                var schema = MCPToolInputSchemaCatalog.Get(route);
                var properties = RequireDictionary(schema["properties"]);
                var valueSchema = RequireDictionary(properties["value"]);
                Assert.That(valueSchema.ContainsKey("type"), Is.False,
                    $"{route} must allow primitive JSON values such as 0.72.");
            }

            var tools = GetAllCatalogTools(compact: false, includeSchema: true);
            Assert.That(tools.Any(tool => tool["route"].ToString() == "component/set-property"), Is.True);
        }

        [Test]
        public void ToolMetadata_LocalizationRoutesRequireOptionalPackage()
        {
            Type registry = typeof(MCPToolMetadata).Assembly.GetType("UnityMCP.Editor.MCPCapabilityRegistry");
            Assert.That(registry, Is.Not.Null);
            var method = registry.GetMethod("IsRouteAvailable", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            Type localizationBridge = typeof(MCPToolMetadata).Assembly.GetType(
                "UnityMCP.Editor.MCPLocalizationBridge");
            Assert.That(localizationBridge, Is.Not.Null);
            var isAvailable = localizationBridge.GetProperty("IsAvailable",
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(isAvailable, Is.Not.Null);
            Assert.That(method.Invoke(null, new object[] { "localization/status" }),
                Is.EqualTo(isAvailable.GetValue(null)));
            Assert.That(method.Invoke(null, new object[] { "scene/hierarchy" }), Is.EqualTo(true));
        }

        private static Dictionary<string, object> RequireDictionary(object value)
        {
            Assert.That(value, Is.TypeOf<Dictionary<string, object>>());
            return (Dictionary<string, object>)value;
        }

        private static List<Dictionary<string, object>> GetAllCatalogTools(
            bool compact, bool includeSchema,
            string category = null,
            List<Dictionary<string, object>> metadataIssues = null)
        {
            var tools = new List<Dictionary<string, object>>();
            int offset = 0;
            while (true)
            {
                var page = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                    compact: compact,
                    includeSchema: includeSchema,
                    offset: offset,
                    limit: 200,
                    category: category,
                    includeMetadataIssues: metadataIssues != null));
                var pageTools = (List<Dictionary<string, object>>)page["tools"];
                tools.AddRange(pageTools);
                if (metadataIssues != null)
                {
                    metadataIssues.AddRange(
                        (List<Dictionary<string, object>>)page["metadataIssues"]);
                }

                if (!page.TryGetValue("nextOffset", out object nextOffset))
                    return tools;
                offset = Convert.ToInt32(nextOffset);
            }
        }

        private static bool HasTag(Dictionary<string, object> metadata, string tag)
        {
            return metadata.TryGetValue("tags", out object value) &&
                   value is IEnumerable values &&
                   values.Cast<object>().Any(item =>
                       string.Equals(item?.ToString(), tag, StringComparison.Ordinal));
        }

        private static bool HasSideEffect(Dictionary<string, object> metadata, string sideEffect)
        {
            return metadata.TryGetValue("sideEffects", out object value) &&
                   value is IEnumerable values &&
                   values.Cast<object>().Any(item =>
                       string.Equals(item?.ToString(), sideEffect, StringComparison.Ordinal));
        }

        private static List<string> GetBuiltInRoutes()
        {
            Type registry = typeof(MCPToolMetadata).Assembly.GetType("UnityMCP.Editor.MCPRouteRegistry");
            Assert.That(registry, Is.Not.Null);
            var property = registry.GetProperty("BuiltInRoutes",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return ((IEnumerable<string>)property.GetValue(null)).ToList();
        }
    }
}
