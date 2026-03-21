using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Collects OrologionPickup floor items when a living player walks within CollectRadius.
    /// On collection: freezes ALL on-screen enemies for FreezeDuration (wiki: 10 s).
    /// Enemies that are already frozen have their timer refreshed (max, not add).
    /// Effect applies to every enemy regardless of type — no immunity check in base game.
    /// Runs on the main thread due to structural changes (adding Frozen to new enemies).
    /// </summary>
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct OrologionPickupSystem : ISystem
    {
        const float CollectRadius   = 0.6f;
        const float FreezeDuration  = 10f;   // wiki: 10 seconds

        public void OnUpdate(ref SystemState state)
        {
            var orologionQuery = SystemAPI.QueryBuilder()
                .WithAll<OrologionPickup, LocalTransform>()
                .Build();

            if (orologionQuery.IsEmpty) return;

            var playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag, LocalTransform, PlayerIndex>()
                .WithNone<Downed>()
                .Build();

            if (playerQuery.IsEmpty) return;

            var orologionEntities   = orologionQuery.ToEntityArray(Allocator.Temp);
            var orologionTransforms = orologionQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var playerEntities      = playerQuery.ToEntityArray(Allocator.Temp);
            var playerTransforms    = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            var em = state.EntityManager;

            for (int o = 0; o < orologionEntities.Length; o++)
            {
                // Find nearest living player within CollectRadius
                int   nearestIdx  = -1;
                float nearestDist = float.MaxValue;

                for (int p = 0; p < playerEntities.Length; p++)
                {
                    float dist = math.distance(orologionTransforms[o].Position.xy,
                                               playerTransforms[p].Position.xy);
                    if (dist <= CollectRadius && dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestIdx  = p;
                    }
                }

                if (nearestIdx < 0) continue;

                // Freeze ALL enemies on screen
                int frozenCount = 0;
                foreach (var (frozenRef, enemyEntity) in
                    SystemAPI.Query<RefRW<Frozen>>()
                        .WithAll<EnemyTag>()
                        .WithEntityAccess())
                {
                    // Refresh existing freeze timers (take max)
                    frozenRef.ValueRW.Timer = math.max(frozenRef.ValueRO.Timer, FreezeDuration);
                    frozenCount++;
                }

                // Add Frozen to enemies that don't have it yet
                var unfrozenEnemyQuery = SystemAPI.QueryBuilder()
                    .WithAll<EnemyTag>()
                    .WithNone<Frozen>()
                    .Build();

                if (!unfrozenEnemyQuery.IsEmpty)
                {
                    var unfrozenEntities = unfrozenEnemyQuery.ToEntityArray(Allocator.Temp);
                    for (int e = 0; e < unfrozenEntities.Length; e++)
                    {
                        em.AddComponentData(unfrozenEntities[e], new Frozen { Timer = FreezeDuration });
                        frozenCount++;
                    }
                    unfrozenEntities.Dispose();
                }

                int pidx = em.GetComponentData<PlayerIndex>(playerEntities[nearestIdx]).Value;
                Debug.Log($"[OrologionPickupSystem] P{pidx} collected Orologion — {frozenCount} enemies frozen for {FreezeDuration}s!");

                em.DestroyEntity(orologionEntities[o]);
            }

            orologionEntities.Dispose();
            orologionTransforms.Dispose();
            playerEntities.Dispose();
            playerTransforms.Dispose();
        }
    }
}
