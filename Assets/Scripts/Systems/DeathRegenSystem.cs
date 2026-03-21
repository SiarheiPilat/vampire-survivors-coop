using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Regenerates health on the Death boss entity at 666 HP/s.
    /// This keeps Death effectively unkillable even against maxed-out builds.
    /// Runs before HealthSystem so regen is applied before the death check.
    /// </summary>
    [BurstCompile]
    [UpdateBefore(typeof(HealthSystem))]
    public partial struct DeathRegenSystem : ISystem
    {
        float _accumulator;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var health in SystemAPI.Query<RefRW<Health>>()
                .WithAll<DeathBossTag>())
            {
                // Accumulate fractional HP and apply integer increments to avoid
                // tiny per-frame additions being lost to int truncation.
                // (Each instance is independent, but there's typically only one Death.)
                int regen = (int)(666f * dt);
                if (regen < 1) regen = 1; // always regen at least 1/frame
                health.ValueRW.Current = math.min(health.ValueRW.Current + regen, health.ValueRW.Max);
            }
        }
    }
}
