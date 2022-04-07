using Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace Systems {
    public partial class TravelToCoreSystem : SystemBase {
        private Translation _coreTranslation;
        private EntityQuery _entityQuery;

        protected override void OnStartRunning() {
            base.OnStartRunning();
            var coreEntity = GetEntityQuery(typeof(CoreHealthComponent),
                    typeof(Translation))
                .GetSingletonEntity();
            _coreTranslation = EntityManager.GetComponentData<Translation>(coreEntity);

            _entityQuery = GetEntityQuery(typeof(PhysicsVelocity),
                ComponentType.ReadOnly<TravelToCore>(),
                ComponentType.ReadOnly<Translation>());
        }

        [BurstCompile]
        private struct TravelJob : IJobEntityBatch {
            public float3 CorePos;
            [ReadOnly] public ComponentTypeHandle<Translation> TranslationHandle; 
            [ReadOnly] public ComponentTypeHandle<TravelToCore> TravelToCoreHandle; 
            public ComponentTypeHandle<PhysicsVelocity> VelocityHandle;
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
                var chunkTranslation = batchInChunk.GetNativeArray(TranslationHandle);
                var chunkVelocity = batchInChunk.GetNativeArray(VelocityHandle);
                var chunkTravelToCore = batchInChunk.GetNativeArray(TravelToCoreHandle);
                for (var i = 0; i < batchInChunk.Count; i++) {
                    var translation = chunkTranslation[i];
                    var travelToCore = chunkTravelToCore[i];

                    float3 dir = math.normalize(CorePos - translation.Value);
                    // Rotate something about its up vector at the speed given by RotationSpeed_IJobChunk.
                    chunkVelocity[i] = new PhysicsVelocity {
                        Linear = dir * travelToCore.Speed
                    };
                }
            }
        }

        protected override void OnUpdate() {
            float3 corePos = _coreTranslation.Value;
            var velocityType = GetComponentTypeHandle<PhysicsVelocity>();
            var translationType = GetComponentTypeHandle<Translation>(true);
            var travelToCoreType = GetComponentTypeHandle<TravelToCore>(true);

            var job = new TravelJob {
                CorePos = corePos,
                TranslationHandle = translationType,
                VelocityHandle = velocityType,
                TravelToCoreHandle = travelToCoreType
            };

            Dependency = job.ScheduleParallel(_entityQuery, Dependency);
            
            Dependency.Complete();
        }
    }
}