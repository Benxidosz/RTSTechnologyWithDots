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
using Debug = UnityEngine.Debug;
using Random = Unity.Mathematics.Random;

namespace Systems {
    [UpdateAfter(typeof(StepPhysicsWorld))]
    [UpdateAfter(typeof(BeginSimulationEntityCommandBufferSystem))]
    public partial class SoldierFindTargetSystem : SystemBase {
        private EntityQuery _soldierQuery;
        private EntityQuery _targetQuery;
        private GridSliceComponent _gridSlice;

        private NativeArray<Entity> _targetPolarGrid;
        private NativeArray<Entity> _soldierPolarGrid;


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

            _targetPolarGrid = new NativeArray<Entity>(
                0, Allocator.Persistent
            );
            _soldierPolarGrid = new NativeArray<Entity>(
                0, Allocator.Persistent
            );
        }

        [BurstCompile]
        private struct PolarIndexCalculateJob : IJobParallelFor {
            public NativeArray<int> CountIndexArray;

            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Translation> TargetTranslationArray;

            public float RStep;
            public float AngleStep;
            public int RCount;
            public int AngleCount;

            public void Execute(int i) {
                var tmpPosition = TargetTranslationArray[i].Value;

                int r = (int) math.floor(math.length(tmpPosition) / RStep);

                float3 baseVec = new float3(0.0f, 0.0f, 1.0f);
                float tmpAlfa = math.acos(math.dot(baseVec,
                    math.normalize(tmpPosition)));
                float3 dir = math.cross(baseVec, math.normalize(tmpPosition));
                if (dir.y < 0)
                    tmpAlfa = 2 * math.PI - tmpAlfa;
                int angle = (int) math.floor(tmpAlfa / AngleStep);

                var countIndex = r * AngleCount + angle;

                if (r >= RCount) {
                    countIndex = (RCount - 1) * AngleCount + angle;
                }

                CountIndexArray[i] = countIndex;
            }
        }

        [BurstCompile]
        private struct PolarGridBuilderJob : IJob {
            public NativeArray<Entity> Grid;
            public NativeArray<int> CellCounts;

            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> CountIndexArray;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> TargetArray;

            public void Execute() {
                for (int i = 0; i < CountIndexArray.Length; ++i) {
                    int countIndex = CountIndexArray[i];
                    var entity = TargetArray[i];
                    int count = CellCounts[countIndex];
                    int index = count + countIndex * TargetArray.Length;

                    Grid[index] = entity;
                    CellCounts[countIndex]++;
                }
            }
        }

        //[BurstCompile]
        private struct SoldierFindTargetJob : IJobEntityBatch {
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

                    float3 baseVec = new float3(0.0f, 0.0f, 1.0f);
                    float tmpAlfa = math.acos(math.dot(baseVec,
                        math.normalize(soldierPosition)));
                    float3 dir = math.cross(baseVec, math.normalize(soldierPosition));
                    if (dir.y < 0)
                        tmpAlfa = 2 * math.PI - tmpAlfa;
                    int angle = (int) math.floor(tmpAlfa / AngleStep);

                    var countIndex = r * AngleCount + angle;

                    if (TargetCellCounts[countIndex] == 0) {
                        bool found = false;
                        int dist = 1;

                        while (!found && dist < MaxDist) {
                            for (int diff = 0; diff <= dist; ++diff) {
                                int rDiff = dist - diff;
                                int tmpR = r + rDiff;
                                if (tmpR < 0)
                                    tmpR = -tmpR;
                                int tmpAnglePos = angle + diff;
                                while (tmpAnglePos >= AngleCount)
                                    tmpAnglePos -= AngleCount;

                                int tmpAngleNeg = angle - diff;
                                while (tmpAngleNeg < 0) {
                                    tmpAngleNeg += AngleCount;
                                }

                                if (tmpR < RCount && tmpAnglePos >= 0) {
                                    var tmpCountIndex = tmpR * AngleCount + tmpAnglePos;
                                    if (TargetCellCounts[tmpCountIndex] > 0) {
                                        found = true;
                                        countIndex = tmpCountIndex;
                                        break;
                                    }
                                }

                                if (tmpR < RCount && tmpAngleNeg < AngleCount) {
                                    var tmpCountIndex = tmpR * AngleCount + tmpAngleNeg;
                                    if (TargetCellCounts[tmpCountIndex] > 0) {
                                        Debug.Log(
                                            $"FOUND: r: {tmpR}, angle: {tmpAnglePos}, countIndex: {tmpAngleNeg}");
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
                        closestTarget = Grid[firstIndex + Random.NextInt(0, count - 1)];
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
            
            var soldierEntityArray = _targetQuery.ToEntityArray(Allocator.TempJob);
            var soldierTranslationArray = _targetQuery.ToComponentDataArray<Translation>(Allocator.TempJob);

            float rStep = _gridSlice.rStep;
            float angleStep = _gridSlice.angleStep;
            int rCount = (int) math.floor(_gridSlice.mapSize / rStep) + 1;
            int angleCount = (int) math.floor(2 * math.PI / angleStep);
            float maxDist = _gridSlice.mapSize;

            int targetCount = targetEntityArray.Length;
            var rand = new Random((uint) Stopwatch.GetTimestamp());

            if (_targetPolarGrid.Length < (rCount + 1) * angleCount * targetEntityArray.Length)
                ReallocateGrid((rCount + 1) * angleCount * targetEntityArray.Length, ref _targetPolarGrid);

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


            var targetIndexBuilderJob = new PolarIndexCalculateJob {
                TargetTranslationArray = targetTranslationArray,
                RCount = rCount,
                RStep = rStep,
                AngleCount = angleCount,
                AngleStep = angleStep,
                CountIndexArray = targetIndexArray
            };

            var targetGridBuilderJob = new PolarGridBuilderJob {
                Grid = _targetPolarGrid,
                CellCounts = targetCellCounts,
                CountIndexArray = targetIndexArray,
                TargetArray = targetEntityArray
            };

            var findTargetJob = new SoldierFindTargetJob {
                TranslationHandle = translationType,
                TargetCompHandle = soldierShootingType,
                TargetCellCounts = targetCellCounts,
                Grid = _targetPolarGrid,
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
            Dependency.Complete();
            int countIndex = 0;
            if (targetCount > 0) {
                countIndex = targetIndexArray[targetCount - 1];
                //Debug.Log($"Target CountIndex: {countIndex}");
            }

            Dependency = targetGridBuilderJob.Schedule(Dependency);
            Dependency.Complete();
            if (targetCount > 0) {
                /*Debug.Log($"TargetCount In Cell {countIndex}: {targetCellCounts[countIndex]}");
                Debug.Log($"Last Target In Cell {countIndex}: {_targetPolarGrid[countIndex]}");
                Debug.Log($"targetCellCounts.Length: {targetCellCounts.Length}");*/
            }

            Dependency = findTargetJob.ScheduleParallel(_soldierQuery, Dependency);
            Dependency.Complete();
        }

        protected override void OnStopRunning() {
            base.OnStopRunning();
            _targetPolarGrid.Dispose();
            _soldierPolarGrid.Dispose();
        }
    }
}