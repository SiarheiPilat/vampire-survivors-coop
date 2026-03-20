using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Spawns enemy bursts around the player centroid on a timer.
    /// Every 30 seconds a new wave starts: enemies scale in count, HP, damage, XP,
    /// and spawn interval shrinks, creating a natural difficulty ramp.
    ///
    /// Wave formula:
    ///   StatMultiplier = 1 + (wave-1) * 0.2   (wave 5 → 2× stats)
    ///   SpawnInterval  = max(3 - (wave-1)*0.15, 1.5)   s
    ///   SpawnCount     = Rng[5+(wave-1), 8+(wave-1)] capped at [5,18]
    ///
    /// Spawn weights: Bat 60%, Zombie 25%, Skeleton 15%.
    /// Weighted by time: bats dominate early, skeletons become more common in wave 3+.
    /// </summary>
    public partial class EnemySpawnerSystem : SystemBase
    {
        const float WaveDuration   = 30f;  // seconds per wave
        const float MaxMultiplier  = 3f;   // stat cap (wave 11+)
        const float MinInterval    = 1.5f; // spawn interval floor

        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingletonEntity<SpawnerData>(out var spawnerEntity))
                return;

            var spawner = EntityManager.GetComponentData<SpawnerData>(spawnerEntity);
            float dt = SystemAPI.Time.DeltaTime;

            // ── Advance time and compute wave ──────────────────────────────────
            spawner.ElapsedTime += dt;

            int newWave = (int)(spawner.ElapsedTime / WaveDuration) + 1;
            if (newWave != spawner.WaveNumber)
            {
                spawner.WaveNumber    = newWave;
                spawner.StatMultiplier = math.min(1f + (newWave - 1) * 0.2f, MaxMultiplier);
                Debug.Log($"[EnemySpawnerSystem] Wave {newWave}! StatMult={spawner.StatMultiplier:F1}x");
            }

            // ── Boss timer ─────────────────────────────────────────────────────
            spawner.BossTimer -= dt;
            if (spawner.BossTimer <= 0f && spawner.BossPrefab != Entity.Null)
            {
                // Spawn boss near a living player
                var bossPlayerQ = EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerTag>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.Exclude<Downed>());
                if (!bossPlayerQ.IsEmpty)
                {
                    var bpt = bossPlayerQ.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                    float3 bc = float3.zero;
                    for (int i = 0; i < bpt.Length; i++) bc += bpt[i].Position;
                    bc /= bpt.Length;
                    bpt.Dispose();

                    float bossAngle = spawner.Rng.NextFloat(0f, math.PI * 2f);
                    float3 bossPos  = new float3(bc.x + math.cos(bossAngle) * 12f,
                                                 bc.y + math.sin(bossAngle) * 12f, 0f);
                    var boss = EntityManager.Instantiate(spawner.BossPrefab);
                    EntityManager.SetComponentData(boss, LocalTransform.FromPositionRotationScale(
                        bossPos, quaternion.identity, 1f));

                    // Scale boss HP and damage by wave multiplier
                    var baseHp    = EntityManager.GetComponentData<Health>(boss);
                    var baseStats = EntityManager.GetComponentData<EnemyStats>(boss);
                    EntityManager.SetComponentData(boss, new Health
                    {
                        Current = (int)(baseHp.Max * spawner.StatMultiplier),
                        Max     = (int)(baseHp.Max * spawner.StatMultiplier)
                    });
                    EntityManager.SetComponentData(boss, new EnemyStats
                    {
                        MoveSpeed     = baseStats.MoveSpeed,
                        ContactDamage = (int)(baseStats.ContactDamage * spawner.StatMultiplier),
                        XpValue       = (int)(baseStats.XpValue * spawner.StatMultiplier)
                    });

                    Debug.Log($"[EnemySpawnerSystem] BOSS spawned at wave {spawner.WaveNumber}!");
                }
                bossPlayerQ.Dispose();

                // Reset interval: 45s base, -2s per wave, floor 25s
                spawner.BossTimer = math.max(45f - (spawner.WaveNumber - 1) * 2f, 25f);
            }

            // ── Spawn timer ────────────────────────────────────────────────────
            float spawnInterval = math.max(3f - (spawner.WaveNumber - 1) * 0.15f, MinInterval);
            spawner.Timer -= dt;

            if (spawner.Timer > 0f)
            {
                EntityManager.SetComponentData(spawnerEntity, spawner);
                return;
            }

            spawner.Timer = spawnInterval;

            // ── Player centroid ────────────────────────────────────────────────
            var playerQuery = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.Exclude<Downed>()
            );

            if (playerQuery.IsEmpty)
            {
                playerQuery.Dispose();
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

            // ── Spawn burst ────────────────────────────────────────────────────
            int wave    = spawner.WaveNumber;
            int minSpawn = math.min(5 + (wave - 1),     12);
            int maxSpawn = math.min(8 + (wave - 1) + 1, 19); // +1 because NextInt is exclusive
            int count   = spawner.Rng.NextInt(minSpawn, maxSpawn);

            // Weight distribution shifts over time:
            // Early: Bat-heavy. Later: Zombies, Skeletons, Slimes, Ghouls (w5+), Specters (w7+).
            float batWeight     = math.max(0.55f - (wave - 1) * 0.04f, 0.25f);
            float zombieWeight  = math.min(0.22f + (wave - 1) * 0.02f, 0.35f);
            float slimeWeight   = spawner.BigSlimePrefab != Entity.Null
                                    ? math.min(0.08f + (wave - 1) * 0.01f, 0.15f) : 0f;
            float ghoulWeight   = spawner.GhoulPrefab   != Entity.Null
                                    ? math.min(math.max(wave - 4, 0) * 0.025f, 0.12f) : 0f;
            float specterWeight = spawner.SpecterPrefab  != Entity.Null
                                    ? math.min(math.max(wave - 6, 0) * 0.02f,  0.08f) : 0f;
            // skeleton fills remainder

            float mult = spawner.StatMultiplier;

            for (int i = 0; i < count; i++)
            {
                float angle     = spawner.Rng.NextFloat(0f, math.PI * 2f);
                float3 spawnPos = new float3(
                    centroid.x + math.cos(angle) * 12f,
                    centroid.y + math.sin(angle) * 12f,
                    0f
                );

                float  roll   = spawner.Rng.NextFloat();
                float  cumBat     = batWeight;
                float  cumZombie  = cumBat     + zombieWeight;
                float  cumSlime   = cumZombie  + slimeWeight;
                float  cumGhoul   = cumSlime   + ghoulWeight;
                float  cumSpecter = cumGhoul   + specterWeight;
                Entity prefab;
                if      (roll < cumBat)     prefab = spawner.BatPrefab;
                else if (roll < cumZombie)  prefab = spawner.ZombiePrefab;
                else if (roll < cumSlime)   prefab = spawner.BigSlimePrefab;
                else if (roll < cumGhoul)   prefab = spawner.GhoulPrefab;
                else if (roll < cumSpecter) prefab = spawner.SpecterPrefab;
                else                        prefab = spawner.SkeletonPrefab;

                var e = EntityManager.Instantiate(prefab);
                EntityManager.SetComponentData(e, LocalTransform.FromPosition(spawnPos));

                // Apply wave scaling to this enemy's stats
                if (mult > 1f)
                {
                    var baseHp    = EntityManager.GetComponentData<Health>(e);
                    var baseStats = EntityManager.GetComponentData<EnemyStats>(e);

                    EntityManager.SetComponentData(e, new Health
                    {
                        Current = (int)(baseHp.Max * mult),
                        Max     = (int)(baseHp.Max * mult)
                    });
                    EntityManager.SetComponentData(e, new EnemyStats
                    {
                        MoveSpeed     = baseStats.MoveSpeed * math.min(mult, 1.5f), // speed cap at 1.5×
                        ContactDamage = (int)(baseStats.ContactDamage * mult),
                        XpValue       = (int)(baseStats.XpValue * mult)
                    });
                }
            }

            EntityManager.SetComponentData(spawnerEntity, spawner);
        }
    }
}
