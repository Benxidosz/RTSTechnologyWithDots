using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Components {
    [Serializable]
    [GenerateAuthoringComponent]
    public struct TargetPosComp : IComponentData {
        public float3 pos;
    }
}