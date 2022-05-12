using Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Systems {
    public partial class ZombiHittingFloorSystem : SystemBase {
        private EndSimulationEntityCommandBufferSystem _commandBufferSystem;
        private EntityQuery _flyingZombies;
        
        protected override void OnStartRunning() {
            _commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            var desc = new EntityQueryDesc {
                All = new[] {
                    ComponentType.ReadOnly<ZombieTag>()
                },
                None = new [] {
                    ComponentType.ReadOnly<TravelToCore>() 
                }
            };
            _flyingZombies = GetEntityQuery(desc);
        }
        
        [BurstCompile]
        struct ZombiDestroyJob : IJobEntityBatch {
            public EntityCommandBuffer CommandBuffer;
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Translation> TranslationHandle;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
                var entityChunk = batchInChunk.GetNativeArray(EntityTypeHandle);
                var translationChunk = batchInChunk.GetNativeArray(TranslationHandle);
                for (var i = 0; i < batchInChunk.Count; i++) {
                    var entity = entityChunk[i];
                    var pos = translationChunk[i].Value;
                    if (pos.y < 0f)
                        CommandBuffer.DestroyEntity(entity);
                }
            }
        }
        
        protected override void OnUpdate() {
            var entityTypeHandle = GetEntityTypeHandle();
            var translationTypeHandle = GetComponentTypeHandle<Translation>();

            var zombiDestroyJob = new ZombiDestroyJob {
                CommandBuffer = _commandBufferSystem.CreateCommandBuffer(),
                EntityTypeHandle = entityTypeHandle,
                TranslationHandle = translationTypeHandle
            };

            Dependency = zombiDestroyJob.Schedule(_flyingZombies, Dependency);
            Dependency.Complete();
        }
    }
}