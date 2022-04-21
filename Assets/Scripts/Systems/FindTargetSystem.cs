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
using Random = Unity.Mathematics.Random;

namespace Systems {
    [UpdateAfter(typeof(StepPhysicsWorld))]
    [UpdateAfter(typeof(BeginSimulationEntityCommandBufferSystem))]
    public partial class FindTargetSystem : SystemBase {
        private EntityQuery _soldierQuery;
        private EntityQuery _targetQuery;
        private GridSliceComponent _gridSlice;

        private NativeArray<Entity> _targetGrid;


        protected override void OnStartRunning() {
            base.OnStartRunning();
            var queryDesc = new EntityQueryDesc {
                All = new ComponentType[] {
                    typeof(SoldierShooting),
                    typeof(SoldierMovement),
                    typeof(Translation)
                }
            };
            var targetDesc = new EntityQueryDesc {
                All = new[] {
                    ComponentType.ReadOnly<TravelToCore>(),
                    ComponentType.ReadOnly<Translation>()
                }
            };
            _soldierQuery = GetEntityQuery(queryDesc);
            _targetQuery = GetEntityQuery(targetDesc);

            _gridSlice = GetSingleton<GridSliceComponent>();

            float rStep = _gridSlice.rStep;
            float angleStep = _gridSlice.angleStep;
            int rCount = (int) math.ceil(_gridSlice.mapSize / rStep) + 1;
            int angleCount = (int) math.ceil(2 * math.PI / angleStep);

            _targetGrid = new NativeArray<Entity>(
                0, Allocator.Persistent
            );
        }

        [BurstCompile]
        private struct IndexCalculateJob : IJobParallelFor {
            public NativeArray<int> CountIndexArray;

            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Translation> TargetTranslationArray;

            public float RStep;
            public float AngleStep;
            public int RCount;
            public int AngleCount;

            public void Execute(int i) {
                var tmpPosition = TargetTranslationArray[i].Value;

                int r = (int) math.floor(math.length(tmpPosition) / RStep);
                float tmpAlfa = math.acos(math.dot(new float3(0.0f, 0.0f, 1.0f),
                    math.normalize(tmpPosition)));
                int angle = (int) math.floor(tmpAlfa / AngleStep);

                var countIndex = r * AngleCount + angle;

                if (r >= RCount) {
                    countIndex = (RCount - 1) * AngleCount + angle;
                }

                CountIndexArray[i] = countIndex;
            }
        }

        [BurstCompile]
        private struct GridBuilderJob : IJob {
            public NativeArray<Entity> Grid;
            public NativeArray<int> CellCounts;

            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> CountIndexArray;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> TargetArray;

            public void Execute() {
                for (int i = 0; i < CountIndexArray.Length; ++i) {
                    int countIndex = CountIndexArray[i];
                    var entity = TargetArray[i];
                    int count = CellCounts[countIndex];
                    int index = count + countIndex * TargetArray.Length + 1;

                    Grid[index] = entity;
                    CellCounts[countIndex]++;
                }
            }
        }

        [BurstCompile]
        private struct FindTargetJob : IJobEntityBatch {
            [ReadOnly] public NativeArray<Entity> Grid;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> TargetCellCounts;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> SoldierCellCounts;
            public ComponentTypeHandle<TargetComp> TargetCompHandle;
            [ReadOnly] public ComponentTypeHandle<Translation> TranslationHandle;
            public float RStep;
            public float AngleStep;
            public int AngleCount;
            public int RCount;
            public int TargetCount;
            public Random Random;
            public float MaxDist;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
                var chunkTranslation = batchInChunk.GetNativeArray(TranslationHandle);
                var chunkSoldierTargetComp = batchInChunk.GetNativeArray(TargetCompHandle);

                for (var i = 0; i < batchInChunk.Count; i++) {
                    var translation = chunkTranslation[i];
                    var soldierShooting = chunkSoldierTargetComp[i];

                    float3 soldierPosition = translation.Value;
                    Entity closestTarget = Entity.Null;

                    int r = (int) math.floor(math.length(soldierPosition) / RStep);
                    float tmpAlfa = math.acos(math.dot(new float3(0.0f, 0.0f, 1.0f),
                        math.normalize(soldierPosition)));
                    int angle = (int) math.floor(tmpAlfa / AngleStep);

                    var countIndex = r * AngleCount + angle;

                    if (TargetCellCounts[countIndex] == 0) {
                        bool found = false;
                        bool has = true;
                        int dist = 1;

                        while (!found && has && dist < MaxDist) {
                            has = false;
                            for (int diff = 0; diff <= dist; ++diff) {
                                int alfaDiffPositive = i;
                                int alfaDiffNegative = -i;
                                int rDiff = dist - diff;
                                int tmpRPos = r + rDiff;
                                int tmpRNeg = r - rDiff;
                                int tmpAnglePos = angle + alfaDiffPositive;
                                int tmpAngleNeg = angle + alfaDiffNegative;
                                if (tmpRPos < RCount && tmpRPos >= 0 && tmpAnglePos >= 0 && tmpAnglePos < AngleCount) {
                                    has = true;
                                    var tmpCountIndex = tmpRPos * AngleCount + tmpAnglePos;
                                    if (TargetCellCounts[tmpCountIndex] > 0) {
                                        found = true;
                                        countIndex = tmpCountIndex;
                                        break;
                                    }
                                }

                                if (tmpRPos < RCount && tmpRPos >= 0 && tmpAngleNeg >= 0 && tmpAngleNeg < AngleCount) {
                                    has = true;
                                    var tmpCountIndex = tmpRPos * AngleCount + tmpAngleNeg;
                                    if (TargetCellCounts[tmpCountIndex] > 0) {
                                        found = true;
                                        countIndex = tmpCountIndex;
                                        break;
                                    }
                                }
                                if (tmpRNeg < RCount && tmpRNeg >= 0 && tmpAnglePos >= 0 && tmpAnglePos < AngleCount) {
                                    has = true;
                                    var tmpCountIndex = tmpRNeg * AngleCount + tmpAnglePos;
                                    if (TargetCellCounts[tmpCountIndex] > 0) {
                                        found = true;
                                        countIndex = tmpCountIndex;
                                        break;
                                    }
                                }

                                if (tmpRNeg < RCount && tmpRNeg >= 0 && tmpAngleNeg >= 0 && tmpAngleNeg < AngleCount) {
                                    has = true;
                                    var tmpCountIndex = tmpRNeg * AngleCount + tmpAngleNeg;
                                    if (TargetCellCounts[tmpCountIndex] > 0) {
                                        found = true;
                                        countIndex = tmpCountIndex;
                                        break;
                                    }
                                }
                            }

                            dist++;
                        }
                    }

                    var count = TargetCellCounts[countIndex];
                    if (count > 0) {
                        var firstIndex = countIndex * TargetCount;
                        closestTarget = Grid[firstIndex + Random.NextInt(1, count)];
                    }

                    if (closestTarget != Entity.Null) {
                        soldierShooting.Target = closestTarget;
                        chunkSoldierTargetComp[i] = soldierShooting;
                    }
                }
            }
        }

        private void ReallocateGrid(int length, ref NativeArray<Entity> grid) {
            grid.Dispose();
            grid = new NativeArray<Entity>(
                length * 2, Allocator.Persistent
            );
        }

        protected override void OnUpdate() {
            Dependency.Complete();
            var translationType = GetComponentTypeHandle<Translation>(true);
            var soldierShootingType = GetComponentTypeHandle<TargetComp>();

            var targetEntityArray = _targetQuery.ToEntityArray(Allocator.TempJob);
            var targetTranslationArray = _targetQuery.ToComponentDataArray<Translation>(Allocator.TempJob);

            float rStep = _gridSlice.rStep;
            float angleStep = _gridSlice.angleStep;
            int rCount = (int) math.ceil(_gridSlice.mapSize / rStep) + 1;
            int angleCount = (int) math.ceil(2 * math.PI / angleStep);
            float maxDist = _gridSlice.mapSize;

            int targetCount = targetEntityArray.Length;
            var rand = new Random((uint) Stopwatch.GetTimestamp());

            if (_targetGrid.Length < (rCount + 1) * angleCount * targetEntityArray.Length) 
                ReallocateGrid((rCount + 1) * angleCount * targetEntityArray.Length, ref _targetGrid);

            var targetCellCounts = new NativeArray<int>(
                (rCount + 1) * angleCount, Allocator.TempJob
            );
            var soldierCellCounts = new NativeArray<int>(
                (rCount + 1) * angleCount, Allocator.TempJob
            );

            for (int i = 0; i < targetCellCounts.Length; ++i) {
                targetCellCounts[i] = 0;
                soldierCellCounts[i] = 0;
            }

            var targetIndexArray = new NativeArray<int>(
                targetCount, Allocator.TempJob
            );

            var targetIndexBuilderJob = new IndexCalculateJob {
                TargetTranslationArray = targetTranslationArray,
                RCount = rCount,
                RStep = rStep,
                AngleCount = angleCount,
                AngleStep = angleStep,
                CountIndexArray = targetIndexArray
            };

            var targetGridBuilderJob = new GridBuilderJob {
                Grid = _targetGrid,
                CellCounts = targetCellCounts,
                CountIndexArray = targetIndexArray,
                TargetArray = targetEntityArray
            };

            var findTargetJob = new FindTargetJob {
                TranslationHandle = translationType,
                TargetCompHandle = soldierShootingType,
                TargetCellCounts = targetCellCounts,
                Grid = _targetGrid,
                RStep = rStep,
                AngleStep = angleStep,
                AngleCount = angleCount,
                RCount = rCount,
                TargetCount = targetCount,
                Random = rand,
                MaxDist = maxDist,
                SoldierCellCounts = soldierCellCounts
            };
            Dependency = targetIndexBuilderJob.Schedule(targetCount, 64, Dependency);
            Dependency = targetGridBuilderJob.Schedule(Dependency);
            Dependency = findTargetJob.ScheduleParallel(_soldierQuery, Dependency);

            Dependency.Complete();
        }

        protected override void OnStopRunning() {
            base.OnStopRunning();
            _targetGrid.Dispose();
        }
    }
}