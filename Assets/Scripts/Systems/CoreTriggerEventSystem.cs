using Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine.Rendering;

namespace Systems {
    public partial class CoreTriggerEventSystem : SystemBase {
        private StepPhysicsWorld _stepPhysics;
        private EntityQuery _coreHealthGroup;
        private EndSimulationEntityCommandBufferSystem _commandBufferSystem;

        protected override void OnCreate() {
            _stepPhysics = World.GetOrCreateSystem<StepPhysicsWorld>();
            _coreHealthGroup = GetEntityQuery(new EntityQueryDesc {
                 Any = new ComponentType[] {
                    typeof(CoreHealthComponent),
                    typeof(TravelToCore)
                }
            });
            _commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }
        
        [BurstCompile]
        struct CoreTriggerJob : ITriggerEventsJob {
            public ComponentDataFromEntity<CoreHealthComponent> CoreHealthGroup;
            [ReadOnly] public ComponentDataFromEntity<TravelToCore> TravelGroup;
            public EntityCommandBuffer CommandBuffer;

            public void Execute(TriggerEvent triggerEvent) {
                Entity entityA = triggerEvent.EntityA;
                Entity entityB = triggerEvent.EntityB;

                bool isCoreA = CoreHealthGroup.HasComponent(entityA);
                bool isCoreB = CoreHealthGroup.HasComponent(entityB);

                bool isTravelA = TravelGroup.HasComponent(entityA);
                bool isTravelB = TravelGroup.HasComponent(entityB);

                if (!isCoreA && !isCoreB || !isTravelA && !isTravelB) {
                    return;
                }

                var coreEntity = isCoreA ? entityA : entityB;
                var zombieEntity = isCoreA ? entityB : entityA;

                var health = CoreHealthGroup[coreEntity].CoreHealth - TravelGroup[zombieEntity].Damage;
                
                CommandBuffer.SetComponent(coreEntity, new CoreHealthComponent {
                    CoreHealth = health 
                });
                CommandBuffer.DestroyEntity(zombieEntity);
            }
        }

        protected override void OnUpdate() {
            if (_coreHealthGroup.CalculateEntityCount() == 0)
                return;

            Dependency = new CoreTriggerJob {
                CoreHealthGroup = GetComponentDataFromEntity<CoreHealthComponent>(),
                TravelGroup = GetComponentDataFromEntity<TravelToCore>(),
                CommandBuffer = _commandBufferSystem.CreateCommandBuffer()
            }.Schedule(_stepPhysics.Simulation, Dependency);

            Dependency.Complete();
        }
    }
}