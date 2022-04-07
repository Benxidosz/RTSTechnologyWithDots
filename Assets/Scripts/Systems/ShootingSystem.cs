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
    public partial class ShootingSystem : SystemBase {
        private EntityQuery _soldierQuery;
        private BeginSimulationEntityCommandBufferSystem _commandBufferSystem;
        private EntityManager _entityManager;
        
        protected override void OnStartRunning() {
            base.OnStartRunning();
            _soldierQuery = GetEntityQuery(typeof(SoldierShooting),typeof(TargetComp),
                typeof(SoldierMovement), typeof(Translation));
            _commandBufferSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            _entityManager = World.EntityManager;
        }
        
        [BurstCompile]
        private struct ShootJob : IJobEntityBatch {
            public ComponentTypeHandle<SoldierShooting> SoldierShootingHandle;
            [ReadOnly] public ComponentTypeHandle<Translation> TranslationHandle;
            [ReadOnly] public ComponentTypeHandle<TargetComp> TargetCompHandle;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> TargetPositionArray;
            public EntityCommandBuffer CommandBuffer;
            [ReadOnly] public float dt;
            
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
                var chunkSoldierShooting = batchInChunk.GetNativeArray(SoldierShootingHandle);
                var chunkSoldierTranslation = batchInChunk.GetNativeArray(TranslationHandle);
                var chunkSoldierTargetComp = batchInChunk.GetNativeArray(TargetCompHandle);
                
                for (var i = 0; i < batchInChunk.Count; i++) {
                    var soldierShooting = chunkSoldierShooting[i];
                    var soldierTranslation = chunkSoldierTranslation[i];
                    var targetComp = chunkSoldierTargetComp[i];
                    var target = targetComp.Target;
                    if (target != Entity.Null) {
                        soldierShooting.ShootingTimer += dt;
                        if (soldierShooting.ShootingTimer > soldierShooting.ShootingSpeed) {
                            var targetPosition = TargetPositionArray[i];
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
                            CommandBuffer.AddComponent<BulletTag>(bullet);
                            soldierShooting.ShootingTimer = 0.0f;
                        }
                    }

                    chunkSoldierShooting[i] = soldierShooting;
                }
            }
        }

        protected override void OnUpdate() {
            var soldierShootingType = GetComponentTypeHandle<SoldierShooting>();
            var translationType = GetComponentTypeHandle<Translation>(true);
            var targetCompType = GetComponentTypeHandle<TargetComp>(true);
            
            var soldierTargetArray = _soldierQuery.ToComponentDataArray<TargetComp>(Allocator.TempJob);
            var targetPositionArray =
                new NativeArray<float3>(_soldierQuery.CalculateEntityCount(), Allocator.TempJob);
            var dt = Time.DeltaTime;
            
            for (int i = 0; i < targetPositionArray.Length; ++i) {
                var target = soldierTargetArray[i].Target;
                if (target != Entity.Null && _entityManager.Exists(target))
                    targetPositionArray[i] = _entityManager.GetComponentData<Translation>(target).Value;
            }
            
            var job = new ShootJob {
                SoldierShootingHandle = soldierShootingType,
                TranslationHandle = translationType,
                CommandBuffer = _commandBufferSystem.CreateCommandBuffer(),
                TargetPositionArray = targetPositionArray,
                dt = dt,
                TargetCompHandle = targetCompType
            };
            Dependency = job.Schedule(_soldierQuery, Dependency);
            soldierTargetArray.Dispose();
            Dependency.Complete();
        }
    }

    public struct BulletTag : IComponentData {
    }
}