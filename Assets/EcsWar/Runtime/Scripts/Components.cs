using Unity.Entities;

namespace EcsWar
{
    [System.Serializable]
    public struct Player : IComponentData
    {
        public int FireRate;
        public float FireSpeed;

        [UnityEngine.HideInInspector]
        public int ElapsedTime;

        [UnityEngine.HideInInspector]
        public Entity BoltPrefabEntity;
    }
}