using System.Diagnostics;
using Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Systems {
    public partial class SoldierTravelSystem : SystemBase {
        private EntityQuery _entityQuery;
        
        private Translation _coreTranslation;
        private ZombieSpawnerComponent _coreSpawner;

        protected override void OnStartRunning() {
            base.OnStartRunning();
            _entityQuery = GetEntityQuery(typeof(PhysicsVelocity),
                ComponentType.ReadOnly<SoldierMovement>(),
                ComponentType.ReadOnly<Translation>());

            Entities
                .ForEach((ref SoldierMovement soldierMovement,
                    in Translation translation) =>
                {
                    soldierMovement.destination = translation.Value;
                })
                .Schedule();
            
            var coreEntity = GetEntityQuery(typeof(CoreHealthComponent),
                    typeof(Translation))
                .GetSingletonEntity();
            _coreTranslation = EntityManager.GetComponentData<Translation>(coreEntity);
            _coreSpawner = EntityManager.GetComponentData<ZombieSpawnerComponent>(coreEntity);
        }

        [BurstCompile]
        private struct TravelJob : IJobEntityBatch {
            public ComponentTypeHandle<Translation> TranslationHandle;
            [ReadOnly] public ComponentTypeHandle<SoldierMovement> SoldierMovementHandle;
            public ComponentTypeHandle<PhysicsVelocity> VelocityHandle;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
                var chunkTranslation = batchInChunk.GetNativeArray(TranslationHandle);
                var chunkVelocity = batchInChunk.GetNativeArray(VelocityHandle);
                var chunkSoldierMovement = batchInChunk.GetNativeArray(SoldierMovementHandle);
                for (var i = 0; i < batchInChunk.Count; i++) {
                    var translation = chunkTranslation[i];
                    var soldierMovement = chunkSoldierMovement[i];
                    if (math.length(translation.Value - soldierMovement.destination) > 0.1f) {
                        float3 dir = math.normalize(soldierMovement.destination - translation.Value);
                        // Rotate something about its up vector at the speed given by RotationSpeed_IJobChunk.
                        chunkVelocity[i] = new PhysicsVelocity {
                            Linear = dir * soldierMovement.speed
                        };
                    } else {
                        chunkTranslation[i] = new Translation {
                            Value = soldierMovement.destination
                        };
                        chunkVelocity[i] = new PhysicsVelocity {
                            Linear = float3.zero
                        };
                    }
                }
            }
        }

        protected override void OnUpdate() {
            var velocityType = GetComponentTypeHandle<PhysicsVelocity>();
            var translationType = GetComponentTypeHandle<Translation>();
            var soldierMoveType = GetComponentTypeHandle<SoldierMovement>();
            var job = new TravelJob {
                TranslationHandle = translationType,
                VelocityHandle = velocityType,
                SoldierMovementHandle = soldierMoveType
            };
            Dependency = job.ScheduleParallel(_entityQuery, Dependency);
            Dependency.Complete();
        }
    }
}