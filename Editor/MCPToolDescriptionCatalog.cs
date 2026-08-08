using System;

namespace UnityMCP.Editor
{
    internal static class MCPToolDescriptionCatalog
    {
        internal static string Get(string route)
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
                    return "Start a persistent Git-package test job that temporarily enables package testables, survives domain reloads, restores manifest.json exactly, and returns a jobAccessToken for reconnect recovery. VM Unity MCP defaults to its package-smoke category; request VMUnityMCP.FullRegression explicitly for the full suite. Poll its returned jobId with jobs/get and jobType package-test.";
                case "testing/get-package-job":
                    return "Inspect or clear the current package-test workflow state. Normal polling uses jobs/get with the package test's jobId and jobType package-test; after reconnect, supply the jobAccessToken returned at start.";
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
                case "editor/execute-code":
                    return "Start an owner-scoped persistent Job that compiles and executes a C# method body in the Unity Editor, with exact-argument idempotency, bounded result serialization, optional typed Unity structs, cancellation, and explicit cleanup code.";
                case "scene/hierarchy":
                    return "Read the active scene hierarchy, optionally returning compact matches filtered by component type.";
                case "scene/instantiate-prefab":
                    return "Instantiate a prefab asset into the currently open scene.";
                case "scene/workspace":
                    return "List loaded scenes, open a scene additively or singly, close a loaded scene with an explicit dirty-scene policy, or set the active scene.";
                case "prefab/create-variant":
                    return "Create a Prefab Variant from an existing Prefab asset and return its saved asset identity.";
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
                    return "Safely rename a Unity asset using AssetDatabase while preserving its .meta GUID, synchronizing Single Sprite names, and renaming matching Multiple Sprite prefixes without changing Sprite IDs.";
                case "asset/move":
                    return "Preflight and move one or more Unity assets with configurable execution, GUID preservation, Sprite internal-name synchronization when filenames change, Multiple Sprite ID preservation, and rollback.";
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
                    return "Apply high-level TextureImporter/Sprite settings such as pixel sprite preset, PPU, pivot, border, and reference settings without changing Single/Multiple mode unless a reference owns it.";
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
                case "shadergraph/info":
                    return "Inspect a Shader Graph's compiled shader properties plus authoritative node, edge, and blackboard-property counts.";
                case "shadergraph/get-properties":
                    return "Read compiled shader properties and Shader Graph texture-property metadata such as Per Renderer Data, Main Texture, tiling/offset, and texel-size generation.";
                case "shadergraph/get-nodes":
                    return "Read only the semantic nodes referenced by Shader Graph GraphData, excluding slots, properties, targets, and other serialized helper objects.";
                case "shadergraph/get-edges":
                    return "Read Shader Graph connections with exact output/input node IDs and slot IDs from GraphData.";
                case "shadergraph/set-node-property":
                    return "Safely set a scalar field on a serialized Shader Graph object, with field/type validation, synchronous import, readback verification, and rollback.";
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
                    return MCPToolDescriptionComposer.Compose(route);
            }
        }
    }
}
