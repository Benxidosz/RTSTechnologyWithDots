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
using UnityEngine;

[UpdateAfter(typeof(FindTargetSystem))]
[UpdateAfter(typeof(BeginSimulationEntityCommandBufferSystem))]
public partial class BulletSystem : SystemBase {
    private StepPhysicsWorld _stepPhysics;
    private EntityQuery _triggerGroup;
    private EntityQuery _bulletGroup;
    private EndSimulationEntityCommandBufferSystem _commandBufferSystem;
    private Translation _coreTranslation;
    
    protected override void OnStartRunning() {
        _stepPhysics = World.GetOrCreateSystem<StepPhysicsWorld>();
        _triggerGroup = GetEntityQuery(new EntityQueryDesc {
            Any = new ComponentType[] {
                typeof(TravelToCore),
                typeof(BulletTag)
            }
        });
        _bulletGroup = GetEntityQuery(ComponentType.ReadOnly<BulletTag>(), typeof(Translation));
        var coreEntity = GetEntityQuery(typeof(CoreHealthComponent),
                typeof(Translation))
            .GetSingletonEntity();
        
        _coreTranslation = EntityManager.GetComponentData<Translation>(coreEntity);
        
        _commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    [BurstCompile]
    private struct BulletDestroyJob : IJobEntityBatch {
        [ReadOnly] public ComponentTypeHandle<Translation> TranslationHandle;
        [ReadOnly] public EntityTypeHandle BulletHandle;
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Entity> BulletArray;
        [ReadOnly] public float3 CorePos;
        public EntityCommandBuffer CommandBuffer;
        public void Execute(ArchetypeChunk batchInChunk, int batchIndex) {
            var chunkTranslation = batchInChunk.GetNativeArray(TranslationHandle);
            var chunkEntity = batchInChunk.GetNativeArray(BulletHandle);
            for (var i = 0; i < batchInChunk.Count; i++) {
                var translation = chunkTranslation[i];
                var dist = math.distance(translation.Value, CorePos);
                if (dist > 50.0f) {
                    CommandBuffer.DestroyEntity(chunkEntity[i]);
                }
            }
        }
    }
    
    [BurstCompile]
    struct BulletTriggerJob : ITriggerEventsJob {
        public ComponentDataFromEntity<BulletTag> BulletGroup;
        [ReadOnly] public ComponentDataFromEntity<TravelToCore> TravelGroup;
        public EntityCommandBuffer CommandBuffer;

        public void Execute(TriggerEvent triggerEvent) {
            Entity entityA = triggerEvent.EntityA;
            Entity entityB = triggerEvent.EntityB;

            bool isBulletA = BulletGroup.HasComponent(entityA);
            bool isBulletB = BulletGroup.HasComponent(entityB);

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
        }
    }
    
    protected override void OnUpdate() {
        var translationType = GetComponentTypeHandle<Translation>(true);

        var bulletEntityArray = _bulletGroup.ToEntityArray(Allocator.TempJob);
        
        var job = new BulletDestroyJob {
            TranslationHandle = translationType,
            BulletArray = bulletEntityArray,
            BulletHandle = GetEntityTypeHandle(),
            CorePos = _coreTranslation.Value,
            CommandBuffer = _commandBufferSystem.CreateCommandBuffer()
        };
        Dependency = job.Schedule(_bulletGroup, Dependency);
        Dependency = new BulletTriggerJob {
            BulletGroup = GetComponentDataFromEntity<BulletTag>(),
            TravelGroup = GetComponentDataFromEntity<TravelToCore>(),
            CommandBuffer = _commandBufferSystem.CreateCommandBuffer()
        }.Schedule(_stepPhysics.Simulation, Dependency);
        
        Dependency.Complete();
    }
}