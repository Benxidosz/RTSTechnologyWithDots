using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct SoldierShooting : IComponentData {
    public float ShootingRange;
    public float ShootingSpeed;
    public float ShootingTimer;
    public Entity BulletPrefab;
}

public class SoldierShootingAuthoring : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs {
    public float ShootingRange;
    public float ShootingSpeed;
    public GameObject BulletPrefab;
    
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
        var bullet = conversionSystem.GetPrimaryEntity(BulletPrefab);

        var component = new SoldierShooting {
            ShootingRange = ShootingRange,
            ShootingSpeed = ShootingSpeed,
            BulletPrefab = bullet
        };
        
        dstManager.AddComponent<SoldierShooting>(entity);
        dstManager.SetComponentData(entity, component);
    }

    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs) {
        referencedPrefabs.Add(BulletPrefab);
    }
}