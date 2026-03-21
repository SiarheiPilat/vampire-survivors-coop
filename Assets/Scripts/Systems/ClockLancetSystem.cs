using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Clock Lancet — two modes:
    ///
    ///   Base: freezes all enemies within FreezeRadius every Cooldown seconds.
    ///   Frozen enemies stop moving but still take damage.
    ///
    ///   Evolved (Infinite Corridor): halves the HP of every on-screen enemy every 1.0 s.
    ///   HP is floored at 1 — enemies cannot be killed by the halving alone.
    ///   Displays a DamageNumberEvent for each enemy halved.
    ///
    /// Not Burst-compiled: uses ComponentLookup writes + EntityManager for structural changes.
    /// Wiki: Clock Lancet — Cooldown 2.0 s, Duration 2.0 s.
    ///       Infinite Corridor — Cooldown 1.0 s, halves HP each pulse.
    /// </summary>
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ClockLancetSystem : ISystem
    {
        ComponentLookup<Frozen>  _frozenLookup;
        ComponentLookup<Health>  _healthLookup;

        public void OnCreate(ref SystemState state)
        {
            _frozenLookup = state.GetComponentLookup<Frozen>(isReadOnly: false);
            _healthLookup = state.GetComponentLookup<Health>(isReadOnly: false);
        }

        public void OnUpdate(ref SystemState state)
        {
            _frozenLookup.Update(ref state);
            _healthLookup.Update(ref state);

            float dt = SystemAPI.Time.DeltaTime;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            var enemyQuery      = SystemAPI.QueryBuilder().WithAll<EnemyTag, LocalTransform, Health>().Build();
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

                float3 playerPos = transform.ValueRO.Position;

                if (clock.ValueRO.IsEvolved)
                {
                    // ── Infinite Corridor: halve HP of all on-screen enemies ──────────
                    // Wiki: "Halves enemies' health." HP floor = 1. Ignores Might.
                    const float ScreenRadius   = 50f; // effectively whole screen + buffer
                    float       screenRadiusSq = ScreenRadius * ScreenRadius;

                    for (int i = 0; i < enemyEntities.Length; i++)
                    {
                        float distSq = math.distancesq(playerPos.xy, enemyTransforms[i].Position.xy);
                        if (distSq > screenRadiusSq) continue;

                        var hp       = _healthLookup[enemyEntities[i]];
                        int oldHp    = hp.Current;
                        hp.Current   = math.max(1, hp.Current / 2);
                        int dmgDealt = oldHp - hp.Current;
                        _healthLookup[enemyEntities[i]] = hp;

                        if (dmgDealt > 0)
                        {
                            var dmgEvt = ecb.CreateEntity();
                            ecb.AddComponent(dmgEvt, new DamageNumberEvent
                            {
                                WorldPosition = enemyTransforms[i].Position,
                                Damage        = dmgDealt
                            });
                        }
                    }
                }
                else
                {
                    // ── Base Clock Lancet: freeze enemies within radius ───────────────
                    float freezeDuration = clock.ValueRO.FreezeDuration * stats.ValueRO.DurationMult;
                    float radiusSq       = clock.ValueRO.FreezeRadius * clock.ValueRO.FreezeRadius;

                    for (int i = 0; i < enemyEntities.Length; i++)
                    {
                        float distSq = math.distancesq(playerPos.xy, enemyTransforms[i].Position.xy);
                        if (distSq > radiusSq) continue;

                        if (_frozenLookup.HasComponent(enemyEntities[i]))
                        {
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
            }

            enemyEntities.Dispose();
            enemyTransforms.Dispose();
        }
    }
}
