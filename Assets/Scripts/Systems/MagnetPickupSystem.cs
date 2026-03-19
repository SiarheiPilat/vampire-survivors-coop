using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Collects MagnetPickup entities when a living player walks within CollectRadius.
    /// On collection: vacuums ALL XP gems on screen — sums their value (scaled by XpMult),
    /// credits the collector, destroys every gem, then destroys the magnet.
    /// Runs on the main thread (structural changes + cross-query access).
    /// </summary>
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct MagnetPickupSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            const float CollectRadius = 0.6f;

            var magnetQuery = SystemAPI.QueryBuilder()
                .WithAll<MagnetPickup, LocalTransform>()
                .Build();

            if (magnetQuery.IsEmpty) return;

            var gemQuery = SystemAPI.QueryBuilder()
                .WithAll<XpGem>()
                .Build();

            var playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag, LocalTransform, PlayerStats>()
                .WithNone<Downed>()
                .Build();

            if (playerQuery.IsEmpty) return;

            var magnetEntities   = magnetQuery.ToEntityArray(Allocator.Temp);
            var magnetTransforms = magnetQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var playerEntities   = playerQuery.ToEntityArray(Allocator.Temp);
            var playerTransforms = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            var em = state.EntityManager;

            for (int m = 0; m < magnetEntities.Length; m++)
            {
                // Find nearest living player within CollectRadius
                int   nearestIdx  = -1;
                float nearestDist = float.MaxValue;

                for (int p = 0; p < playerEntities.Length; p++)
                {
                    float dist = math.distance(magnetTransforms[m].Position.xy,
                                               playerTransforms[p].Position.xy);
                    if (dist <= CollectRadius && dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestIdx  = p;
                    }
                }

                if (nearestIdx < 0) continue;

                // Vacuum all XP gems: sum up their XP, destroy them
                if (!gemQuery.IsEmpty)
                {
                    var gemEntities = gemQuery.ToEntityArray(Allocator.Temp);
                    var gems        = gemQuery.ToComponentDataArray<XpGem>(Allocator.Temp);

                    var stats  = em.GetComponentData<PlayerStats>(playerEntities[nearestIdx]);
                    float xpGained = 0f;
                    int   gemCount = gems.Length;
                    for (int g = 0; g < gemCount; g++)
                        xpGained += gems[g].Value * stats.XpMult;

                    stats.Xp += xpGained;
                    em.SetComponentData(playerEntities[nearestIdx], stats);

                    for (int g = 0; g < gemEntities.Length; g++)
                        em.DestroyEntity(gemEntities[g]);

                    gemEntities.Dispose();
                    gems.Dispose();

                    int pidx = em.GetComponentData<PlayerIndex>(playerEntities[nearestIdx]).Value;
                    Debug.Log($"[MagnetPickupSystem] P{pidx} used magnet — +{xpGained:F0} XP from {gemCount} gems!");
                }

                em.DestroyEntity(magnetEntities[m]);
            }

            magnetEntities.Dispose();
            magnetTransforms.Dispose();
            playerEntities.Dispose();
            playerTransforms.Dispose();
        }
    }
}
