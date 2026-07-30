using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Unity MCP configuration facade.
    ///
    /// Precedence is: explicit tool argument, ProjectSettings team default,
    /// user preference, built-in default. Safety caps and destructive choices
    /// are intentionally not configurable.
    /// </summary>
    public static class MCPSettingsManager
    {
        private const string GlobalUserPrefix = "UnityMCP_user_v2_";
        private static readonly HashSet<string> UngatedCategories =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "_meta", "agents", "ping", "queue"
            };

        private static string _instancePrefix;
        private static string _projectUserPrefix;
        private static string[] _allCategories;
        private static Dictionary<string, bool> _enabledCategories;
        private static MCPProjectConfiguration _projectConfiguration;
        private static bool _projectConfigurationFileExists;
        private static long _projectConfigurationWriteTicks;

        private static string InstancePrefix
        {
            get
            {
                if (_instancePrefix != null)
                    return _instancePrefix;

                string projectPath = GetProjectPath();
                _instancePrefix = $"UnityMCP_inst_v2_{StableHash(projectPath)}_";
                string legacyPrefix = $"UnityMCP_inst_{projectPath.GetHashCode():X8}_";
                MigrateLegacyInt(legacyPrefix, _instancePrefix, "Port");
                MigrateLegacyBool(legacyPrefix, _instancePrefix, "UseManualPort");
                MigrateLegacyBool(legacyPrefix, _instancePrefix, "AutoStart");
                return _instancePrefix;
            }
        }

        /// <summary>
        /// Per-user, per-project settings shared by the main Editor and clones.
        /// These remain EditorPrefs because they describe a local workflow rather
        /// than a team-owned project contract.
        /// </summary>
        private static string ProjectUserPrefix
        {
            get
            {
                if (_projectUserPrefix != null)
                    return _projectUserPrefix;

                string guid = PlayerSettings.productGUID.ToString("N");
                if (string.IsNullOrEmpty(guid) ||
                    guid == "00000000000000000000000000000000")
                    guid = "path" + StableHash(GetProjectPath());
                _projectUserPrefix = $"UnityMCP_proj_{guid}_";
                return _projectUserPrefix;
            }
        }

        // ─── Connection preferences ───

        public static int Port
        {
            get => EditorPrefs.GetInt(InstancePrefix + "Port", 7890);
            set => EditorPrefs.SetInt(InstancePrefix + "Port", value);
        }

        public static bool UseManualPort
        {
            get => EditorPrefs.GetBool(InstancePrefix + "UseManualPort", false);
            set => EditorPrefs.SetBool(InstancePrefix + "UseManualPort", value);
        }

        public static bool AutoStart
        {
            get => EditorPrefs.GetBool(InstancePrefix + "AutoStart", true);
            set => EditorPrefs.SetBool(InstancePrefix + "AutoStart", value);
        }

        public static int AutoPortRangeStart
        {
            get => ClampPort(EditorPrefs.GetInt(
                GlobalUserPrefix + "AutoPortRangeStart", 7890));
            set => EditorPrefs.SetInt(
                GlobalUserPrefix + "AutoPortRangeStart", ClampPort(value));
        }

        public static int AutoPortRangeEnd
        {
            get => ClampPort(EditorPrefs.GetInt(
                GlobalUserPrefix + "AutoPortRangeEnd", 7899));
            set => EditorPrefs.SetInt(
                GlobalUserPrefix + "AutoPortRangeEnd", ClampPort(value));
        }

        public static void SetAutoPortRange(int start, int end)
        {
            start = ClampPort(start);
            end = ClampPort(end);
            if (end < start)
                end = start;
            AutoPortRangeStart = start;
            AutoPortRangeEnd = end;
        }

        public static void GetAutoPortRange(out int start, out int end)
        {
            start = AutoPortRangeStart;
            end = Math.Max(start, AutoPortRangeEnd);
        }

        public static bool StartOnVirtualPlayers
        {
            get => EditorPrefs.GetBool(
                ProjectUserPrefix + "StartOnVirtualPlayers", false);
            set => EditorPrefs.SetBool(
                ProjectUserPrefix + "StartOnVirtualPlayers", value);
        }

        // ─── Response and local-history preferences ───

        public static bool OverrideDefaultResultLimit
        {
            get => EditorPrefs.GetBool(
                GlobalUserPrefix + "OverrideDefaultResultLimit", false);
            set => EditorPrefs.SetBool(
                GlobalUserPrefix + "OverrideDefaultResultLimit", value);
        }

        public static int DefaultResultLimit
        {
            get => Math.Max(1, Math.Min(500, EditorPrefs.GetInt(
                GlobalUserPrefix + "DefaultResultLimit", 100)));
            set => EditorPrefs.SetInt(
                GlobalUserPrefix + "DefaultResultLimit",
                Math.Max(1, Math.Min(500, value)));
        }

        public static bool IncludePrefabFileDiffByDefault
        {
            get => EditorPrefs.GetBool(
                GlobalUserPrefix + "IncludePrefabFileDiffByDefault", false);
            set => EditorPrefs.SetBool(
                GlobalUserPrefix + "IncludePrefabFileDiffByDefault", value);
        }

        public static bool ActionHistoryPersistence
        {
            get => EditorPrefs.GetBool(
                ProjectUserPrefix + "ActionHistoryPersistence", false);
            set => EditorPrefs.SetBool(
                ProjectUserPrefix + "ActionHistoryPersistence", value);
        }

        public static int ActionHistoryMaxEntries
        {
            get => Math.Max(1, Math.Min(10000, EditorPrefs.GetInt(
                ProjectUserPrefix + "ActionHistoryMaxEntries", 500)));
            set => EditorPrefs.SetInt(
                ProjectUserPrefix + "ActionHistoryMaxEntries",
                Math.Max(1, Math.Min(10000, value)));
        }

        public static int JobHistoryMaxEntries
        {
            get => Math.Max(20, Math.Min(2000, EditorPrefs.GetInt(
                GlobalUserPrefix + "JobHistoryMaxEntries", 200)));
            set => EditorPrefs.SetInt(
                GlobalUserPrefix + "JobHistoryMaxEntries",
                Math.Max(20, Math.Min(2000, value)));
        }

        // ─── Team-owned ProjectSettings ───

        public static bool ContextEnabled
        {
            get
            {
                var settings = GetProjectConfiguration();
                return settings.Found
                    ? settings.Valid && settings.ContextEnabled
                    : EditorPrefs.GetBool(
                        ProjectUserPrefix + "ContextEnabled", true);
            }
            set => UpdateProjectConfiguration(settings =>
                settings.ContextEnabled = value);
        }

        public static string ContextPath
        {
            get
            {
                var settings = GetProjectConfiguration();
                return settings.Found
                    ? settings.Valid
                        ? settings.ContextPath
                        : MCPProjectConfiguration.DefaultContextPath
                    : EditorPrefs.GetString(
                        ProjectUserPrefix + "ContextPath",
                        MCPProjectConfiguration.DefaultContextPath);
            }
            set => UpdateProjectConfiguration(settings =>
                settings.ContextPath = value);
        }

        public static string ExecuteCodeAdditionalNamespacesText
        {
            get
            {
                var settings = GetProjectConfiguration();
                if (settings.Found)
                {
                    return settings.Valid
                        ? string.Join(Environment.NewLine,
                            settings.ExecuteCodeAdditionalNamespaces)
                        : "";
                }
                return EditorPrefs.GetString(
                    ProjectUserPrefix + "ExecuteCodeAdditionalNamespaces", "");
            }
            set
            {
                string configured = value ?? "";
                UpdateProjectConfiguration(settings =>
                {
                    settings.ExecuteCodeAdditionalNamespaces.Clear();
                    settings.ExecuteCodeAdditionalNamespaces.AddRange(
                        SplitNamespaces(configured));
                });
            }
        }

        public static string DefaultPhysicsDimension
        {
            get
            {
                var settings = GetProjectConfiguration();
                return settings.Found && settings.Valid
                    ? settings.PhysicsDimension
                    : MCPProjectConfiguration.DefaultPhysicsDimension;
            }
            set => UpdateProjectConfiguration(settings =>
                settings.PhysicsDimension = value);
        }

        public static string ScreenshotOutputDirectory
        {
            get
            {
                var settings = GetProjectConfiguration();
                return settings.Found && settings.Valid
                    ? settings.ScreenshotDirectory
                    : MCPProjectConfiguration.DefaultScreenshotDirectory;
            }
            set => UpdateProjectConfiguration(settings =>
                settings.ScreenshotDirectory = value);
        }

        public static string CreateDefaultScreenshotPath(string prefix)
        {
            string safePrefix = string.IsNullOrWhiteSpace(prefix)
                ? "Capture"
                : new string(prefix.Where(character =>
                    char.IsLetterOrDigit(character) ||
                    character == '-' ||
                    character == '_').ToArray());
            if (string.IsNullOrEmpty(safePrefix))
                safePrefix = "Capture";
            return ScreenshotOutputDirectory.TrimEnd('/') + "/" +
                   safePrefix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") +
                   ".png";
        }

        public static IReadOnlyList<string> GetExecuteCodeAdditionalNamespaces()
        {
            return SplitNamespaces(ExecuteCodeAdditionalNamespacesText);
        }

        internal static MCPProjectConfiguration GetProjectConfiguration()
        {
            string path = MCPProjectConfiguration.GetFullPath();
            bool exists = File.Exists(path);
            long ticks = exists ? File.GetLastWriteTimeUtc(path).Ticks : 0L;
            if (_projectConfiguration == null ||
                exists != _projectConfigurationFileExists ||
                ticks != _projectConfigurationWriteTicks)
            {
                _projectConfiguration = MCPProjectConfiguration.Load();
                _projectConfigurationFileExists = exists;
                _projectConfigurationWriteTicks = ticks;
            }
            return _projectConfiguration;
        }

        internal static void ReloadProjectConfiguration()
        {
            _projectConfiguration = null;
            GetProjectConfiguration();
        }

        // ─── Category management ───

        public static string[] GetAllCategoryNames()
        {
            if (_allCategories != null)
                return _allCategories;

            _allCategories = MCPRouteRegistry.BuiltInRoutes
                .Select(ExtractCategory)
                .Where(category => !UngatedCategories.Contains(category))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(category => category, StringComparer.Ordinal)
                .ToArray();
            return _allCategories;
        }

        public static Dictionary<string, bool> GetEnabledCategories()
        {
            if (_enabledCategories != null)
                return _enabledCategories;

            _enabledCategories = GetAllCategoryNames()
                .ToDictionary(category => category, _ => true,
                    StringComparer.Ordinal);

            string saved = EditorPrefs.GetString(
                ProjectUserPrefix + "EnabledCategories", "");
            if (!string.IsNullOrEmpty(saved))
            {
                foreach (string part in saved.Split(','))
                {
                    string[] keyValue = part.Split(':');
                    if (keyValue.Length == 2 &&
                        _enabledCategories.ContainsKey(keyValue[0]) &&
                        bool.TryParse(keyValue[1], out bool enabled))
                    {
                        _enabledCategories[keyValue[0]] = enabled;
                    }
                }
            }
            return _enabledCategories;
        }

        public static bool IsCategoryEnabled(string category)
        {
            if (string.IsNullOrEmpty(category) ||
                UngatedCategories.Contains(category))
                return true;

            var categories = GetEnabledCategories();
            string normalized = category.ToLowerInvariant();
            return !categories.ContainsKey(normalized) ||
                   categories[normalized];
        }

        public static void SetCategoryEnabled(string category, bool enabled)
        {
            var categories = GetEnabledCategories();
            string normalized = (category ?? "").ToLowerInvariant();
            if (!categories.ContainsKey(normalized))
                return;

            categories[normalized] = enabled;
            SaveEnabledCategories();
        }

        // ─── Reset and diagnostics ───

        public static void ResetUserPreferencesToDefaults()
        {
            Port = 7890;
            UseManualPort = false;
            AutoStart = true;
            SetAutoPortRange(7890, 7899);
            StartOnVirtualPlayers = false;
            OverrideDefaultResultLimit = false;
            DefaultResultLimit = 100;
            IncludePrefabFileDiffByDefault = false;
            ActionHistoryPersistence = false;
            ActionHistoryMaxEntries = 500;
            JobHistoryMaxEntries = 200;
            _enabledCategories = null;
            EditorPrefs.DeleteKey(ProjectUserPrefix + "EnabledCategories");
        }

        public static void ResetProjectSettingsToDefaults()
        {
            var settings = new MCPProjectConfiguration();
            settings.Save();
            CacheProjectConfiguration(settings);
            DeleteLegacyProjectConfigurationKeys();
        }

        public static void ResetToDefaults()
        {
            ResetUserPreferencesToDefaults();
            ResetProjectSettingsToDefaults();
        }

        internal static Dictionary<string, object> GetConfigurationSnapshot()
        {
            GetAutoPortRange(out int portStart, out int portEnd);
            var project = GetProjectConfiguration();
            var disabledCategories = GetAllCategoryNames()
                .Where(category => !IsCategoryEnabled(category))
                .ToArray();

            return new Dictionary<string, object>
            {
                {
                    "precedence",
                    new[]
                    {
                        "explicit tool argument",
                        "ProjectSettings team default",
                        "user preference",
                        "built-in default"
                    }
                },
                {
                    "preferences",
                    new Dictionary<string, object>
                    {
                        { "autoStart", AutoStart },
                        { "useManualPort", UseManualPort },
                        { "configuredPort", Port },
                        { "autoPortRangeStart", portStart },
                        { "autoPortRangeEnd", portEnd },
                        { "startOnVirtualPlayers", StartOnVirtualPlayers },
                        { "overrideDefaultResultLimit", OverrideDefaultResultLimit },
                        { "defaultResultLimit", DefaultResultLimit },
                        { "includePrefabFileDiffByDefault", IncludePrefabFileDiffByDefault },
                        { "actionHistoryPersistence", ActionHistoryPersistence },
                        { "actionHistoryMaxEntries", ActionHistoryMaxEntries },
                        { "jobHistoryMaxEntries", JobHistoryMaxEntries },
                        { "disabledCategories", disabledCategories }
                    }
                },
                {
                    "projectSettings",
                    new Dictionary<string, object>
                    {
                        { "path", MCPProjectConfiguration.ConfigPath },
                        { "found", project.Found },
                        { "valid", project.Valid },
                        { "error", project.Error },
                        { "contextEnabled", ContextEnabled },
                        { "contextPath", ContextPath },
                        {
                            "executeCodeAdditionalNamespaces",
                            GetExecuteCodeAdditionalNamespaces().ToArray()
                        },
                        { "defaultPhysicsDimension", DefaultPhysicsDimension },
                        { "screenshotOutputDirectory", ScreenshotOutputDirectory }
                    }
                },
                {
                    "invariants",
                    new[]
                    {
                        "request and response hard limits",
                        "queue capacity and ownership",
                        "destructive confirmation fields",
                        "dryRun, save, discard, overwrite, run, and terminate choices",
                        "raw serialized and stack-trace detail",
                        "tool-specific hard caps"
                    }
                }
            };
        }

        internal static string GetStableProjectScopeHash()
        {
            return StableHash(GetProjectPath());
        }

        private static void UpdateProjectConfiguration(
            Action<MCPProjectConfiguration> update)
        {
            MCPProjectConfiguration settings = GetWritableProjectConfiguration();
            update(settings);
            try
            {
                settings.Save();
            }
            catch
            {
                _projectConfiguration = null;
                throw;
            }
            CacheProjectConfiguration(settings);
            DeleteLegacyProjectConfigurationKeys();
        }

        private static MCPProjectConfiguration GetWritableProjectConfiguration()
        {
            MCPProjectConfiguration settings = GetProjectConfiguration();
            if (!settings.Valid)
            {
                throw new InvalidOperationException(
                    $"{MCPProjectConfiguration.ConfigPath}: {settings.Error}");
            }
            if (settings.Found)
                return settings;

            settings.ContextEnabled = EditorPrefs.GetBool(
                ProjectUserPrefix + "ContextEnabled", true);
            settings.ContextPath = EditorPrefs.GetString(
                ProjectUserPrefix + "ContextPath",
                MCPProjectConfiguration.DefaultContextPath);
            settings.ExecuteCodeAdditionalNamespaces.Clear();
            settings.ExecuteCodeAdditionalNamespaces.AddRange(SplitNamespaces(
                EditorPrefs.GetString(
                    ProjectUserPrefix + "ExecuteCodeAdditionalNamespaces", "")));
            return settings;
        }

        private static void CacheProjectConfiguration(
            MCPProjectConfiguration settings)
        {
            _projectConfiguration = settings;
            string path = MCPProjectConfiguration.GetFullPath();
            _projectConfigurationFileExists = File.Exists(path);
            _projectConfigurationWriteTicks = _projectConfigurationFileExists
                ? File.GetLastWriteTimeUtc(path).Ticks
                : 0L;
        }

        private static List<string> SplitNamespaces(string configured)
        {
            return MCPProjectConfiguration.NormalizeNamespaces(
                (configured ?? "").Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries));
        }

        private static void DeleteLegacyProjectConfigurationKeys()
        {
            EditorPrefs.DeleteKey(ProjectUserPrefix + "ContextEnabled");
            EditorPrefs.DeleteKey(ProjectUserPrefix + "ContextPath");
            EditorPrefs.DeleteKey(
                ProjectUserPrefix + "ExecuteCodeAdditionalNamespaces");
        }

        private static void SaveEnabledCategories()
        {
            string serialized = string.Join(",",
                _enabledCategories
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key}:{pair.Value}"));
            EditorPrefs.SetString(
                ProjectUserPrefix + "EnabledCategories", serialized);
        }

        private static string ExtractCategory(string route)
        {
            int slash = route.IndexOf('/');
            return slash > 0 ? route.Substring(0, slash) : route;
        }

        private static int ClampPort(int value)
        {
            return Math.Max(1025, Math.Min(65535, value));
        }

        private static string GetProjectPath()
        {
            string dataPath = Application.dataPath.Replace('\\', '/');
            string projectPath = dataPath.EndsWith(
                "/Assets", StringComparison.OrdinalIgnoreCase)
                ? dataPath.Substring(0, dataPath.Length - "/Assets".Length)
                : dataPath;
            projectPath = Path.GetFullPath(projectPath).Replace('\\', '/')
                .TrimEnd('/');
            if (Application.platform == RuntimePlatform.WindowsEditor)
                projectPath = projectPath.ToLowerInvariant();
            return projectPath;
        }

        private static string StableHash(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? ""));
                var builder = new StringBuilder(16);
                for (int index = 0; index < 8; index++)
                    builder.Append(hash[index].ToString("x2"));
                return builder.ToString();
            }
        }

        private static void MigrateLegacyInt(
            string legacyPrefix, string currentPrefix, string key)
        {
            string currentKey = currentPrefix + key;
            string legacyKey = legacyPrefix + key;
            if (!EditorPrefs.HasKey(currentKey) && EditorPrefs.HasKey(legacyKey))
                EditorPrefs.SetInt(currentKey, EditorPrefs.GetInt(legacyKey));
        }

        private static void MigrateLegacyBool(
            string legacyPrefix, string currentPrefix, string key)
        {
            string currentKey = currentPrefix + key;
            string legacyKey = legacyPrefix + key;
            if (!EditorPrefs.HasKey(currentKey) && EditorPrefs.HasKey(legacyKey))
                EditorPrefs.SetBool(currentKey, EditorPrefs.GetBool(legacyKey));
        }
    }
}
