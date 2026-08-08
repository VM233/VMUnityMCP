using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    public sealed class SerializedEnumFlagsTestObject : ScriptableObject
    {
        public List<SerializedEnumFlagsTestConfig> configs = new()
        {
            new SerializedEnumFlagsTestConfig()
        };

        public SerializedEnumFlagsTestValue factionType;
    }
}
