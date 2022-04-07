using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties.UI;
using Unity.Transforms;

namespace Systems {
    public struct SelectionRingStateData : ISystemStateComponentData {
        public Entity SelectionUI;
    }

    [GenerateAuthoringComponent]
    public struct SelectionUIPrefab : IComponentData {
        public Entity Prefab;
    }

    [UpdateAfter(typeof(UnitSelectionSystem))]
    public partial class SpawnSelectionRingSystem : SystemBase {
        private SelectionUIPrefab _selectionUIPrefab;
        private EndSimulationEntityCommandBufferSystem _endSimulationEntityCommandBuffer;

        protected override void OnStartRunning() {
            base.OnStartRunning();
            _selectionUIPrefab = GetSingleton<SelectionUIPrefab>();
            _endSimulationEntityCommandBuffer = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate() {
            var ecb = _endSimulationEntityCommandBuffer.CreateCommandBuffer();
            var selectionPrefab = _selectionUIPrefab.Prefab;
            Entities
                .WithAll<SelectedEntityTag>()
                .WithNone<SelectionRingStateData>()
                .ForEach((Entity selectedEntity) => {
                    var selectionUI = ecb.Instantiate(selectionPrefab);
                    var newSelectionStateData = new SelectionRingStateData {
                        SelectionUI = selectionUI
                    };
                    ecb.AddComponent<SelectionRingStateData>(selectedEntity);
                    ecb.SetComponent(selectedEntity, newSelectionStateData);
                    ecb.AddComponent<Parent>(selectionUI);
                    ecb.SetComponent(selectionUI, new Parent {
                        Value = selectedEntity
                    });
                    ecb.AddComponent<LocalToParent>(selectionUI);
                    ecb.SetComponent(selectionUI, new LocalToParent {
                        Value = float4x4.zero
                    });
                }).Run();
            
            Dependency.Complete();
        }
    }
}