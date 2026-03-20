using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Ticks each player's WeaponState.SwingTimer. When it reaches 0, spawns a
    /// HitArc entity encoding the arc's origin, direction, range, and damage.
    /// HitArcSystem consumes and destroys HitArc entities.
    /// </summary>
    [BurstCompile]
    public partial struct WhipSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (weaponState, moveInput, transform, stats, entity) in
                SystemAPI.Query<RefRW<WeaponState>, RefRO<MoveInput>, RefRO<LocalTransform>, RefRO<PlayerStats>>()
                    .WithAll<PlayerTag>().WithNone<Downed>().WithEntityAccess())
            {
                ref var ws = ref weaponState.ValueRW;
                ws.SwingTimer -= dt;

                if (ws.SwingTimer > 0f) continue;

                float2 dir = moveInput.ValueRO.Value;
                if (math.lengthsq(dir) < 0.01f)
                    dir = new float2(1f, 0f); // default right when player is idle

                float  baseDamage = ws.Damage * stats.ValueRO.Might;
                int    amount     = math.max(1, ws.Amount);
                float  baseAngle  = math.atan2(dir.y, dir.x);
                float  stepRad    = 2f * math.PI / amount;

                for (int a = 0; a < amount; a++)
                {
                    float  angle    = baseAngle + a * stepRad;
                    float2 arcDir   = new float2(math.cos(angle), math.sin(angle));
                    var    arcEntity = ecb.CreateEntity();
                    ecb.AddComponent(arcEntity, new HitArc
                    {
                        Damage      = baseDamage,
                        Direction   = arcDir,
                        Range       = ws.Range,
                        ArcDegrees  = ws.ArcDegrees,
                        Origin      = transform.ValueRO.Position,
                        OwnerEntity = ws.IsEvolved ? entity : Entity.Null,
                        HealPerHit  = ws.HealPerHit
                    });
                }

                ws.SwingTimer = ws.SwingCooldown * stats.ValueRO.CooldownMult;
            }
        }
    }
}
