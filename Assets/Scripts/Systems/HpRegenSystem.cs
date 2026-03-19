using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Regenerates HP for players whose PlayerStats.HpRegen > 0.
    /// Uses a fractional accumulator so sub-integer regen rates (e.g. 0.2 HP/s)
    /// work correctly without floating-point Health.
    /// Wiki: Pummarola grants +0.2 HP/s per level (up to 5 levels in original).
    /// </summary>
    [BurstCompile]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct HpRegenSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            new RegenJob { DeltaTime = dt }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(PlayerTag))]
        [WithNone(typeof(Downed))]
        partial struct RegenJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(ref PlayerStats stats, ref Health health)
            {
                if (stats.HpRegen <= 0f || health.Current >= health.Max) return;

                stats.HpRegenAccum += stats.HpRegen * DeltaTime;

                int toRegen = (int)stats.HpRegenAccum;
                if (toRegen <= 0) return;

                stats.HpRegenAccum -= toRegen;
                health.Current = math.min(health.Current + toRegen, health.Max);
            }
        }
    }
}
