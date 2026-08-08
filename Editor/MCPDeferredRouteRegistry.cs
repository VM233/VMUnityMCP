using System;
using System.Collections.Generic;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Executable registry and single source of truth for callback-based routes.
    /// Kept outside MCPBridgeServer so metadata discovery never initializes the
    /// HTTP listener as a side effect.
    /// </summary>
    internal static class MCPDeferredRouteRegistry
    {
        internal delegate void Handler(Dictionary<string, object> args, Action<object> resolve,
            Action<object> progress);

        private static readonly Dictionary<string, Handler> Routes =
            new Dictionary<string, Handler>(StringComparer.Ordinal)
            {
                { "testing/list-tests", (args, resolve, _) => MCPTestRunnerCommands.ListTests(args, resolve) },
                { "wait/editor-idle", (args, resolve, _) => MCPEditorCommands.WaitForIdle(args, resolve) },
                { "editor/play-mode", (args, resolve, _) => MCPEditorCommands.SetPlayMode(args, resolve) },
                { "uitoolkit/refresh", (args, resolve, _) => MCPUICommands.RefreshUIToolkit(args, resolve) },
                {
                    "uitoolkit/builder-preview",
                    (args, resolve, _) => MCPUICommands.OpenUIBuilderPreview(args, resolve)
                },
                { "screenshot/game", (args, resolve, _) => MCPGameViewCaptureCommands.CaptureGameView(args, resolve) },
                {
                    "packages/update-git",
                    (args, resolve, _) => MCPPackageManagerCommands.UpdateGitPackageDeferred(args, resolve)
                },
                {
                    "packages/list",
                    (args, resolve, _) => MCPPackageManagerCommands.ListPackagesDeferred(args, resolve)
                },
                {
                    "packages/add",
                    (args, resolve, _) => MCPPackageManagerCommands.AddPackageDeferred(args, resolve)
                },
                {
                    "packages/remove",
                    (args, resolve, _) => MCPPackageManagerCommands.RemovePackageDeferred(args, resolve)
                },
                {
                    "packages/search",
                    (args, resolve, _) => MCPPackageManagerCommands.SearchPackageDeferred(args, resolve)
                },
                {
                    "profiler/memory-snapshot",
                    (args, resolve, _) => MCPMemoryProfilerCommands.TakeMemorySnapshot(args, resolve)
                },
                { "prefab-asset/add-component", MCPPrefabAssetCommands.AddComponentDeferred },
                { "prefab-asset/configure-component", MCPPrefabAssetCommands.ConfigureComponentDeferred },
                { "prefab-asset/transaction-edit", MCPPrefabAssetCommands.TransactionEditDeferred },
                { "asset/import", MCPAssetCommands.ImportDeferred },
                { "asset/move", MCPAssetCommands.MoveDeferred },
                { "component/set-reference", MCPComponentCommands.SetReferencesDeferred },
                {
                    "localization/upsert-entry",
                    (args, resolve, progress) =>
                        MCPLocalizationBridge.ExecuteDeferred("localization/upsert-entry", args, resolve, progress)
                }
            };

        internal static IEnumerable<string> RouteNames => Routes.Keys;

        internal static bool Contains(string route)
        {
            return string.IsNullOrEmpty(route) == false && Routes.ContainsKey(route);
        }

        internal static bool TryGet(string route, out Handler handler)
        {
            return Routes.TryGetValue(route, out handler);
        }
    }
}
