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

            foreach (var (weaponState, moveInput, transform) in
                SystemAPI.Query<RefRW<WeaponState>, RefRO<MoveInput>, RefRO<LocalTransform>>()
                    .WithAll<PlayerTag>().WithNone<Downed>())
            {
                ref var ws = ref weaponState.ValueRW;
                ws.SwingTimer -= dt;

                if (ws.SwingTimer > 0f) continue;

                float2 dir = moveInput.ValueRO.Value;
                if (math.lengthsq(dir) < 0.01f)
                    dir = new float2(1f, 0f); // default right when player is idle

                var arcEntity = ecb.CreateEntity();
                ecb.AddComponent(arcEntity, new HitArc
                {
                    Damage     = ws.Damage,
                    Direction  = math.normalizesafe(dir),
                    Range      = ws.Range,
                    ArcDegrees = ws.ArcDegrees,
                    Origin     = transform.ValueRO.Position
                });

                ws.SwingTimer = ws.SwingCooldown;
            }
        }
    }
}
