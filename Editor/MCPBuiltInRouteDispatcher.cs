using System.Collections.Generic;

namespace UnityMCP.Editor
{
    internal static class MCPBuiltInRouteDispatcher
    {
        internal static object Dispatch(string path, Dictionary<string, object> arguments)
        {
            switch (path)
            {
                // ─── Ping ───
                case "ping":
                    return MCPBridgeServer.BuildPingResponse();

                // ─── Instance Routing ───
                case "instance/current":
                    return MCPInstanceCommands.Current(arguments);
                case "instance/list":
                    return MCPInstanceCommands.List(arguments);
                case "instance/resolve":
                    return MCPInstanceCommands.Resolve(arguments);
                case "instance/assert-project":
                    return MCPInstanceCommands.AssertProject(arguments);
                case "mcp/health":
                    return MCPHealthCommands.GetHealth(arguments);
                case "mcp/set-autostart":
                    return MCPHealthCommands.SetServerAutoStart(arguments);
                case "jobs/list":
                    return MCPJobHistory.List(arguments);
                case "jobs/get":
                    return MCPJobCommands.Get(arguments);
                case "jobs/cancel":
                    return MCPJobCommands.Cancel(arguments);
                case "jobs/cleanup":
                    return MCPJobCommands.Cleanup(arguments);

                // ─── Editor State ───
                case "editor/state":
                    return MCPEditorCommands.GetEditorState();
                case "wait/editor-idle":
                    return new { error = "wait/editor-idle must be executed through the deferred route." };
                case "uitoolkit/refresh":
                    return new { error = "uitoolkit/refresh must be executed through the deferred route." };
                case "editor/play-mode":
                    return MCPResponse.Error(
                        "editor/play-mode must be executed through the deferred route.",
                        "deferred_route_required");
                case "editor/execute-menu-item":
                    return MCPEditorCommands.ExecuteMenuItem(arguments);
                case "editor/execute-code":
                    return MCPEditorCommands.ExecuteCode(arguments);

                // ─── Scene ───
                case "scene/info":
                    return MCPSceneCommands.GetSceneInfo();
                case "scene/open":
                    return MCPSceneCommands.OpenScene(arguments);
                case "scene/save":
                    return MCPSceneCommands.SaveScene(arguments);
                case "scene/new":
                    return MCPSceneCommands.NewScene();
                case "scene/hierarchy":
                    return MCPSceneCommands.GetHierarchy(arguments);
                case "scene/instantiate-prefab":
                    return MCPAssetCommands.InstantiatePrefab(arguments);
                case "scene/workspace":
                    return MCPSceneWorkspaceCommands.Execute(arguments);

                // ─── GameObject ───
                case "gameobject/create":
                    return MCPGameObjectCommands.Create(arguments);
                case "gameobject/delete":
                    return MCPGameObjectCommands.Delete(arguments);
                case "gameobject/info":
                    return MCPGameObjectCommands.GetInfo(arguments);
                case "gameobject/set-transform":
                    return MCPGameObjectCommands.SetTransform(arguments);
                case "gameobject/duplicate":
                    return MCPGameObjectCommands.Duplicate(arguments);
                case "gameobject/set-active":
                    return MCPGameObjectCommands.SetActive(arguments);
                case "gameobject/reparent":
                    return MCPGameObjectCommands.Reparent(arguments);

                // ─── Component ───
                case "component/add":
                    return MCPComponentCommands.Add(arguments);
                case "component/remove":
                    return MCPComponentCommands.Remove(arguments);
                case "component/get-properties":
                    return MCPComponentCommands.GetProperties(arguments);
                case "component/set-property":
                    return MCPComponentCommands.SetProperty(arguments);
                case "component/set-reference":
                    return MCPComponentCommands.SetReferences(arguments);
                case "component/get-referenceable":
                    return MCPComponentCommands.GetReferenceableObjects(arguments);

                // ─── SerializedObject ───
                case "serialized-object/get":
                    return MCPSerializedObjectCommands.Get(arguments);
                case "serialized-object/set":
                    return MCPSerializedObjectCommands.Set(arguments);

                // ─── Assets ───
                case "asset/list":
                    return MCPAssetCommands.List(arguments);
                case "asset/import":
                    return MCPAssetCommands.Import(arguments);
                case "asset/import-settings/get":
                    return MCPAssetImportSettingsCommands.Get(arguments);
                case "asset/import-settings/set":
                    return MCPAssetImportSettingsCommands.Set(arguments);
                case "asset/refresh":
                    return MCPAssetCommands.Refresh(arguments);
                case "asset/get-refresh-job":
                    return MCPAssetCommands.GetRefreshJob(arguments);
                case "asset/import-unitypackage":
                    return MCPAssetCommands.ImportUnityPackage(arguments);
                case "asset/export-unitypackage":
                    return MCPAssetCommands.ExportUnityPackage(arguments);
                case "asset/delete":
                    return MCPAssetCommands.Delete(arguments);
                case "asset/rename":
                    return MCPAssetCommands.Rename(arguments);
                case "asset/move":
                    return MCPAssetCommands.Move(arguments);
                case "asset/create-prefab":
                    return MCPAssetCommands.CreatePrefab(arguments);
                case "asset/create-material":
                    return MCPAssetCommands.CreateMaterial(arguments);
                case "asset/create-folder":
                    return MCPAssetWorkspaceCommands.EnsureFolder(arguments);
                case "asset/copy":
                    return MCPAssetWorkspaceCommands.Copy(arguments);
                case "asset/dependencies":
                    return MCPAssetWorkspaceCommands.Dependencies(arguments);
                case "asset/transaction":
                    return MCPAssetWorkspaceCommands.Transaction(arguments);

                // ─── Scripts ───
                case "script/create":
                    return MCPScriptCommands.Create(arguments);
                case "script/read":
                    return MCPScriptCommands.Read(arguments);
                case "script/update":
                    return MCPScriptCommands.Update(arguments);

                // ─── Renderer ───
                case "renderer/set-material":
                    return MCPRendererCommands.SetMaterial(arguments);
                case "material/properties/get":
                    return MCPMaterialCommands.GetProperties(arguments);
                case "material/properties/set":
                    return MCPMaterialCommands.SetProperties(arguments);

                // ─── Build ───
                case "build/start":
                    return MCPBuildCommands.StartBuild(arguments);
                case "build/get-job":
                    return MCPBuildCommands.GetBuildJob(arguments);
                case "build/profile":
                    return MCPBuildProfileCommands.Execute(arguments);

                // ─── Optional Package Workflows ───
                case "addressables/info":
                    return MCPAddressablesCommands.Info(arguments);
                case "addressables/transaction":
                    return MCPAddressablesCommands.Transaction(arguments);
                case "addressables/build":
                    return MCPAddressablesCommands.StartBuild(arguments);
                case "timeline/info":
                    return MCPTimelineCommands.Info(arguments);
                case "timeline/transaction":
                    return MCPTimelineCommands.Transaction(arguments);
                case "cinemachine/info":
                    return MCPCinemachineCommands.Info(arguments);
                case "cinemachine/transaction":
                    return MCPCinemachineCommands.Transaction(arguments);

                // ─── Console ───
                case "console/query":
                    return MCPConsoleCommands.Query(arguments);
                case "console/clear":
                    return MCPConsoleCommands.Clear();

                // ─── Script Debug Helpers ───
                case "debug/attach-unity":
                    return MCPDebugCommands.AttachUnity(arguments);
                case "debug/set-breakpoint":
                    return MCPDebugCommands.SetBreakpoint(arguments);
                case "debug/stack-trace":
                    return MCPDebugCommands.StackTrace(arguments);
                case "debug/variables":
                    return MCPDebugCommands.Variables(arguments);
                case "debug/evaluate":
                    return MCPDebugCommands.Evaluate(arguments);

                // ─── Compilation ───
                case "compilation/errors":
                    return MCPConsoleCommands.GetCompilationErrors(arguments);

                // ─── Project ───
                case "project/info":
                    return MCPProjectCommands.GetInfo();
                // ─── Animation ───
                case "animation/create-controller":
                    return MCPAnimationCommands.CreateController(arguments);
                case "animation/controller-info":
                    return MCPAnimationCommands.GetControllerInfo(arguments);
                case "animation/add-parameter":
                    return MCPAnimationCommands.AddParameter(arguments);
                case "animation/remove-parameter":
                    return MCPAnimationCommands.RemoveParameter(arguments);
                case "animation/add-state":
                    return MCPAnimationCommands.AddState(arguments);
                case "animation/remove-state":
                    return MCPAnimationCommands.RemoveState(arguments);
                case "animation/add-transition":
                    return MCPAnimationCommands.AddTransition(arguments);
                case "animation/transition-info":
                    return MCPAnimationCommands.GetTransitionInfo(arguments);
                case "animation/update-state":
                    return MCPAnimationCommands.UpdateState(arguments);
                case "animation/update-transition":
                    return MCPAnimationCommands.UpdateTransition(arguments);
                case "animation/connect-states":
                    return MCPAnimationCommands.ConnectStates(arguments);
                case "animation/validate-controller":
                    return MCPAnimationCommands.ValidateController(arguments);
                case "animation/create-clip":
                    return MCPAnimationCommands.CreateClip(arguments);
                case "animation/clip-info":
                    return MCPAnimationCommands.GetClipInfo(arguments);
                case "animation/set-clip-curve":
                    return MCPAnimationCommands.SetClipCurve(arguments);
                case "animation/set-object-reference-curve":
                    return MCPAnimationCommands.SetObjectReferenceCurve(arguments);
                case "animation/add-layer":
                    return MCPAnimationCommands.AddLayer(arguments);
                case "animation/assign-controller":
                    return MCPAnimationCommands.AssignController(arguments);
                case "animation/get-curve-keyframes":
                    return MCPAnimationCommands.GetCurveKeyframes(arguments);
                case "animation/remove-curve":
                    return MCPAnimationCommands.RemoveCurve(arguments);
                case "animation/add-keyframe":
                    return MCPAnimationCommands.AddKeyframe(arguments);
                case "animation/remove-keyframe":
                    return MCPAnimationCommands.RemoveKeyframe(arguments);
                case "animation/add-event":
                    return MCPAnimationCommands.AddAnimationEvent(arguments);
                case "animation/remove-event":
                    return MCPAnimationCommands.RemoveAnimationEvent(arguments);
                case "animation/get-events":
                    return MCPAnimationCommands.GetAnimationEvents(arguments);
                case "animation/set-clip-settings":
                    return MCPAnimationCommands.SetClipSettings(arguments);
                case "animation/remove-transition":
                    return MCPAnimationCommands.RemoveTransition(arguments);
                case "animation/remove-layer":
                    return MCPAnimationCommands.RemoveLayer(arguments);
                case "animation/create-blend-tree":
                    return MCPAnimationCommands.CreateBlendTree(arguments);
                case "animation/get-blend-tree":
                    return MCPAnimationCommands.GetBlendTreeInfo(arguments);

                // ─── Prefab (Advanced) ───
                case "prefab/info":
                    return MCPPrefabCommands.GetPrefabInfo(arguments);
                case "prefab/create-variant":
                    return MCPPrefabCommands.CreateVariant(arguments);
                case "prefab/apply-overrides":
                    return MCPPrefabCommands.ApplyOverrides(arguments);
                case "prefab/revert-overrides":
                    return MCPPrefabCommands.RevertOverrides(arguments);
                case "prefab/unpack":
                    return MCPPrefabCommands.Unpack(arguments);
                // ─── Prefab Asset (Direct Editing) ───
                case "prefab-asset/hierarchy":
                    return MCPPrefabAssetCommands.GetHierarchy(arguments);
                case "prefab-asset/get-properties":
                    return MCPPrefabAssetCommands.GetComponentProperties(arguments);
                case "prefab-asset/set-property":
                    return MCPPrefabAssetCommands.SetComponentProperty(arguments);
                case "prefab-asset/add-component":
                    return MCPPrefabAssetCommands.AddComponent(arguments);
                case "prefab-asset/configure-component":
                    return MCPPrefabAssetCommands.ConfigureComponent(arguments);
                case "prefab-asset/remove-component":
                    return MCPPrefabAssetCommands.RemoveComponent(arguments);
                case "prefab-asset/move-component":
                    return MCPPrefabAssetCommands.MoveComponent(arguments);
                case "prefab-asset/set-reference":
                    return MCPPrefabAssetCommands.SetReference(arguments);
                case "prefab-asset/add-gameobject":
                    return MCPPrefabAssetCommands.AddGameObject(arguments);
                case "prefab-asset/instantiate-child-prefab":
                    return MCPPrefabAssetCommands.InstantiatePrefab(arguments);
                case "prefab-asset/remove-gameobject":
                    return MCPPrefabAssetCommands.RemoveGameObject(arguments);
                case "prefab-asset/move-gameobject":
                    return MCPPrefabAssetCommands.MoveGameObject(arguments);
                case "prefab-asset/find":
                    return MCPPrefabAssetCommands.Find(arguments);
                case "prefab-asset/transaction-edit":
                    return MCPPrefabAssetCommands.TransactionEdit(arguments);
                case "prefab-asset/cleanup-missing-overrides":
                    return MCPPrefabAssetCommands.CleanupMissingVariantOverrides(arguments);

                // ─── Prefab Variant Management ───
                case "prefab-asset/variant-info":
                    return MCPPrefabAssetCommands.GetVariantInfo(arguments);
                case "prefab-asset/compare-variant":
                    return MCPPrefabAssetCommands.CompareVariantToBase(arguments);
                case "prefab-asset/apply-variant-override":
                    return MCPPrefabAssetCommands.ApplyVariantOverride(arguments);
                case "prefab-asset/revert-variant-override":
                    return MCPPrefabAssetCommands.RevertVariantOverride(arguments);
                case "prefab-asset/transfer-variant-overrides":
                    return MCPPrefabAssetCommands.TransferVariantOverrides(arguments);

                // ─── Physics ───
                case "physics/raycast":
                    return MCPPhysicsCommands.Raycast(arguments);
                case "physics/overlap-sphere":
                    return MCPPhysicsCommands.OverlapSphere(arguments);
                case "physics/overlap-box":
                    return MCPPhysicsCommands.OverlapBox(arguments);
                case "physics/collision-matrix":
                    return MCPPhysicsCommands.GetCollisionMatrix(arguments);
                case "physics/set-collision-layer":
                    return MCPPhysicsCommands.SetCollisionLayer(arguments);
                case "physics/set-gravity":
                    return MCPPhysicsCommands.SetGravity(arguments);

                // ─── Lighting ───
                case "lighting/info":
                    return MCPLightingCommands.GetLightingInfo(arguments);
                case "lighting/create":
                    return MCPLightingCommands.CreateLight(arguments);
                case "lighting/set-environment":
                    return MCPLightingCommands.SetEnvironment(arguments);
                case "lighting/create-reflection-probe":
                    return MCPLightingCommands.CreateReflectionProbe(arguments);
                case "lighting/create-light-probe-group":
                    return MCPLightingCommands.CreateLightProbeGroup(arguments);

                // ─── Audio ───
                case "audio/info":
                    return MCPAudioCommands.GetAudioInfo(arguments);
                case "audio/create-source":
                    return MCPAudioCommands.CreateAudioSource(arguments);
                case "audio/set-global":
                    return MCPAudioCommands.SetGlobalAudio(arguments);
                case "audio-mixer/info":
                    return MCPAudioMixerCommands.Info(arguments);
                case "audio-mixer/transaction":
                    return MCPAudioMixerCommands.Transaction(arguments);

                // ─── Tags & Layers ───
                case "taglayer/info":
                    return MCPTagLayerCommands.GetTagsAndLayers(arguments);
                case "taglayer/add-tag":
                    return MCPTagLayerCommands.AddTag(arguments);
                case "taglayer/set-tag":
                    return MCPTagLayerCommands.SetTag(arguments);
                case "taglayer/set-layer":
                    return MCPTagLayerCommands.SetLayer(arguments);
                case "taglayer/set-static":
                    return MCPTagLayerCommands.SetStatic(arguments);

                // ─── Selection & Scene View ───
                case "selection/get":
                    return MCPSelectionCommands.GetSelection(arguments);
                case "selection/set":
                    return MCPSelectionCommands.SetSelection(arguments);
                case "selection/focus-scene-view":
                    return MCPSelectionCommands.FocusSceneView(arguments);

                // ─── Input Actions ───
                case "input/create":
                    return MCPInputCommands.CreateInputActions(arguments);
                case "input/info":
                    return MCPInputCommands.GetInputActionsInfo(arguments);
                case "input/add-map":
                    return MCPInputCommands.AddActionMap(arguments);
                case "input/remove-map":
                    return MCPInputCommands.RemoveActionMap(arguments);
                case "input/add-action":
                    return MCPInputCommands.AddAction(arguments);
                case "input/remove-action":
                    return MCPInputCommands.RemoveAction(arguments);
                case "input/add-binding":
                    return MCPInputCommands.AddBinding(arguments);
                case "input/add-composite-binding":
                    return MCPInputCommands.AddCompositeBinding(arguments);

                // ─── Assembly Definitions ───
                case "asmdef/create":
                    return MCPAssemblyDefCommands.CreateAssemblyDef(arguments);
                case "asmdef/info":
                    return MCPAssemblyDefCommands.GetAssemblyDefInfo(arguments);
                case "asmdef/list":
                    return MCPAssemblyDefCommands.ListAssemblyDefs(arguments);
                case "asmdef/add-references":
                    return MCPAssemblyDefCommands.AddReferences(arguments);
                case "asmdef/remove-references":
                    return MCPAssemblyDefCommands.RemoveReferences(arguments);
                case "asmdef/set-platforms":
                    return MCPAssemblyDefCommands.SetPlatforms(arguments);
                case "asmdef/update-settings":
                    return MCPAssemblyDefCommands.UpdateSettings(arguments);
                case "asmdef/create-ref":
                    return MCPAssemblyDefCommands.CreateAssemblyRef(arguments);

                // ─── Profiler ───
                case "profiler/enable":
                    return MCPProfilerCommands.EnableProfiler(arguments);
                case "profiler/stats":
                    return MCPProfilerCommands.GetRenderingStats(arguments);
                case "profiler/memory":
                    return MCPProfilerCommands.GetMemoryInfo(arguments);
                case "profiler/frame-data":
                    return MCPProfilerCommands.GetFrameData(arguments);
                case "profiler/analyze":
                    return MCPProfilerCommands.AnalyzePerformance(arguments);

                // ─── Frame Debugger ───
                case "debugger/enable":
                    return MCPProfilerCommands.EnableFrameDebugger(arguments);
                case "debugger/events":
                    return MCPProfilerCommands.GetFrameEvents(arguments);
                case "debugger/event-details":
                    return MCPProfilerCommands.GetFrameEventDetails(arguments);

                // ─── Memory Profiler ───
                case "profiler/memory-status":
                    return MCPMemoryProfilerCommands.GetStatus(arguments);
                case "profiler/memory-breakdown":
                    return MCPMemoryProfilerCommands.GetMemoryBreakdown(arguments);
                case "profiler/memory-top-assets":
                    return MCPMemoryProfilerCommands.GetTopMemoryConsumers(arguments);
                case "profiler/memory-snapshot-status":
                    return MCPMemoryProfilerCommands.GetMemorySnapshotStatus(arguments);
                case "profiler/memory-snapshot":
                    return MCPResponse.Error(
                        "profiler/memory-snapshot must be executed through the deferred route.",
                        "deferred_route_required");

                // ─── Shader Graph ───
                case "shadergraph/status":
                    return MCPShaderGraphCommands.GetStatus(arguments);
                case "shadergraph/list-shaders":
                    return MCPShaderGraphCommands.ListShaders(arguments);
                case "shadergraph/list":
                    return MCPShaderGraphCommands.ListShaderGraphs(arguments);
                case "shadergraph/info":
                    return MCPShaderGraphCommands.GetShaderGraphInfo(arguments);
                case "shadergraph/get-properties":
                    return MCPShaderGraphCommands.GetShaderProperties(arguments);
                case "shadergraph/create":
                    return MCPShaderGraphCommands.CreateShaderGraph(arguments);
                case "shadergraph/open":
                    return MCPShaderGraphCommands.OpenShaderGraph(arguments);
                case "shadergraph/list-subgraphs":
                    return MCPShaderGraphCommands.ListSubGraphs(arguments);
                case "shadergraph/list-vfx":
                    return MCPShaderGraphCommands.ListVFXGraphs(arguments);
                case "shadergraph/open-vfx":
                    return MCPShaderGraphCommands.OpenVFXGraph(arguments);
                case "shadergraph/get-nodes":
                    return MCPShaderGraphCommands.GetGraphNodes(arguments);
                case "shadergraph/get-edges":
                    return MCPShaderGraphCommands.GetGraphEdges(arguments);
                case "shadergraph/add-node":
                    return MCPShaderGraphCommands.AddGraphNode(arguments);
                case "shadergraph/remove-node":
                    return MCPShaderGraphCommands.RemoveGraphNode(arguments);
                case "shadergraph/connect":
                    return MCPShaderGraphCommands.ConnectGraphNodes(arguments);
                case "shadergraph/disconnect":
                    return MCPShaderGraphCommands.DisconnectGraphNodes(arguments);
                case "shadergraph/set-node-property":
                    return MCPShaderGraphCommands.SetGraphNodeProperty(arguments);
                case "shadergraph/get-node-types":
                    return MCPShaderGraphCommands.GetNodeTypes(arguments);
                case "vfxgraph/info":
                    return MCPVFXGraphCommands.Info(arguments);
                case "vfxgraph/transaction":
                    return MCPVFXGraphCommands.Transaction(arguments);

                // ─── Agent Management ───
                case "agents/list":
                    return MCPRequestQueue.GetActiveSessions();
                case "agents/log":
                {
                    var agentArgs = arguments;
                    string id = agentArgs.ContainsKey("agentId") ? agentArgs["agentId"].ToString() : "";
                    return new Dictionary<string, object>
                    {
                        { "agentId", id },
                        { "log", MCPRequestQueue.GetAgentLog(id) },
                    };
                }

                // ─── Search ───
                case "search/scene":
                    return MCPSearchCommands.SearchScene(arguments);
                case "search/missing-references":
                    return MCPSearchCommands.FindMissingReferences(arguments);
                case "search/scene-stats":
                    return MCPSearchCommands.GetSceneStats(arguments);

                // ─── Project Settings ───
                case "settings/quality":
                    return MCPProjectSettingsCommands.GetQualitySettings(arguments);
                case "settings/quality-level":
                    return MCPProjectSettingsCommands.SetQualityLevel(arguments);
                case "settings/physics":
                    return MCPProjectSettingsCommands.GetPhysicsSettings(arguments);
                case "settings/set-physics":
                    return MCPProjectSettingsCommands.SetPhysicsSettings(arguments);
                case "settings/time":
                    return MCPProjectSettingsCommands.GetTimeSettings(arguments);
                case "settings/set-time":
                    return MCPProjectSettingsCommands.SetTimeSettings(arguments);
                case "settings/player":
                    return MCPProjectSettingsCommands.GetPlayerSettings(arguments);
                case "settings/set-player":
                    return MCPProjectSettingsCommands.SetPlayerSettings(arguments);
                case "settings/render-pipeline":
                    return MCPProjectSettingsCommands.GetRenderPipelineInfo(arguments);

                // ─── Undo ───
                case "undo/perform":
                    return MCPUndoCommands.PerformUndo(arguments);
                case "undo/redo":
                    return MCPUndoCommands.PerformRedo(arguments);
                case "undo/history":
                    return MCPUndoCommands.GetUndoHistory(arguments);
                case "undo/clear":
                    return MCPUndoCommands.ClearUndo(arguments);

                // ─── Screenshot / Scene View ───
                case "screenshot/game":
                    return MCPGameViewCaptureCommands.CaptureGameView(arguments);
                case "screenshot/scene":
                    return MCPScreenshotCommands.CaptureSceneView(arguments);
                case "screenshot/editor-window":
                    return MCPScreenshotCommands.CaptureEditorWindow(arguments);
                case "screenshot/crop":
                    return MCPScreenshotCommands.CropImage(arguments);
                case "sceneview/info":
                    return MCPScreenshotCommands.GetSceneViewInfo(arguments);
                case "sceneview/set-camera":
                    return MCPScreenshotCommands.SetSceneViewCamera(arguments);
                case "gameview/info":
                    return MCPScreenshotCommands.GetGameViewInfo(arguments);
                case "gameview/set-resolution":
                    return MCPScreenshotCommands.SetGameViewResolution(arguments);
                case "gameview/set-scale":
                    return MCPScreenshotCommands.SetGameViewScale(arguments);

                // ─── Graphics & Visuals ───
                case "graphics/asset-preview":
                    return MCPGraphicsCommands.CaptureAssetPreview(arguments);
                case "graphics/mesh-info":
                    return MCPGraphicsCommands.GetMeshInfo(arguments);
                case "graphics/material-info":
                    return MCPGraphicsCommands.GetMaterialInfo(arguments);
                case "graphics/image-alpha-bounds":
                    return MCPGraphicsCommands.InspectImageAlphaBounds(arguments);
                case "graphics/rect-gap":
                    return MCPGraphicsCommands.MeasureRectGap(arguments);
                case "graphics/annotate-rects":
                    return MCPGraphicsCommands.AnnotateRects(arguments);
                case "graphics/compare-images":
                    return MCPGraphicsCommands.CompareImages(arguments);
                case "graphics/renderer-info":
                    return MCPGraphicsCommands.GetRendererInfo(arguments);
                case "graphics/lighting-summary":
                    return MCPGraphicsCommands.GetLightingSummary(arguments);

                // ─── Sprite Sheet ───
                case "sprite/sheet-info":
                    return MCPSpriteSheetCommands.GetSheetInfo(arguments);
                case "sprite/pixel-check":
                    return MCPSpritePixelCommands.Check(arguments);
                case "sprite/replace-and-slice":
                    return MCPSpriteSheetCommands.ReplaceAndSlice(arguments);
                case "sprite/slice-sheet":
                    return MCPSpriteSheetCommands.SliceSheet(arguments);
                case "sprite/update-animation-clip":
                    return MCPSpriteSheetCommands.UpdateAnimationClip(arguments);
                case "sprite/replace-slice-update-clip":
                    return MCPSpriteSheetCommands.ReplaceSliceAndUpdateClip(arguments);

                // ─── Terrain ───
                case "terrain/create":
                    return MCPTerrainCommands.CreateTerrain(arguments);
                case "terrain/info":
                    return MCPTerrainCommands.GetTerrainInfo(arguments);
                case "terrain/set-height":
                    return MCPTerrainCommands.SetHeight(arguments);
                case "terrain/flatten":
                    return MCPTerrainCommands.FlattenTerrain(arguments);
                case "terrain/add-layer":
                    return MCPTerrainCommands.AddTerrainLayer(arguments);
                case "terrain/get-height":
                    return MCPTerrainCommands.GetHeightAtPosition(arguments);
                case "terrain/list":
                    return MCPTerrainCommands.ListTerrains(arguments);
                case "terrain/raise-lower":
                    return MCPTerrainCommands.RaiseLowerHeight(arguments);
                case "terrain/smooth":
                    return MCPTerrainCommands.SmoothHeight(arguments);
                case "terrain/noise":
                    return MCPTerrainCommands.SetHeightsFromNoise(arguments);
                case "terrain/set-heights-region":
                    return MCPTerrainCommands.SetHeightsRegion(arguments);
                case "terrain/get-heights-region":
                    return MCPTerrainCommands.GetHeightsRegion(arguments);
                case "terrain/remove-layer":
                    return MCPTerrainCommands.RemoveTerrainLayer(arguments);
                case "terrain/paint-layer":
                    return MCPTerrainCommands.PaintTerrainLayer(arguments);
                case "terrain/fill-layer":
                    return MCPTerrainCommands.FillTerrainLayer(arguments);
                case "terrain/add-tree-prototype":
                    return MCPTerrainCommands.AddTreePrototype(arguments);
                case "terrain/remove-tree-prototype":
                    return MCPTerrainCommands.RemoveTreePrototype(arguments);
                case "terrain/place-trees":
                    return MCPTerrainCommands.PlaceTrees(arguments);
                case "terrain/clear-trees":
                    return MCPTerrainCommands.ClearTrees(arguments);
                case "terrain/get-tree-instances":
                    return MCPTerrainCommands.GetTreeInstances(arguments);
                case "terrain/add-detail-prototype":
                    return MCPTerrainCommands.AddDetailPrototype(arguments);
                case "terrain/paint-detail":
                    return MCPTerrainCommands.PaintDetail(arguments);
                case "terrain/scatter-detail":
                    return MCPTerrainCommands.ScatterDetail(arguments);
                case "terrain/clear-detail":
                    return MCPTerrainCommands.ClearDetail(arguments);
                case "terrain/set-holes":
                    return MCPTerrainCommands.SetHoles(arguments);
                case "terrain/set-settings":
                    return MCPTerrainCommands.SetTerrainSettings(arguments);
                case "terrain/resize":
                    return MCPTerrainCommands.ResizeTerrain(arguments);
                case "terrain/create-grid":
                    return MCPTerrainCommands.CreateTerrainGrid(arguments);
                case "terrain/set-neighbors":
                    return MCPTerrainCommands.SetTerrainNeighbors(arguments);
                case "terrain/import-heightmap":
                    return MCPTerrainCommands.ImportHeightmap(arguments);
                case "terrain/export-heightmap":
                    return MCPTerrainCommands.ExportHeightmap(arguments);
                case "terrain/get-steepness":
                    return MCPTerrainCommands.GetSteepness(arguments);

                // ─── Particle System ───
                case "particle/create":
                    return MCPParticleCommands.CreateParticleSystem(arguments);
                case "particle/info":
                    return MCPParticleCommands.GetParticleSystemInfo(arguments);
                case "particle/set-main":
                    return MCPParticleCommands.SetMainModule(arguments);
                case "particle/set-emission":
                    return MCPParticleCommands.SetEmission(arguments);
                case "particle/set-shape":
                    return MCPParticleCommands.SetShape(arguments);
                case "particle/playback":
                    return MCPParticleCommands.PlaybackControl(arguments);

                // ─── ScriptableObject ───
                case "scriptableobject/create":
                    return MCPScriptableObjectCommands.CreateScriptableObject(arguments);
                case "scriptableobject/info":
                    return MCPScriptableObjectCommands.GetScriptableObjectInfo(arguments);
                case "scriptableobject/set-field":
                    return MCPScriptableObjectCommands.SetScriptableObjectField(arguments);
                case "scriptableobject/list-types":
                    return MCPScriptableObjectCommands.ListScriptableObjectTypes(arguments);

                // ─── Texture ───
                case "texture/info":
                    return MCPTextureCommands.GetTextureInfo(arguments);
                case "texture/find-duplicates":
                    return MCPImageDuplicateCommands.FindDuplicates(arguments);
                case "texture/set-import":
                    return MCPTextureCommands.SetTextureImportSettings(arguments);
                case "texture/reimport":
                    return MCPTextureCommands.ReimportTexture(arguments);
                case "texture/apply-sprite-preset":
                    return MCPTextureCommands.ApplySpriteImportPreset(arguments);
                case "texture/import-image":
                    return MCPTextureCommands.ImportImage(arguments);
                case "texture/check-import-settings":
                    return MCPTextureCommands.CheckImportSettings(arguments);
                case "texture/check-ui-import-settings":
                    return MCPTextureCommands.CheckUIImportSettings(arguments);

                // ─── Sprite Atlas ───
                case "spriteatlas/create":
                    return MCPSpriteAtlasCommands.CreateSpriteAtlas(arguments);
                case "spriteatlas/info":
                    return MCPSpriteAtlasCommands.GetSpriteAtlasInfo(arguments);
                case "spriteatlas/add":
                    return MCPSpriteAtlasCommands.AddToSpriteAtlas(arguments);
                case "spriteatlas/remove":
                    return MCPSpriteAtlasCommands.RemoveFromSpriteAtlas(arguments);
                case "spriteatlas/settings":
                    return MCPSpriteAtlasCommands.SetSpriteAtlasSettings(arguments);
                case "spriteatlas/delete":
                    return MCPSpriteAtlasCommands.DeleteSpriteAtlas(arguments);
                case "spriteatlas/list":
                    return MCPSpriteAtlasCommands.ListSpriteAtlases(arguments);

                // ─── Navigation ───
                case "navigation/bake":
                    return MCPNavigationCommands.BakeNavMesh(arguments);
                case "navigation/clear":
                    return MCPNavigationCommands.ClearNavMesh(arguments);
                case "navigation/add-agent":
                    return MCPNavigationCommands.AddNavMeshAgent(arguments);
                case "navigation/add-obstacle":
                    return MCPNavigationCommands.AddNavMeshObstacle(arguments);
                case "navigation/info":
                    return MCPNavigationCommands.GetNavMeshInfo(arguments);
                case "navigation/set-destination":
                    return MCPNavigationCommands.SetAgentDestination(arguments);

                // ─── UI ───
                case "ui/create-canvas":
                    return MCPUICommands.CreateCanvas(arguments);
                case "ui/create-element":
                    return MCPUICommands.CreateUIElement(arguments);
                case "ui/info":
                    return MCPUICommands.GetUIInfo(arguments);
                case "ui/set-text":
                    return MCPUICommands.SetUIText(arguments);
                case "ui/set-image":
                    return MCPUICommands.SetUIImage(arguments);
                case "uitoolkit/audit-uss-styles":
                    return MCPUIToolkitUssAuditCommands.AuditUssStyles(arguments);
                case "uitoolkit/audit-uxml-layout":
                    return MCPUIToolkitUxmlAuditCommands.AuditUxmlLayout(arguments);
                case "uitoolkit/windows":
                    return MCPUICommands.ListEditorUIWindows(arguments);
                case "uitoolkit/tree":
                    return MCPUICommands.GetEditorUITree(arguments);
                case "uitoolkit/query":
                    return MCPUICommands.QueryEditorUI(arguments);
                case "uitoolkit/style":
                    return MCPUICommands.GetEditorUIStyle(arguments);
                case "uitoolkit/repaint":
                    return MCPUICommands.RepaintEditorUI(arguments);
                case "uitoolkit/asset-inspect":
                    return MCPUICommands.InspectUIToolkitAsset(arguments);
                case "uitoolkit/runtime-documents":
                    return MCPUICommands.ListRuntimeUIDocuments(arguments);
                case "uitoolkit/runtime-tree":
                    return MCPUICommands.GetRuntimeUITree(arguments);
                case "uitoolkit/runtime-query":
                    return MCPUICommands.QueryRuntimeUI(arguments);
                case "uitoolkit/runtime-style":
                    return MCPUICommands.GetRuntimeUIStyle(arguments);
                case "uitoolkit/diagnose-runtime":
                    return MCPUICommands.DiagnoseRuntimeUI(arguments);
                case "uitoolkit/visual-check":
                    return MCPUICommands.VisualCheckRuntimeUI(arguments);
                case "uitoolkit/locate-element":
                    return MCPUICommands.LocateUIToolkitElement(arguments);
                case "uitoolkit/capture-element":
                    return MCPUICommands.CaptureUIToolkitElement(arguments);
                case "uitoolkit/compare-element":
                    return MCPUICommands.CompareUIToolkitElement(arguments);
                case "uitoolkit/generated-children":
                    return MCPUICommands.InspectUIToolkitGeneratedChildren(arguments);
                case "uitoolkit/resource-audit":
                    return MCPUICommands.AuditUIToolkitResources(arguments);
                case "uitoolkit/runtime-repaint":
                    return MCPUICommands.RepaintRuntimeUI(arguments);
                case "uitoolkit/assert-layout":
                    return MCPUICommands.AssertUIToolkitLayout(arguments);
                case "uitoolkit/builder-preview":
                    return MCPUICommands.OpenUIBuilderPreview(arguments);
                case "uitoolkit/edit-uxml":
                    return MCPUIAuthoringCommands.EditUxml(arguments);
                case "uitoolkit/edit-uss":
                    return MCPUIAuthoringCommands.EditUss(arguments);
                case "uitoolkit/authoring-transaction":
                    return MCPUIAuthoringCommands.AuthoringTransaction(arguments);

                // ─── Localization (optional package) ───
                case "localization/status":
                case "localization/locales":
                case "localization/create-locale":
                case "localization/set-selected-locale":
                case "localization/collections":
                case "localization/create-collection":
                case "localization/entries":
                case "localization/upsert-entry":
                case "localization/remove-entry":
                case "localization/validate":
                case "localization/settings":
                case "localization/variables":
                case "localization/upsert-variable":
                case "localization/remove-variable":
                    return MCPLocalizationBridge.Execute(path, arguments);

                // ─── Package Manager ───
                case "packages/list":
                    return MCPPackageManagerCommands.ListPackages(arguments);
                case "packages/add":
                    return MCPPackageManagerCommands.AddPackage(arguments);
                case "packages/remove":
                    return MCPPackageManagerCommands.RemovePackage(arguments);
                case "packages/search":
                    return MCPPackageManagerCommands.SearchPackage(arguments);
                case "packages/info":
                    return MCPPackageManagerCommands.GetPackageInfo(arguments);
                case "packages/status":
                    return MCPPackageManagerCommands.GetPackageStatus(arguments);
                case "packages/update-git":
                    return MCPPackageManagerCommands.UpdateGitPackage(arguments);
                case "packages/lint-metas":
                    return MCPPackageManagerCommands.LintPackageMetas(arguments);

                // ─── Constraints & LOD ───
                case "constraint/add":
                    return MCPConstraintCommands.AddConstraint(arguments);
                case "constraint/info":
                    return MCPConstraintCommands.GetConstraintInfo(arguments);
                case "lod/create":
                    return MCPConstraintCommands.CreateLODGroup(arguments);
                case "lod/info":
                    return MCPConstraintCommands.GetLODGroupInfo(arguments);

                // ─── Prefs ───
                case "editorprefs/get":
                    return MCPPrefsCommands.GetEditorPref(arguments);
                case "editorprefs/set":
                    return MCPPrefsCommands.SetEditorPref(arguments);
                case "editorprefs/delete":
                    return MCPPrefsCommands.DeleteEditorPref(arguments);
                case "playerprefs/get":
                    return MCPPrefsCommands.GetPlayerPref(arguments);
                case "playerprefs/set":
                    return MCPPrefsCommands.SetPlayerPref(arguments);
                case "playerprefs/delete":
                    return MCPPrefsCommands.DeletePlayerPref(arguments);
                case "playerprefs/delete-all":
                    return MCPPrefsCommands.DeleteAllPlayerPrefs(arguments);

                // ─── MPPM Scenario Management ───
                case "scenario/list":
                    return MCPScenarioCommands.ListScenarios(arguments);
                case "scenario/status":
                    return MCPScenarioCommands.GetScenarioStatus(arguments);
                case "scenario/activate":
                    return MCPScenarioCommands.ActivateScenario(arguments);
                case "scenario/start":
                    return MCPScenarioCommands.StartScenario(arguments);
                case "scenario/stop":
                    return MCPScenarioCommands.StopScenario(arguments);
                case "scenario/info":
                    return MCPScenarioCommands.GetMultiplayerInfo(arguments);
                case "scenario/create":
                    return MCPScenarioCommands.CreateScenario(arguments);

                // ─── MPPM Virtual Player management ───
                case "mppm/list-players":
                    return MCPScenarioCommands.MppmListPlayers(arguments);
                case "mppm/activate-player":
                    return MCPScenarioCommands.MppmActivatePlayer(arguments);
                case "mppm/deactivate-player":
                    return MCPScenarioCommands.MppmDeactivatePlayer(arguments);

                // ─── Testing ───
                case "testing/run-tests":
                    return MCPTestRunnerCommands.RunTests(arguments);
                case "testing/get-job":
                    return MCPTestRunnerCommands.GetTestJob(arguments);
                case "testing/run-package-tests":
                    return MCPPackageTestCommands.RunPackageTests(arguments);
                case "testing/get-package-job":
                    return MCPPackageTestCommands.GetPackageTestJob(arguments);
                // testing/list-tests is handled via the deferred path in HandleRequest

                default:
                    return new { error = $"Unknown API endpoint: {path}" };
            }
        }
    }
}
