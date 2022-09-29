using Unity.Entities;
using UnityEngine;

namespace EcsWar {

    // [GenerateAuthoringComponent]
    public struct LifeDecayData : IComponentData {
        public int Life;
    }

    public class LifeDecayDataAuthoring : MonoBehaviour {
        public int Life;
    }

    public class LifeDecayDataAuthoringBaker : Baker<LifeDecayDataAuthoring> {

        public override void Bake(LifeDecayDataAuthoring authoring) {
            AddComponent(new LifeDecayData {
                Life = authoring.Life,
            });
        }
    }
}
