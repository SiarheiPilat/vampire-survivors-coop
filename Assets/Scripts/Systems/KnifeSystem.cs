using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Fires a knife projectile in the player's facing direction every Cooldown seconds.
    /// Unlike Magic Wand (nearest-enemy aim), the knife travels in the last movement direction,
    /// defaulting to right on spawn. Projectile is handled by the shared Projectile system.
    /// Wiki base stats: Damage 10, Speed 15 u/s, Cooldown 0.35 s.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct KnifeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (knife, facing, transform) in
                SystemAPI.Query<RefRW<KnifeState>, RefRO<FacingDirection>, RefRO<LocalTransform>>()
                    .WithAll<PlayerTag>()
                    .WithNone<Downed>())
            {
                knife.ValueRW.Timer -= dt;
                if (knife.ValueRO.Timer > 0f) continue;

                knife.ValueRW.Timer = knife.ValueRO.Cooldown;

                float2 dir2 = math.lengthsq(facing.ValueRO.Value) > 0.001f
                    ? math.normalize(facing.ValueRO.Value)
                    : new float2(1f, 0f); // default right

                var bullet = ecb.CreateEntity();
                ecb.AddComponent(bullet, new Projectile
                {
                    Damage    = knife.ValueRO.Damage,
                    Speed     = knife.ValueRO.Speed,
                    Direction = new float3(dir2.x, dir2.y, 0f),
                    MaxRange  = knife.ValueRO.MaxRange,
                    Traveled  = 0f
                });
                ecb.AddComponent(bullet, LocalTransform.FromPosition(transform.ValueRO.Position));
            }
        }
    }
}
