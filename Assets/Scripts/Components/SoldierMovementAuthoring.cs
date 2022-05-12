using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct SoldierMovement : IComponentData {
    public float3 destination;
    public float speed;
}

public class SoldierMovementAuthoring : MonoBehaviour, IConvertGameObjectToEntity {
    public float3 destination;
    public float speed;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
        var comp = new SoldierMovement {
            destination = destination,
            speed = speed
        };
        
        dstManager.AddComponent<SoldierMovement>(entity);
        dstManager.SetComponentData(entity, comp);
    }
}