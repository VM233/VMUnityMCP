using System;

namespace UnityMCP.Editor.Tests
{
    [Flags]
    public enum SerializedEnumFlagsTestValue
    {
        None = 0,
        Enemy = 1,
        Neutral = 2,
        Friendly = 4,
        Allied = 8
    }
}
