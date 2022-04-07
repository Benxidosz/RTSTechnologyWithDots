using Components;
using Unity.Entities;
using UnityEngine;

namespace Components {
    [GenerateAuthoringComponent]
    public struct TravelToCore : IComponentData {
        public int Damage;
        public float Speed;
        public Entity Soldier;
    }
    
    public struct DeadTag : IComponentData { }
}