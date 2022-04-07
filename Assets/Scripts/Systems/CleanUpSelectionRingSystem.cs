using Unity.Entities;

namespace Systems {
    [UpdateAfter(typeof(UnitSelectionSystem))]
    public partial class CleanUpSelectionRingSystem : SystemBase {
        private EndSimulationEntityCommandBufferSystem _endSimulationEntityCommandBuffer;
        protected override void OnStartRunning() {
            base.OnStartRunning();
            _endSimulationEntityCommandBuffer = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate() {
            var ecb = _endSimulationEntityCommandBuffer.CreateCommandBuffer();
            Entities
                .WithNone<SelectedEntityTag>()
                .ForEach((Entity e, in SelectionRingStateData selectionRingState) => {
                    ecb.DestroyEntity(selectionRingState.SelectionUI);
                    ecb.RemoveComponent<SelectionRingStateData>(e);
                }).Run();
            Dependency.Complete();
        }
    }
}