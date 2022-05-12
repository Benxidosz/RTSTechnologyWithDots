using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Systems {
    [AlwaysUpdateSystem]
    public partial class UnitSelectionSystem : SystemBase {
        private Camera _mainCamera;
        private BuildPhysicsWorld _buildPhysicsWorld;
        private CollisionWorld _collisionWorld;

        protected override void OnCreate() {
            base.OnCreate();
            _buildPhysicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
        }

        protected override void OnUpdate() {
            if (Input.GetMouseButtonUp(0)) {
                if (!Input.GetKey(KeyCode.LeftShift))
                    DeselectSoldiers();
                SelectSingleSoldier();
            }
            if (Input.GetMouseButtonUp(1)) {
                MoveSelected();
            }
            Dependency.Complete();
        }

        private void MoveSelected() {
            _collisionWorld = _buildPhysicsWorld.PhysicsWorld.CollisionWorld;
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            var ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            var rayStart = ray.origin;
            var rayEnd = ray.GetPoint(100.0f);

            if (Raycast(rayStart, rayEnd, new CollisionFilter {
                BelongsTo = (uint) CollisionLayers.Selection,
                CollidesWith = (uint) CollisionLayers.Ground
            }, out var raycastHit)) {
                float3 hitPos = raycastHit.Position;
                hitPos.y = 1.0f;
                Entities
                    .WithAll<SelectedEntityTag>()
                    .ForEach((ref SoldierMovement movement) => {
                        movement.destination = hitPos;
                    }).Run();
            }
        }

        private void DeselectSoldiers() {
            EntityManager.RemoveComponent<SelectedEntityTag>(GetEntityQuery(typeof(SelectedEntityTag)));
        }

        private void SelectSingleSoldier() {
            _collisionWorld = _buildPhysicsWorld.PhysicsWorld.CollisionWorld;
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            var ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            var rayStart = ray.origin;
            var rayEnd = ray.GetPoint(200.0f);

            if (Raycast(rayStart, rayEnd, new CollisionFilter {
                BelongsTo = (uint) CollisionLayers.Selection,
                CollidesWith = (uint) (CollisionLayers.Ground | CollisionLayers.Soldier)
            }, out var raycastHit)) {
                var hitEntity = _buildPhysicsWorld.PhysicsWorld.Bodies[raycastHit.RigidBodyIndex].Entity;
                if (EntityManager.HasComponent<SelectableSoldierTag>(hitEntity)) {
                    EntityManager.AddComponent<SelectedEntityTag>(hitEntity);
                }
            }
        }

        private bool Raycast(float3 rayStart, float3 rayEnd, CollisionFilter filter, out RaycastHit raycastHit) {
            var raycastInput = new RaycastInput {
                Start = rayStart,
                End = rayEnd,
                
                Filter = filter
            };
            return _collisionWorld.CastRay(raycastInput, out raycastHit);
        }
    }
}