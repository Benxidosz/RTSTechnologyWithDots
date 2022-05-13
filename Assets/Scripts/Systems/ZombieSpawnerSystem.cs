using System.Diagnostics;
using Components;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Debug = UnityEngine.Debug;

namespace Systems {
    [UpdateAfter(typeof(CoreTriggerEventSystem))]
    [UpdateAfter(typeof(BulletSystem))]
    public partial class ZombieSpawnerSystem : SystemBase {
        private Translation _zombieSpawnerTranslation;
        private Entity _zombieSpawner;
        private EntityQuery _zombieGroup;
        private EntityQuery _deadZombie;
        private Entity _prefab;
        private BeginSimulationEntityCommandBufferSystem _beginSimECB;
        private int spawned = -5;
        private int levelHardener = 10;

        protected override void OnStartRunning() {
            base.OnStartRunning();
            _zombieSpawner = GetEntityQuery(typeof(CoreHealthComponent),
                    typeof(Translation))
                .GetSingletonEntity();
            _zombieSpawnerTranslation = EntityManager.GetComponentData<Translation>(_zombieSpawner);
            var _zombieSpawnerComponent = EntityManager.GetComponentData<ZombieSpawnerComponent>(_zombieSpawner);

            _zombieGroup = GetEntityQuery(ComponentType.ReadOnly<ZombieTag>());

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
                commandBuffer.SetComponent(entity, new SoldierMovement {
                    destination = translation.Value,
                    speed = 5f
                });
            }
        }

        private struct ZombiSpawningJob : IJob {
            public Entity ZombieSpawner;
            public ZombieSpawnerComponent ZombieSpawnerComp;
            public int increaseValue;
            public EntityCommandBuffer CommandBuffer;
            public void Execute() {
                CommandBuffer.SetComponent(ZombieSpawner, new ZombieSpawnerComponent {
                    Count = ZombieSpawnerComp.Count + increaseValue * 2,
                    MaxRadius = ZombieSpawnerComp.MaxRadius,
                    MaxSpeed = ZombieSpawnerComp.MaxSpeed,
                    MaxSoldierRadius = ZombieSpawnerComp.MaxSoldierRadius,
                    MinSoldierRadius = ZombieSpawnerComp.MinSoldierRadius,
                    MinRadius = ZombieSpawnerComp.MinRadius,
                    MinSpeed = ZombieSpawnerComp.MaxSpeed
                });
            }
        }
        protected override void OnUpdate() {
            var _zombieSpawnerComponent = EntityManager.GetComponentData<ZombieSpawnerComponent>(_zombieSpawner);
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
                    commandBuffer.AddComponent<ZombieTag>(entity);
                }
            }).Schedule();
            Dependency.Complete();
            this.spawned += count;
            var increaseValue = 0;
            if (spawned / levelHardener > 0 && spawned > 0) {
                increaseValue = spawned / levelHardener;
                spawned = spawned % levelHardener - (increaseValue * 2);
                levelHardener += increaseValue * 5;
            }
            Dependency = new ZombiSpawningJob {
                ZombieSpawner = _zombieSpawner,
                ZombieSpawnerComp = _zombieSpawnerComponent,
                increaseValue = increaseValue,
                CommandBuffer = commandBuffer
            }.Schedule();
            _beginSimECB.AddJobHandleForProducer(Dependency);
            Dependency.Complete();
        }
    }
    
    public struct ZombieTag : IComponentData { }
}