using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace EcsWar
{
    public partial class BoltEmitSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities
                .WithStructuralChanges()
                .ForEach((Entity playerEntity, ref Player player, ref Translation playerTr, ref Rotation playerRot) =>
                {
                    player.ElapsedTime += 1;
                    if (player.ElapsedTime > player.FireRate)
                    {
                        var boltEnt = EntityManager.Instantiate(player.BoltPrefabEntity);
                        EntityManager.SetComponentData(boltEnt, new MoveData()
                        {
                            Linear = math.mul(playerRot.Value, new float3(0, 0, player.FireSpeed))
                        });
                        EntityManager.SetComponentData(boltEnt, playerRot);
                        EntityManager.SetComponentData(boltEnt, playerTr);
                        player.ElapsedTime = 0;
                    }
                }).Run();
        }
    }
}