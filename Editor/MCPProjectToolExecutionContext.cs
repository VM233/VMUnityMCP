using System;

namespace UnityMCP.Editor
{
    public static class MCPProjectToolExecutionContext
    {
        public static string JobId => MCPPersistentJobRunner.CurrentJobId;

        public static bool IsCancellationRequested =>
            MCPPersistentJobRunner.IsCurrentJobCancellationRequested;

        public static void ThrowIfCancellationRequested()
        {
            if (IsCancellationRequested)
                throw new OperationCanceledException($"Project tool job '{JobId}' was canceled.");
        }
    }
}
