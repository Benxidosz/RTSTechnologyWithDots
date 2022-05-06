using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Components {
    [Serializable]
    [GenerateAuthoringComponent]
    public struct TargetEntityComp : IComponentData {
        public Entity Target;
    }
}
