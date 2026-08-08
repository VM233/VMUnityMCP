using System.Collections.Generic;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Project-tool contract for work that yields between Editor updates. Every value required by a
    /// later step is returned in <see cref="MCPProjectToolJobStep.State"/> and persisted by the Job owner.
    /// </summary>
    public interface IMCPPersistentProjectTool : IMCPProjectTool
    {
        MCPProjectToolJobStep ExecuteJobStep(Dictionary<string, object> args,
            Dictionary<string, object> state);
    }
}
