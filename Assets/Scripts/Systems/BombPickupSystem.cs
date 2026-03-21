using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Collects BombPickup floor items when a living player walks within CollectRadius.
    /// On collection: deals 80 flat damage to ALL enemies within BombRadius of the player.
    /// Damage is applied directly to Health.Current (bypasses Armor — it's environmental).
    /// Enemies killed by the bomb explosion are not handled here; HealthSystem catches
    /// Health.Current ≤ 0 on the next frame as normal.
    ///
    /// Runs on the main thread; no structural changes — health is written directly.
    /// </summary>
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct BombPickupSystem : ISystem
    {
        const float CollectRadius = 0.6f;
        const float BombRadius    = 3.0f;   // world units — AoE blast range
        const int   BombDamage    = 80;     // flat damage, does NOT scale with Might

        public void OnUpdate(ref SystemState state)
        {
            var bombQuery = SystemAPI.QueryBuilder()
                .WithAll<BombPickup, LocalTransform>()
                .Build();
            if (bombQuery.IsEmpty) return;

            var playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag, LocalTransform, PlayerIndex>()
                .WithNone<Downed>()
                .Build();
            if (playerQuery.IsEmpty) return;

            var bombEntities      = bombQuery.ToEntityArray(Allocator.Temp);
            var bombTransforms    = bombQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var playerEntities    = playerQuery.ToEntityArray(Allocator.Temp);
            var playerTransforms  = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            var em = state.EntityManager;

            for (int b = 0; b < bombEntities.Length; b++)
            {
                // Find nearest living player within CollectRadius
                int   nearestIdx  = -1;
                float nearestDist = float.MaxValue;

                for (int p = 0; p < playerEntities.Length; p++)
                {
                    float dist = math.distance(bombTransforms[b].Position.xy,
                                               playerTransforms[p].Position.xy);
                    if (dist <= CollectRadius && dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestIdx  = p;
                    }
                }
                if (nearestIdx < 0) continue;

                float2 blastOrigin = playerTransforms[nearestIdx].Position.xy;
                int    pidx        = em.GetComponentData<PlayerIndex>(playerEntities[nearestIdx]).Value;

                // Deal flat damage to all enemies within BombRadius
                int hitCount = 0;
                foreach (var (healthRef, enemyTransform, _) in
                    SystemAPI.Query<RefRW<Health>, RefRO<LocalTransform>>()
                        .WithAll<EnemyTag>()
                        .WithEntityAccess())
                {
                    float dist = math.distance(enemyTransform.ValueRO.Position.xy, blastOrigin);
                    if (dist > BombRadius) continue;

                    healthRef.ValueRW.Current -= BombDamage;
                    hitCount++;
                }

                Debug.Log($"[BombPickupSystem] P{pidx} detonated bomb — {hitCount} enemies hit for {BombDamage} dmg (r={BombRadius}u)!");

                em.DestroyEntity(bombEntities[b]);
            }

            bombEntities.Dispose();
            bombTransforms.Dispose();
            playerEntities.Dispose();
            playerTransforms.Dispose();
        }
    }
}
