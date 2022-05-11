using Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace Systems {
    [UpdateAfter(typeof(BulletSystem))]
    [UpdateAfter(typeof(FindTargetSystem))]
    public partial class ShootingSystem : SystemBase {
        private EntityQuery _soldierQuery;
        private BeginSimulationEntityCommandBufferSystem _commandBufferSystem;
        private EntityManager _entityManager;
        private EntityQuery _towerQuery;

        protected override void OnStartRunning() {
            base.OnStartRunning();
            _soldierQuery = GetEntityQuery(typeof(Shooting), typeof(TargetEntityComp),
                typeof(SoldierMovement), typeof(Translation));
            _commandBufferSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            _entityManager = World.EntityManager;
            var towerDesc = new EntityQueryDesc {
                All = new[] {
                    ComponentType.ReadOnly<Shooting>(),
                    ComponentType.ReadWrite<TargetPosComp>(), 
                    ComponentType.ReadOnly<Translation>()
                }
            };
            _towerQuery = GetEntityQuery(towerDesc);
        }

        [BurstCompile]
        private struct SoldierShootJob : IJobEntityBatch {
            public ComponentTypeHandle<Shooting> SoldierShootingHandle;
            [ReadOnly] public ComponentTypeHandle<Translation> TranslationHandle;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> TargetPositionArray;
            public EntityCommandBuffer CommandBuffer;
            [ReadOnly] public float dt;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
                var chunkSoldierShooting = batchInChunk.GetNativeArray(SoldierShootingHandle);
                var chunkSoldierTranslation = batchInChunk.GetNativeArray(TranslationHandle);

                for (var i = 0; i < batchInChunk.Count; i++) {
                    var soldierShooting = chunkSoldierShooting[i];
                    var soldierTranslation = chunkSoldierTranslation[i];
                    
                    soldierShooting.ShootingTimer += dt;
                    if (soldierShooting.ShootingTimer > soldierShooting.ShootingSpeed) {
                        var targetPosition = TargetPositionArray[i];
                        if (!targetPosition.Equals(float3.zero)) {
                            var dir = math.normalize(targetPosition - soldierTranslation.Value);
                            var velocityComponent = new PhysicsVelocity {
                                Linear = dir * 25.0f
                            };
                            var translateComponent = new Translation {
                                Value = soldierTranslation.Value
                            };
                            var bullet = CommandBuffer.Instantiate(soldierShooting.BulletPrefab);
                            CommandBuffer.SetComponent(bullet, velocityComponent);
                            CommandBuffer.SetComponent(bullet, translateComponent);
                            CommandBuffer.AddComponent<SoldierBulletTag>(bullet);
                            CommandBuffer.AddComponent<BulletTag>(bullet);
                            soldierShooting.ShootingTimer = 0.0f;
                        }
                    }

                    chunkSoldierShooting[i] = soldierShooting;
                }
            }
        }
        
        private struct TowerShootJob : IJobEntityBatch {
            public ComponentTypeHandle<Shooting> TowerShootingHandle;
            [ReadOnly] public ComponentTypeHandle<Translation> TranslationHandle;
            [ReadOnly] public ComponentTypeHandle<TargetPosComp> TargetPosHandle;
            public EntityCommandBuffer CommandBuffer;
            [ReadOnly] public float dt;
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
                var chunkTowerShooting = batchInChunk.GetNativeArray(TowerShootingHandle);
                var chunkTowerTranslation = batchInChunk.GetNativeArray(TranslationHandle);
                var chunkTowerTarget = batchInChunk.GetNativeArray(TargetPosHandle);
                
                for (var i = 0; i < batchInChunk.Count; i++) {
                    var towerShooting = chunkTowerShooting[i];
                    var towerTranslation = chunkTowerTranslation[i];
                    var towerTarget = chunkTowerTarget[i];
                    
                    towerShooting.ShootingTimer += dt;
                    if (towerShooting.ShootingTimer > towerShooting.ShootingSpeed) {
                        var targetPosition = towerTarget.pos;
                        if (!targetPosition.Equals(float3.zero)) {
                            var dir = math.normalize(targetPosition - towerTranslation.Value);
                            var velocityComponent = new PhysicsVelocity {
                                Linear = dir * 30.0f
                            };
                            var translateComponent = new Translation {
                                Value = towerTranslation.Value
                            };
                            var bullet = CommandBuffer.Instantiate(towerShooting.BulletPrefab);
                            CommandBuffer.SetComponent(bullet, velocityComponent);
                            CommandBuffer.SetComponent(bullet, translateComponent);
                            CommandBuffer.AddComponent<BombTag>(bullet);
                            CommandBuffer.AddComponent<BulletTag>(bullet);
                            towerShooting.ShootingTimer = 0.0f;
                        }
                    }

                    chunkTowerShooting[i] = towerShooting;
                }
            }
        }

        protected override void OnUpdate() {
            var shootingType = GetComponentTypeHandle<Shooting>();
            var translationType = GetComponentTypeHandle<Translation>(true);
            var targetPosType = GetComponentTypeHandle<TargetPosComp>();

            var soldierTargetArray = _soldierQuery.ToComponentDataArray<TargetEntityComp>(Allocator.TempJob);
            var targetPositionArray =
                new NativeArray<float3>(_soldierQuery.CalculateEntityCount(), Allocator.TempJob);
            var dt = Time.DeltaTime;

            for (int i = 0; i < targetPositionArray.Length; ++i) {
                var target = soldierTargetArray[i].Target;
                if (target != Entity.Null && _entityManager.Exists(target))
                    targetPositionArray[i] = _entityManager.GetComponentData<Translation>(target).Value;
            }

            var soldierShootJob = new SoldierShootJob {
                SoldierShootingHandle = shootingType,
                TranslationHandle = translationType,
                CommandBuffer = _commandBufferSystem.CreateCommandBuffer(),
                TargetPositionArray = targetPositionArray,
                dt = dt
            };
            var towerShootJob = new TowerShootJob {
                TowerShootingHandle = shootingType,
                TranslationHandle = translationType,
                CommandBuffer = _commandBufferSystem.CreateCommandBuffer(),
                TargetPosHandle = targetPosType,
                dt = dt
            };
            
            Dependency = soldierShootJob.Schedule(_soldierQuery, Dependency);
            Dependency = towerShootJob.Schedule(_towerQuery, Dependency);
            soldierTargetArray.Dispose();
            Dependency.Complete();
        }
    }

    public struct BulletTag : IComponentData { }
    public struct SoldierBulletTag : IComponentData { }
    public struct BombTag : IComponentData { }
}