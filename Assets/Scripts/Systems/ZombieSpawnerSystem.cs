using System.Diagnostics;
using Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Systems {
    [UpdateAfter(typeof(CoreTriggerEventSystem))]
    [UpdateAfter(typeof(BulletSystem))]
    public partial class ZombieSpawnerSystem : SystemBase {
        private Translation _zombieSpawnerTranslation;
        private ZombieSpawnerComponent _zombieSpawnerComponent;
        private EntityQuery _zombieGroup;
        private EntityQuery _deadZombie;
        private Entity _prefab;
        private BeginSimulationEntityCommandBufferSystem _beginSimECB;

        protected override void OnStartRunning() {
            base.OnStartRunning();
            var zombieSpawner = GetEntityQuery(typeof(CoreHealthComponent),
                    typeof(Translation))
                .GetSingletonEntity();
            _zombieSpawnerTranslation = EntityManager.GetComponentData<Translation>(zombieSpawner);
            _zombieSpawnerComponent = EntityManager.GetComponentData<ZombieSpawnerComponent>(zombieSpawner);

            _zombieGroup = GetEntityQuery(ComponentType.ReadOnly<TravelToCore>(),
                ComponentType.ReadOnly<Translation>());

            _deadZombie = GetEntityQuery(ComponentType.ReadOnly<DeadTag>(),
                ComponentType.ReadOnly<TravelToCore>());

                _prefab = _zombieSpawnerComponent.Prefab;
            _beginSimECB = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();

            var count = _zombieSpawnerComponent.SoldierCount;
            var soldierPrefab = _zombieSpawnerComponent.Soldier;
            var commandBuffer = _beginSimECB.CreateCommandBuffer();
            var spawnerTranslate = _zombieSpawnerTranslation;
            var rand = new Unity.Mathematics.Random((uint)Stopwatch.GetTimestamp());
            
            for (int i = 0; i < count; i++) {
                var entity = commandBuffer.Instantiate(soldierPrefab);
                var alfa = rand.NextFloat(0.0f, 2 * math.PI);
                var radius = rand.NextFloat(_zombieSpawnerComponent.MinSoldierRadius, _zombieSpawnerComponent.MaxSoldierRadius);
                var translation = new Translation {
                    Value = spawnerTranslate.Value + new float3(math.cos(alfa), 0.0f, math.sin(alfa)) * radius
                };
                commandBuffer.SetComponent(entity, translation);
            }
        }
        protected override void OnUpdate() {
            var commandBuffer = _beginSimECB.CreateCommandBuffer();
            var count = _zombieSpawnerComponent.Count - _zombieGroup.CalculateEntityCount();
            var zombiePrefab = _prefab;
            var spawnerTranslate = _zombieSpawnerTranslation;
            var spawnerComponent = _zombieSpawnerComponent;
            var rand = new Unity.Mathematics.Random((uint)Stopwatch.GetTimestamp());
            
            commandBuffer.DestroyEntitiesForEntityQuery(_deadZombie);
            
            Job.WithCode(() => {
                for (int i = 0; i < count; i++) {
                    var entity = commandBuffer.Instantiate(zombiePrefab);
                    var alfa = rand.NextFloat(0.0f, 2 * math.PI);
                    var radius = rand.NextFloat(spawnerComponent.MinRadius, spawnerComponent.MaxRadius);
                    var translation = new Translation {
                        Value = spawnerTranslate.Value + new float3(math.cos(alfa), 0.0f, math.sin(alfa)) * radius
                    };

                    var speed = rand.NextFloat(spawnerComponent.MinSpeed, spawnerComponent.MaxSpeed);
                    var travelComp = new TravelToCore {
                        Damage = 1,
                        Speed = speed
                    };
                    commandBuffer.SetComponent(entity, translation);
                    commandBuffer.SetComponent(entity, travelComp);
                }
            }).Schedule();
            
            _beginSimECB.AddJobHandleForProducer(Dependency);
            Dependency.Complete();
        }
    }
}