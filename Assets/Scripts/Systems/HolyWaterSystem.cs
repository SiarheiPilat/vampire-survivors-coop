using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Manages Holy Water in two passes each frame:
    ///
    ///   Pass 1 — Fire: each non-downed player with HolyWaterState throws a flask
    ///   in a random direction every Cooldown seconds. The flask is instantiated
    ///   from BulletPrefab with a HolyWaterProjectile component added.
    ///
    ///   Pass 2 — Land: moves all HolyWaterProjectile entities; when Traveled >=
    ///   MaxRange the flask "lands" — a HolyWaterPuddle entity is created at that
    ///   position (via ECB) and the flask is destroyed.
    ///
    /// Puddle lifetime/damage is handled by HolyWaterPuddleSystem.
    /// Wiki base stats: Damage 20/tick, Cooldown 6.0 s, travel ~4 u.
    /// </summary>
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct HolyWaterSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            if (!SystemAPI.HasSingleton<BulletPrefabData>()) return;
            var bulletPrefab = SystemAPI.GetSingleton<BulletPrefabData>().BulletPrefab;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // ── Pass 1: fire flasks from players ─────────────────────────────
            foreach (var (hw, transform, stats, entity) in
                SystemAPI.Query<RefRW<HolyWaterState>, RefRO<LocalTransform>, RefRO<PlayerStats>>()
                    .WithAll<PlayerTag>()
                    .WithNone<Downed>()
                    .WithEntityAccess())
            {
                hw.ValueRW.Timer -= dt;
                if (hw.ValueRO.Timer > 0f) continue;

                hw.ValueRW.Timer = hw.ValueRO.Cooldown * stats.ValueRO.CooldownMult;

                float damage = hw.ValueRO.Damage * stats.ValueRO.Might;
                int   amount = math.max(1, hw.ValueRO.Amount);

                for (int a = 0; a < amount; a++)
                {
                    float  angle = hw.ValueRW.Rng.NextFloat(0f, 2f * math.PI);
                    float2 dir2  = new float2(math.cos(angle), math.sin(angle));

                    var flask = ecb.Instantiate(bulletPrefab);
                    ecb.AddComponent(flask, new HolyWaterProjectile
                    {
                        Direction      = dir2,
                        Speed          = hw.ValueRO.Speed,
                        Traveled       = 0f,
                        MaxRange       = hw.ValueRO.MaxRange,
                        Damage         = damage,
                        PuddleRadius   = hw.ValueRO.Radius,
                        PuddleLifetime = hw.ValueRO.PuddleLifetime,
                        TickCooldown   = hw.ValueRO.TickCooldown
                    });
                    ecb.SetComponent(flask, LocalTransform.FromPositionRotationScale(
                        transform.ValueRO.Position, quaternion.identity, 0.25f));
                }
            }

            // ── Pass 2: move flasks and land them ────────────────────────────
            foreach (var (flask, transform, entity) in
                SystemAPI.Query<RefRW<HolyWaterProjectile>, RefRW<LocalTransform>>()
                    .WithEntityAccess())
            {
                float2 move     = flask.ValueRO.Direction * flask.ValueRO.Speed * dt;
                transform.ValueRW.Position += new float3(move.x, move.y, 0f);
                flask.ValueRW.Traveled     += math.length(move);

                if (flask.ValueRO.Traveled < flask.ValueRO.MaxRange) continue;

                // Land — create puddle at this position
                var puddle = ecb.CreateEntity();
                ecb.AddComponent(puddle, new HolyWaterPuddle
                {
                    Lifetime     = flask.ValueRO.PuddleLifetime,
                    Damage       = flask.ValueRO.Damage,
                    Radius       = flask.ValueRO.PuddleRadius,
                    TickTimer    = 0f,
                    TickCooldown = flask.ValueRO.TickCooldown
                });
                ecb.AddComponent(puddle, LocalTransform.FromPosition(transform.ValueRO.Position));

                ecb.DestroyEntity(entity);
            }
        }
    }
}
