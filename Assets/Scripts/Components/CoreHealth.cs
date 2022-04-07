using Unity.Entities;

namespace Components {
    
    [GenerateAuthoringComponent]
    public struct CoreHealthComponent : IComponentData {
        public int CoreHealth;
    }
}