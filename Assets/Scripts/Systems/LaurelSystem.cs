using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Laurel — defensive shield that grants periodic invincibility.
    /// Every Cooldown × CooldownMult seconds, sets the player's Invincible.Timer to
    /// InvulDuration × DurationMult (or refreshes if already invincible from another source).
    ///
    /// Integrates with ContactDamageSystem: that system skips any player with Invincible.Timer > 0,
    /// so no extra pipeline changes are needed.
    ///
    /// Wiki base stats: Cooldown 10.0 s, Duration ~0.5 s. Benefits from CooldownMult and DurationMult.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct LaurelSystem : ISystem
    {
        ComponentLookup<Invincible> _invincibleLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _invincibleLookup = state.GetComponentLookup<Invincible>(isReadOnly: false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _invincibleLookup.Update(ref state);
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (laurel, stats, entity) in
                SystemAPI.Query<RefRW<LaurelState>, RefRO<PlayerStats>>()
                    .WithAll<PlayerTag>()
                    .WithNone<Downed>()
                    .WithEntityAccess())
            {
                laurel.ValueRW.Timer -= dt;
                if (laurel.ValueRO.Timer > 0f) continue;

                laurel.ValueRW.Timer = laurel.ValueRO.Cooldown * stats.ValueRO.CooldownMult;

                float invulDuration = laurel.ValueRO.InvulDuration * stats.ValueRO.DurationMult;

                if (_invincibleLookup.HasComponent(entity))
                {
                    var inv = _invincibleLookup[entity];
                    inv.Timer = math.max(inv.Timer, invulDuration);
                    _invincibleLookup[entity] = inv;
                }
            }
        }
    }
}
