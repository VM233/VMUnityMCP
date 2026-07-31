# Changelog

All notable changes to this package will be documented in this file.

## [5.6.1] - 2026-07-31

- Restore the complete Localization tool family to the first-class public
  surface so its existing schema contract and package tests stay aligned.
- Publish a concrete description for the newly first-class Prefab Variant
  creation route.
- Make batched Localization entry upserts re-resolve collections, tables,
  Locales, and assets by stable identifiers between Editor frames, preventing
  destroyed Unity object references after AssetDatabase reloads.

## [5.6.0] - 2026-07-31

- Expose the existing folder creation, Prefab Variant creation, Prefab
  hierarchy, atomic Prefab transaction, and Localization entry upsert/removal
  routes as a compact first-class asset-authoring workflow.
- Document that checked-in assets remain the source of truth and one-off Editor
  builders must not become parallel asset producers.

## [5.5.4] - 2026-07-31

- Enforce the documented project-scoped idempotency contract for persistent
  Jobs. An exact retry from a reconnected MCP agent now recovers the original
  Job and access token instead of creating a duplicate operation.
- Keep exact-argument conflict detection across agents and add regression
  coverage for both recovery and collision paths.
- Preserve required empty collections inside a completed persistent
  project-tool Job result, rather than compacting the already validated
  business payload a second time.

## [5.5.3] - 2026-07-31

- Add semantic descriptions to every shared persistent Job output field so
  execute-code, project tools, status, cancellation, and cleanup publish a
  self-describing machine contract.
- Add regression coverage preventing an undocumented Job output field from
  entering live tool metadata.

## [5.5.2] - 2026-07-31

- Keep presence-only Editor-state response compaction while making the compact
  snapshot unambiguous: `editor/state` now authors `isIdle`, which becomes the
  mandatory `idle` tag whenever compilation, asset updating, and Play Mode
  transitions are all inactive.
- Add regression coverage proving every compact Editor-state snapshot retains
  at least one authoritative process-state tag.

## [5.5.1] - 2026-07-31

- Preserve `inputSchema` and `outputSchema` as exact machine-readable
  contracts at the transport boundary. JSON Schema properties named `tags` or
  `sideEffects`, and schema keywords such as `readOnly`, are no longer mistaken
  for schema-v5 transport metadata during recursive response compaction.
- Add regression coverage for business fields whose names overlap the
  presence-only metadata vocabulary.

## [5.5.0] - 2026-07-31

- Replace public tool-capability booleans with one presence-only `tags` array.
  A missing tag now means false; exact mutations remain in `sideEffects`.
- Remove redundant exposure, validity, schema-enforcement, cleanup, and
  operation-kind aliases from project-tool list/get and `_meta/tools`.
  Invalid tools expose the `invalid` tag plus `validationError`.
- Require every project tool, including lazy catalog-only tools, to declare
  exactly one operation kind so missing safety metadata cannot silently enter
  discovery.
- Publish only tool-specific `errorCodes`; the three standard project-tool
  failure codes remain part of the shared contract instead of repeating on
  every descriptor.
- Compact discovery pagination to `nextOffset` only when another page exists,
  and omit request echoes, derived counts, false `hasMore`, and null offsets.
- Compact persistent Job snapshots with presence-only lifecycle tags and
  optional fields. Null results, empty timestamps, false state flags, derived
  routes, and derived cancel modes are no longer repeated.
- Apply the same transport contract to specialized build, test, package-test,
  import, refresh, and profiler workflows: positive lifecycle metadata is
  merged into sorted `tags`, derived polling/state aliases are omitted, and
  idle compilation diagnostics are suppressed.
- Represent Editor process state as presence-only tags and remove the derived
  play-mode transition alias plus successful wait-configuration echoes.
- Advance live tool metadata to schema version 5.

## [5.4.0] - 2026-07-31

- Add machine-readable `outputSchema`, stable error-code, cleanup-tool, and
  fine-grained side-effect contracts to project-tool discovery and direct
  metadata. Side effects now distinguish asset and Scene writes, runtime
  mutation, Logic Tick or Editor-frame advancement, temporary objects,
  captured artifacts, external I/O, domain reloads, and arbitrary code.
- Validate declared project-tool outputs before success and expose long-running
  or explicitly requested project tools through the same durable Job owner.
- Move `editor/execute-code` to persistent Jobs with owner/token-scoped
  `jobs/get`, cooperative `jobs/cancel`, persisted cleanup through
  `jobs/cleanup`, exact-argument `idempotencyKey` reuse, and explicit
  interrupted state after a domain reload.
- Add `IMCPPersistentProjectTool` and `MCPProjectToolJobStep` so long operations
  can yield between Editor updates, persist all continuation state, report
  progress, observe cancellation between steps, and produce a cleanup token.
- Add `unityStructFormat=compact|structured` to execute-code. Structured mode
  preserves typed Unity values such as vectors, colors, bounds, rays, matrices,
  and poses without transport compaction rewriting them.
- Treat pre-start cancellation as side-effect-free and never run cleanup for
  code that did not cross the execution boundary.
- Preserve the exact validated project-tool result shape across the HTTP
  transport, including required empty collections, counts, and flags, so the
  public `structuredContent` continues to satisfy its advertised
  `outputSchema`.
- Normalize and compact each HTTP response exactly once; repeated compaction
  could otherwise erase a project tool's preserved schema shape after its
  internal envelope had already been removed.

## [5.3.5] - 2026-07-30

- Classify `editor/execute-code` compilation failures with the stable
  `execute_code_compilation_failed` error code instead of the generic `error`
  fallback.
- Return structured non-retryable pre-execution errors and
  `userCodeExecuted=false` for missing code, invalid namespace imports,
  unavailable Roslyn, and caller-code compilation failures.
- Add regression coverage for compilation diagnostics, caller line numbers,
  error classification, and the no-execution guarantee.

## [5.3.4] - 2026-07-30

- Enforce project-tool JSON Schema `not` and `const` constraints before
  invocation, including recursive equality for object and array constants.
- Validate and inspect nested `not` schemas during discovery so extension
  packages can publish selector exclusivity as an executable contract.
- Add regression coverage proving invalid `not` and `const` arguments fail at
  the shared project-tool boundary.

## [5.3.3] - 2026-07-30

- Move callback-based route names and handlers into one executable deferred
  registry, and compose the authoritative route catalog from that registry
  plus the non-deferred manifest.
- Remove every deferred route from the hand-maintained non-deferred manifest,
  including the former `testing/list-tests` special case, and enforce exact
  union/disjointness contracts in the regression suite.
- Keep route metadata discovery free of HTTP-listener initialization side
  effects by separating the deferred registry from `MCPBridgeServer`.

## [5.3.2] - 2026-07-30

- Make `prefab-asset/add-component` honor its optional initial `properties`
  map through the same `SerializedObject` assignment path used by prefab
  transactions, including nested lists and arrays.
- Verify the newly added component and configured fields by serialized
  readback after saving; invalid initial fields fail atomically without
  leaving a partially configured component.
- Reconcile a reload-interrupted add without duplicating the component, then
  reapply and verify its requested initial properties before reporting
  success.
- Publish the `properties` map in the route-owned schema so lazy MCP clients
  no longer treat supported initialization data as an undeclared argument.

## [5.3.1] - 2026-07-30

- Add a public primary-result resolver so project-tool packages can reuse the
  shared Unity MCP preference without duplicating precedence or bypassing their
  own hard caps.
- Preserve a sole empty primary collection during wire compaction so zero-match
  project-tool and list results remain visible inside completed queue tickets.
- Replace the drift-prone hand-maintained README tool table with the live
  capability/discovery contract and document package-owned project-tool
  configuration boundaries.

## [5.3.0] - 2026-07-30

- Replace pseudo-project EditorPrefs with versionable `ProjectSettings/UnityMCPSettings.json` for project context, Execute Code namespaces, Physics query dimension, and screenshot output, with non-destructive migration on first write.
- Add user preferences for the automatic port range, optional primary result-limit override, compact-by-default Prefab YAML responses, and persistent Job History size; move local MPPM, Action History, and category controls to Preferences.
- Derive tool categories from the authoritative route registry, use stable SHA-256 project/instance preference keys, and preserve legacy values where their old keys can still be resolved.
- Apply configured screenshot and Physics defaults only when request arguments are omitted, annotate tool schemas with their default source, and keep destructive choices, raw detail, queue/transport safety limits, and complex graph budgets explicit.
- Keep `mcp/health` compact unless recent actions are requested, while reporting the effective configuration and automatic port range.
- Audit all 405 built-in routes and enforce the audited route-manifest fingerprint in regression tests so future tools cannot bypass configuration review.
- Ignore Unity-excluded tilde directories such as `Documentation~` in package `.meta` linting instead of reporting false missing-meta failures.

## [5.2.1] - 2026-07-30

- Defer project-binding classification for the outer `queue/submit` transport envelope until its inner `apiPath` is parsed, allowing unbound read-only metadata discovery while continuing to reject unbound mutations.

## [5.2.0] - 2026-07-30

- Add first-class persistent-job cancellation for Player Builds, Unity tests, package-test workflows, Memory Profiler snapshots, and Addressables builds, with explicit cooperative/pre-start cancellation results.
- Add semantic Texture, Model, and Audio importer inspection/editing, multi-scene workspace management, and typed Material shader-property/keyword editing without exposing Unity's internal serialized layouts.
- Extend the existing Physics raycast and overlap routes with one `dimension=2D|3D` contract, deterministic bounded results, total counts, and truncation metadata instead of duplicating the tool surface.
- Add lazy, capability-gated VFX Graph, Audio Mixer, Build Profile, Addressables, Timeline, and Cinemachine inspection/transaction routes; Addressables content builds reuse the persistent job system.
- Declare optional capabilities independently by route, package name, detected version, or minimum Unity version so unavailable integrations remain out of the published tool surface.
- Make the route registry authoritative for every deferred route, including `testing/list-tests`, and enforce the relationship with regression tests.
- Bound large graph, hierarchy, component, property, and query replies; keep raw serialized graph data and catalog metadata diagnostics opt-in; reject ambiguous selectors and unknown transaction fields; preflight ordered transactions before mutation.
- Preserve the legacy `scene/open` response and dirty-scene error contract while assigning additive/close/active-scene behavior to `scene/workspace`.

## [5.1.0] - 2026-07-30

- Bound the default first-class catalog to a release-managed set of common routes; keep specialized and compatibility routes available through lazy discovery.
- Add one paginated `search/scene` route that combines name, component, tag, layer, and shader filters while retaining legacy search routes lazily.
- Add recursive project-tool argument validation and demote incomplete first-class project metadata to the three-stage catalog instead of publishing ambiguous schemas.
- Harden the local HTTP bridge with method/origin checks, bounded request bodies, concise structured errors, atomic queue admission, execution-aware timeouts, and late-callback rejection.
- Distinguish listener liveness from main-thread queue readiness, reject warm-up submissions with a retryable structured response, and report active test-run contention without invoking Unity APIs from listener threads.
- Remove repeated instance metadata and unsolicited stack traces from normal replies, and improve schema descriptions and array item contracts.
- Keep package-test status compact by default while preserving opt-in detailed test results through the dedicated test job query.
- Preserve the declared Unity 2021.3.18 minimum by avoiding later-only object lookup helpers.

## [5.0.0] - 2026-07-30

- Remove the complete UMA integration, including command handlers, routes, optional capability detection, category settings, self-tests, assembly references, and Unity assets.
- Replace project-tool discovery with a strict three-stage contract: compact `project-tools/list`, schema-bearing `project-tools/get`, and validated `project-tools/execute`.
- Separate project-tool summary and detail serialization so list responses and execution errors cannot leak parameter schemas.
- Remove compatibility behavior that returned complete descriptors from `project-tools/list`.

## [4.3.2] - 2026-07-30

- Add a default-on **Project Settings > Unity MCP > UI Toolkit Audit** switch for authored UXML `tooltip` attribute findings.
- Apply the project rule switch consistently to requested, menu-driven, and automatic UXML audits while exposing the effective rule state in audit results.

## [4.3.1] - 2026-07-30

- Extend the UXML layout audit with an independent `authored-tooltip-attribute` finding for every authored `tooltip` attribute.
- Support explicitly required authored tooltips only through the reasoned `uxml-layout-audit: allow-tooltip` suppression, with deterministic self-test and EditMode regression coverage.

## [4.3.0] - 2026-07-29

- Extend the UXML layout audit with an independent finding for plain direct content wrappers whose fixed cross-axis size repeats or exceeds a single-axis `ScrollView` viewport.
- Preserve intentional narrower constraints, bidirectional scrolling, unknown viewport extents, visual/clipping/interaction ownership, and reasoned local suppressions.

## [4.2.0] - 2026-07-29

- Extend the USS audit with independent findings for advanced text generation without auto sizing, ineffective text alignment on shrink-wrapped Labels centered by their sole-child parent, and inheritable text styles owned by such child Labels.
- Index authored UXML parent-child structure and inline styles so text-style findings respect the effective stylesheet cascade, explicit Label sizing, flex expansion, sibling content, and reasoned local suppressions.

## [4.1.0] - 2026-07-29

- Add project-configurable namespaces for Execute Code and expose the setting in Project Settings and Preferences without redundant scope notices.
- Remove Amplify Shader Editor support and obsolete compatibility surfaces from tool metadata, request handling, and response contracts while retaining Unity-version compatibility adapters.
- Fix collection-wide localization entry removal so shared data and every locale table are persisted as dirty assets.
- Tighten current project-context classification and related regression coverage.

## [4.0.1] - 2026-07-29

- Remove the Welcome window completely, including its automatic startup hook, Editor menu entry, dedicated assembly, styles, configuration, and image assets.

## [4.0.0] - 2026-07-29

- Establish the independently maintained **VM Unity MCP** package in the standalone `VM233/VMUnityMCP` repository.
- Change the Unity package ID from `com.anklebreaker.unity-mcp` to `com.vm233.unity-mcp` and rename the package assemblies from `AnkleBreaker.UnityMCP.*` to `VMUnityMCP.*`.
- Keep the original AnkleBreaker license and required attribution while moving product authorship, update checks, package testing defaults, documentation, and Editor branding to VM233.

## [3.3.48] - 2026-07-29

- Add an opt-in, project-configurable pixel-grid audit for structural offsets, margin/gap spacing, and padding in USS declarations and UXML inline styles.
- Allow each pixel-art project to choose its own positive grid step, while keeping the check disabled by default and supporting reasoned optical or seam-correction suppressions.

## [3.3.47] - 2026-07-29

- Extend the USS static audit to report declarations that repeat the same winning value already supplied by the common `PanelSettings` theme or another stylesheet loaded by the authored UXML.
- Resolve theme and stylesheet imports in cascade order, while leaving different-value overrides, dynamic pseudo-state rules, runtime class contracts, multi-theme projects, and unsupported ambiguous selectors unreported.

## [3.3.46] - 2026-07-29

- Keep the domain-reload ticket snapshot focused on mutations and the explicitly resumable Editor-idle wait instead of caching completed read-only responses such as repeated tool metadata.
- Skip unchanged snapshot rewrites, cap retained terminal mutations, and replace oversized recovery results with a structured non-retryable marker so background discovery cannot stall the Editor on synchronous multi-megabyte disk flushes.

## [3.3.45] - 2026-07-29

- Extend the UXML layout audit to report authored element names with no indexed lookup consumer.
- Report fixed-size flex parents whose fixed, non-shrinking children exactly partition the same axis instead of leaving one flexible remainder region.

## [3.3.44] - 2026-07-29

- Pause resumable editor-idle timeout budgets across planned Unity domain reloads and recover legacy expired snapshots with a fresh active-time budget.
- Keep synchronous transport timeouts out of persistent ticket state, and allow legacy waiters to read terminal results written by the replacement AppDomain.
- Preserve the instance registry lease during reload with an explicit `isReloading` marker, while retaining normal unregister behavior for real server stops and Editor quit.

## [3.3.43] - 2026-07-29

- Route execute-code Unity value structs through the shared compact formatter before reflection can expand derived properties.
- Recognize and consistency-check the complete Unity 6 `Rect` alias family, including position, center, min/max, size, and edge properties.

## [3.3.42] - 2026-07-29

- Compact Unity value objects at the shared response transport boundary: vectors, rectangles, bounds, colors, sizes, ranges, edges, matrices, rays, and matching dictionary shapes now use concise scalar strings.
- Group coordinate, dimension, margin, and padding members inside larger payloads; omit strictly derived byte-unit and boolean aliases; shorten the repeated MCP instance marker to `Project@port`.
- Preserve expanded rectangle and bounds data when redundant fields disagree so diagnostics do not hide inconsistent source values.

## [3.3.41] - 2026-07-29

- Extend the UXML layout audit to report transparent full-width wrappers that only center one fixed-size visual child and optionally repeat that child's height.

## [3.3.40] - 2026-07-29

- Extend the UXML layout audit to report a sole transparent Label whose inline main-axis growth leaves its centered glyph at the same position already established by the parent.

## [3.3.39] - 2026-07-29

- Extend the UXML layout audit to report centered Labels whose inline cross-axis stretch changes only a transparent layout box while leaving the centered glyph position unchanged.

## [3.3.38] - 2026-07-29

- Extend the UXML layout audit to report inline declarations that repeat the winning default from a loaded USS file, including defaults supplied through implicit UI Toolkit classes such as `.unity-base-field`.

## [3.3.37] - 2026-07-28

- Extend the UXML layout audit to report repeated inline layout declarations when authored elements already use a shared semantic layout variant for the same properties.
- Rename the automatic UXML audit setting to `automaticAudit.uxmlLayoutContracts` to reflect its broader contract coverage.

## [3.3.36] - 2026-07-28

- Validate modal-free scene save success through the same response normalization used by the bridge.

## [3.3.35] - 2026-07-28

- Make modal-scene regression fixtures compatible with Unity Test Runner's initial untitled scene.

## [3.3.34] - 2026-07-28

- Prevent MCP scene transitions from opening modal save/reload dialogs; dirty scenes now return a structured error that requires an explicit save decision, and untitled scenes accept an explicit save path.
- Reject asset-level delete, rename, move, overwrite, transaction, and targeted refresh operations that would mutate a loaded scene asset.

## [3.3.33] - 2026-07-28

- Omit empty arrays and objects from every transported tool response while preserving non-empty diagnostics, including deprecated compiler warnings.

## [3.3.32] - 2026-07-28

- Add first-class, read-only UI Toolkit static audits for single-consumer USS selectors and layout-only manually centered UXML containers.
- Make automatic import and filesystem-write auditing an opt-in project policy through `ProjectSettings/UnityMCPUIToolkitAudit.json`, with configurable asset roots, runtime source roots, and exclusions.

## [3.3.31] - 2026-07-28

- Collapse test-job summaries that repeat progress counters and normalize named result pagination to the same collection/total/next-offset contract used by ordinary list tools.

## [3.3.30] - 2026-07-28

- Recognize `returned<Collection>Count` aliases such as `returnedIssueCount` when their value exactly matches the corresponding returned collection.

## [3.3.29] - 2026-07-28

- Compact completed queue-ticket results as independent wire-response roots so asynchronous tools receive the same success-envelope removal as synchronous routes without removing per-item success values from batch results.

## [3.3.28] - 2026-07-28

- Compact all HTTP tool responses at the transport boundary by removing redundant success envelopes, duplicate error messages, derivable collection counts and presence flags, completed-pagination metadata, false truncation flags, and overlapping array, persistence, operation, and summary aliases.
- Replace the compilation diagnostic count/boolean aliases with grouped error and warning counts while continuing to return deprecated warnings independently of the requested severity filter.

## [3.3.27] - 2026-07-28

- Preserve unnamed enum values such as combined flags as their underlying integer during SerializedProperty reads and writes, preventing prefab and ScriptableObject property inspection from indexing `enumNames` with Unity's `-1` sentinel.

## [3.3.26] - 2026-07-28

- Preserve compiler diagnostics across Domain Reload, identify obsolete/deprecated warnings, and include their summary in compilation, Editor-idle, asset-refresh, package-test, and build responses even when the primary compilation filter requests errors only.

## [3.3.25] - 2026-07-28

- Omit the implicit `Transform` from prefab hierarchy, scene hierarchy, filtered scene matches, and GameObject component inventories while retaining `RectTransform`.

## [3.3.24] - 2026-07-28

- Serialize destroyed or otherwise Unity-null `UnityEngine.Object` values from `editor/execute-code` as JSON `null` instead of dereferencing stale native wrappers.
- Expire editor-idle tickets against their persisted absolute deadline while queued or recovering from Domain Reload, and restore expired waits as terminal timeout results instead of stale queued work.

## [3.3.23] - 2026-07-28

- Keep pseudo-state and contextual USS rules separate from unconditional class declarations in `uitoolkit/asset-inspect`, preventing `:hover` and `:checked` values from leaking into default style results.
- Detect visible UI Builder content that overflows the configured canvas and automatically enable Match Game View before stable preview capture.

## [3.3.22] - 2026-07-27

- Synchronize Single Sprite sub-asset names and every TextureImporter name table when `asset/rename` or a filename-changing `asset/move` renames the texture, including immediate and frame-batched move execution.

## [3.3.21] - 2026-07-23

- Publish Animator transition condition fields as object-array item schemas so `unity_animation_update_transition` can replace, add, and update conditions directly.

## [3.3.20] - 2026-07-23

- Capture runtime UI Toolkit elements from the Game View render texture instead of the Editor window shell, preventing black element crops from GPU-composited Game View content.
- Capture UI Builder previews through a temporarily raised on-screen window path because Win32 `PrintWindow` omits its GPU-composited viewport.
- Added explicit `auto`, `print-window`, and `screen` Editor-window capture modes with capture-method diagnostics.

## [3.3.19] - 2026-07-22

- Route project-context reads through the Unity main-thread request queue so `EditorPrefs` and project-path access cannot throw worker-thread HTTP 500 errors.

## [3.3.18] - 2026-07-22

- Classified project-context endpoints and category subroutes as read-only so `unity_get_project_context` no longer fails project-binding validation, while retaining explicit wrong-project rejection.

## [3.3.17] - 2026-07-22

- Made `prefab-asset/add-component` persist its wait/mutation phase before `AssetDatabase.Refresh`, resume the same deferred request after a Domain Reload, and reconcile the saved component count before deciding whether to complete or replay the mutation.
- Retry transient prefab file operations for Win32 sharing, lock, and user-mapped-file errors (including 1224), write normalized YAML through atomic replacement, and treat exhausted post-save normalization as a warning instead of overriding a successful Unity save.
- Verify `prefab-asset/set-property` through serialized Unity readback and recover a successful result when the requested value persisted despite a save-path exception.

## [3.3.16] - 2026-07-21

- Added optional `layer` support to `prefab-asset/add-gameobject` and transaction `addGameObject` operations, with parent-layer inheritance when omitted and final Layer readback in results.

## [3.3.15] - 2026-07-20

- Added `spriteSlice` to first-class `asset/import`, enabling validated fixed-grid slicing during batch import so sparse animation frames are never collapsed by Unity automatic slicing.

## [3.3.14] - 2026-07-18

- Finish package-test restoration once the original manifest bytes are restored and the Editor is idle, without relying on transient test-assembly presence.

## [3.3.13] - 2026-07-18

- Complete package-test manifest restoration after the requested test assemblies become available again instead of waiting forever in `restoring`.

## [3.3.12] - 2026-07-18

- Finalize Memory Profiler callbacks from a pre-registered Editor update instead of registering `delayCall` inside the native completion callback.
- Recover a timed-out capture when Unity has closed a stable `.tmpsnap` containing valid Memory Profiler header and footer signatures.
- Bind delayed native callbacks to their originating capture job so a recovered capture cannot corrupt a later snapshot.

## [3.3.11] - 2026-07-18

- Capture Memory Profiler snapshots through Unity's current public API, write through `.tmpsnap` before finalizing `.snap`, and default to managed objects, native objects, and native allocations.
- Keep long-running snapshot jobs observable after the initiating request times out through the new first-class `unity_profiler_memory_snapshot_status` tool.

## [3.3.10] - 2026-07-17

- Added first-class `unity_asset_import_unitypackage` / `asset/import-unitypackage` support for reload-safe non-interactive `.unitypackage` import jobs with callback-confirmed completion, stable failure results, new-asset reporting, and packaged GUID preservation.

## [3.3.9] - 2026-07-17

- Added the initial first-class `.unitypackage` import route and schema. Superseded by the reload-safe callback job in 3.3.10.

## [3.3.8] - 2026-07-17

- Kept project binding metadata out of project-tool business arguments, preventing `project-tools/execute` and direct project-tool routes from failing strict schemas with unknown `expectedProjectPath` or `expectedProjectName` arguments.

## [3.3.7] - 2026-07-16

- Wait for a package test assembly's emitted DLL before starting Test Runner, so an asmdef that only exists in Unity's compilation graph cannot produce a false `No tests matched` result or strand manifest restoration.
- Stop forcing a full `AssetDatabase.Refresh` when enabling or restoring package testables; Package Manager resolution now owns the required package import and compilation.

## [3.3.6] - 2026-07-16

- Defaulted refresh jobs to non-forced imports and suppress `ImportAssetOptions.ForceUpdate` for targeted compilation assets, preventing a single script refresh from forcing broad dependency reimports while still compiling timestamp-changed sources.
- Report compilation paths whose requested ForceUpdate was intentionally skipped, including after reload recovery.

## [3.3.5] - 2026-07-16

- Preserved explicit prefab transaction array edits during YAML stabilization, preventing successful saves from silently restoring the pre-edit serialized list.
- Made `asset/get-refresh-job` actively reconcile an idle `waiting-for-editor` job and re-register its update callback, so polling reaches the terminal result without requiring an MCP reconnect.

## [3.3.4] - 2026-07-16

- Exposed `profiler/memory-snapshot` as a first-class deferred tool and wait for the Memory Profiler completion callback or an explicit timeout instead of returning fire-and-forget success.
- Resolved the built-in `UnityEngine.Profiling.Memory.Experimental` API before older Editor namespace fallbacks and return stable missing-package, API, capture, and timeout error codes.

## [3.3.3] - 2026-07-16

- Allowed an exact AssetDatabase refresh `jobId` or original request ID to recover a persistent job after the polling agent identity changes across a script reload, while retaining owner-only implicit lookup and clearing.
- Recognized compiled Unity Test Runner assemblies through `CompilationPipeline` instead of requiring them to load into the default AppDomain, preventing package-test workflows from stalling in `waiting-for-assembly`.
- Made Play Mode actions explicit, idempotent, reload-resumable target states and wait for confirmed state changes before returning success; added a dedicated `resume` action.
- Exposed Profiler control, rendering, frame, analysis, and memory routes as typed first-class MCP tools.

## [3.3.2] - 2026-07-16

- Replaced frame-count-based request-queue cleanup with a low-frequency time cadence and skipped persistent ticket snapshot rewrites when no ticket expired, eliminating periodic Editor main-thread stalls during Play Mode.

## [3.3.1] - 2026-07-15

- Mapped docked EditorWindow captures through their host client area, fixing UI Toolkit element capture on mixed-DPI and negative-origin multi-monitor layouts.

## [3.3.0] - 2026-07-15

- Added first-class `unity_prefab_asset_configure_component` / `prefab-asset/configure-component` for atomic ensure-and-configure edits, including serialized properties and asset or same-prefab ObjectReferences.
- Added `configureComponent` to prefab transactions, including nested reference-type waiting, indexed referenced components, YAML diff roots, and add-versus-update summaries.
- Preserve and apply `references` after wrapping the public configure request as an internal transaction operation.

## [3.2.10] - 2026-07-15

- Clipped UI Builder document analysis to the visible viewport and added document-only checkerboard/shell detection for Unity versions where the reflected canvas and document bounds are identical.
- Kept valid colored and sufficiently complex previews verifiable even when no comparable canvas-background ring is available.

## [3.2.9] - 2026-07-15

- Made UI Builder preview validation compare the mapped document pixels with the surrounding canvas background, so editor chrome, blank shells, and checkerboard-only captures no longer count as valid visual evidence.
- Added client-area coordinates to editor-window captures and used them for UI Toolkit element mapping, avoiding floating-window title-bar offsets in crops and preview analysis.

## [3.2.8] - 2026-07-15

- Updated the first-class schema regression suite to treat `expectedProjectPath` as part of every mutating tool contract.

## [3.2.7] - 2026-07-15

- Added `expectedProjectPath` to every mutating route schema so first-class tools can bind multi-instance requests without falling back to `advanced/execute`.
- Added request-ID matching to `asset/get-refresh-job`, allowing the MCP server to recover the exact persistent refresh job after an outer queue timeout or domain reload without mistaking an older job for the current request.

## [3.2.6] - 2026-07-15

- Made `asset/refresh` strictly targeted whenever `assetPaths` are supplied, removing the implicit full synchronous AssetDatabase refresh that amplified Unity memory pressure during repeated small USS/UXML imports. Full external-change reconciliation now requires omitting `assetPaths`, and refresh results report an explicit `targeted` or `full` mode.

## [3.2.5] - 2026-07-14

- Blocked Git package updates while a package-test workflow is non-terminal, preventing its exact manifest restoration from overwriting a concurrent package revision change.
- Made the deferred prefab refresh regression assert the plugin's missing-type refresh scheduling directly, avoiding false failures from prefab loading importing unrelated files on Unity 6.4.
- Failed filtered Unity Test Runner jobs with `no_tests_matched` when zero tests are selected, eliminating false-success package-test runs.

## [3.2.4] - 2026-07-14

- Rebuilt terminal Unity Test Runner details from the final result tree and persisted failure diagnostics across reloads, so package-test failures retain their names, messages, and stacks after manifest restoration.

## [3.2.3] - 2026-07-14

- Limited parameter-level `wait/editor-idle` coalescing to active tickets, preventing a later wait from reusing a stale completed result while retaining completed-ticket reuse for transport idempotency keys.

## [3.2.2] - 2026-07-14

- Normalized unbound and wrong-project rejection payloads with stable `target_project_required` and `wrong_unity_project` error codes.

## [3.2.1] - 2026-07-14

- Fixed package-test compilation by keeping regression access to internal refresh and localization types reflection-based.
- Explicitly classified route/tool metadata endpoints as read-only under conservative target-binding defaults.

## [3.2.0] - 2026-07-14

- Added first-class asset folder creation, generic asset copy, incoming/outgoing dependency graphs, and rollback-capable cross-asset transactions.
- Added structured UXML and USS editing plus rollback-capable multi-file UI Toolkit authoring transactions.
- Added first-class Package Manager add, remove, list, and paginated search routes without blocking the Unity main thread.
- Added owner-scoped persistent job history for asset refresh, Player Build, Unity Test Runner, and package-test workflows.
- Added queue cancellation, stable request idempotency, per-agent/global capacity limits, metadata-driven read scheduling, and general domain-reload restoration. Interrupted reads resume; interrupted mutations become explicit non-retryable `UncertainAfterReload` results.
- Made `asset/refresh` reuse its persisted job and queue ticket for the same owner/request across a domain reload, while persisting `Executing` before every Unity action so unrelated mutations are never replayed as if they had not started.
- Replaced runtime C# source parsing with an explicit route registry guarded by regression tests, and filtered optional Localization, Shader Graph, Amplify, and UMA routes by live capability detection.
- Made project-tool first-class exposure explicit through `MCPProjectToolAttribute.FirstClass`; unselected project tools remain available through paginated discovery and `project-tools/execute`.
- Required mutating requests to bind to an expected Unity project, with the MCP server automatically forwarding selected-instance identity and stable idempotency keys.
- Standardized pagination metadata for large asset, package, project-tool, dependency, job, test, and metadata responses.

## [3.1.22] - 2026-07-14

- Ordered targeted asset refreshes by known AssetDatabase dependencies and completed each import synchronously, preventing dependent UXML imports from observing stale USS timestamps in SourceAssetDB.

## [3.1.21] - 2026-07-14

- Made `screenshot/game` capture the Game View's completed render texture directly while Play Mode is paused, without advancing simulation ticks or waiting for a new rendered frame.
- Added paused-frame vertical orientation correction, supersized output support, PNG readback validation, and regression coverage for render-texture capture.

## [3.1.20] - 2026-07-14

- Made `wait/editor-idle` tickets reload-resumable with the original ticket ID, remaining deadline, persisted terminal result, and explicit resume diagnostics.
- Coalesced duplicate active editor-idle waits across reconnecting agents and allowed multiple synchronous callers to observe the same ticket safely.
- Persisted queue snapshots with atomic replacement and a validated backup, preventing a domain reload from reading a partial ticket file.
- Classified editor-idle waits as non-mutating queue work so they no longer block unrelated reads while waiting for Unity to settle.

## [3.1.19] - 2026-07-14

- Added decoded-pixel and file-byte duplicate detection to batch `asset/import`, including project/folder scopes, skip/error/report policies, existing-asset matches, and within-batch matches.
- Added the first-class read-only `texture/find-duplicates` project image audit tool, with bounded folder, extension, asset, and group controls.

## [3.1.18] - 2026-07-14

- Use Unity's indexed `TypeCache` for project-tool discovery instead of scanning every loaded assembly and type, preventing metadata requests and regression tests from timing out in large projects or after runtime code compilation.

## [3.1.17] - 2026-07-13

- Return the same stable dictionary result shape for `asset/import` preflight failures as for completed batch results.

## [3.1.16] - 2026-07-13

- Upgraded `asset/import` to preflight and import up to 500 assets with shared TextureImporter defaults, immediate or frame-batched execution, per-item results, overwrite protection, and rollback.
- Removed the single-file `sourcePath`/`destinationPath` request shape in favor of the canonical `imports` collection and shared `execution` model.

## [3.1.15] - 2026-07-13

- Keep AssetDatabase refresh jobs non-terminal until compilation, asset updating, and a stable idle window have completed, so `succeeded` no longer races a delayed domain reload.

## [3.1.14] - 2026-07-13

- Exposed `build/run-test` as a first-class persistent Player Build job with `build/get-job` polling, so normal builds no longer fall through the queue's 30-second synchronous timeout.
- Changed `asset/refresh` into a reload-safe persistent job with `asset/get-refresh-job` polling and removed the duplicate targeted-import pass after a full external-change reconciliation.
- Documented that a successful Player Build report is authoritative and does not require a follow-up forced AssetDatabase refresh.

## [3.1.13] - 2026-07-13

- Made package-test polling actively restore the workflow update pump after domain reloads so waiting, timeout, test execution, and manifest restoration cannot stall behind a lost editor callback.

## [3.1.12] - 2026-07-13

- Added explicit `MutatesRuntime` metadata for project tools so runtime state changes can be exposed as first-class tools without misclassifying them as asset edits.

## [3.1.11] - 2026-07-13

- Exposed Animator transition inspection, state updates, transition updates, and state connection workflows as first-class MCP tools.

## [3.1.10] - 2026-07-13

- Exposed texture inspection and sprite-import preset application as first-class MCP tools alongside external asset import.

## [3.1.9] - 2026-07-12

- Allowed primitive JSON values in prefab, serialized-object, and localization value schemas.
- Added first-class component property editing and support for inherited `Behaviour.enabled`.
- Allowed an explicit empty prefab path to reference a prefab root object or component.
- Reconciled external AssetDatabase changes before ordered targeted imports to avoid stale timestamp warnings.

## [3.1.8] - 2026-07-11

- Validate nested serialized component-reference migration with a built-in runtime component instead of an Editor-only test component that Unity cannot attach to prefabs.

## [3.1.7] - 2026-07-11

- Preserve direct, nested managed-reference, and exposed references when `prefab-asset/move-component` replaces the source component with its destination copy.

## [3.1.6] - 2026-07-11

- Limit MCP queue processing to one request per Editor update and pause processing during compilation, asset updates, and a short post-reload stabilization window, preventing reconnect backlogs from triggering long `MCPBridgeServer.OnEditorUpdate` stalls.
- Remove the redundant unbounded legacy main-thread queue; synchronous HTTP requests now rely on the existing fair ticket queue for main-thread execution.

## [3.1.5] - 2026-07-11

- Use Roslyn `Preview` for dynamic code because Unity 6.4 classifies `is not` patterns as preview syntax.
- Complete external asset reconciliation synchronously before `asset/refresh` returns success.

## [3.1.4] - 2026-07-11

- Compile `editor/execute-code` with the latest language version supported by Unity's bundled Roslyn, including `is not` patterns.
- Targeted `asset/refresh` calls now reconcile external file creation and deletion by default, preventing stale deleted scripts from remaining in Unity's compiler source list.

## [3.1.3] - 2026-07-11

- Corrected the prefab hierarchy identity-transform regression test to validate the returned root node shape.

## [3.1.2] - 2026-07-11

- Omit identity Transform values from GameObject, scene hierarchy, prefab hierarchy/find, terrain, lighting, prefab instantiation, and physics overlap responses. Zero positions, identity rotations, and unit scales are no longer serialized.
- Package test workflows now retain summaries and failed/inconclusive details by default instead of every passing test result. Full passing details remain available from `testing/get-job` with `includeDetails=true`.
- Remove compatibility aliases from tool schemas and use canonical request fields only.
- Omit redundant MCP annotations whose values are false and remove annotation titles that duplicate tool names.

## [3.0.0] - 2026-07-11

- Fixed prefab transaction property writes rejecting serialized array-size paths such as `items.Array.size` with `Cannot set property type: ArraySize`.
- Fixed prefab batch/transaction edits unconditionally refreshing assets before checking already-loaded component types. Missing types now produce a retryable response before a delayed refresh, so a script-triggered Domain Reload does not cut off the active MCP response.
- Fixed prefab asset edits rewriting untouched YAML whitespace or serializing unrelated default component fields.
- Replaced separate batch routes with a shared `execution` object. `execution.mode` supports `auto`, `immediate`, and `batched`, with common per-frame, timeout, and error-continuation controls.
- Removed `prefab-asset/batch-edit`, `asset/move-batch`, `component/batch-wire`, and `localization/upsert-entries`. Their multi-operation behavior now lives on `prefab-asset/transaction-edit`, `asset/move`, `component/set-reference`, and `localization/upsert-entry`.

### Added
- **Optional Unity Localization tools** - When `com.unity.localization` is installed, first-class tools expose Locale management, String/Asset Table Collections, localized entry CRUD, Smart String flags and persistent variables, validation, and Localization Settings. The integration assembly and tool metadata stay hidden when the package is absent.
- **Completed visual capture results** - `screenshot/game` now waits for a stable, decodable PNG and reports dimensions, byte size, elapsed time, and readiness instead of returning before the next frame writes the file.
- **Readable project-tool names** - project tools expose compact `unity_pt_*` names capped for MCP clients, retain the legacy name in metadata, and can opt into an explicit `MCPProjectToolAttribute.ShortName`.
- **First-class testing tools** - Test discovery, test execution, job polling, and persistent Git package self-tests now expose concrete tools and schemas.
- **Persistent package test workflow** - `testing/run-package-tests` backs up `Packages/manifest.json`, enables the requested package in `testables`, runs its test assembly after reload, then restores the original manifest bytes.
- **Atomic prefab component moves** - `prefab-asset/move-component` / `unity_prefab_asset_move_component` copies a component to another GameObject, verifies the destination, removes the source, and saves once while preserving serialized data.
- **Compact scene component filtering** - `scene/hierarchy` accepts `componentType`, `nameContains`, `pathContains`, and `maxResults` to return compact flat matches instead of serializing the entire scene tree.
- **Unified asset moves** - `asset/move` accepts a `moves` array, preflights the complete request, preserves GUID/meta state, rolls back completed moves on stop-on-error failures, and supports immediate or frame-batched execution.
- **Project tool selection hints** - `MCPProjectToolAttribute` can now declare `ReadOnly`, `MutatesAssets`, `Dangerous`, `LongRunning`, `MayReloadDomain`, and `RequiresPlayMode`; `_meta/tools` also infers common read-only `get/list/*summary` project tools and mutating asset/prefab tools when hints are not explicit.
- **Tool metadata profiles** - `_meta/tools` now uses a single `ToolProfile` registry for first-class/fallback/lazy exposure plus `readOnly`, `mutatesAssets`, `dangerous`, `longRunning`, `mayReloadDomain`, and `requiresPlayMode` hints.
- **Project tool input validation** - project tools declared with `MCPProjectToolAttribute.InputSchemaJson` now validate schema shape at discovery time and validate required fields, primitive JSON types, and `additionalProperties=false` before execution.
- **Reload-aware queue snapshots** - queue tickets persist small status snapshots through Unity domain reloads. Polling a lost ticket now returns a retryable `ticket_lost_after_reload` response instead of a generic not-found result.
- **Concrete tool surface cleanup** - `asset/refresh`, serialized-object get/set, compilation errors, common prefab-asset read/write routes, and clearer prefab instantiation aliases are now first-class in `_meta/tools`. `advanced/execute` remains available but is advertised as a fallback instead of a preferred entrypoint.
- **Prefab transaction operation schemas** - `prefab-asset/transaction-edit` exposes operation-level schemas for `addComponent`, `setProperty`, `setReference`, `addGameObject`, `instantiatePrefab`, `removeComponent`, `removeGameObject`, and `moveGameObject`, plus the shared execution schema.
- **Multi-editor project routing safety** — new `instance/current`, `instance/list`, `instance/resolve`, and `instance/assert-project` routes expose the shared Unity MCP instance registry so clients can resolve the correct Editor by `projectPath` before sending commands. Requests can also include `expectedProjectPath` / `targetProjectPath` / `unityProjectPath` or the `X-UnityMCP-Expected-Project-Path` header; if a command reaches the wrong Unity project, the bridge returns `wrong_unity_project` before executing Unity API work.
- **First-class project tools** — `MCPProjectToolAttribute` now supports `InputSchemaJson`, `project-tools/list` returns schemas and direct routes, and `_meta/tools` exposes each valid project tool as a concrete `unity_project_tool_*` tool routed through `project-tools/call/<toolName>`.
- **First-class route metadata** — stable routes advertised in the README now include `firstClass=true` in `_meta/tools`, so MCP clients can expose concrete tools with route-owned schemas and descriptions instead of routing them through the generic advanced entry.

### Changed
- **Compact targeted UI asset inspection** - `uitoolkit/asset-inspect` names queries omit the unrelated general element list by default, share one result budget, and return only relevant USS classes unless full output is requested.
- **Token-bounded metadata** - `_meta/tools` now defaults to compact first-class metadata without schemas, returns at most 50 tools per page, and requires explicit flags for schemas or legacy duplicate collections. Full catalogs support category filters and pagination.
- **Bounded query responses** - scene and prefab hierarchies, Console queries, test discovery/results, SerializedObject reads, and execute-code serialization now use conservative defaults with pagination or explicit truncation metadata. Console stacks and test stacks are opt-in.
- **Lean first-class surface** - duplicate prefab aliases and low-frequency visual, animation, build, package, and queue routes remain available through the advanced catalog instead of occupying every MCP `tools/list` response.
- **Project tool exposure** - read-only and asset-mutating project tools remain concrete; runtime mutation commands are discovered through `project-tools/list` and called through `project-tools/execute` instead of all occupying `tools/list`.
- **Prefab diff summaries** - prefab mutations return summary diffs by default; callers can explicitly request `minimal` or `full` lines.

### Fixed
- **Execute-code assembly context safety** - dynamic code that references Unity, project, or package assemblies now skips the isolated AppDomain and runs against Unity's loaded assembly context, preventing missing dependency failures and unsafe cross-domain asset serialization. Pure framework-only code remains unloadable through AppDomain isolation.
- **UI Builder preview evidence** - `uitoolkit/builder-preview` now waits for the requested UXML document and a laid-out canvas, focuses and repaints across stable frames, restores previous focus, and rejects failed or visually blank captures instead of reporting unconditional success.
- **Editor-window DPI cropping** - docked EditorWindow captures prefer raw screen-pixel coordinates, use explicit local/scaled fallbacks, and report the selected coordinate mode plus center-content diagnostics.
- **Execute-code UI Toolkit and diagnostics** - dynamic code includes `UnityEngine.UIElements`, accepts additional namespace imports, maps compiler diagnostics back to user-code line numbers, and uses a collectible `AssemblyLoadContext` when available instead of permanently accumulating dynamic assemblies.
- **Play-mode screenshot contract** - `screenshot/game` now rejects EditMode immediately with `requires_play_mode` instead of waiting for a frame that Unity will never render.
- **Package-test failure evidence** - persistent package test workflows now save paginated test details and stack traces before restoring `manifest.json`, so failures remain diagnosable after the test assembly unloads.
- **Direct collection arguments** - UI Toolkit asset inspection accepts arrays and other enumerable values in direct C# calls as well as JSON `List<object>` inputs.
- **Prefab YAML block ordering** - prefab saves preserve the original order of surviving Unity YAML object blocks, append only new blocks, remove deleted blocks, validate block equivalence, and continue stripping trailing whitespace.
- **Test Runner completion** - jobs finalize from completed leaf results when Unity's root `RunFinished` callback arrives late; an unfocused Editor is reported as informational state instead of a blocking reason.
- **Prefab mutation rollback and YAML diffs** - failed `prefab-asset/add-gameobject` and component moves restore the original prefab bytes; successful prefab saves remove trailing YAML whitespace; line diffs now use a real edit script and report complete added/removed totals independently from truncation.
- **Execute-code structured results** - nested arrays, lists, dictionaries, anonymous objects, and Unity values are serialized recursively instead of degrading to CLR type names such as `System.String[]`.
- **SerializeReference array writes** - `serialized-object/get` now reports `$managedReferenceType`, and `serialized-object/set` can instantiate new managed-reference elements from that type or infer it from a homogeneous existing list. Unsupported writes now return a structured error without a Unity Console exception.
- **Prefab transaction reliability** - `prefab-asset/transaction-edit` applies operations according to the shared execution policy, returns progress snapshots and structured timeout failures, and reports explicit persistence state (`saved`, `saveAttempted`, `partialPersistedKnown`, `persistedState`).
- **Serialized complex fields** - component property read/write now expands and accepts serialized arrays/lists plus generic child objects instead of reporting complex list fields only as `Generic`.
- **Deferred write exclusivity** - multi-frame write requests now block later writes from leaving the queue until the active write completes, preventing interleaved asset edits while a deferred prefab batch is still applying.
- **Queue failure status details** - `queue/status` now includes top-level `success=false`, `error`, and `message` fields for failed tickets so MCP clients can preserve validation and project-tool errors.
- **Editor idle diagnostics** - `editor/state` now includes `isUpdating`, `isChangingPlayMode`, and `isPlayingOrWillChangePlaymode` so the MCP server can distinguish true Editor busyness from queue polling false negatives.
- **Package meta lint false positives** - `packages/lint-metas` now skips hidden dotfiles and dot directories such as `.gitattributes`, `.gitignore`, and `.github`, matching Unity's non-imported file behavior.
- **Error result consistency** - bridge and queue paths now normalize error payloads with `success=false`, `errorCode`, `message`, and `retryable` while keeping existing successful result payloads backward-compatible.
- **Long direct calls** - synchronous direct calls that exceed the immediate wait window now return a retryable response with a `ticketId` and `pollRoute` while the queued Unity operation continues in the background.
- **Deferred route direct-call timeout** — direct calls to deferred routes such as `advanced/execute` now wait on a deferred queue ticket instead of wrapping another main-thread wait, preventing 30s timeouts when the route is used as the stable generic entry.

## [2.32.0] - 2026-06-02

### Added
- **`screenshot/editor-window` command** — `MCPScreenshotCommands.CaptureEditorWindow` captures any EditorWindow (Inspector, Project, Console, custom windows) to a PNG via the Win32 `PrintWindow` API (`PW_RENDERFULLCONTENT`), occlusion-proof (the window renders itself offscreen — no raise or focus-steal). Docked windows are captured by PrintWindowing the main window and cropping the panel rect; floating windows by resolving their own HWND (exact title match) and capturing the whole window. Defaults to `Assets/Screenshots/`, honours any user-chosen `.png` path; bounds dimensions against `SystemInfo.maxTextureSize`, all GDI handles + the `Texture2D` released in `try/finally`. **Windows editor only** (`#if UNITY_EDITOR_WIN`) — returns a clear unsupported-platform error on macOS/Linux (no `PrintWindow` equivalent); use `screenshot/scene` / `screenshot/game` (camera-based) there. Companion to the `unity-mcp-server` 2.30.0 change.

### Changed
- **Welcome window reworked into a modular, themed system** — the single-file `Editor/MCPWelcomeWindow.cs` is replaced by `1-Scripts/Editor/WelcomeWindow/` (own assembly `UnityMCP.Editor.Welcome`, namespace `UnityMCP.Editor.Welcome`): a USS theme, Welcome + Studio tabs, auto-open on first load with per-project detection, a config-driven content seam (custom sections / buttons, cross-sell entries via `welcome.gen.json`), a devlog fetcher, and bundled icons.

## [2.31.2] - 2026-05-21

### Changed
- **Settings panel grouped into labelled sections** — the Dashboard's *Settings* foldout now has three bold sub-headers (**General**, **Port**, **Multiplayer Play Mode (MPPM)**) instead of an unlabelled flat list. The *Start on Virtual Players* toggle is now under the explicit **MPPM** header so its scope is clear, and it was moved below the Port settings. UI-only change, no behaviour difference.

## [2.31.1] - 2026-05-21

### Fixed
- **MPPM scenario commands now work on MPPM 2.0 (Unity 6)** — the 2.31.0 Unity 6 port resolved the scenario types under the wrong names. In MPPM 2.0 the scenario "config" ScriptableObject was renamed `OrchestratedScenario` (from `ScenarioConfig`) and the status struct `ScenarioStatusData` (from `ScenarioStatus`); `MCPScenarioCommands` now resolves both. `create_scenario` no longer requires the removed `RemoteInstanceDescription` type (remote instances were dropped in MPPM 2.0), and `list_scenarios` reads instance counts from `OrchestratedScenario`'s fields. All MPPM tools verified end-to-end on Unity 6000.5.0b8 + MPPM 2.0.2.

## [2.31.0] - 2026-05-21

### Added
- **MPPM Virtual Player management** — new commands `mppm/list-players`, `mppm/activate-player`, `mppm/deactivate-player` to list and activate/deactivate Multiplayer Play Mode virtual players by 1-based index.
- **`scenario/create`** — create an MPPM `ScenarioConfig` asset programmatically (one Main Editor instance + N Virtual Editor instances with configurable Host/Client/Server roles).

### Changed
- **MPPM scenario commands now work on Unity 6** — `MCPScenarioCommands` resolves the MPPM scenario types from both the legacy package assembly (`Unity.Multiplayer.PlayMode.Scenarios.Editor`, pre-Unity-6) and the built-in `UnityEditor.MultiplayerModule` introduced in Unity 6; previously all `mppm/*` commands returned "MPPM is not installed" on Unity 6. `scenario/start` / `scenario/stop` also enter/exit Play mode so virtual-player launch hooks fire.

## [2.30.0] - 2026-05-21

### Changed
- **MCP settings are now scoped per project / per instance** — `EditorPrefs` is global to the machine, so settings were previously shared across every Unity project and instance (e.g. one project's manual port leaked to all others). `MCPSettingsManager` now namespaces keys into two tiers: **instance-scoped** (`Port`, `UseManualPort`, `AutoStart` — keyed by project path, unique per main Editor / ParrelSync clone / MPPM virtual player) and **project-scoped** (`StartOnVirtualPlayers`, project context, action-history and category settings — keyed by `PlayerSettings.productGUID`, shared by a project and its clones / virtual players). Existing settings are migrated to the new keys automatically on first load.

## [2.29.1] - 2026-05-21

### Fixed
- **MPPM Virtual Player detection on Unity 6** — `MCPScenarioCommands.IsVirtualPlayer()` (the gate behind the 2.29.0 "Start on Virtual Players" setting) only resolved the pre-Unity-6 type `Unity.Multiplayer.Playmode.CurrentPlayer`. On Unity 6 that API moved to `Unity.Multiplayer.PlayMode.CurrentPlayer` in the built-in `UnityEngine.MultiplayerModule`, so detection always returned false and the gate never engaged. It now resolves both locations (Unity 6 first, pre-6 fallback). Verified live on Unity 6000.5 with MPPM.

## [2.29.0] - 2026-05-21

### Added
- **"Start on Virtual Players" setting** — new MCP settings toggle controlling whether the bridge auto-starts on Multiplayer Play Mode (MPPM) virtual players. Previously every virtual player launched its own MCP bridge, which is usually unwanted noise. Default is **on** (behaviour unchanged); turn it off so only the main Editor runs a bridge. Virtual players are detected via `Unity.Multiplayer.Playmode.CurrentPlayer.IsMainEditor`; manual start on a virtual player still works. Addresses [unity-mcp-server#21](https://github.com/AnkleBreaker-Studio/unity-mcp-server/issues/21).

## [2.28.1] - 2026-05-21

### Fixed
- **Manual (fixed) port not reclaimed after a domain reload** — with a manual port configured, `MCPBridgeServer.Start()` bound the port directly and gave up permanently on the first failure. Right after a domain reload the port can be briefly unbindable while the previous listener's socket is released; auto-port mode already survived this (it probes and falls back) but manual mode had neither probe nor retry. `Start()` now retries the same manual port up to 10 times on a 0.5s delay before giving up. Addresses [unity-mcp-server#10](https://github.com/AnkleBreaker-Studio/unity-mcp-server/issues/10).

## [2.28.0] - 2026-05-21

### Added
- **Unity 6.5 (6000.5) compatibility** — The plugin compiles and runs on Unity 6.5. The InstanceID APIs deprecated as compile errors in 6.5 (`Object.GetInstanceID`, `EditorUtility.InstanceIDToObject`, `SerializedProperty.objectReferenceInstanceIDValue`, `AssetPreview.IsLoadingAssetPreview(int)`) are now routed through a version-gated `MCPObjectId` shim — it uses `EntityId` with `EntityId.ToULong`/`FromULong` on 6.5 and the classic APIs on 2021.3–6.4. Fixes [#14](https://github.com/AnkleBreaker-Studio/unity-mcp-plugin/issues/14) and [unity-mcp-server#24](https://github.com/AnkleBreaker-Studio/unity-mcp-server/issues/24).

### Changed
- **`instanceId` is now a string** — Unity 6.5 entity ids are 64-bit values that exceed JavaScript's safe-integer range (2^53), so as JSON numbers they were rounded crossing the Node MCP server and object-by-`instanceId` resolution failed. The JSON `instanceId` field is now a decimal string on every Unity version (opaque, lossless). Requires `unity-mcp-server` ≥ 2.28.3.

## [2.27.2] - 2026-05-21

### Fixed
- **Roslyn assemblies not found on macOS** — `MCPEditorCommands.TryLoadRoslyn()` assumed the Windows/Linux `Data/` editor layout; on macOS the assemblies live inside `Unity.app/Contents/`, so `unity_execute_code` always failed with "Roslyn is not available". The lookup now detects the `.app` bundle and adds `Unity.app/Contents` as a data root, plus `Tools/ScriptUpdater`. Contributed by [@dougfy](https://github.com/dougfy) in [#13](https://github.com/AnkleBreaker-Studio/unity-mcp-plugin/pull/13).

## [2.27.1] - 2026-05-21

### Fixed
- **UPM install compile failure (`CS0103` cascade)** — `MCPPrefsCommands`, `MCPConstraintCommands` and `MCPProfilerCommands` shipped `.cs.meta` files with hand-typed placeholder GUIDs. Under a UPM git install (`Library/PackageCache/`), Unity 6 silently skipped indexing those scripts, cascading into `CS0103` errors. The three GUIDs were regenerated with proper random values. Fixes [#11](https://github.com/AnkleBreaker-Studio/unity-mcp-plugin/issues/11). Contributed by [@BadranRaza](https://github.com/BadranRaza) in [#12](https://github.com/AnkleBreaker-Studio/unity-mcp-plugin/pull/12).

## [2.27.0] - 2026-04-22

### Fixed
- **Path-based lookup for inactive GameObjects** — `MCPGameObjectCommands.FindGameObject` now passes `FindObjectsInactive.Include` to `FindObjectsByType<GameObject>`. Every tool routed through path-based lookup (`prefab_info`, `set_active`, `info`, `delete`, `set_transform`, `reparent`, etc.) now works correctly on inactive GameObjects, whereas they previously failed with "GameObject not found". Fixes [unity-mcp-server#16](https://github.com/AnkleBreaker-Studio/unity-mcp-server/issues/16). Contributed by [@BadranRaza](https://github.com/BadranRaza) in [#8](https://github.com/AnkleBreaker-Studio/unity-mcp-plugin/pull/8).
- **Prefab-instance detection on scene instances** — `MCPPrefabCommands.GetPrefabInfo` now uses `PrefabUtility.IsPartOfPrefabInstance` instead of `PrefabUtility.GetPrefabInstanceStatus == NotAPrefab`. This eliminates known false-negative cases (non-root children, instances with missing nested assets) where scene GameObjects that are valid prefab instances were reported as "not a prefab instance". Contributed by [@BadranRaza](https://github.com/BadranRaza) in [#8](https://github.com/AnkleBreaker-Studio/unity-mcp-plugin/pull/8).
- **Bridge server started in AssetImportWorker subprocesses** — Unity spawns batch-mode `AssetImportWorker` subprocesses for parallel asset import, and these were running the plugin's `[InitializeOnLoad]` constructor and claiming ports in the 7890-7899 range on top of the main Editor. A single user with a few projects open could easily exhaust the range, blocking legitimate editor instances. `MCPBridgeServer` now early-returns when `Application.isBatchMode` is true.
- **Infinite retry loop on port exhaustion** — When no port was available, `MCPInstanceRegistry.FindAvailablePort()` returned `PortRangeStart` (7890) by default; `MCPBridgeServer.Start()` then retried via `EditorApplication.delayCall`, hit the same default, and looped forever, spamming `Failed to start on port 7890`. `FindAvailablePort()` now returns `-1` when nothing is free, and `Start()` gives up cleanly. Fixes [unity-mcp-server#10](https://github.com/AnkleBreaker-Studio/unity-mcp-server/issues/10).

### Changed
- **Declared minimum Unity version corrected** — `unityRelease` bumped from `0f1` to `18f1`. The plugin has been using `Object.FindObjectsByType` (introduced in Unity 2021.3.18) for several releases, so the declared minimum was inaccurate. No effective support window change.

## [2.26.0] - 2026-04-02

### Added
- **SpriteAtlas management** — 7 new HTTP endpoints for Unity SpriteAtlas workflow (contributed by [@zaferdace](https://github.com/zaferdace)):
  - `spriteatlas/create` — Create a new SpriteAtlas asset
  - `spriteatlas/info` — Get SpriteAtlas details (packed sprites, packing/texture settings)
  - `spriteatlas/add` — Add sprites or folders to a SpriteAtlas
  - `spriteatlas/remove` — Remove entries from a SpriteAtlas
  - `spriteatlas/settings` — Configure packing, texture, and platform-specific settings
  - `spriteatlas/delete` — Delete a SpriteAtlas asset
  - `spriteatlas/list` — List all SpriteAtlases in the project
- New `MCPSpriteAtlasCommands.cs` — Dedicated SpriteAtlas command handler
- **Self-test system overhaul** — Probes for all 43 command modules (18 new categories), robust test runner with domain reload resume and timeout handling

### Fixed
- **Unity 2023+ / Unity 6 compatibility** — Resolved 43 `CS0618` deprecation warnings across the codebase
- **Self-test conditional compilation** — UMA probe wrapped in `#if UMA_INSTALLED`, Scenario probe handles missing MPPM package gracefully

## [2.25.0] - 2026-03-25

### Added
- **UMA (Unity Multipurpose Avatar) integration** — 13 new HTTP endpoints for the complete UMA asset pipeline:
  - `uma/inspect-fbx` — Inspect FBX meshes for UMA compatibility
  - `uma/create-slot` — Create SlotDataAsset from mesh data
  - `uma/create-overlay` — Create OverlayDataAsset with texture assignments
  - `uma/create-wardrobe-recipe` — Create WardrobeRecipe combining slots and overlays
  - `uma/create-wardrobe-from-fbx` — Atomic FBX-to-wardrobe pipeline (inspect → slot → overlay → recipe in one call)
  - `uma/wardrobe-equip` — Equip/unequip wardrobe items on DynamicCharacterAvatar
  - `uma/list-global-library` — Browse the UMA Global Library contents
  - `uma/list-wardrobe-slots` — List available wardrobe slots
  - `uma/list-uma-materials` — List UMA-compatible materials
  - `uma/get-project-config` — Get UMA project configuration
  - `uma/verify-recipe` — Validate a WardrobeRecipe for missing references
  - `uma/rebuild-global-library` — Force rebuild the Global Library index
  - `uma/register-assets` — Register Slot/Overlay/Recipe assets in the Global Library
- New `MCPUMACommands.cs` — Dedicated UMA command handler with conditional compilation (`UMA_INSTALLED`)
- UMA routes wired into `MCPBridgeServer.cs`

## [2.24.0] - 2026-03-25

### Added
- **Unity Test Runner integration** — Run and manage tests directly from AI assistants
  - `testing/run-tests` — Start EditMode/PlayMode test runs, returns job ID for async polling
  - `testing/get-job` — Poll test job status and results (passed/failed/skipped counts, duration)
  - `testing/list-tests` — Discover available tests with names, categories, and run state
  - Async job-based pattern with deferred execution on Unity main thread
  - Supports filtering by test name, category, assembly, or group
- **Compilation error tracking via CompilationPipeline** — Dedicated error buffer independent of console log
  - `CompilationPipeline.assemblyCompilationFinished` captures errors/warnings per assembly
  - `CompilationPipeline.compilationStarted` auto-clears buffer on new compilation cycle
  - Thread-safe with lock-based synchronization
  - Not affected by console `Clear()` or Play Mode log flooding
  - Returns file, line, column, message, severity, assembly, and timestamp
  - Supports filtering by severity (`error`, `warning`, `all`) and count limit
  - Includes `isCompiling` flag in response
- **HTTP route `compilation/errors`** — New endpoint on the bridge server for the MCP server's `unity_get_compilation_errors` tool

### Fixed
- **Unity 2021.3 LTS compilation compatibility** — Replaced `string.Contains(string, StringComparison)` with `IndexOf` for .NET Standard 2.0 compatibility
- **Operator precedence bug** — Fixed `!IndexOf >= 0` (CS0023) to `IndexOf < 0` in test name filtering

## [2.9.1] - 2026-02-26

### Changed
- **MCP connector renamed to `unity-mcp`** for better Cowork discovery (technical name only)
  - AnkleBreaker branding preserved in all user-facing UI (menu, dashboard, logs, tooltips)
  - Menu item remains: `Window > AB Unity MCP`
  - Log prefix remains: `[AB-UMCP]`
- Updated README with clear two-part installation instructions and Cowork setup guide
- Added Project Context to dashboard documentation

## [2.9.0] - 2026-02-26

### Added
- Project Context System — auto-inject project documentation to AI agents
- MCPContextManager for file discovery and template generation
- Context endpoints on HTTP bridge (direct read-only, bypasses queue)
- Context UI foldout in dashboard window

## [2.8.0] - 2026-02-25

### Added
- Multi-agent async request queue with fair round-robin scheduling
- Agent session tracking and action logging
- Read batching (up to 5/frame) and write serialization (1/frame)
- Queue management API endpoints
- Dashboard with live queue monitoring and agent sessions
- Self-test system for verifying all 21 categories
- Toolbar status element with server controls

## [1.0.0] - 2026-02-25

### Added
- Initial release
- HTTP bridge server on localhost:7890
- Scene management (open, save, create, hierarchy)
- GameObject operations (create, delete, inspect, transform)
- Component management (add, remove, get/set properties)
- Asset management (list, import, delete, prefabs, materials)
- Script operations (create, read, update)
- Build system (multi-platform builds)
- Console log access
- Play mode control
- Editor state monitoring
- Project info retrieval
- Menu item execution
- MiniJson serializer (zero dependencies)
