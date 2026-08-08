using System;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    internal sealed class AssetDeletePersistenceProcessor : AssetModificationProcessor
    {
        internal static string WatchedAssetPath;
        internal static AnimationClip Target;

        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            if (Target != null && string.Equals(assetPath, WatchedAssetPath, StringComparison.Ordinal))
            {
                Target.frameRate = 17f;
                EditorUtility.SetDirty(Target);
            }

            return AssetDeleteResult.DidNotDelete;
        }

        internal static void Reset()
        {
            WatchedAssetPath = null;
            Target = null;
        }
    }
}
