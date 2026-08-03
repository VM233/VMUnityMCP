using System;
using System.Collections.Generic;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Routes cancellation to the workflow that owns a persistent MCP job.
    /// Queue ticket cancellation remains a separate transport-level concern.
    /// </summary>
    internal static class MCPJobCommands
    {
        public static object Get(Dictionary<string, object> args)
        {
            string requestedJobType = GetString(args, "jobType");
            string jobId = GetString(args, "jobId");
            if (MCPPersistentJobRunner.OwnsJobType(requestedJobType) ||
                MCPPersistentJobRunner.ContainsJob(jobId))
                return MCPPersistentJobRunner.Get(args);

            object historyResult = MCPJobHistory.Get(args);
            var history = MCPResponse.ToDictionary(historyResult);
            if (history != null &&
                history.TryGetValue("job", out object jobValue) &&
                MCPResponse.ToDictionary(jobValue) is { } job &&
                MCPPersistentJobRunner.OwnsJobType(GetString(job, "jobType")))
            {
                return MCPPersistentJobRunner.Get(args);
            }
            return historyResult;
        }

        public static object Cancel(Dictionary<string, object> args)
        {
            string jobId = GetString(args, "jobId");
            if (string.IsNullOrEmpty(jobId))
                return MCPResponse.Error("jobId is required.", "invalid_arguments");

            if (MCPPersistentJobRunner.ContainsJob(jobId))
                return MCPPersistentJobRunner.Cancel(args);

            string requestedJobType = GetString(args, "jobType");
            var lookupArgs = new Dictionary<string, object>
            {
                { "jobId", jobId },
                { "_agentId", GetString(args, "_agentId", "anonymous") },
            };
            string jobAccessToken = GetString(args, "jobAccessToken");
            if (!string.IsNullOrEmpty(jobAccessToken))
                lookupArgs["jobAccessToken"] = jobAccessToken;
            if (!string.IsNullOrEmpty(requestedJobType))
                lookupArgs["jobType"] = requestedJobType;

            var lookup = MCPResponse.ToDictionary(MCPJobHistory.Get(lookupArgs));
            if (lookup == null || lookup.TryGetValue("job", out object jobValue) == false)
                return lookup ?? MCPResponse.Error($"Job '{jobId}' was not found.", "job_not_found");

            var job = jobValue as Dictionary<string, object>;
            if (job == null)
                return MCPResponse.Error($"Job '{jobId}' has invalid persisted metadata.", "job_metadata_invalid");

            string jobType = GetString(job, "jobType");
            if (MCPPersistentJobRunner.OwnsJobType(jobType))
                return MCPPersistentJobRunner.Cancel(args);

            var cancelArgs = new Dictionary<string, object>(args ?? new Dictionary<string, object>())
            {
                ["jobId"] = jobId,
                ["jobType"] = jobType,
                ["_agentId"] = GetString(job, "ownerAgentId", "anonymous"),
            };

            switch (jobType)
            {
                case "player-build":
                    return MCPBuildCommands.CancelBuild(cancelArgs);
                case "unity-test":
                    return MCPTestRunnerCommands.CancelTestJob(cancelArgs);
                case "package-test":
                    return MCPPackageTestCommands.CancelPackageTest(cancelArgs);
                case "memory-snapshot":
                    return MCPMemoryProfilerCommands.CancelMemorySnapshot(cancelArgs);
                case "addressables-build":
                    return MCPAddressablesCommands.CancelBuild(cancelArgs);
                default:
                    return MCPResponse.Error(
                        $"Job type '{jobType}' does not expose a cancellation contract.",
                        "job_not_cancellable", false, new Dictionary<string, object>
                        {
                            { "jobId", jobId },
                            { "jobType", jobType },
                            { "status", GetString(job, "status", "unknown") },
                        });
            }
        }

        public static object Cleanup(Dictionary<string, object> args)
        {
            return MCPPersistentJobRunner.RequestCleanup(args);
        }

        private static string GetString(Dictionary<string, object> values, string key,
            string defaultValue = "")
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : defaultValue;
        }
    }
}
