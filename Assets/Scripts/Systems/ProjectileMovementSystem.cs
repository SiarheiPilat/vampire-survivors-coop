using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Moves all Projectile entities each frame. Three movement modes, selected by fields:
    ///
    ///   Straight  (Gravity == 0, TurnDistance == 0):
    ///     position += Direction * Speed * dt
    ///     Despawn when Traveled >= MaxRange.
    ///
    ///   Arcing    (Gravity > 0):
    ///     Velocity.y -= Gravity * dt; position += Velocity * dt
    ///     Despawn when Traveled >= MaxRange.
    ///
    ///   Returning (TurnDistance > 0):
    ///     Travels straight until Traveled >= TurnDistance, then Returning = true.
    ///     Once returning: Direction tracks owner position each frame.
    ///     Despawn when within 0.5 u of owner, or Traveled >= MaxRange (safety).
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(MagicWandSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ProjectileMovementSystem : ISystem
    {
        ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);

            float dt = SystemAPI.Time.DeltaTime;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (proj, transform, entity) in
                SystemAPI.Query<RefRW<Projectile>, RefRW<LocalTransform>>()
                    .WithEntityAccess())
            {
                // Tick down pierce lock so the same enemy can be re-hit after it exits
                if (proj.ValueRO.PierceLockTimer > 0f)
                {
                    proj.ValueRW.PierceLockTimer -= dt;
                    if (proj.ValueRW.PierceLockTimer <= 0f)
                        proj.ValueRW.LastPierceHit = Entity.Null;
                }

                float3 move;

                if (proj.ValueRO.Returning)
                {
                    // Homing return — steer toward owner every frame
                    if (proj.ValueRO.OwnerEntity != Entity.Null &&
                        _transformLookup.HasComponent(proj.ValueRO.OwnerEntity))
                    {
                        float3 ownerPos = _transformLookup[proj.ValueRO.OwnerEntity].Position;
                        float3 toOwner  = ownerPos - transform.ValueRO.Position;
                        float  dist     = math.length(toOwner);

                        if (dist < 0.5f)
                        {
                            ecb.DestroyEntity(entity);
                            continue;
                        }

                        proj.ValueRW.Direction = math.normalizesafe(toOwner);
                    }
                    move = proj.ValueRO.Direction * proj.ValueRO.Speed * dt;
                }
                else if (proj.ValueRO.Gravity > 0f)
                {
                    // Arcing: integrate gravity
                    proj.ValueRW.Velocity.y -= proj.ValueRO.Gravity * dt;
                    move = proj.ValueRO.Velocity * dt;
                }
                else
                {
                    // Straight
                    move = proj.ValueRO.Direction * proj.ValueRO.Speed * dt;

                    // Check turn distance for returning projectiles (Cross etc.)
                    if (proj.ValueRO.TurnDistance > 0f &&
                        proj.ValueRO.Traveled >= proj.ValueRO.TurnDistance)
                    {
                        proj.ValueRW.Returning = true;
                    }
                }

                transform.ValueRW.Position += new float3(move.x, move.y, 0f);
                proj.ValueRW.Traveled      += math.length(move);

                // Safety despawn / bounce for non-returning projectiles
                if (!proj.ValueRO.Returning && proj.ValueRO.Traveled >= proj.ValueRO.MaxRange)
                {
                    if (proj.ValueRO.BounceCount > 0)
                    {
                        // Reflect off whichever wall the dominant direction implies
                        var dir = proj.ValueRO.Direction;
                        if (math.abs(dir.x) >= math.abs(dir.y))
                            proj.ValueRW.Direction = new float3(-dir.x, dir.y, 0f);  // side wall
                        else
                            proj.ValueRW.Direction = new float3(dir.x, -dir.y, 0f); // top/bottom
                        proj.ValueRW.Traveled    = 0f;
                        proj.ValueRW.BounceCount = (byte)(proj.ValueRO.BounceCount - 1);
                    }
                    else
                    {
                        ecb.DestroyEntity(entity);
                    }
                }
            }
        }
    }
}
