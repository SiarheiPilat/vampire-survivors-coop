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
        [Header("Enemy Prefabs")]
        public GameObject batPrefab;
        public GameObject zombiePrefab;
        public GameObject skeletonPrefab;
        public GameObject bigSlimePrefab;
        public GameObject smallSlimePrefab;
        public GameObject bossPrefab;
        public GameObject ghoulPrefab;    // fast undead; spawns from wave 5
        public GameObject specterPrefab;  // knockback-immune ghost; spawns from wave 7

        [Header("Pickup Prefabs")]
        public GameObject xpGemPrefab;
        public GameObject goldCoinPrefab;
        public GameObject healthPickupPrefab;
        public GameObject magnetPickupPrefab;
        public GameObject chestPrefab;
        public GameObject orologionPickupPrefab;
        public GameObject bombPickupPrefab;

        class Baker : Baker<SpawnerAuthoring>
        {
            public override void Bake(SpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new SpawnerData
                {
                    BatPrefab        = GetEntity(authoring.batPrefab,      TransformUsageFlags.Dynamic),
                    ZombiePrefab     = GetEntity(authoring.zombiePrefab,   TransformUsageFlags.Dynamic),
                    SkeletonPrefab   = GetEntity(authoring.skeletonPrefab, TransformUsageFlags.Dynamic),
                    BigSlimePrefab   = authoring.bigSlimePrefab      != null ? GetEntity(authoring.bigSlimePrefab,      TransformUsageFlags.Dynamic) : Entity.Null,
                    SmallSlimePrefab = authoring.smallSlimePrefab    != null ? GetEntity(authoring.smallSlimePrefab,    TransformUsageFlags.Dynamic) : Entity.Null,
                    BossPrefab       = authoring.bossPrefab          != null ? GetEntity(authoring.bossPrefab,          TransformUsageFlags.Dynamic) : Entity.Null,
                    GhoulPrefab      = authoring.ghoulPrefab         != null ? GetEntity(authoring.ghoulPrefab,         TransformUsageFlags.Dynamic) : Entity.Null,
                    SpecterPrefab    = authoring.specterPrefab       != null ? GetEntity(authoring.specterPrefab,       TransformUsageFlags.Dynamic) : Entity.Null,
                    XpGemPrefab      = authoring.xpGemPrefab         != null ? GetEntity(authoring.xpGemPrefab,         TransformUsageFlags.Dynamic) : Entity.Null,
                    GoldCoinPrefab   = authoring.goldCoinPrefab      != null ? GetEntity(authoring.goldCoinPrefab,      TransformUsageFlags.Dynamic) : Entity.Null,
                    HealthPickupPrefab = authoring.healthPickupPrefab != null ? GetEntity(authoring.healthPickupPrefab, TransformUsageFlags.Dynamic) : Entity.Null,
                    MagnetPickupPrefab = authoring.magnetPickupPrefab != null ? GetEntity(authoring.magnetPickupPrefab, TransformUsageFlags.Dynamic) : Entity.Null,
                    ChestPrefab            = authoring.chestPrefab            != null ? GetEntity(authoring.chestPrefab,            TransformUsageFlags.Dynamic) : Entity.Null,
                    OrologionPickupPrefab  = authoring.orologionPickupPrefab  != null ? GetEntity(authoring.orologionPickupPrefab,  TransformUsageFlags.Dynamic) : Entity.Null,
                    BombPickupPrefab       = authoring.bombPickupPrefab       != null ? GetEntity(authoring.bombPickupPrefab,       TransformUsageFlags.Dynamic) : Entity.Null,
                    Timer          = 3f,
                    BossTimer      = 45f,
                    Rng            = Unity.Mathematics.Random.CreateFromIndex(42),
                    ElapsedTime    = 0f,
                    WaveNumber     = 1,
                    StatMultiplier = 1f
                });
            }
        }
    }
}
