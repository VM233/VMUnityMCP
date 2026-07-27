using System.Collections.Generic;
using UnityEditor;

namespace UnityMCP.Editor
{
    internal static class MCPRuntimePreconditions
    {
        public static bool TryRequirePlayMode(string route, string purpose,
            out Dictionary<string, object> error)
        {
            if (EditorApplication.isPlaying)
            {
                error = null;
                return true;
            }

            error = MCPResponse.Error(
                $"{route} requires Play Mode because {purpose}.",
                "requires_play_mode",
                false,
                new Dictionary<string, object>
                {
                    { "route", route },
                    { "requiresPlayMode", true },
                    { "isPlaying", false },
                    { "isPaused", EditorApplication.isPaused },
                });
            return false;
        }
    }
}
