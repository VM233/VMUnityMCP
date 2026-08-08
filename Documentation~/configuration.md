# Unity MCP catalog, configuration, and ownership audit

VM Unity MCP 6 publishes 397 executable built-in routes from two authorities:
`MCPRouteRegistry` owns synchronous names and `MCPDeferredRouteRegistry` owns
callback-based names together with their handlers. Their union is executable;
the two `_meta` endpoints remain internal discovery transport and are not
advertised as callable tools.

The audited executable-route SHA-256 is
`91724eb19902998c3655d20274ac45b0c06efc92b9f6f6227a49016d4e35a81d`.
The regression suite compares this value with the live registry. Adding,
removing, or renaming a route therefore requires an explicit configuration
review instead of silently inheriting defaults.

## Canonical catalog

Paginated `_meta/tools` is the only tool catalog. It contains all available
built-in routes plus every valid direct project/package route under
`project-tools/call/<toolName>`. Optional package tools appear only when their
capability is installed. Pages are capped at 200 and publish a `nextOffset`
only when another page exists.

Every catalog entry must provide:

- one route and one typed tool name;
- category, module ID, capability, and normalized operation kind;
- a task-oriented description and search terms;
- complete input and output schemas;
- positive tags, concrete side effects, preconditions, and error codes when
  applicable.

Built-in operation kinds are the stable filter values `inspect`, `mutate`, and
`job`. The route and search terms carry the specific action. Project tools use
the same defaults unless they explicitly declare a domain operation kind.

`MCPToolProfileCatalog` explicitly assigns lifecycle and effects to every
built-in route. Duplicate declarations and missing profiles fail catalog
initialization. There is no mutating fallback. Specialized descriptions live
in `MCPToolDescriptionCatalog`; ordinary routes use an audited module/action
composer, which fails on an unknown module instead of publishing route-name
placeholder prose.

Five single-condition search routes were removed. `search/scene` is the sole
composable scene-search authority for name, component, tag, layer, and shader
criteria. The advanced executor, first-class tiers, exposure allowlists, and
project-tools list/get/execute endpoints are also removed.

## Project and package tools

`MCPProjectToolAttribute` is an authoring contract, not an exposure tier. A
valid tool declares exactly one of `ReadOnly`, `MutatesAssets`,
`MutatesRuntime`, or `MutatesProjectFiles`, plus strict input/output schemas.
It may also declare module, capability, operation kind, when/not-to-use text,
aliases, search terms, preconditions, completion evidence, side effects,
errors, cleanup, and persistent-job behavior.

When module ID is omitted, the complete path segment before `/` is preserved,
including hyphens. Capability defaults to the noun portion of the next segment
after removing a structural action prefix; for example,
`unity-mcp-tests/set-runtime-state` becomes module `unity-mcp-tests` and
capability `runtime-state`.

Schemas are recursively validated before invocation. Supported constraints
include nested objects and arrays, primitive types, bounds, patterns, `enum`,
`const`, `allOf`, `anyOf`, `oneOf`, and `not`. Catalog quality additionally
requires descriptions for request properties and item schemas for arrays.

Long-running tools and explicit `runAsJob=true` calls use the persistent Job
owner. Class tools implement `IMCPPersistentProjectTool` and return all
continuation state in `MCPProjectToolJobStep`; the bridge never relies on a
retained tool instance. Cleanup remains an explicit typed tool and capability
token contract.

## Configuration precedence

Effective values use this order:

1. explicit tool argument;
2. team-owned `ProjectSettings/UnityMCPSettings.json` value;
3. local `Preferences > Unity MCP` value;
4. built-in default.

Team settings contain only portable project choices: project context,
additional execute-code namespaces, the default Physics query dimension, and
the screenshot output directory. Local preferences own bridge startup and
ports, MPPM startup, response limits, optional Prefab diff detail, histories,
and locally enabled categories.

The optional result limit is injected only for a route with one unambiguous
primary collection and only when the caller omitted that argument. Current
consumers include catalog pages, asset/package/material/localization reads,
jobs and test results, the composable scene search, Physics queries, Prefab
hierarchy/find, profiler pages, terrain trees, texture duplicates, and UI
Toolkit queries/audits. Exact field ownership remains in
`MCPToolConfigurationPolicy` and is regression-checked against the route
manifest.

Physics dimension defaults apply only to read queries. Screenshot directory
defaults apply only to capture tools that expose a path. Prefab YAML diff
preferences apply only to routes that already publish
`includePrefabFileDiff`. Domain packages own their own settings and may reuse
the shared primary-result limit without moving domain semantics into this
bridge.

## Values that remain explicit

The following never become hidden cross-tool defaults:

- mutation operations and values, paths, selectors, object identities, scene
  modes, package refs, build targets, and test filters;
- `dryRun`, save/discard, overwrite, run/terminate, destructive confirmation,
  cleanup, and equivalent lifecycle choices;
- raw serialization, stack traces, full graph data, complete snapshots, and
  other potentially large diagnostic expansions;
- hard request/response caps, queue capacity, ownership and idempotency,
  recovery limits, and tool-specific safety maxima.

This boundary lets clients compose tools from visible contracts without
turning project or operator state into an implicit mutation API.

## Validation gates

The EditMode regression suite checks route/dispatcher parity, manifest hash,
profile exhaustiveness, catalog pagination, schema and description quality,
optional capability gating, direct project routes, normalized module and
capability metadata, strict schema enforcement, persistent jobs, and removal
of retired generic/duplicate routes.
