using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    public partial class FindTargetSystem : SystemBase {
        private EntityQuery _soldierQuery;
        private EntityQuery _targetQuery;
        private EntityQuery _towerQuery;
        private GridSliceComponent _gridSlice;

        private NativeArray<Entity> _targetPolarGrid;
        private NativeArray<Entity> _soldierPolarGrid;

        private NativeArray<int> _targetCellCounts;
        private NativeArray<int> _soldierCellCounts;

        private NativeArray<int> _targetIndexArray;
        private NativeArray<int> _soldierIndexArray;

        private NativeArray<int> _soldierGridTargetCountIndex;
        
        private int frame = 0;


        protected override void OnStartRunning() {
            base.OnStartRunning();
            var soldierDesc = new EntityQueryDesc {
                All = new[] {
                    ComponentType.ReadOnly<Shooting>(),
                    ComponentType.ReadWrite<TargetEntityComp>(), 
                    typeof(SoldierMovement),
                    ComponentType.ReadOnly<Translation>()
                }
            };
            var targetDesc = new EntityQueryDesc {
                All = new[] {
                    ComponentType.ReadOnly<TravelToCore>(),
                    ComponentType.ReadOnly<Translation>()
                }
            };

            var towerDesc = new EntityQueryDesc {
                All = new[] {
                    ComponentType.ReadOnly<Shooting>(),
                    ComponentType.ReadWrite<TargetPosComp>(), 
                    ComponentType.ReadOnly<Translation>()
                }
            };
            _soldierQuery = GetEntityQuery(soldierDesc);
            _targetQuery = GetEntityQuery(targetDesc);
            _towerQuery = GetEntityQuery(towerDesc);

            _gridSlice = GetSingleton<GridSliceComponent>();

            _targetPolarGrid = new NativeArray<Entity>(
                0, Allocator.Persistent
            );
            _soldierPolarGrid = new NativeArray<Entity>(
                0, Allocator.Persistent
            );
            
            float rStep = _gridSlice.rStep;
            float angleStep = _gridSlice.angleStep;
            int rCount = (int) math.floor(_gridSlice.mapSize / rStep) + 1;
            int angleCount = (int) math.floor(2 * math.PI / angleStep);
            
            _targetCellCounts = new NativeArray<int>(
                (rCount + 1) * angleCount, Allocator.Persistent
            );
            _soldierCellCounts = new NativeArray<int>(
                (rCount + 1) * angleCount, Allocator.Persistent
            );
            
            _targetIndexArray = new NativeArray<int>(
                0, Allocator.Persistent
            );
            _soldierIndexArray = new NativeArray<int>(
                0, Allocator.Persistent
            );
            
            _soldierGridTargetCountIndex = new NativeArray<int>(
                (rCount + 1) * angleCount, Allocator.Persistent
            );
        }

        [BurstCompile]
        private struct PolarIndexCalculateJob : IJobParallelFor {
            public NativeArray<int> CountIndexArray;

            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Translation> TranslationArray;

            public float RStep;
            public float AngleStep;
            public int RCount;
            public int AngleCount;

            public void Execute(int i) {
                var tmpPosition = TranslationArray[i].Value;

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

            [ReadOnly] public NativeArray<int> CountIndexArray;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> EntityArray;

            public void Execute() {
                for (int i = 0; i < EntityArray.Length; ++i) {
                    int countIndex = CountIndexArray[i];
                    var entity = EntityArray[i];
                    int count = CellCounts[countIndex];
                    int index = count + countIndex * EntityArray.Length;

                    Grid[index] = entity;
                    CellCounts[countIndex]++;
                }
            }
        }
        
        [BurstCompile]
        private struct GridFindTargetJob : IJobParallelFor {
            [ReadOnly] public NativeArray<int> TargetCellCounts;
            [ReadOnly] public NativeArray<int> SoldierCellCounts;
            public NativeArray<int> SoldierGridTargetCountIndex;
            public int AngleCount;
            public float MaxDist;
            
            public void Execute(int countIndex) {
                if (SoldierCellCounts[countIndex] == 0)
                    return;
                int targetCountIndex = countIndex;
                if (TargetCellCounts[targetCountIndex] == 0) {
                    bool found = false;
                    int dist = 1;
                    while (!found && dist < MaxDist) {
                        for (int diff = 0; diff <= dist; ++diff) {
                            int rDiff = dist - diff;
                            int aDiff = diff;

                            int tmpTargetCountIndexPos = targetCountIndex + rDiff * AngleCount + aDiff;
                            int tmpTargetCountIndexNeg = targetCountIndex + rDiff * AngleCount - aDiff;

                            if (tmpTargetCountIndexPos >= 0 && tmpTargetCountIndexPos < TargetCellCounts.Length) {
                                if (TargetCellCounts[tmpTargetCountIndexPos] > 0) {
                                    found = true;
                                    targetCountIndex = tmpTargetCountIndexPos;
                                    break;
                                }
                            }

                            if (tmpTargetCountIndexNeg >= 0 && tmpTargetCountIndexNeg < TargetCellCounts.Length) {
                                if (TargetCellCounts[tmpTargetCountIndexNeg] > 0) {
                                    found = true;
                                    targetCountIndex = tmpTargetCountIndexNeg;
                                    break;
                                }
                            }
                        }

                        dist++;
                    }
                }

                SoldierGridTargetCountIndex[countIndex] = targetCountIndex;
            }
        }

        [BurstCompile]
        private struct SoldierFindTargetJob : IJobEntityBatch {
            [ReadOnly] public NativeArray<Entity> Grid;
            [ReadOnly] public NativeArray<int> SoldierGridTargetCountIndex;
            [ReadOnly] public NativeArray<int> TargetCellCounts;
            public ComponentTypeHandle<TargetEntityComp> TargetCompHandle;
            [ReadOnly] public ComponentTypeHandle<Translation> TranslationHandle;
            public float RStep;
            public float AngleStep;
            public int AngleCount;
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

                    int r = (int) math.floor(math.length(soldierPosition) / RStep);

                    float3 baseVec = new float3(0.0f, 0.0f, 1.0f);
                    float tmpAlfa = math.acos(math.dot(baseVec,
                        math.normalize(soldierPosition)));
                    float3 dir = math.cross(baseVec, math.normalize(soldierPosition));
                    if (dir.y < 0)
                        tmpAlfa = 2 * math.PI - tmpAlfa;
                    int angle = (int) math.floor(tmpAlfa / AngleStep);

                    var countIndex = r * AngleCount + angle;

                    var targetCountIndex = SoldierGridTargetCountIndex[countIndex];
                    if (targetCountIndex > -1) {
                        int count = TargetCellCounts[targetCountIndex];

                        if (count > 0) {
                            var firstIndex = targetCountIndex * TargetCount;
                            closestTarget = Grid[firstIndex + Random.NextInt(0, count - 1)];
                        }

                        if (closestTarget != Entity.Null) {
                            soldierShooting.Target = closestTarget;
                            chunkSoldierTargetComp[i] = soldierShooting;
                        }
                    }
                }
            }
        }

        [BurstCompile]
        public struct TowerFindTarget : IJobEntityBatch {
            [ReadOnly] public NativeArray<int> TargetCellCounts;
            
            public ComponentTypeHandle<TargetPosComp> TargetCompHandle;
            [ReadOnly] public ComponentTypeHandle<Translation> TranslationHandle;
            
            public float RStep;
            public float AngleStep;
            public int AngleCount;
            public Random Random;
            public float MaxDist;
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
                var chunkTranslation = batchInChunk.GetNativeArray(TranslationHandle);
                var chunkTowerTargetComp = batchInChunk.GetNativeArray(TargetCompHandle);
                
                for (var i = 0; i < batchInChunk.Count; i++) {
                    var translation = chunkTranslation[i];
                    var towerShooting = chunkTowerTargetComp[i];

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

                    int targetCountIndex = countIndex;
                    int targetRDiff = 0;
                    int targetAngleDiff = 0; 
                    if (TargetCellCounts[targetCountIndex] == 0) {
                        bool found = false;
                        int dist = 1;
                        while (!found && dist < MaxDist) {
                            for (int diff = 0; diff <= dist; ++diff) {
                                int rDiff = dist - diff;
                                int aDiff = diff;

                                int tmpTargetCountIndexPos = countIndex + rDiff * AngleCount + aDiff;
                                int tmpTargetCountIndexNeg = countIndex + rDiff * AngleCount - aDiff;

                                if (tmpTargetCountIndexPos >= 0 && tmpTargetCountIndexPos < TargetCellCounts.Length) {
                                    if (TargetCellCounts[tmpTargetCountIndexPos] > 0) {
                                        found = true;
                                        targetCountIndex = tmpTargetCountIndexPos;
                                        targetRDiff = rDiff;
                                        targetAngleDiff = aDiff;
                                        angle += aDiff;
                                        r += rDiff;
                                        break;
                                    }
                                }

                                if (tmpTargetCountIndexNeg >= 0 && tmpTargetCountIndexNeg < TargetCellCounts.Length) {
                                    if (TargetCellCounts[tmpTargetCountIndexNeg] > 0) {
                                        found = true;
                                        targetCountIndex = tmpTargetCountIndexNeg;
                                        targetRDiff = rDiff;
                                        targetAngleDiff = -aDiff;
                                        angle += aDiff;
                                        r += rDiff;
                                        break;
                                    }
                                }
                            }

                            dist++;
                        }
                    }

                    r = targetCountIndex / AngleCount;
                    angle = targetCountIndex - 1;
                    float targetR = Random.NextFloat((r - 1) * RStep, r * RStep);
                    float targetA = Random.NextFloat((-angle + 1) * AngleStep, (-angle + 2) * AngleStep);
                    float3 targetPos = new float3(math.cos(targetA) * targetR, 0f, math.sin(targetA) * targetR);

                    towerShooting.pos = targetPos;
                    chunkTowerTargetComp[i] = towerShooting;
                }
            }
        }

        private void ReallocateArray<T>(int length, ref NativeArray<T> array) where T : struct {
            array.Dispose();
            array = new NativeArray<T>(
                length * 2, Allocator.Persistent
            );
        }

        protected override void OnUpdate() {
            //TODO: Limit the refresh time. All Array to global -> tasks in different time.
            Dependency.Complete();

            var targetEntityArray = _targetQuery.ToEntityArray(Allocator.TempJob);
            var targetTranslationArray = _targetQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
            
            var soldierEntityArray = _soldierQuery.ToEntityArray(Allocator.TempJob);
            var soldierTranslationArray = _soldierQuery.ToComponentDataArray<Translation>(Allocator.TempJob);

            float rStep = _gridSlice.rStep;
            float angleStep = _gridSlice.angleStep;
            int rCount = (int) math.floor(_gridSlice.mapSize / rStep) + 1;
            int angleCount = (int) math.floor(2 * math.PI / angleStep);
            float maxDist = _gridSlice.mapSize;

            int targetCount = targetEntityArray.Length;
            int soldierCount = soldierEntityArray.Length;
            var rand = new Random((uint) Stopwatch.GetTimestamp());

            if (_targetPolarGrid.Length < (rCount + 1) * angleCount * targetCount)
                ReallocateArray((rCount + 1) * angleCount * targetCount, ref _targetPolarGrid);
            if (_soldierPolarGrid.Length < (rCount + 1) * angleCount * soldierCount)
                ReallocateArray((rCount + 1) * angleCount * soldierCount, ref _soldierPolarGrid);
            if (_targetIndexArray.Length < targetCount)
                ReallocateArray(targetCount * 2, ref _targetIndexArray);
            if (_soldierIndexArray.Length < soldierCount)
                ReallocateArray(soldierCount * 2, ref _soldierIndexArray);
            
            for (int i = 0; i < _targetCellCounts.Length; ++i) {
                _targetCellCounts[i] = 0;
                _soldierCellCounts[i] = 0;
                _soldierGridTargetCountIndex[i] = -1;
            }

            var targetIndexBuilderJob = new PolarIndexCalculateJob {
                TranslationArray = targetTranslationArray,
                RCount = rCount,
                RStep = rStep,
                AngleCount = angleCount,
                AngleStep = angleStep,
                CountIndexArray = _targetIndexArray
            };
            
            var soldierIndexBuilderJob = new PolarIndexCalculateJob {
                TranslationArray = soldierTranslationArray,
                RCount = rCount,
                RStep = rStep,
                AngleCount = angleCount,
                AngleStep = angleStep,
                CountIndexArray = _soldierIndexArray
            };
            
            Dependency = targetIndexBuilderJob.Schedule(targetCount, 64, Dependency);
            Dependency = soldierIndexBuilderJob.Schedule(soldierCount, 64, Dependency);
            Dependency.Complete();

            var targetGridBuilderJob = new PolarGridBuilderJob {
                Grid = _targetPolarGrid,
                CellCounts = _targetCellCounts,
                CountIndexArray = _targetIndexArray,
                EntityArray = targetEntityArray
            };
            
            var soldierGridBuilderJob = new PolarGridBuilderJob {
                Grid = _soldierPolarGrid,
                CellCounts = _soldierCellCounts,
                CountIndexArray = _soldierIndexArray,
                EntityArray = soldierEntityArray
            };
            
            Dependency = targetGridBuilderJob.Schedule(Dependency);
            Dependency = soldierGridBuilderJob.Schedule(Dependency);
            Dependency.Complete();

            var gridFindTargetCountIndex = new GridFindTargetJob {
                TargetCellCounts = _targetCellCounts,
                SoldierCellCounts = _soldierCellCounts,
                SoldierGridTargetCountIndex = _soldierGridTargetCountIndex,
                AngleCount = angleCount,
                MaxDist = maxDist,
            };
            
            Dependency = gridFindTargetCountIndex.Schedule(_soldierCellCounts.Length, 64, Dependency);
            Dependency.Complete();
            
            var translationType = GetComponentTypeHandle<Translation>(true);
            var soldierShootingType = GetComponentTypeHandle<TargetEntityComp>();
            var towerShootingType = GetComponentTypeHandle<TargetPosComp>();


            var findTargetJob = new SoldierFindTargetJob {
                TranslationHandle = translationType,
                TargetCompHandle = soldierShootingType,
                TargetCellCounts = _targetCellCounts,
                Grid = _targetPolarGrid,
                RStep = rStep,
                AngleStep = angleStep,
                AngleCount = angleCount,
                TargetCount = targetCount,
                Random = rand,
                SoldierGridTargetCountIndex = _soldierGridTargetCountIndex
            };

            var findTowerTargetJob = new TowerFindTarget {
                TranslationHandle = translationType,
                TargetCompHandle = towerShootingType,
                RStep = rStep,
                AngleStep = angleStep,
                AngleCount = angleCount,
                Random = rand,
                TargetCellCounts = _targetCellCounts,
                MaxDist = maxDist
            };

            Dependency = findTargetJob.ScheduleParallel(_soldierQuery, Dependency);
            Dependency = findTowerTargetJob.ScheduleParallel(_towerQuery, Dependency);
            Dependency.Complete();
        }

        protected override void OnStopRunning() {
            base.OnStopRunning();
            _targetPolarGrid.Dispose();
            _soldierPolarGrid.Dispose();
            
            _targetCellCounts.Dispose();
            _soldierCellCounts.Dispose();
            
            _soldierIndexArray.Dispose();
            _targetIndexArray.Dispose();

            _soldierGridTargetCountIndex.Dispose();
        }
    }
}