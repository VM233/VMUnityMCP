using System.Collections.Generic;

namespace UnityMCP.Editor
{
    internal sealed class MCPToolProfile
    {
        internal bool ReadOnly;
        internal bool MutatesAssets;
        internal bool MutatesRuntime;
        internal bool Dangerous;
        internal bool LongRunning;
        internal bool MayReloadDomain;
        internal bool RequiresPlayMode;

        internal static MCPToolProfile Create(bool readOnly = false,
            bool mutatesAssets = false, bool mutatesRuntime = false,
            bool dangerous = false, bool longRunning = false,
            bool mayReloadDomain = false, bool requiresPlayMode = false)
        {
            return new MCPToolProfile
            {
                ReadOnly = readOnly,
                MutatesAssets = mutatesAssets,
                MutatesRuntime = mutatesRuntime,
                Dangerous = dangerous,
                LongRunning = longRunning,
                MayReloadDomain = mayReloadDomain,
                RequiresPlayMode = requiresPlayMode,
            };
        }

        internal MCPToolProfile Clone()
        {
            return new MCPToolProfile
            {
                ReadOnly = ReadOnly,
                MutatesAssets = MutatesAssets,
                MutatesRuntime = MutatesRuntime,
                Dangerous = Dangerous,
                LongRunning = LongRunning,
                MayReloadDomain = MayReloadDomain,
                RequiresPlayMode = RequiresPlayMode,
            };
        }

        internal Dictionary<string, object> ToAnnotations()
        {
            var annotations = new Dictionary<string, object>();
            if (ReadOnly)
            {
                annotations["readOnlyHint"] = true;
                annotations["idempotentHint"] = true;
            }

            if (Dangerous)
                annotations["destructiveHint"] = true;
            return annotations;
        }
    }
}
