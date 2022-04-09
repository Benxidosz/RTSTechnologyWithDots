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
        
        [BurstCompile]
        private struct IndexCalculateJob : IJobParallelFor {
            public NativeArray<int> CountIndexArray;

            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Translation> TargetTranslationArray;

            public float RStep;
            public float AngleStep;
            public int RCount;
            public int AngleCount;
            public int TargetCount;
            public void Execute(int i) {
                var tmpPosition = TargetTranslationArray[i].Value;

                int r = (int) math.floor(math.length(tmpPosition) / RStep);
                float tmpAlfa = math.acos(math.dot(new float3(0.0f, 0.0f, 1.0f),
                    math.normalize(tmpPosition)));
                int angle = (int) math.floor(tmpAlfa / AngleStep);

                var countIndex = (r * AngleCount + angle) * TargetCount;

                if (r >= RCount) {
                    countIndex = ((RCount - 1) * AngleCount + angle) * TargetCount;
                }

                CountIndexArray[i] = countIndex;
            }
        }

        [BurstCompile]
        private struct GridBuilderJob : IJob {
            public NativeArray<EntityWithPositionOrCount> Grid;
            
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> CountIndexArray;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<TravelToCore> TargetTravelArray;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> TargetArray;

            public void Execute() {
                for (int i = 0; i < CountIndexArray.Length; ++i) {
                    int countIndex = CountIndexArray[i];
                    var entity = TargetArray[i];
                    var travel = TargetTravelArray[i];
                    int count = Grid[countIndex].Count;
                    int index = count + countIndex + 1;
                    
                    Grid[index] = new EntityWithPositionOrCount {
                        Entity = entity,
                        TravelComponent = travel
                    };
                    Grid[countIndex] = new EntityWithPositionOrCount {
                        Count = count + 1
                    };
                }
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

            var indexArray = new NativeArray<int>(
                targetCount, Allocator.TempJob
            );


            for (int r = 0; r < rCount; r++) {
                for (int angle = 0; angle < angleCount; angle++) {
                    var countIndex = (r * angleCount + angle) * targetEntityArray.Length;
                    grid[countIndex] = new EntityWithPositionOrCount {
                        Count = 0
                    };
                }
            }

            var indexBuilderJob = new IndexCalculateJob {
                TargetTranslationArray = targetTranslationArray,
                RCount = rCount,
                RStep = rStep,
                AngleCount = angleCount,
                AngleStep = angleStep,
                TargetCount = targetCount,
                CountIndexArray = indexArray
            };

            var gridBuilderJob = new GridBuilderJob {
                Grid = grid,
                CountIndexArray = indexArray,
                TargetTravelArray = targetTravelArray,
                TargetArray = targetEntityArray
            };

            var findTargetJob = new FindTargetJob {
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
            Dependency = indexBuilderJob.Schedule(targetCount, 64, Dependency);
            Dependency = gridBuilderJob.Schedule(Dependency);
            Dependency.Complete();
            
            Dependency = findTargetJob.ScheduleParallel(_soldierQuery, Dependency);

            Dependency.Complete();
            
            grid.Dispose();
        }
    }

    struct EntityWithPositionOrCount {
        public Entity Entity;
        public TravelToCore TravelComponent;
        public int Count;
    }
}