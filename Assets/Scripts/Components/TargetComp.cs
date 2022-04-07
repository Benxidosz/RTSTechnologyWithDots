using System;
using Unity.Entities;

namespace Components {
    [Serializable]
    [GenerateAuthoringComponent]
    public struct TargetComp : IComponentData {
        public Entity Target;
    }
}
