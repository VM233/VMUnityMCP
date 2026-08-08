using System;
using System.Collections.Generic;

namespace UnityMCP.Editor
{
    public sealed class MCPProjectToolException : Exception
    {
        public string ErrorCode { get; }

        public bool Retryable { get; }

        public Dictionary<string, object> Details { get; }

        public MCPProjectToolException(string errorCode, string message, bool retryable = false,
            Dictionary<string, object> details = null) : base(message)
        {
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "project_tool_failed" : errorCode;
            Retryable = retryable;
            Details = details;
        }
    }
}
