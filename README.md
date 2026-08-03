# VM Unity MCP

VM Unity MCP is an independently maintained Unity Editor bridge for structured,
queue-safe MCP automation. It provides route-specific tools for Unity assets,
scenes, UI Toolkit, packages, builds, tests, diagnostics, and project-defined
workflows.

This project was originally derived from
[AnkleBreaker Studio's Unity MCP Plugin](https://github.com/AnkleBreaker-Studio/unity-mcp-plugin)
and retains its license and attribution requirements. **Powered by AnkleBreaker
MCP.**

## Install

In Unity Package Manager, choose **Add package from git URL...**:

```text
https://github.com/VM233/VMUnityMCP.git
```

Pin a commit for reproducible projects:

```text
https://github.com/VM233/VMUnityMCP.git#<commit-hash>
```

The Editor package is one side of the connection. Run a compatible MCP server,
such as [VM233/unity-mcp-server](https://github.com/VM233/unity-mcp-server),
from the MCP client. The bridge listens on `127.0.0.1`; automatic ports use
`7890-7899` initially.

## Capability model

The public MCP surface is generated from live Editor metadata rather than a
hand-maintained README list. This prevents installed Unity packages, Unity
versions, and the currently selected project from producing stale concrete
tools.

The built-in catalog is the exact union of the non-deferred route manifest and
the executable deferred-handler registry. Deferred names are not repeated in a
second list, and reading metadata does not initialize the HTTP listener.

| Area | Contract |
|---|---|
| Discovery | `_meta/capabilities` reports package/version-gated integrations. `_meta/tools` provides the compact route catalog for server synchronization. |
| Common tools | The companion server exposes a bounded, release-managed set of concrete `unity_*` tools with route-owned schemas. |
| Specialized routes | Lower-frequency routes remain available through lazy metadata or `unity_advanced_execute`. |
| Project extensions | `unity_project_tools_list`, `unity_project_tools_get`, and `unity_project_tools_execute` discover and execute project/package-defined tools without leaking one project's catalog into another project. |
| Multi-instance safety | Instance discovery, explicit port selection, expected-project binding, and per-agent queue ownership prevent cross-project mutation. |
| Long work | Builds, tests, refreshes, package tests, snapshots, Addressables builds, execute-code, and long project tools use persistent jobs or tickets; owned cancel routes are cooperative. |
| Optional packages | Localization, Shader Graph, VFX Graph, Addressables, Timeline, Cinemachine, and Build Profiles publish only when their capability is available. |

Common asset-authoring composition is intentionally available without arbitrary
Editor code: create folders, copy assets, and create Prefab Variants; inspect a
Prefab hierarchy; apply one atomic Prefab transaction; then create or update
framework-owned configuration through a project/package tool. Localization
entry upsert and removal are concrete tools when Unity Localization is
installed. Project code should not retain one-off Editor builders that mirror
checked-in asset values; temporary migrations are removed after asset readback
succeeds.

Successful `asset/delete` operations save asset changes produced by Unity deletion callbacks before
returning. Dependent configuration such as Addressables entries is therefore persisted together with
the deletion instead of remaining only in the current Editor session.

Filename-changing `asset/rename` and `asset/move` operations keep Sprite asset
identity coherent as well as preserving the `.meta` GUID. Single Sprite names
follow the filename. Multiple Sprite names that use the previous filename as
their prefix are migrated to the new prefix while retaining every Sprite ID,
so AnimationClip and Prefab references remain valid.

Major route families include:

- scene workspaces, GameObjects, components, Prefabs, materials, importers, and
  serialized assets;
- UI Toolkit authoring, static audits, runtime inspection, screenshots, and
  visual comparison;
- Animator, Audio Mixer, VFX Graph, Shader Graph, Timeline, Cinemachine,
  Addressables, Localization, Physics 2D/3D, terrain, lighting, and navigation;
- Console, compilation, debugger, Profiler, testing, builds, packages, jobs,
  queue diagnostics, and Editor execution.

Shader Graph inspection treats the graph's `GraphData` node, property, and edge
references as authoritative. Texture properties report authoring flags that can
change generated shader declarations, while scalar graph-object edits validate
the existing field and type, synchronously import, verify readback, and roll
back on failure.

Use live tool metadata for exact names and schemas. Do not copy a tool list from
this README into a client manifest.

## Project extensions

Project-specific workflows can live in an Editor assembly in the project or in
another package:

```csharp
using System.Collections.Generic;
using UnityMCP.Editor;

public static class ProjectMcpTools
{
    [MCPProjectTool("example/add-content",
        Description = "Create and register one example content asset.",
        InputSchemaJson =
            "{\"type\":\"object\",\"properties\":{" +
            "\"id\":{\"type\":\"string\",\"description\":\"Content id.\"}" +
            "},\"required\":[\"id\"],\"additionalProperties\":false}",
        OutputSchemaJson =
            "{\"type\":\"object\",\"properties\":{" +
            "\"id\":{\"type\":\"string\"}," +
            "\"created\":{\"type\":\"boolean\"}" +
            "},\"required\":[\"id\",\"created\"],\"additionalProperties\":false}",
        SideEffects = MCPProjectToolSideEffect.WritesAssets,
        ErrorCodes = new[] { "content_id_conflict" },
        MutatesAssets = true)]
    public static object AddContent(Dictionary<string, object> args)
    {
        return new { id = args["id"], created = true };
    }
}
```

Declare exactly one operation kind: `ReadOnly`, `MutatesAssets`,
`MutatesRuntime`, or `MutatesProjectFiles`. Use `MutatesProjectFiles` for writes
to project-owned files outside Unity's `Assets` database, such as generated
reports or repository configuration; discovery exposes it as the exact
`writesProjectFiles` side effect. Add `RequiresPlayMode`, `Dangerous`,
`LongRunning`, or
`MayReloadDomain` when applicable. Strict schemas should describe every
property and reject unknown business arguments. The bridge recursively enforces
the supported JSON Schema subset before invocation, including
`allOf`/`anyOf`/`oneOf`, `not`, `const`, nested objects, and array items.
Declared output schemas are also enforced before success. Use `SideEffects` for
the concrete effect classes, `ErrorCodes` for domain failures, and
`CleanupToolName` only when the operation produces a token owned by that cleanup
tool.

These booleans are authoring inputs, not public response fields. Discovery
serializes positive capabilities as a sorted `tags` array, for example
`["dangerous", "longRunning"]`; absence means false. Concrete effects such as
`writesAssets`, `writesProjectFiles`, `changesRuntimeState`, or `reloadsDomain`
are reported once in
`sideEffects`. Empty tags/effects, empty cleanup names, valid-state aliases, and
standard project-tool error codes are omitted. `project-tools/get` includes
only tool-specific extra `errorCodes`.

`LongRunning=true` and an explicit `runAsJob=true` both return a persistent Job.
Class-based tools can implement `IMCPPersistentProjectTool` to yield a
`MCPProjectToolJobStep` between Editor updates. Every continuation value must be
returned in the step state; the bridge does not retain the tool instance.

The canonical client workflow is:

```json
{ "offset": 0, "limit": 100 }
```

through `project-tools/list`, then:

```json
{ "toolName": "example/add-content" }
```

through `project-tools/get`, then:

```json
{
  "toolName": "example/add-content",
  "args": { "id": "example_item" }
}
```

through `project-tools/execute`.

`project-tools/list` intentionally omits schemas. The Node server intentionally
keeps all project-defined tools behind this three-stage contract because a
single MCP session can switch between open Unity projects.

Project-tool packages can reuse
`MCPSettingsManager.ResolvePrimaryResultLimit(...)` for a single primary
collection. It preserves the shared Unity MCP user preference while keeping
explicit values and package-specific hard caps authoritative. Domain-specific
packages should own their own Project Settings and Preferences rather than
adding unrelated fields to Unity MCP.

## Configuration

Default precedence is:

1. explicit tool argument;
2. team-owned Project Settings;
3. local Preferences;
4. built-in default.

Team settings are stored in `ProjectSettings/UnityMCPSettings.json` and edited
under **Project Settings > Unity MCP**:

- project context;
- additional namespaces for `editor/execute-code`;
- default Physics query dimension;
- screenshot output directory.

Operator settings are edited under **Preferences > Unity MCP**:

- bridge startup and manual/automatic ports;
- MPPM startup;
- optional primary-result-limit override;
- optional Prefab YAML diff detail;
- Action and Job history sizes;
- locally enabled tool categories.

Safety caps, paths/selectors, mutation operations, overwrite/save/discard
choices, build/test targets, raw diagnostic expansion, and destructive
confirmation remain explicit or invariant.

The full ownership matrix and authoritative built-in route audit are in
[Documentation~/configuration.md](Documentation~/configuration.md).

## Response and safety conventions

- Tool metadata publishes an `outputSchema` for every route. The companion
  server exposes the actual result through MCP `structuredContent`; the text
  block remains a short human-readable summary. Shared persistent Job fields
  include semantic descriptions so clients can compose status, cancellation,
  and cleanup without relying on route-specific prose.
- Successful bridge responses may omit redundant `success=true`; errors keep a
  stable error code and retryability.
- Transported type descriptors keep the complete `fullType`, `fullTypeName`, or
  `fullName` identifier and omit matching short `type`, `component`, `typeName`,
  `name`, or fallback `title` aliases. Distinct names and custom titles remain;
  request selectors still accept simple or complete names.
- Project-tool success envelopes are unwrapped without compacting the validated
  result object. Required empty collections, counts, flags, and nested members
  therefore retain the exact shape declared by the tool's `outputSchema`.
  The same preservation applies when that envelope is nested in a persistent
  Job snapshot returned by `jobs/get`.
- Published `inputSchema` and `outputSchema` objects are transported without
  response compaction. Business properties named `tags` or `sideEffects` and
  JSON Schema keywords such as `readOnly` therefore retain their declared
  schema shapes.
- `editor/execute-code` reports invalid submissions and compiler diagnostics as
  non-retryable structured errors. Compilation failures use
  `execute_code_compilation_failed` and return `userCodeExecuted=false`, so
  callers can distinguish invalid C# from bridge or runtime failures.
- `editor/execute-code` always returns a persistent Job. Poll `jobs/get`; use
  `jobs/cancel` before execution or between incremental steps; and use
  `jobs/cleanup` only when the Job reports an available cleanup contract.
  Idempotency keys are project-scoped recovery capabilities: an exact retry
  after an MCP reconnect returns the original Job and access token, while
  reusing a key with different arguments is rejected.
- `jobs/cancel` reports success when the target accepts cancellation, including
  workflows that reach `canceled` immediately. Read the terminal outcome through
  `jobs/get`; cancellation acceptance is not returned as a command failure.
- History-backed refresh, build, Test Runner, package-test, memory-snapshot,
  Addressables, and Unity-package-import Jobs return `jobId`, `jobType`, and a
  durable `jobAccessToken`. The originating agent can poll normally; after an MCP
  reconnect, pass the token to `jobs/get` or `jobs/cancel`. `jobs/list` and public
  history snapshots never return the token.
- `jobs/get` and `testing/get-job` report a successful snapshot read independently
  from the observed job outcome. Inspect `status` and `error` for `failed` or
  `canceled` jobs; those outcomes do not turn polling into a queue-command failure
  or overwrite the business status. Package-test starts return `jobId` plus
  `jobType="package-test"`; poll that identity through `jobs/get`.
- `testing/get-package-job` is the specialized inspection and terminal-state
  cleanup route for the current package-test workflow. It accepts the same
  `jobId` and access token, but is not a second public job identity or the normal
  polling path.
- Job capability and positive state flags use the same presence-only `tags`
  contract (`incrementalJob`, `cleanupDeclared`, `cleanupAvailable`,
  `cancellationRequested`, and `reused`). Fields that do not yet have a value
  are omitted; `status`, timestamps, progress, results, and errors remain
  explicit runtime facts.
- The transport applies that rule uniformly to specialized build, test,
  package-test, import, refresh, and profiler workflows. Poll-route aliases,
  status-derived booleans, idle compilation diagnostics, and false lifecycle
  markers are omitted. Business facts whose false value is meaningful—such as
  `valid`, `visible`, `found`, or `fileExists`—remain explicit.
- Editor process state uses the same presence vocabulary (`playing`, `paused`,
  `compiling`, `updating`, `changingPlayMode`, and `idle`). The composite
  `isPlayingOrWillChangePlaymode` alias and successful wait-configuration
  echoes are not sent. Every `editor/state` snapshot contains at least one
  process-state tag: a stable Editor reports `idle`, including while Play Mode
  itself is stable; missing positive tags mean those states are false.
- Execute-code uses compact Unity value strings by default. Pass
  `unityStructFormat="structured"` for typed objects with stable fields.
- Completed pagination aliases and exact duplicate counts are removed on the
  wire. A sole empty primary collection is preserved so a zero-match result
  cannot disappear from a completed ticket.
- Prefab mutations return semantic results without YAML diffs initially.
  Request `includePrefabFileDiff=true` or enable the personal preference when
  line detail is needed.
- `prefab-asset/add-component` accepts an optional `properties` map and applies
  it before the first save. Success is returned only after serialized
  readback confirms both the new component and its requested initial values;
  use `prefab-asset/configure-component` for idempotent ensure/update work or
  ObjectReference wiring. Pass `createPathIfMissing=true` when the component's
  semantic GameObject path does not exist yet; the path and component are then
  created and configured in one rollback-capable transaction.
- All Prefab asset mutations use one authoring-session owner. Serialized views
  close before save, the isolated authoring root unloads before YAML
  normalization, import, or persistent readback, and only the adopted product
  can commit. Failures restore the exact original bytes through the same atomic
  file publisher and synchronously re-adopt them before returning.
- When `referenceAssetPath` contains more than one compatible object, Prefab
  reference routes require `referenceSubAssetName` or the lossless decimal
  string `referenceSubAssetLocalId`. Ambiguous paths fail with bounded candidate
  details instead of silently assigning the first imported subasset.
- Sprite sheet slicing replaces the complete Sprite name-fileID mapping and
  reserializes importer metadata. Renaming or shrinking a sheet therefore does
  not leave stale subasset names or removed frames in the texture `.meta` file.
- The `pixel-sprite` importer preset preserves an existing Single/Multiple
  Sprite mode. Use `texture/set-import`, a reference importer, or a slicing
  route when changing that structural mode is intentional.
- Potentially large raw graph, stack, serialized, and metadata diagnostics are
  opt-in.
- Mutating requests validate the expected project before dispatch. Stable
  idempotency keys, non-reused ticket identities, and persistent queue snapshots
  protect reload-sensitive workflows. The allocator high-water survives even
  when replay-safe read ticket payloads are intentionally discarded.
- MCP-owned durable state uses one path-keyed publication owner. Writers publish
  complete immutable text or binary snapshots through private files, disk flush,
  and atomic replacement; target and backup adoption is serialized with readers,
  deletes, and bounded operating-system lease handoff. Queue, Job, refresh,
  build, import, package-test, history, Addressables, and instance-registry state
  therefore share one failure-closed persistence contract.
- HTTP listener startup is transactional: partial binds and registrations are
  rolled back before retry. Shutdown publishes stopped state first, closes the
  exact listener generation, and tolerates clients disconnecting while a
  response is in flight. Project paths compare case-insensitively on Windows and
  preserve casing on Linux and macOS.
- Use `unity_wait_editor_idle` after compilation, package changes, refreshes, or
  domain reloads before issuing dependent work.

## Development and validation

The package contains EditMode regression tests for route authority, metadata,
schemas, configuration precedence, queue/reload behavior, response compaction,
and tool families. Use the persistent `testing/run-package-tests` workflow when
testing a Git package inside a consumer project. With VM Unity MCP and no
`testNames`, `categories`, or `groupNames`, this workflow runs the curated
`VMUnityMCP.PackageSmoke` category. Request
`categories: ["VMUnityMCP.FullRegression"]` only when the complete integration
suite is warranted; exact names, fixture groups, and other categories remain
available for focused validation. The start response contains a `jobId` and
`jobType="package-test"` plus a `jobAccessToken`; poll that identity with
`jobs/get`, supplying the token after an MCP reconnect, until a terminal status
reports `manifestRestored` or the explicit `manifestRestoreFailed` outcome. The
workflow refuses to replace a malformed pre-existing `testables` value and leaves
externally changed manifest bytes untouched during restoration.

When tool metadata changes, synchronize and test the companion Node server,
then reconnect the MCP client before judging its cached concrete tool list.

## License and related projects

- [VM Unity MCP server](https://github.com/VM233/unity-mcp-server)
- [Original Unity MCP Plugin](https://github.com/AnkleBreaker-Studio/unity-mcp-plugin)
- [Original Unity MCP Server](https://github.com/AnkleBreaker-Studio/unity-mcp-server)

See [LICENSE](LICENSE) for the complete terms and attribution.
