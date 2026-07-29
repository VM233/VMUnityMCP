using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class MCPSettingsGUI
    {
        public const string UserPreferencesPath = "Preferences/Unity MCP";
        public const string ProjectSettingsPath = "Project/Unity MCP";

        private static Vector2 _categoryScrollPosition;
        private static string _uiToolkitAuditWriteError = "";

        public static void DrawUserPreferences(bool showResetButton)
        {
            DrawAutoStartSettings();
            EditorGUILayout.Space(6);
            DrawPortSettings();

            if (showResetButton)
            {
                EditorGUILayout.Space(8);
                if (GUILayout.Button("Reset User Preferences to Defaults"))
                {
                    if (EditorUtility.DisplayDialog(
                        "Reset User Preferences",
                        "Reset Unity MCP user preferences to defaults?",
                        "Reset",
                        "Cancel"))
                    {
                        MCPSettingsManager.ResetUserPreferencesToDefaults();
                    }
                }
            }
        }

        public static void DrawProjectSettings(bool showResetButton)
        {
            DrawProjectStartupSettings();
            EditorGUILayout.Space(8);
            DrawExecuteCodeSettings();
            EditorGUILayout.Space(8);
            DrawUIToolkitAuditSettings();
            EditorGUILayout.Space(8);
            DrawProjectContextSettings();
            EditorGUILayout.Space(8);
            DrawActionHistorySettings();
            EditorGUILayout.Space(8);
            DrawCategorySettings();

            if (showResetButton)
            {
                EditorGUILayout.Space(8);
                if (GUILayout.Button("Reset Project Settings to Defaults"))
                {
                    if (EditorUtility.DisplayDialog(
                        "Reset Project Settings",
                        "Reset Unity MCP project settings to defaults?",
                        "Reset",
                        "Cancel"))
                    {
                        MCPSettingsManager.ResetProjectSettingsToDefaults();
                        ResetUIToolkitAuditSettings();
                    }
                }
            }
        }

        private static void DrawUIToolkitAuditSettings()
        {
            EditorGUILayout.LabelField("UI Toolkit Audit", EditorStyles.boldLabel);

            MCPUIToolkitAuditProjectSettings settings =
                MCPUIToolkitAuditProjectSettings.Load();
            if (settings.Valid == false)
            {
                EditorGUILayout.HelpBox(
                    $"{MCPUIToolkitAuditProjectSettings.ConfigPath}: {settings.Error}",
                    MessageType.Error);
                return;
            }

            bool enabled = EditorGUILayout.Toggle(
                new GUIContent(
                    "Audit UXML Tooltip Attributes",
                    "Report tooltip attributes authored directly in UXML. Enabled by default."),
                settings.UxmlTooltipAttributes);
            if (enabled != settings.UxmlTooltipAttributes)
            {
                settings.UxmlTooltipAttributes = enabled;
                TrySaveUIToolkitAuditSettings(settings);
            }

            if (string.IsNullOrEmpty(_uiToolkitAuditWriteError) == false)
                EditorGUILayout.HelpBox(_uiToolkitAuditWriteError, MessageType.Error);
        }

        private static void ResetUIToolkitAuditSettings()
        {
            var settings = MCPUIToolkitAuditProjectSettings.Load();
            if (settings.Valid == false)
            {
                _uiToolkitAuditWriteError =
                    $"{MCPUIToolkitAuditProjectSettings.ConfigPath}: {settings.Error}";
                return;
            }

            settings.UxmlTooltipAttributes = true;
            TrySaveUIToolkitAuditSettings(settings);
        }

        private static void TrySaveUIToolkitAuditSettings(
            MCPUIToolkitAuditProjectSettings settings)
        {
            try
            {
                settings.Save();
                _uiToolkitAuditWriteError = "";
            }
            catch (System.Exception exception)
            {
                _uiToolkitAuditWriteError =
                    $"Failed to save {MCPUIToolkitAuditProjectSettings.ConfigPath}: " +
                    exception.Message;
            }
        }

        public static void DrawProjectStartupSettings()
        {
            EditorGUILayout.LabelField("Multiplayer Play Mode (MPPM)", EditorStyles.boldLabel);

            bool startOnVirtualPlayers = EditorGUILayout.Toggle(
                new GUIContent(
                    "Start on Virtual Players",
                    "When off, the MCP bridge does not auto-start on Multiplayer Play Mode virtual players. Manual start still works."),
                MCPSettingsManager.StartOnVirtualPlayers);

            if (startOnVirtualPlayers != MCPSettingsManager.StartOnVirtualPlayers)
                MCPSettingsManager.StartOnVirtualPlayers = startOnVirtualPlayers;
        }

        private static void DrawExecuteCodeSettings()
        {
            EditorGUILayout.LabelField("Execute Code", EditorStyles.boldLabel);

            var label = new GUIContent(
                "Additional Namespaces",
                "One namespace per line. Each namespace is imported by every unity_execute_code compilation.");
            string configured = MCPSettingsManager.ExecuteCodeAdditionalNamespacesText;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            string updated = EditorGUILayout.TextArea(configured, GUILayout.MinHeight(54));
            EditorGUILayout.EndHorizontal();

            if (updated != configured)
                MCPSettingsManager.ExecuteCodeAdditionalNamespacesText = updated;

            foreach (string namespaceName in MCPSettingsManager.GetExecuteCodeAdditionalNamespaces())
            {
                if (MCPEditorCommands.IsValidNamespace(namespaceName))
                    continue;

                EditorGUILayout.HelpBox(
                    $"'{namespaceName}' is not a valid C# namespace.",
                    MessageType.Warning);
                break;
            }
        }

        private static void DrawAutoStartSettings()
        {
            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);

            bool autoStart = EditorGUILayout.Toggle(
                "Auto-start on Editor Load",
                MCPSettingsManager.AutoStart);

            if (autoStart != MCPSettingsManager.AutoStart)
                MCPSettingsManager.AutoStart = autoStart;
        }

        private static void DrawPortSettings()
        {
            EditorGUILayout.LabelField("Port", EditorStyles.boldLabel);

            bool useManualPort = EditorGUILayout.Toggle(
                "Use Manual Port",
                MCPSettingsManager.UseManualPort);

            if (useManualPort != MCPSettingsManager.UseManualPort)
                MCPSettingsManager.UseManualPort = useManualPort;

            if (useManualPort)
            {
                int port = EditorGUILayout.IntField("Server Port", MCPSettingsManager.Port);
                bool validPort = port > 1024 && port < 65536;

                if (validPort && port != MCPSettingsManager.Port)
                    MCPSettingsManager.Port = port;

                if (!validPort)
                    EditorGUILayout.HelpBox("Port must be between 1025 and 65535.", MessageType.Warning);

                if (MCPBridgeServer.IsRunning && MCPBridgeServer.ActivePort != MCPSettingsManager.Port)
                    EditorGUILayout.HelpBox("Restart server to apply port change.", MessageType.Info);
            }
            else
            {
                string autoInfo = MCPBridgeServer.IsRunning
                    ? $"Auto-selected port {MCPBridgeServer.ActivePort} (range: {MCPInstanceRegistry.PortRangeStart}-{MCPInstanceRegistry.PortRangeEnd})"
                    : $"Will auto-select from range {MCPInstanceRegistry.PortRangeStart}-{MCPInstanceRegistry.PortRangeEnd}";
                EditorGUILayout.HelpBox(autoInfo, MessageType.None);
            }
        }

        private static void DrawProjectContextSettings()
        {
            EditorGUILayout.LabelField("Project Context", EditorStyles.boldLabel);

            bool contextEnabled = EditorGUILayout.Toggle(
                "Enable Context",
                MCPSettingsManager.ContextEnabled);

            if (contextEnabled != MCPSettingsManager.ContextEnabled)
                MCPSettingsManager.ContextEnabled = contextEnabled;

            string contextPath = EditorGUILayout.TextField(
                "Context Path",
                MCPSettingsManager.ContextPath);

            if (contextPath != MCPSettingsManager.ContextPath)
                MCPSettingsManager.ContextPath = contextPath;
        }

        private static void DrawActionHistorySettings()
        {
            EditorGUILayout.LabelField("Action History", EditorStyles.boldLabel);

            bool persistence = EditorGUILayout.Toggle(
                "Persist Action History",
                MCPSettingsManager.ActionHistoryPersistence);

            if (persistence != MCPSettingsManager.ActionHistoryPersistence)
                MCPSettingsManager.ActionHistoryPersistence = persistence;

            int maxEntries = EditorGUILayout.IntField(
                "Max Entries",
                MCPSettingsManager.ActionHistoryMaxEntries);

            maxEntries = Mathf.Max(1, maxEntries);
            if (maxEntries != MCPSettingsManager.ActionHistoryMaxEntries)
                MCPSettingsManager.ActionHistoryMaxEntries = maxEntries;
        }

        private static void DrawCategorySettings()
        {
            EditorGUILayout.LabelField("Tool Categories", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Enable All"))
                SetAllCategories(true);
            if (GUILayout.Button("Disable All"))
                SetAllCategories(false);
            EditorGUILayout.EndHorizontal();

            _categoryScrollPosition = EditorGUILayout.BeginScrollView(
                _categoryScrollPosition,
                GUILayout.Height(180));

            foreach (string category in MCPSettingsManager.GetAllCategoryNames())
            {
                bool enabled = MCPSettingsManager.IsCategoryEnabled(category);
                bool newEnabled = EditorGUILayout.ToggleLeft(category, enabled);

                if (newEnabled != enabled)
                    MCPSettingsManager.SetCategoryEnabled(category, newEnabled);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void SetAllCategories(bool enabled)
        {
            foreach (string category in MCPSettingsManager.GetAllCategoryNames())
                MCPSettingsManager.SetCategoryEnabled(category, enabled);
        }
    }
}
