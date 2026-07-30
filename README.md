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

Major route families include:

- scene workspaces, GameObjects, components, Prefabs, materials, importers, and
  serialized assets;
- UI Toolkit authoring, static audits, runtime inspection, screenshots, and
  visual comparison;
- Animator, Audio Mixer, VFX Graph, Shader Graph, Timeline, Cinemachine,
  Addressables, Localization, Physics 2D/3D, terrain, lighting, and navigation;
- Console, compilation, debugger, Profiler, testing, builds, packages, jobs,
  queue diagnostics, and Editor execution.

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

Declare exactly one operation kind: `ReadOnly`, `MutatesAssets`, or
`MutatesRuntime`. Add `RequiresPlayMode`, `Dangerous`, `LongRunning`, or
`MayReloadDomain` when applicable. Strict schemas should describe every
property and reject unknown business arguments. The bridge recursively enforces
the supported JSON Schema subset before invocation, including
`allOf`/`anyOf`/`oneOf`, `not`, `const`, nested objects, and array items.
Declared output schemas are also enforced before success. Use `SideEffects` for
the concrete effect classes, `ErrorCodes` for domain failures, and
`CleanupToolName` only when the operation produces a token owned by that cleanup
tool.

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
  block remains a short human-readable summary.
- Successful bridge responses may omit redundant `success=true`; errors keep a
  stable error code and retryability.
- `editor/execute-code` reports invalid submissions and compiler diagnostics as
  non-retryable structured errors. Compilation failures use
  `execute_code_compilation_failed` and return `userCodeExecuted=false`, so
  callers can distinguish invalid C# from bridge or runtime failures.
- `editor/execute-code` always returns a persistent Job. Poll `jobs/get`; use
  `jobs/cancel` before execution or between incremental steps; and use
  `jobs/cleanup` only when the Job reports an available cleanup contract.
  Reusing an `idempotencyKey` with different arguments is rejected.
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
  ObjectReference wiring.
- Potentially large raw graph, stack, serialized, and metadata diagnostics are
  opt-in.
- Mutating requests validate the expected project before dispatch. Stable
  idempotency keys and persistent queue snapshots protect reload-sensitive
  workflows.
- Use `unity_wait_editor_idle` after compilation, package changes, refreshes, or
  domain reloads before issuing dependent work.

## Development and validation

The package contains EditMode regression tests for route authority, metadata,
schemas, configuration precedence, queue/reload behavior, response compaction,
and tool families. Use the persistent `testing/run-package-tests` workflow when
testing a Git package inside a consumer project.

When tool metadata changes, synchronize and test the companion Node server,
then reconnect the MCP client before judging its cached concrete tool list.

## License and related projects

- [VM Unity MCP server](https://github.com/VM233/unity-mcp-server)
- [Original Unity MCP Plugin](https://github.com/AnkleBreaker-Studio/unity-mcp-plugin)
- [Original Unity MCP Server](https://github.com/AnkleBreaker-Studio/unity-mcp-server)

See [LICENSE](LICENSE) for the complete terms and attribution.
