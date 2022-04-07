using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[AddComponentMenu("DOTS Samples/Spawner")]
public class Spawner : MonoBehaviour {
    [SerializeField] private GameObject prefab;
    [SerializeField] private int countX;
    [SerializeField] private int countY;

    private void Start() {
        var settings = GameObjectConversionSettings.FromWorld(World.DefaultGameObjectInjectionWorld, null);
        var entityPrefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(prefab, settings);
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        for (int x = 0; x < countX; x++) {
            for (int y = 0; y < countY; y++) {
                var entity = entityManager.Instantiate(entityPrefab);

                var pos = transform.TransformPoint(new float3(x * 1.3F, 
                    noise.cnoise(new float2(x, y) * 0.21F) * 2,
                    y * 1.3F));
                
                entityManager.SetComponentData(entity, new Translation {Value = pos});
            }
        }
    }
}