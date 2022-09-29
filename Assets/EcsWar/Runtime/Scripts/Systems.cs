using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace EcsWar
{
    public partial class MoveSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities
                .WithoutBurst()
                .ForEach((ref Translation tr, ref Rotation rot, in MoveData move) =>
                {
                    rot.Value = math.mul(rot.Value, quaternion.Euler(move.Angular));
                    tr.Value = tr.Value + move.Linear;
                }).Run();
        }
    }

    public partial class LifDataLifeSystem : SystemBase
    {
        private EntityCommandBufferSystem _entityCommandBufferSystem;

        protected override void OnCreate()
        {
            _entityCommandBufferSystem = World.GetExistingSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var ecb = _entityCommandBufferSystem.CreateCommandBuffer();
            Entities
                .ForEach((Entity entity, ref LifeData lifeData) =>
                {
                    if (lifeData.Life <= 0)
                    {
                        ecb.DestroyEntity(entity);
                    }
                }).Schedule();
            _entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }

    public partial class LifDataDecaySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities
                .ForEach((Entity entity, ref LifeData lifeData, ref LifeDecayData decay) =>
                {
                    lifeData.Life -= decay.Life;
                }).Schedule();
        }
    }
}