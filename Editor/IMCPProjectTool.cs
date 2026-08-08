using System.Collections.Generic;

namespace UnityMCP.Editor
{
    public interface IMCPProjectTool
    {
        object Execute(Dictionary<string, object> args);
    }
}
