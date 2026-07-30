using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Owns the public, presence-only metadata vocabulary shared by tool discovery and Jobs.
    /// Authoring APIs may keep strongly typed booleans, but wire contracts expose only positive
    /// tags and exact side effects so false/default state is represented by absence.
    /// </summary>
    internal static class MCPContractMetadata
    {
        internal const int ToolMetadataSchemaVersion = 5;

        internal static class Tag
        {
            internal const string ReadOnly = "readOnly";
            internal const string Dangerous = "dangerous";
            internal const string LongRunning = "longRunning";
            internal const string RequiresPlayMode = "requiresPlayMode";
            internal const string FirstClass = "firstClass";
            internal const string Fallback = "fallback";
            internal const string Cleanup = "cleanup";
            internal const string CleanupDeclared = "cleanupDeclared";
            internal const string CleanupAvailable = "cleanupAvailable";
            internal const string IncrementalJob = "incrementalJob";
            internal const string OutputSchema = "outputSchema";
            internal const string Invalid = "invalid";
            internal const string CancellationRequested = "cancellationRequested";
            internal const string Reused = "reused";
        }

        internal static List<string> BuildToolTags(
            bool readOnly = false,
            bool dangerous = false,
            bool longRunning = false,
            bool requiresPlayMode = false,
            bool firstClass = false,
            bool fallback = false,
            bool cleanup = false,
            bool incrementalJob = false,
            bool outputSchema = false,
            bool invalid = false)
        {
            var tags = new List<string>();
            Add(tags, Tag.ReadOnly, readOnly);
            Add(tags, Tag.Dangerous, dangerous);
            Add(tags, Tag.LongRunning, longRunning);
            Add(tags, Tag.RequiresPlayMode, requiresPlayMode);
            Add(tags, Tag.FirstClass, firstClass);
            Add(tags, Tag.Fallback, fallback);
            Add(tags, Tag.Cleanup, cleanup);
            Add(tags, Tag.IncrementalJob, incrementalJob);
            Add(tags, Tag.OutputSchema, outputSchema);
            Add(tags, Tag.Invalid, invalid);
            return Normalize(tags);
        }

        internal static List<string> BuildSideEffects(
            IEnumerable declared,
            bool readOnly = false,
            bool mutatesAssets = false,
            bool mutatesRuntime = false,
            bool mayReloadDomain = false)
        {
            var effects = ReadStrings(declared);
            Add(effects, "readsProjectState", readOnly);
            Add(effects, "writesAssets", mutatesAssets);
            Add(effects, "changesRuntimeState", mutatesRuntime);
            Add(effects, "reloadsDomain", mayReloadDomain);
            return Normalize(effects);
        }

        internal static List<string> ReadTags(IReadOnlyDictionary<string, object> metadata)
        {
            return metadata != null && metadata.TryGetValue("tags", out object value)
                ? ReadStrings(value as IEnumerable)
                : new List<string>();
        }

        internal static bool HasTag(IReadOnlyDictionary<string, object> metadata, string tag)
        {
            return ReadTags(metadata).Contains(tag, StringComparer.Ordinal);
        }

        internal static bool HasString(IEnumerable values, string expected)
        {
            return ReadStrings(values).Contains(expected, StringComparer.Ordinal);
        }

        internal static void SetTags(Dictionary<string, object> target, IEnumerable<string> tags)
        {
            if (target == null)
                return;

            List<string> normalized = Normalize(tags);
            if (normalized.Count == 0)
                target.Remove("tags");
            else
                target["tags"] = normalized;
        }

        internal static void AddTag(Dictionary<string, object> target, string tag)
        {
            if (target == null || string.IsNullOrWhiteSpace(tag))
                return;

            var tags = target.TryGetValue("tags", out object value)
                ? ReadStrings(value as IEnumerable)
                : new List<string>();
            tags.Add(tag);
            SetTags(target, tags);
        }

        internal static void AddOptionalString(
            Dictionary<string, object> target, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                target[key] = value;
        }

        internal static void AddOptionalList(
            Dictionary<string, object> target, string key, IEnumerable values)
        {
            List<string> normalized = ReadStrings(values);
            if (normalized.Count > 0)
                target[key] = Normalize(normalized);
        }

        private static List<string> ReadStrings(IEnumerable values)
        {
            var result = new List<string>();
            if (values == null || values is string)
                return result;

            foreach (object value in values)
            {
                string text = value?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(text))
                    result.Add(text);
            }
            return result;
        }

        private static List<string> Normalize(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }

        private static void Add(ICollection<string> values, string value, bool include)
        {
            if (include)
                values.Add(value);
        }
    }
}
