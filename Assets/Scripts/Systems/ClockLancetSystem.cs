using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Clock Lancet — freezes all enemies within FreezeRadius every Cooldown seconds.
    /// Frozen enemies stop moving (EnemyMovementSystem skips Frozen entities) but can
    /// still take damage. FrozenTickSystem removes the Frozen component when the timer expires.
    ///
    /// Not Burst-compiled: uses ComponentLookup to refresh already-frozen enemies in place,
    /// and ECB for structural changes (adding Frozen to unfrozen enemies).
    /// Wiki base stats: Cooldown 2.0 s, Duration 2.0 s.
    /// Auto-granted to all characters at level 11 via LevelUpSystem.
    /// </summary>
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ClockLancetSystem : ISystem
    {
        ComponentLookup<Frozen> _frozenLookup;

        public void OnCreate(ref SystemState state)
        {
            _frozenLookup = state.GetComponentLookup<Frozen>(isReadOnly: false);
        }

        public void OnUpdate(ref SystemState state)
        {
            _frozenLookup.Update(ref state);

            float dt = SystemAPI.Time.DeltaTime;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            var enemyQuery      = SystemAPI.QueryBuilder().WithAll<EnemyTag, LocalTransform>().Build();
            var enemyEntities   = enemyQuery.ToEntityArray(Allocator.Temp);
            var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            foreach (var (clock, transform, stats) in
                SystemAPI.Query<RefRW<ClockLancetState>, RefRO<LocalTransform>, RefRO<PlayerStats>>()
                    .WithAll<PlayerTag>()
                    .WithNone<Downed>())
            {
                clock.ValueRW.Timer -= dt;
                if (clock.ValueRO.Timer > 0f) continue;

                clock.ValueRW.Timer = clock.ValueRO.Cooldown * stats.ValueRO.CooldownMult;

                float freezeDuration = clock.ValueRO.FreezeDuration * stats.ValueRO.DurationMult;
                float radiusSq       = clock.ValueRO.FreezeRadius * clock.ValueRO.FreezeRadius;
                float3 playerPos     = transform.ValueRO.Position;

                for (int i = 0; i < enemyEntities.Length; i++)
                {
                    float distSq = math.distancesq(playerPos.xy, enemyTransforms[i].Position.xy);
                    if (distSq > radiusSq) continue;

                    if (_frozenLookup.HasComponent(enemyEntities[i]))
                    {
                        // Refresh timer: take the longer of current vs new duration
                        var existing = _frozenLookup[enemyEntities[i]];
                        existing.Timer = math.max(existing.Timer, freezeDuration);
                        _frozenLookup[enemyEntities[i]] = existing;
                    }
                    else
                    {
                        ecb.AddComponent(enemyEntities[i], new Frozen { Timer = freezeDuration });
                    }
                }
            }

            enemyEntities.Dispose();
            enemyTransforms.Dispose();
        }
    }
}
