# Unity MCP configuration architecture and tool audit

This document records the configuration review for the 405 built-in routes
published by `MCPRouteRegistry`. The registry composes its catalog from a
non-deferred manifest and `MCPDeferredRouteRegistry`, which owns both deferred
names and executable handlers. The two sources must be disjoint, their union
must exactly equal the published catalog, and metadata inspection does not
initialize `MCPBridgeServer`.

The audited manifest SHA-256 is
`8010bd6ec922b7fb31608bf7b252120b1666952e9e19c52349fed06783a32fbf`.
The regression suite compares that fingerprint with the authoritative route
manifest, so adding, removing, or renaming a route requires another
configuration review.

## Precedence and ownership

Effective values use this order:

1. An explicit tool argument.
2. A team-owned default from `ProjectSettings/UnityMCPSettings.json`.
3. A local user preference from `Preferences > Unity MCP`.
4. The package's built-in default.

Safety caps are not part of that override chain. Request/response hard limits,
queue capacity and ownership, destructive confirmations, and tool-specific
maximums remain package invariants.

`ProjectSettings/UnityMCPSettings.json` contains only portable team contracts:

- project context enablement and path;
- additional namespaces for `editor/execute-code`;
- the default Physics query dimension;
- the default screenshot output directory.

`Preferences > Unity MCP` contains machine or operator choices:

- instance auto-start and manual port;
- the global automatic port range;
- whether MPPM virtual players auto-start the bridge;
- optional primary-result-limit override;
- whether Prefab mutations include YAML diffs when omitted by the caller;
- Action History persistence/size and persistent Job History size;
- locally enabled tool categories.

The detailed UI Toolkit audit remains in
`ProjectSettings/UnityMCPUIToolkitAudit.json`. It is a separate domain policy
with roots, exclusions, automatic audit switches, and rule-specific values.

### Project-tool package settings

The built-in route policy never injects domain-specific defaults into the
opaque `args` object of `project-tools/execute`. A project-tool package owns
its own settings UI, storage, schema annotations, and per-tool decision about
which omitted arguments may use those settings.

Project-tool arguments are validated recursively before invocation. The
supported schema subset includes nested objects and arrays, primitive types,
bounds and patterns, `enum`, `const`, and
`allOf`/`anyOf`/`oneOf`/`not`. Extension packages should express selector
exclusivity in their schema instead of duplicating it in the bridge.

For one unambiguous primary result collection, extension packages can call
`MCPSettingsManager.ResolvePrimaryResultLimit(...)`. The helper preserves the
same explicit-argument -> shared user preference -> package-default order and
then applies the package's hard bounds. It does not make selectors, mutation
fields, or multi-axis graph budgets configurable.

For example, VMFramework MCP keeps team GameTag validation coverage in
`ProjectSettings/VMFrameworkMCPSettings.json`, local inspection/trace response
choices under `Preferences > VMFramework MCP`, and reuses the shared Unity MCP
result preference for simple paginated reads. VMFramework content paths and
localization tables remain owned by VMFramework GeneralSettings.

## Cross-tool settings added

The optional result-limit preference applies only to a route with one clear
primary result collection. It sets the named argument only when the caller
omits it:

- `limit`: `_meta/tools`, `addressables/info`, `asset/dependencies`,
  `asset/list`, `build/profile`, `cinemachine/info`, `jobs/list`,
  `localization/entries`, `material/properties/get`, `packages/list`,
  `packages/search`, `project-tools/list`, `search/by-component`,
  `search/by-layer`, `search/by-name`, `search/by-shader`, `search/by-tag`,
  `search/missing-references`, `search/scene`, `testing/get-job`, and
  `terrain/get-tree-instances`;
- `maxResults`: `packages/lint-metas`, the three Physics queries,
  `prefab-asset/find`, `shadergraph/get-node-types`, `shadergraph/list`,
  `shadergraph/list-shaders`, `testing/list-tests`,
  `uitoolkit/asset-inspect`, `uitoolkit/query`, and
  `uitoolkit/runtime-query`;
- other primary budgets: `console/query.count`,
  `debug/stack-trace.maxFrames`, `editor/execute-code.maxResultItems`,
  `localization/validate.maxIssues`, `prefab-asset/hierarchy.maxNodes`,
  `profiler/frame-data.maxItems`,
  `profiler/memory-breakdown.maxPerCategory`,
  `profiler/memory-top-assets.count`, `scene/hierarchy.maxNodes`,
  `serialized-object/get.maxProperties`, `texture/find-duplicates.maxGroups`,
  `uitoolkit/audit-uss-styles.maxIssues`,
  `uitoolkit/audit-uxml-layout.maxIssues`,
  `uitoolkit/runtime-tree.maxNodes`, and `uitoolkit/tree.maxNodes`.

The Physics project default applies only to `physics/raycast`,
`physics/overlap-sphere`, and `physics/overlap-box`. Collision matrices,
gravity writes, and collision-layer writes keep their existing explicit
contracts.

The screenshot project directory applies to `screenshot/game`,
`screenshot/scene`, `screenshot/editor-window`, and
`uitoolkit/builder-preview`. Crop and annotation outputs remain adjacent to
their explicit source image, while element captures remain temporary evidence
unless an output path is supplied.

The Prefab YAML-diff preference applies to mutation routes that already expose
`includePrefabFileDiff`: add/configure/remove/move component, add/remove
GameObject, set property/reference, missing-override cleanup, and atomic
transaction edit. It is disabled initially because semantic operation results
are sufficient for normal calls and YAML lines can dominate the response.

## Per-family review

Every route in the manifest was checked. The result by tool family is:

| Families | Routes reviewed | Configuration decision |
|---|---:|---|
| `_meta`, `ping`, `agents`, `instance`, `queue`, `mcp`, `wait`, `advanced`, `mppm` | 19 | Port discovery and compact result defaults are preferences. Queue limits, ownership, transport size, ticket retention, and execution deadlines remain invariants. `mcp/health` is compact by default and reports effective configuration. |
| `editor`, `console`, `compilation`, `debug`, `debugger`, `profiler`, `undo`, `selection`, `search` | 40 | Primary diagnostic result budgets may use the user override. Play-mode actions, attach waits, stack traces, snapshots, evaluation, and mutations remain explicit. |
| `asset`, `texture`, `sprite`, `spriteatlas`, `material`, `serialized-object`, `scriptableobject`, `script`, `asmdef`, `taglayer` | 62 | Read-result budgets may use the preference. Importer values, presets, roots, overwrite, dedupe, reimport, refresh, raw serialization, and file mutations stay request-owned. Sprite slicing atomically owns the complete SpriteRect and name-fileID tables; removed frames and renamed entries are not retained as compatibility aliases. |
| `prefab`, `prefab-asset`, `component`, `gameobject`, `renderer`, `constraint`, `lod` | 43 | Prefab YAML response detail is a user preference; hierarchy/find budgets may use the result preference. Selectors, references, transforms, apply/revert/unpack, and transaction operations stay explicit. |
| `scene`, `sceneview`, `physics`, `navigation`, `lighting`, `particle`, `terrain`, `scenario` | 71 | Physics read queries have a project dimension default. Scene replacement/save/discard, runtime simulation, baking, clearing, placement, scenario activation, and terrain edits stay explicit. |
| `build`, `testing`, `jobs`, `packages`, `project-tools`, `project`, `settings`, `editorprefs`, `playerprefs` | 39 | Read pages and histories may use preferences. Build target/output/run/overwrite, test mode/filter, package ref/resolve, the generic project-tool envelope, and preference mutations remain explicit. A project-tool package may resolve its own omitted domain defaults after schema validation. |
| `ui`, `uitoolkit`, `gameview`, `screenshot`, `graphics` | 47 | Screenshot directory is a project default; bounded read results may use the preference; UI audit keeps its dedicated project policy. Capture dimensions, transports, tolerances, expected images, refresh, and runtime/editor target selection stay explicit. |
| `animation`, `audio`, `audio-mixer`, `input`, `shadergraph`, `vfxgraph`, `timeline`, `cinemachine`, `addressables`, `localization` | 84 | Simple list pages may use the result preference. Graph/mixer/timeline multi-axis budgets, raw serialized detail, operations, package capability behavior, locale/table values, labels, addresses, and runtime overrides stay explicit. |

The counts total 405. Complex graph tools deliberately retain independent
budgets such as nodes, edges, slots, clips, markers, groups, effects, and
properties. Collapsing those into one global number would make response shape
less predictable rather than easier to use.

## Values deliberately not configurable

The following fields must continue to be selected per request:

- `dryRun`, `save`, `discard`, `overwrite`, `force`, `run`,
  `terminateAfter`, and equivalent destructive or lifecycle choices;
- paths, selectors, target object identities, scene modes, build targets,
  package refs, test modes, import settings, and ordered transaction
  operations;
- `includeSerialized`, `includeStackTrace`, full metadata diagnostics, raw
  graph data, and other potentially large diagnostic detail;
- hard response/request caps, queue capacity, idempotency/ownership rules,
  reload recovery limits, and tool-specific maximums.

This boundary keeps configuration convenient without turning hidden state into
an implicit mutation contract.

Transport compaction removes empty optional containers, but preserves a sole
empty primary collection. This keeps zero-match list/search results meaningful
inside completed queue tickets while still omitting redundant empty warning or
diagnostic arrays from otherwise informative responses.
