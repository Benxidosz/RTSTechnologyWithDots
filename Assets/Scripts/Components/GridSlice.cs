using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Components {
    [Serializable]
    public struct GridSliceComponent : IComponentData {
        public float mapSize;

        public float rStep;
        public float angleStep;
    }

    public class GridSlice : MonoBehaviour, IConvertGameObjectToEntity {
        [SerializeField] private float mapSize;

        [SerializeField] private float rStep;
        [SerializeField] private float angleStepInDegree;
    
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
            var component = new GridSliceComponent {
                mapSize = mapSize,
                rStep = rStep,
                angleStep = math.radians(angleStepInDegree)
            };
            
            dstManager.AddComponent<GridSliceComponent>(entity);
            dstManager.SetComponentData(entity, component);
        }
    }
}