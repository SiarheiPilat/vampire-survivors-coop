using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Spawns Gatti Amari cats from each player with GattiAmariState every Cooldown seconds.
    /// Each cat is a BulletPrefab entity with a GattiAmariCat component added.
    /// Movement and attacks are handled by GattiAmariCatSystem.
    /// Wiki base stats: Damage 10, Cooldown 5.0 s, 1 cat per trigger, 5.0 s lifetime.
    /// </summary>
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct GattiAmariSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            if (!SystemAPI.HasSingleton<BulletPrefabData>()) return;
            var bulletPrefab = SystemAPI.GetSingleton<BulletPrefabData>().BulletPrefab;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (gatti, transform, stats) in
                SystemAPI.Query<RefRW<GattiAmariState>, RefRO<LocalTransform>, RefRO<PlayerStats>>()
                    .WithAll<PlayerTag>()
                    .WithNone<Downed>())
            {
                gatti.ValueRW.Timer -= dt;
                if (gatti.ValueRO.Timer > 0f) continue;

                gatti.ValueRW.Timer = gatti.ValueRO.Cooldown * stats.ValueRO.CooldownMult;

                float lifetime = gatti.ValueRO.CatLifetime * stats.ValueRO.DurationMult;

                if (gatti.ValueRO.IsEvolved)
                {
                    // ── Vicious Hunger: 2 giant cats, 30 dmg, 1.5u radius, 7s lifetime ──
                    // Wiki: Vicious Hunger — 30 dmg, 8s CD, Amount 2, Duration 7s
                    float vhDamage = gatti.ValueRO.Damage * stats.ValueRO.Might;
                    float vhRadius = 1.5f * stats.ValueRO.AreaMult;
                    for (int a = 0; a < 2; a++)
                    {
                        float  spawnAngle = (float)a / 2f * math.PI * 2f;
                        float3 spawnPos   = transform.ValueRO.Position +
                            new float3(math.cos(spawnAngle) * 0.5f, math.sin(spawnAngle) * 0.5f, 0f);
                        uint   seed = (uint)(stats.GetHashCode() * 1234567891u + (uint)a * 2654435761u + 7u);
                        if (seed == 0) seed = 7u;

                        var cat = ecb.Instantiate(bulletPrefab);
                        ecb.AddComponent(cat, new GattiAmariCat
                        {
                            Damage         = vhDamage,
                            Radius         = vhRadius,
                            Lifetime       = lifetime,
                            AttackTimer    = 0f,
                            AttackCooldown = 1.0f,
                            WanderTimer    = 0f,
                            WanderDir      = new float2(math.cos(spawnAngle), math.sin(spawnAngle)),
                            Rng            = new Unity.Mathematics.Random(seed),
                        });
                        // Giant cats: scale 0.6u
                        ecb.SetComponent(cat, LocalTransform.FromPositionRotationScale(
                            spawnPos, quaternion.identity, 0.6f));
                    }
                }
                else
                {
                    // ── Normal Gatti Amari: wandering small cats ──
                    float damage = gatti.ValueRO.Damage * stats.ValueRO.Might;
                    float radius = 0.5f * stats.ValueRO.AreaMult;
                    int   amount = math.max(1, gatti.ValueRO.Amount);

                    for (int a = 0; a < amount; a++)
                    {
                        float  spawnAngle = (float)a / amount * math.PI * 2f;
                        float3 spawnPos   = transform.ValueRO.Position +
                            new float3(math.cos(spawnAngle) * 0.4f, math.sin(spawnAngle) * 0.4f, 0f);
                        uint seed = (uint)(stats.GetHashCode() * 2654435761u + (uint)a * 987654321u + 1u);
                        if (seed == 0) seed = 1;

                        var cat = ecb.Instantiate(bulletPrefab);
                        ecb.AddComponent(cat, new GattiAmariCat
                        {
                            Damage         = damage,
                            Radius         = radius,
                            Lifetime       = lifetime,
                            AttackTimer    = 0f,
                            AttackCooldown = 1.0f,
                            WanderTimer    = 0f,
                            WanderDir      = new float2(math.cos(spawnAngle), math.sin(spawnAngle)),
                            Rng            = new Unity.Mathematics.Random(seed),
                        });
                        // Small cats: scale 0.3u
                        ecb.SetComponent(cat, LocalTransform.FromPositionRotationScale(
                            spawnPos, quaternion.identity, 0.3f));
                    }
                }
            }
        }
    }
}
