using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Authoring
{
    /// <summary>
    /// Attach to the EnemySpawner GameObject in the game scene.
    /// Baker converts the three prefab references into entity refs stored in SpawnerData.
    /// </summary>
    public class SpawnerAuthoring : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject batPrefab;
        public GameObject zombiePrefab;
        public GameObject skeletonPrefab;

        class Baker : Baker<SpawnerAuthoring>
        {
            public override void Bake(SpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new SpawnerData
                {
                    BatPrefab      = GetEntity(authoring.batPrefab,      TransformUsageFlags.Dynamic),
                    ZombiePrefab   = GetEntity(authoring.zombiePrefab,   TransformUsageFlags.Dynamic),
                    SkeletonPrefab = GetEntity(authoring.skeletonPrefab, TransformUsageFlags.Dynamic),
                    Timer          = 3f,
                    Rng            = Unity.Mathematics.Random.CreateFromIndex(42),
                    ElapsedTime    = 0f,
                    WaveNumber     = 1,
                    StatMultiplier = 1f
                });
            }
        }
    }
}
