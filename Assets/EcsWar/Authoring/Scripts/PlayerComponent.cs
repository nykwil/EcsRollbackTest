using Unity.Entities;
using UnityEngine;

namespace EcsWar {

    public class PlayerComponent : MonoBehaviour {
        public Player player;
        public GameObject BoltPrefab = null;
    }

    public class PlayerComponentBaker : Baker<PlayerComponent> {

        public override void Bake(PlayerComponent authoring) {
            var p = authoring.player;
            p.BoltPrefabEntity = GetEntity(authoring.BoltPrefab);
            AddComponent(p);
        }
    }
}
