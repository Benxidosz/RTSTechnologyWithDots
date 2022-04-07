using System;
using System.Collections.Generic;
using System.Diagnostics;
using Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEditor;
using Debug = UnityEngine.Debug;
using Random = Unity.Mathematics.Random;

namespace Systems {
    [UpdateAfter(typeof(StepPhysicsWorld))]
    [UpdateAfter(typeof(BeginSimulationEntityCommandBufferSystem))]
    public partial class FindTargetSystem : SystemBase {
        private EntityQuery _soldierQuery;
        private EntityQuery _targetQuery;
        private GridSliceComponent _gridSlice;

        protected override void OnStartRunning() {
            base.OnStartRunning();
            var queryDesc = new EntityQueryDesc {
                All = new ComponentType[] {
                    typeof(SoldierShooting),
                    typeof(SoldierMovement)
                }
            };
            _soldierQuery = GetEntityQuery(queryDesc);
            _targetQuery = GetEntityQuery(ComponentType.ReadOnly<TravelToCore>(),
                ComponentType.ReadOnly<Translation>());

            _gridSlice = GetSingleton<GridSliceComponent>();
        }

        private struct GridBuilderJob : IJobFor {
            public NativeArray<EntityWithPositionOrCount> Grid;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Translation> TargetTranslationArray;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> TargetArray;

            public void Execute(int index) {
                throw new NotImplementedException();
            }
        } 

        [BurstCompile]
        private struct FindTargetJob : IJobEntityBatch {
            [ReadOnly] public NativeArray<EntityWithPositionOrCount> Grid;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> Soldiers;
            public ComponentTypeHandle<TargetComp> TargetCompHandle;
            [ReadOnly] public ComponentTypeHandle<Translation> TranslationHandle;
            public float RStep;
            public float AngleStep;
            public int AngleCount;
            public int RCount;
            public int TargetCount;
            public Random Random;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
                var chunkTranslation = batchInChunk.GetNativeArray(TranslationHandle);
                var chunkSoldierTargetComp = batchInChunk.GetNativeArray(TargetCompHandle);
                
                for (var i = 0; i < batchInChunk.Count; i++) {
                    var translation = chunkTranslation[i];
                    var soldierShooting = chunkSoldierTargetComp[i];

                    float3 soldierPosition = translation.Value;
                    Entity closestTarget = Entity.Null;
                    TravelToCore closestTravelComp;
                    
                    int r = (int) math.floor(math.length(soldierPosition) / RStep);
                    float tmpAlfa = math.acos(math.dot(new float3(0.0f, 0.0f, 1.0f),
                        math.normalize(soldierPosition)));
                    int angle = (int) math.floor(tmpAlfa / AngleStep);

                    var countIndex = (r * AngleCount + angle) * TargetCount;
                    if (Grid[countIndex].Count == 0) {
                        bool found = false;
                        bool has = true;
                        int dist = 1;
                    
                        while (!found && has && dist < 100) {
                            has = false;
                            for (int diff = -dist; diff <= dist; ++diff) {
                                int alfaDiff = i;
                                int rDiff = dist - diff;
                                int tmpR = r + rDiff;
                                int tmpAngle = angle + alfaDiff;
                                if (tmpR >= 0 && tmpR < RCount && tmpAngle >= 0 && tmpAngle < AngleCount) {
                                    has = true;
                                    var tmpCountIndex = (tmpR * AngleCount + tmpAngle) * TargetCount;
                                    if (Grid[tmpCountIndex].Count > 0) {
                                        found = true;
                                        countIndex = tmpCountIndex;
                                        break;
                                    }
                                }
                            }

                            dist++;
                        }
                    }

                    var count = Grid[countIndex].Count;
                    if (count > 0) {
                        var firstIndex = countIndex + 1;
                        closestTarget = Grid[firstIndex + Random.NextInt(1, count)].Entity;
                        closestTravelComp = Grid[firstIndex + Random.NextInt(1, count)].TravelComponent;
                    }
                    if (closestTarget != Entity.Null) {
                        soldierShooting.Target = closestTarget;
                        closestTravelComp.Soldier = Soldiers[i];
                        chunkSoldierTargetComp[i] = soldierShooting;
                    }
                }
            }
        }

        protected override void OnUpdate() {
            Dependency.Complete();
            var translationType = GetComponentTypeHandle<Translation>(true);
            var soldierShootingType = GetComponentTypeHandle<TargetComp>();

            var targetEntityArray = _targetQuery.ToEntityArray(Allocator.TempJob);
            var targetTranslationArray = _targetQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
            var targetTravelArray = _targetQuery.ToComponentDataArray<TravelToCore>(Allocator.TempJob);
            var soldierArray = _soldierQuery.ToEntityArray(Allocator.TempJob);

            float rStep = _gridSlice.rStep;
            float angleStep = _gridSlice.angleStep;
            int rCount = (int) math.ceil(_gridSlice.mapSize / rStep) + 1;
            int angleCount = (int) math.ceil(2 * math.PI / angleStep);

            int targetCount = targetEntityArray.Length;
            var rand = new Random((uint) Stopwatch.GetTimestamp());

            var grid = new NativeArray<EntityWithPositionOrCount>(
                (rCount + 1) * angleCount * (targetEntityArray.Length + 1), Allocator.TempJob
            );


            for (int r = 0; r < rCount; r++) {
                for (int angle = 0; angle < angleCount; angle++) {
                    var countIndex = (r * angleCount + angle) * targetEntityArray.Length;
                    grid[countIndex] = new EntityWithPositionOrCount {
                        Count = 0
                    };
                }
            }

            for (int i = 0; i < targetEntityArray.Length; ++i) {
                var tmpEntity = targetEntityArray[i];
                var tmpPosition = targetTranslationArray[i].Value;
                var tmpTravel = targetTravelArray[i];

                int r = (int) math.floor(math.length(tmpPosition) / rStep);
                float tmpAlfa = math.acos(math.dot(new float3(0.0f, 0.0f, 1.0f),
                    math.normalize(tmpPosition)));
                int angle = (int) math.floor(tmpAlfa / angleStep);

                var countIndex = (r * angleCount + angle) * targetEntityArray.Length;

                if (r >= rCount) {
                    countIndex = (rCount * angleCount + angle) * targetEntityArray.Length;
                }

                var count = grid[countIndex].Count;
                var index = countIndex + 1 + count;
                grid[index] = new EntityWithPositionOrCount {
                    Entity = tmpEntity,
                    TravelComponent = tmpTravel
                };
                grid[countIndex] = new EntityWithPositionOrCount {
                    Count = count + 1
                };
            }


            var job = new FindTargetJob {
                TranslationHandle = translationType,
                TargetCompHandle = soldierShootingType,
                Grid = grid,
                RStep = rStep,
                AngleStep = angleStep,
                AngleCount = angleCount,
                RCount = rCount,
                TargetCount = targetCount,
                Random = rand,
                Soldiers = soldierArray
            };
            Dependency = job.ScheduleParallel(_soldierQuery, Dependency);

            Dependency.Complete();
            
            targetEntityArray.Dispose();
            targetTranslationArray.Dispose();
            targetTravelArray.Dispose();
            grid.Dispose();
        }
    }

    struct EntityWithPositionOrCount {
        public Entity Entity;
        public TravelToCore TravelComponent;
        public int Count;
    }
}