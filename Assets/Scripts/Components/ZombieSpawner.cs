using System;
using Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Components {
    [GenerateAuthoringComponent]
    public struct ZombieSpawnerComponent : IComponentData {
        public float MinRadius;
        public float MaxRadius;
        public float MinSoldierRadius;
        public float MaxSoldierRadius;
        public float MinSpeed;
        public float MaxSpeed;
        public int Count;
        public int SoldierCount;
        public Entity Prefab;
        public Entity Soldier;
    }
}