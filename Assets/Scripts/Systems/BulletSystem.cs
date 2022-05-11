using System;
using System.Diagnostics;
using System.Linq;
using Components;
using Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Debug = UnityEngine.Debug;
using Random = Unity.Mathematics.Random;

[UpdateAfter(typeof(FindTargetSystem))]
[UpdateAfter(typeof(BeginSimulationEntityCommandBufferSystem))]
public partial class BulletSystem : SystemBase {
    private StepPhysicsWorld _stepPhysics;
    private EntityQuery _bulletGroup;
    private EndSimulationEntityCommandBufferSystem _commandBufferSystem;
    private Translation _coreTranslation;
    private GridSliceComponent _gridSlice;
    private EntityQuery _targetQuery;

    protected override void OnStartRunning() {
        _stepPhysics = World.GetOrCreateSystem<StepPhysicsWorld>();
        _bulletGroup = GetEntityQuery(ComponentType.ReadOnly<BulletTag>(), typeof(Translation));
        var coreEntity = GetEntityQuery(typeof(CoreHealthComponent),
                typeof(Translation))
            .GetSingletonEntity();

        _coreTranslation = EntityManager.GetComponentData<Translation>(coreEntity);
        _gridSlice = GetSingleton<GridSliceComponent>();

        _commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        var targetDesc = new EntityQueryDesc {
            All = new[] {
                ComponentType.ReadOnly<TravelToCore>(),
                ComponentType.ReadOnly<Translation>()
            }
        };
        _targetQuery = GetEntityQuery(targetDesc);
    }

    [BurstCompile]
    private struct BulletDestroyJob : IJobEntityBatch {
        [ReadOnly] public ComponentTypeHandle<Translation> TranslationHandle;
        [ReadOnly] public EntityTypeHandle BulletHandle;
        [ReadOnly] public float3 CorePos;
        public EntityCommandBuffer CommandBuffer;
        public float MapSize;

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
            var chunkTranslation = batchInChunk.GetNativeArray(TranslationHandle);
            var chunkEntity = batchInChunk.GetNativeArray(BulletHandle);
            for (var i = 0; i < batchInChunk.Count; i++) {
                var translation = chunkTranslation[i];
                var dist = math.distance(translation.Value, CorePos);
                if (dist > MapSize) {
                    CommandBuffer.DestroyEntity(chunkEntity[i]);
                }
            }
        }
    }

    [BurstCompile]
    struct SoldierBulletTriggerJob : ITriggerEventsJob {
        public ComponentDataFromEntity<SoldierBulletTag> SoldierBulletGroup;
        [ReadOnly] public ComponentDataFromEntity<TravelToCore> TravelGroup;
        public EntityCommandBuffer CommandBuffer;
        public NativeArray<int> Result;

        public void Execute(TriggerEvent triggerEvent) {
            Entity entityA = triggerEvent.EntityA;
            Entity entityB = triggerEvent.EntityB;

            bool isBulletA = SoldierBulletGroup.HasComponent(entityA);
            bool isBulletB = SoldierBulletGroup.HasComponent(entityB);

            bool isTravelA = TravelGroup.HasComponent(entityA);
            bool isTravelB = TravelGroup.HasComponent(entityB);

            if (!isBulletA && !isBulletB || !isTravelA && !isTravelB) {
                return;
            }

            var bulletEntity = isBulletA ? entityA : entityB;
            var zombieEntity = isBulletA ? entityB : entityA;
            var zombieComp = TravelGroup[zombieEntity];

            if (zombieComp.Soldier != Entity.Null) {
                CommandBuffer.SetComponent(zombieComp.Soldier, new TargetEntityComp {
                    Target = default
                });
            }


            CommandBuffer.DestroyEntity(bulletEntity);
            CommandBuffer.DestroyEntity(zombieEntity);
            ++Result[0];
        }
    }

    [BurstCompile]
    struct BombCollisionJob : ITriggerEventsJob {
        public ComponentDataFromEntity<BombTag> BombBulletGroup;
        public ComponentDataFromEntity<Translation> TranslationGroup;
        public EntityCommandBuffer CommandBuffer;

        [ReadOnly] public NativeArray<int> TargetCellCounts;
        [ReadOnly] public NativeArray<Entity> Grid;
        
        [ReadOnly] public float RStep;
        [ReadOnly] public float AngleStep;
        [ReadOnly] public int RCount;
        [ReadOnly] public int AngleCount;
        [ReadOnly] public float MaxDist;
        [ReadOnly] public int TargetCount;
        public NativeArray<int> Result;

        public void Execute(TriggerEvent collisionEvent) {
            Entity entityA = collisionEvent.EntityA;
            Entity entityB = collisionEvent.EntityB;

            bool isBombA = BombBulletGroup.HasComponent(entityA);
            bool isBombB = BombBulletGroup.HasComponent(entityB);

            if (!isBombA && !isBombB) {
                return;
            }

            var bombEntity = isBombA ? entityA : entityB;
            var bombPos =  TranslationGroup[bombEntity].Value;
            
            int r = (int) math.floor(math.length(bombPos) / RStep);

            float3 baseVec = new float3(0.0f, 0.0f, 1.0f);
            float tmpAlfa = math.acos(math.dot(baseVec,
                math.normalize(bombPos)));
            float3 dir = math.cross(baseVec, math.normalize(bombPos));
            if (dir.y < 0)
                tmpAlfa = 2 * math.PI - tmpAlfa;
            int angle = (int) math.floor(tmpAlfa / AngleStep);
            var countIndex = r * AngleCount + angle;
            
            int dist = 1;
            while (dist < 4) {
                for (int diff = 0; diff <= dist; ++diff) {
                    int rDiff = dist - diff;
                    int aDiff = diff;

                    int tmpTargetCountIndexPos = countIndex + rDiff * AngleCount + aDiff;
                    int tmpTargetCountIndexNeg = countIndex + rDiff * AngleCount - aDiff;

                    if (tmpTargetCountIndexPos >= 0 && tmpTargetCountIndexPos < TargetCellCounts.Length) {
                        var firstIndex = tmpTargetCountIndexPos * TargetCount;
                        for (int i = 0; i < TargetCellCounts[tmpTargetCountIndexPos]; ++i) {
                            var tmpEntity = Grid[firstIndex + i];
                            if (tmpEntity != Entity.Null) {
                                var entityPos = TranslationGroup[tmpEntity].Value;
                                var tmpDir = entityPos - bombPos;
                                if (math.length(tmpDir) < 15.0f) {
                                    tmpDir = math.normalize(tmpDir + new float3(0f, 5f, 0f));
                                    CommandBuffer.SetComponent(tmpEntity, new PhysicsGravityFactor {
                                        Value = 2f
                                    });
                                    CommandBuffer.SetComponent(tmpEntity, new PhysicsVelocity {
                                        Linear = tmpDir * new float3(10f, 30, 10f),
                                        Angular = tmpDir * 20f
                                    });
                                    CommandBuffer.RemoveComponent<TravelToCore>(tmpEntity);
                                    ++Result[0];
                                }
                            }
                        }
                    }
                    if (tmpTargetCountIndexNeg >= 0 && tmpTargetCountIndexNeg < TargetCellCounts.Length) {
                        var firstIndex = tmpTargetCountIndexNeg * TargetCount;
                        for (int i = 0; i < TargetCellCounts[tmpTargetCountIndexNeg]; ++i) {
                            var tmpEntity = Grid[firstIndex + i];
                            if (tmpEntity != Entity.Null) {
                                var entityPos = TranslationGroup[tmpEntity].Value;
                                var tmpDir = entityPos - bombPos;
                                if (math.length(entityPos - bombPos) < 15.0f) {
                                    tmpDir = math.normalize(tmpDir + new float3(0f, 5f, 0f));
                                    CommandBuffer.SetComponent(tmpEntity, new PhysicsGravityFactor {
                                        Value = 2f
                                    });
                                    CommandBuffer.SetComponent(tmpEntity, new PhysicsVelocity {
                                        Linear = tmpDir * new float3(10f, 30, 10f),
                                        Angular = tmpDir * 20f
                                    });
                                    CommandBuffer.RemoveComponent<TravelToCore>(tmpEntity);
                                    ++Result[0];
                                }
                            }
                        }
                    }
                }
                dist++;
            }

            CommandBuffer.DestroyEntity(bombEntity);
        }
    }

    protected override void OnUpdate() {
        var translationType = GetComponentTypeHandle<Translation>(true);
        
        float rStep = _gridSlice.rStep;
        float angleStep = _gridSlice.angleStep;
        int rCount = (int) math.floor(_gridSlice.mapSize / rStep) + 1;
        int angleCount = (int) math.floor(2 * math.PI / angleStep);
        var result = new NativeArray<int>(1, Allocator.TempJob);

        var job = new BulletDestroyJob {
            TranslationHandle = translationType,
            BulletHandle = GetEntityTypeHandle(),
            MapSize = _gridSlice.mapSize,
            CorePos = _coreTranslation.Value,
            CommandBuffer = _commandBufferSystem.CreateCommandBuffer()
        };
        Dependency = job.Schedule(_bulletGroup, Dependency);
        
        Dependency = new BombCollisionJob {
            BombBulletGroup = GetComponentDataFromEntity<BombTag>(),
            CommandBuffer = _commandBufferSystem.CreateCommandBuffer(),
            TranslationGroup = GetComponentDataFromEntity<Translation>(),
            TargetCellCounts = FindTargetSystem.Instance.TargetGrid.CellCounts,
            Grid = FindTargetSystem.Instance.TargetGrid.Grid,
            RStep = rStep,
            AngleStep = angleStep,
            AngleCount = angleCount,
            TargetCount = _targetQuery.CalculateEntityCount(),
            Result = result
        }.Schedule(_stepPhysics.Simulation, Dependency);
        
        Dependency.Complete();
        
        Dependency = new SoldierBulletTriggerJob {
            SoldierBulletGroup = GetComponentDataFromEntity<SoldierBulletTag>(),
            TravelGroup = GetComponentDataFromEntity<TravelToCore>(),
            CommandBuffer = _commandBufferSystem.CreateCommandBuffer(),
            Result = result
        }.Schedule(_stepPhysics.Simulation, Dependency);

        Dependency.Complete();
        
        GameManager.Instance.IncreasePoint(result[0]);
        result.Dispose();
    }
}