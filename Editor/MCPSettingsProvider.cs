using System.Collections.Generic;
using UnityEditor;

namespace UnityMCP.Editor
{
    internal static class MCPSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateUserPreferencesProvider()
        {
            return new SettingsProvider(MCPSettingsGUI.UserPreferencesPath, SettingsScope.User)
            {
                label = "Unity MCP",
                guiHandler = _ => MCPSettingsGUI.DrawUserPreferences(true),
                keywords = new HashSet<string>
                {
                    "Unity",
                    "MCP",
                    "port",
                    "port range",
                    "auto-start",
                    "virtual players",
                    "result limit",
                    "prefab diff",
                    "action history",
                    "job history",
                    "categories",
                    "preferences"
                }
            };
        }

        [SettingsProvider]
        public static SettingsProvider CreateProjectSettingsProvider()
        {
            return new SettingsProvider(MCPSettingsGUI.ProjectSettingsPath, SettingsScope.Project)
            {
                label = "Unity MCP",
                guiHandler = _ => MCPSettingsGUI.DrawProjectSettings(true),
                keywords = new HashSet<string>
                {
                    "Unity",
                    "MCP",
                    "execute code",
                    "namespace",
                    "usings",
                    "UI Toolkit",
                    "audit",
                    "tooltip",
                    "context",
                    "physics",
                    "2D",
                    "3D",
                    "screenshot",
                    "tool defaults"
                }
            };
        }
    }
}
