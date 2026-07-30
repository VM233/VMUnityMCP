using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [Serializable]
    public sealed class PrefabComponentListPathFixture
    {
        public List<string> names = new List<string>();
    }

    public sealed class PrefabComponentListFieldsFixture : MonoBehaviour
    {
        public List<string> bindObjectsNames = new List<string>();
        public List<PrefabComponentListPathFixture> containerPaths =
            new List<PrefabComponentListPathFixture>();
    }
}
