using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Non-Burst SystemBase. Every 3 seconds spawns a burst of 5-8 enemies
    /// around the player centroid at radius 12 units.
    /// Weighted random: Bat 60%, Zombie 25%, Skeleton 15%.
    /// </summary>
    public partial class EnemySpawnerSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingletonEntity<SpawnerData>(out var spawnerEntity))
                return;

            var spawner = EntityManager.GetComponentData<SpawnerData>(spawnerEntity);
            spawner.Timer -= SystemAPI.Time.DeltaTime;

            if (spawner.Timer > 0f)
            {
                EntityManager.SetComponentData(spawnerEntity, spawner);
                return;
            }

            // Compute player centroid
            var playerQuery = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>()
            );

            if (playerQuery.IsEmpty)
            {
                playerQuery.Dispose();
                spawner.Timer = 3f;
                EntityManager.SetComponentData(spawnerEntity, spawner);
                return;
            }

            var playerTransforms = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            playerQuery.Dispose();

            float3 centroid = float3.zero;
            for (int i = 0; i < playerTransforms.Length; i++)
                centroid += playerTransforms[i].Position;
            centroid /= playerTransforms.Length;
            playerTransforms.Dispose();

            // Spawn burst
            int count = spawner.Rng.NextInt(5, 9); // 5 inclusive, 9 exclusive → 5-8

            for (int i = 0; i < count; i++)
            {
                float angle     = spawner.Rng.NextFloat(0f, math.PI * 2f);
                float3 spawnPos = new float3(
                    centroid.x + math.cos(angle) * 12f,
                    centroid.y + math.sin(angle) * 12f,
                    0f
                );

                float  roll   = spawner.Rng.NextFloat();
                Entity prefab = roll < 0.60f ? spawner.BatPrefab :
                                roll < 0.85f ? spawner.ZombiePrefab :
                                               spawner.SkeletonPrefab;

                var e = EntityManager.Instantiate(prefab);
                EntityManager.SetComponentData(e, LocalTransform.FromPosition(spawnPos));
            }

            spawner.Timer = 3f;
            EntityManager.SetComponentData(spawnerEntity, spawner);
        }
    }
}
