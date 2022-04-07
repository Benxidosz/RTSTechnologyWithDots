using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
[GenerateAuthoringComponent]
public struct SoldierMovement : IComponentData {
    public float3 destination;
    public float speed;
}