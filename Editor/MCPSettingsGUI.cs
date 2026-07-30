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
        private static string _projectSettingsWriteError = "";

        public static void DrawUserPreferences(bool showResetButton)
        {
            DrawAutoStartSettings();
            EditorGUILayout.Space(6);
            DrawPortSettings();
            EditorGUILayout.Space(8);
            DrawResponseSettings();
            EditorGUILayout.Space(8);
            DrawActionHistorySettings();
            EditorGUILayout.Space(8);
            DrawCategorySettings();

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
            if (!DrawProjectSettingsStorage())
                return;

            DrawExecuteCodeSettings();
            EditorGUILayout.Space(8);
            DrawUIToolkitAuditSettings();
            EditorGUILayout.Space(8);
            DrawProjectContextSettings();
            EditorGUILayout.Space(8);
            DrawToolDefaultSettings();

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

        private static bool DrawProjectSettingsStorage()
        {
            EditorGUILayout.LabelField("Storage", EditorStyles.boldLabel);
            var settings = MCPSettingsManager.GetProjectConfiguration();
            if (!settings.Valid)
            {
                EditorGUILayout.HelpBox(
                    $"{MCPProjectConfiguration.ConfigPath}: {settings.Error}",
                    MessageType.Error);
                if (GUILayout.Button("Replace Invalid Project Settings with Defaults") &&
                    EditorUtility.DisplayDialog(
                        "Replace Invalid Project Settings",
                        $"Replace {MCPProjectConfiguration.ConfigPath} with Unity MCP defaults?",
                        "Replace",
                        "Cancel"))
                {
                    MCPSettingsManager.ResetProjectSettingsToDefaults();
                    _projectSettingsWriteError = "";
                }
                return false;
            }

            string message = settings.Found
                ? $"Team settings are stored in {MCPProjectConfiguration.ConfigPath}."
                : $"Changing a team setting will create {MCPProjectConfiguration.ConfigPath}. Existing local values are migrated on first write.";
            EditorGUILayout.HelpBox(message, MessageType.Info);
            if (!string.IsNullOrEmpty(_projectSettingsWriteError))
                EditorGUILayout.HelpBox(_projectSettingsWriteError, MessageType.Error);
            EditorGUILayout.Space(8);
            return true;
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
            {
                TryUpdateProjectSetting(() =>
                    MCPSettingsManager.ExecuteCodeAdditionalNamespacesText = updated);
            }

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

            bool startOnVirtualPlayers = EditorGUILayout.Toggle(
                new GUIContent(
                    "Start on Virtual Players",
                    "When off, the MCP bridge does not auto-start on Multiplayer Play Mode virtual players. Manual start still works."),
                MCPSettingsManager.StartOnVirtualPlayers);

            if (startOnVirtualPlayers != MCPSettingsManager.StartOnVirtualPlayers)
                MCPSettingsManager.StartOnVirtualPlayers = startOnVirtualPlayers;
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
                MCPSettingsManager.GetAutoPortRange(
                    out int configuredStart, out int configuredEnd);
                int rangeStart = EditorGUILayout.IntField(
                    "Auto Range Start", configuredStart);
                int rangeEnd = EditorGUILayout.IntField(
                    "Auto Range End", configuredEnd);
                bool validRange = rangeStart > 1024 &&
                                  rangeStart < 65536 &&
                                  rangeEnd > 1024 &&
                                  rangeEnd < 65536 &&
                                  rangeEnd >= rangeStart;
                if (validRange &&
                    (rangeStart != configuredStart ||
                     rangeEnd != configuredEnd))
                {
                    MCPSettingsManager.SetAutoPortRange(rangeStart, rangeEnd);
                }
                if (!validRange)
                {
                    EditorGUILayout.HelpBox(
                        "The automatic port range must stay between 1025 and 65535, with the end at or above the start.",
                        MessageType.Warning);
                }

                string autoInfo = MCPBridgeServer.IsRunning
                    ? $"Auto-selected port {MCPBridgeServer.ActivePort} (range: {MCPInstanceRegistry.PortRangeStart}-{MCPInstanceRegistry.PortRangeEnd})"
                    : $"Will auto-select from range {MCPInstanceRegistry.PortRangeStart}-{MCPInstanceRegistry.PortRangeEnd}";
                EditorGUILayout.HelpBox(autoInfo, MessageType.None);
                if (MCPBridgeServer.IsRunning &&
                    (rangeStart != configuredStart ||
                     rangeEnd != configuredEnd))
                {
                    EditorGUILayout.HelpBox(
                        "Restart the server to apply the automatic port range.",
                        MessageType.Info);
                }
            }
        }

        private static void DrawResponseSettings()
        {
            EditorGUILayout.LabelField("Tool Responses", EditorStyles.boldLabel);

            bool overrideLimit = EditorGUILayout.Toggle(
                new GUIContent(
                    "Override Result Defaults",
                    "Use one personal default for tools with a single primary paginated or bounded result collection. Explicit request values still win."),
                MCPSettingsManager.OverrideDefaultResultLimit);
            if (overrideLimit != MCPSettingsManager.OverrideDefaultResultLimit)
                MCPSettingsManager.OverrideDefaultResultLimit = overrideLimit;

            using (new EditorGUI.DisabledScope(!overrideLimit))
            {
                int limit = EditorGUILayout.IntField(
                    "Default Result Limit",
                    MCPSettingsManager.DefaultResultLimit);
                limit = Mathf.Clamp(limit, 1, 500);
                if (limit != MCPSettingsManager.DefaultResultLimit)
                    MCPSettingsManager.DefaultResultLimit = limit;
            }

            bool includePrefabDiff = EditorGUILayout.Toggle(
                new GUIContent(
                    "Include Prefab YAML Diffs",
                    "Include potentially large before/after prefab file diffs when a request does not explicitly choose. Disabled by default; semantic operation results are still returned."),
                MCPSettingsManager.IncludePrefabFileDiffByDefault);
            if (includePrefabDiff !=
                MCPSettingsManager.IncludePrefabFileDiffByDefault)
            {
                MCPSettingsManager.IncludePrefabFileDiffByDefault =
                    includePrefabDiff;
            }
        }

        private static void DrawProjectContextSettings()
        {
            EditorGUILayout.LabelField("Project Context", EditorStyles.boldLabel);

            bool contextEnabled = EditorGUILayout.Toggle(
                "Enable Context",
                MCPSettingsManager.ContextEnabled);

            if (contextEnabled != MCPSettingsManager.ContextEnabled)
            {
                TryUpdateProjectSetting(() =>
                    MCPSettingsManager.ContextEnabled = contextEnabled);
            }

            string contextPath = EditorGUILayout.TextField(
                "Context Path",
                MCPSettingsManager.ContextPath);

            if (contextPath != MCPSettingsManager.ContextPath)
            {
                TryUpdateProjectSetting(() =>
                    MCPSettingsManager.ContextPath = contextPath);
            }
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

            int jobMaxEntries = EditorGUILayout.IntField(
                "Job History Max Entries",
                MCPSettingsManager.JobHistoryMaxEntries);
            jobMaxEntries = Mathf.Clamp(jobMaxEntries, 20, 2000);
            if (jobMaxEntries != MCPSettingsManager.JobHistoryMaxEntries)
                MCPSettingsManager.JobHistoryMaxEntries = jobMaxEntries;
        }

        private static void DrawToolDefaultSettings()
        {
            EditorGUILayout.LabelField("Tool Defaults", EditorStyles.boldLabel);

            string dimension = MCPSettingsManager.DefaultPhysicsDimension;
            int dimensionIndex = string.Equals(
                dimension, "2D", System.StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0;
            int updatedDimensionIndex = EditorGUILayout.Popup(
                new GUIContent(
                    "Physics Dimension",
                    "Used by raycast and overlap tools only when dimension is omitted. Explicit request values always win."),
                dimensionIndex,
                new[] { "3D", "2D" });
            if (updatedDimensionIndex != dimensionIndex)
            {
                string updatedDimension =
                    updatedDimensionIndex == 1 ? "2D" : "3D";
                TryUpdateProjectSetting(() =>
                    MCPSettingsManager.DefaultPhysicsDimension =
                        updatedDimension);
            }

            string screenshotDirectory =
                MCPSettingsManager.ScreenshotOutputDirectory;
            string updatedScreenshotDirectory = EditorGUILayout.TextField(
                new GUIContent(
                    "Screenshot Directory",
                    "Project-relative default folder for Game View, Scene View, Editor Window, and UI Builder captures."),
                screenshotDirectory);
            if (updatedScreenshotDirectory != screenshotDirectory)
            {
                TryUpdateProjectSetting(() =>
                    MCPSettingsManager.ScreenshotOutputDirectory =
                        updatedScreenshotDirectory);
            }
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

        private static void TryUpdateProjectSetting(System.Action update)
        {
            try
            {
                update();
                _projectSettingsWriteError = "";
            }
            catch (System.Exception exception)
            {
                _projectSettingsWriteError = exception.GetBaseException().Message;
            }
        }
    }
}
