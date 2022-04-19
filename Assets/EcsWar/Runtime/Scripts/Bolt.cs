using Unity.Entities;

namespace EcsWar {

    public partial class LifDataLifeSystem : SystemBase {
        private EntityCommandBufferSystem _entityCommandBufferSystem;

        protected override void OnCreate() {
            _entityCommandBufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate() {
            var ecb = _entityCommandBufferSystem.CreateCommandBuffer();
            Entities
                .ForEach((Entity entity, ref LifeData lifeData) => {
                    if (lifeData.Life <= 0) {
                        ecb.DestroyEntity(entity);
                    }
                }).Schedule();
            _entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }

    public partial class LifDataDecaySystem : SystemBase {

        protected override void OnUpdate() {
            Entities
                .ForEach((Entity entity, ref LifeData lifeData, ref LifeDecayData decay) => {
                    lifeData.Life -= decay.Life;
                }).Schedule();
        }
    }
}