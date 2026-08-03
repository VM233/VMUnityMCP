using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class MCPJobHistory
    {
        private const int MaxSnapshotCharacters = 128 * 1024;
        private static readonly object Sync = new object();
        private static List<Dictionary<string, object>> entries;

        public static void Record(string jobType, string jobId, string ownerAgentId, string status, object snapshot)
        {
            if (string.IsNullOrEmpty(jobType) || string.IsNullOrEmpty(jobId)) return;
            lock (Sync)
            {
                EnsureLoaded();
                entries.RemoveAll(item => GetString(item, "jobType") == jobType &&
                                          GetString(item, "jobId") == jobId);
                object boundedSnapshot = BoundSnapshot(snapshot, status);
                entries.Add(new Dictionary<string, object>
                {
                    { "jobType", jobType }, { "jobId", jobId },
                    { "ownerAgentId", string.IsNullOrEmpty(ownerAgentId) ? "anonymous" : ownerAgentId },
                    { "status", status ?? "unknown" }, { "updatedAt", DateTime.UtcNow.ToString("O") },
                    { "snapshot", boundedSnapshot },
                });
                entries = entries.OrderByDescending(item => ParseDate(GetString(item, "updatedAt")))
                    .Take(MCPSettingsManager.JobHistoryMaxEntries).ToList();
                Save();
            }
        }

        public static object List(Dictionary<string, object> args)
        {
            lock (Sync)
            {
                EnsureLoaded();
                string agentId = GetString(args, "_agentId", "anonymous");
                string jobType = GetString(args, "jobType");
                string status = GetString(args, "status");
                int offset = Math.Max(0, GetInt(args, "offset", 0));
                int limit = Math.Max(1, Math.Min(200, GetInt(args, "limit", 50)));
                var filtered = entries.Where(item =>
                        GetString(item, "ownerAgentId", "anonymous") == agentId &&
                        (string.IsNullOrEmpty(jobType) || GetString(item, "jobType") == jobType) &&
                        (string.IsNullOrEmpty(status) || GetString(item, "status") == status))
                    .OrderByDescending(item => ParseDate(GetString(item, "updatedAt"))).ToList();
                var page = filtered.Skip(offset).Take(limit)
                    .Select(item => new Dictionary<string, object>(item)).ToList();
                return new Dictionary<string, object>
                {
                    { "success", true }, { "ownerAgentId", agentId }, { "total", filtered.Count },
                    { "offset", offset }, { "limit", limit },
                    { "hasMore", offset + page.Count < filtered.Count },
                    { "nextOffset", offset + page.Count < filtered.Count ? (object)(offset + page.Count) : null },
                    { "jobs", page },
                };
            }
        }

        public static object Get(Dictionary<string, object> args)
        {
            lock (Sync)
            {
                EnsureLoaded();
                string agentId = GetString(args, "_agentId", "anonymous");
                string jobType = GetString(args, "jobType");
                string jobId = GetString(args, "jobId");
                if (string.IsNullOrEmpty(jobId))
                    return MCPResponse.Error("jobId is required.", "invalid_arguments");
                var match = entries.FirstOrDefault(item => GetString(item, "jobId") == jobId &&
                    (string.IsNullOrEmpty(jobType) || GetString(item, "jobType") == jobType));
                if (match == null) return MCPResponse.Error($"Job '{jobId}' was not found.", "job_not_found");
                if (GetString(match, "ownerAgentId", "anonymous") != agentId)
                    return MCPResponse.Error("Job belongs to another agent.", "job_owner_mismatch");
                return new Dictionary<string, object>
                {
                    { "success", true }, { "job", new Dictionary<string, object>(match) },
                };
            }
        }

        private static object BoundSnapshot(object snapshot, string status)
        {
            if (snapshot == null) return new Dictionary<string, object> { { "status", status ?? "unknown" } };
            string json = MiniJson.Serialize(snapshot);
            if (json.Length <= MaxSnapshotCharacters) return snapshot;
            return new Dictionary<string, object>
            {
                { "status", status ?? "unknown" }, { "snapshotTruncated", true },
                { "originalCharacterCount", json.Length },
            };
        }

        private static void EnsureLoaded()
        {
            if (entries != null) return;
            entries = new List<Dictionary<string, object>>();
            string path = GetPath();
            try
            {
                if (!MCPPersistenceFile.TryReadAllText(path, out string contents)) return;
                if (!(MiniJson.Deserialize(contents) is IList list)) return;
                entries = list.Cast<object>().Select(MCPResponse.ToDictionary).Where(item => item != null)
                    .Take(MCPSettingsManager.JobHistoryMaxEntries).ToList();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Unity MCP Jobs] Failed to read job history: {exception.Message}");
            }
        }

        private static void Save()
        {
            MCPPersistenceFile.WriteAllText(GetPath(), MiniJson.Serialize(entries));
        }

        private static string GetPath()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Library", "UnityMCP", "job-history-v1.json");
        }

        private static DateTime ParseDate(string value)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                out DateTime parsed) ? parsed : DateTime.MinValue;
        }

        private static string GetString(Dictionary<string, object> args, string key, string defaultValue = "")
        {
            return args != null && args.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : defaultValue;
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            return args != null && args.TryGetValue(key, out object value) && value != null &&
                   int.TryParse(value.ToString(), out int parsed) ? parsed : defaultValue;
        }
    }
}
