using System;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    public enum ManagedReferenceTargetType
    {
        Component,
        GameObject
    }

    [Serializable]
    public sealed class ManagedReferenceTargetConfig
    {
        public ManagedReferenceTargetType type = ManagedReferenceTargetType.Component;
    }

    [Serializable]
    public sealed class ManagedReferencePropertyConfig
    {
        public ManagedReferenceTargetConfig target = new ManagedReferenceTargetConfig();
        public string parameterName;
    }

    public sealed class ManagedReferencePropertyTestComponent : MonoBehaviour
    {
        [SerializeReference]
        public ManagedReferencePropertyConfig config = new ManagedReferencePropertyConfig();
    }
}
