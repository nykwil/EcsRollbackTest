using Unity.Entities;
using UnityEngine;

namespace EcsWar {

    // [GenerateAuthoringComponent]
    public struct LifeData : IComponentData {
        public int Life;
    }

    public class LifeDataAuthoring : MonoBehaviour {
        public int Life;
    }

    public class LifeDataAuthoringBaker : Baker<LifeDataAuthoring> {

        public override void Bake(LifeDataAuthoring authoring) {
            AddComponent(new LifeData {
                Life = authoring.Life,
            });
        }
    }

}
