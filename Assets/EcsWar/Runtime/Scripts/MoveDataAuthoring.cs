using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace EcsWar {

    // [GenerateAuthoringComponent]
    public struct MoveData : IComponentData {
        public float3 Angular;
        public float3 Linear;
    }

    public class MoveDataAuthoring : MonoBehaviour {
        public float3 Angular;
        public float3 Linear;
    }

    public class MoveDataAuthoringBaker : Baker<MoveDataAuthoring> {

        public override void Bake(MoveDataAuthoring authoring) {
            AddComponent(new MoveData {
                Angular = authoring.Angular,
                Linear = authoring.Linear,
            });
        }
    }
}
