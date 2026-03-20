using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Processes PendingExplosion entities spawned by expired evolving projectiles
    /// (currently: NO FUTURE / Runetracer evolution).
    ///
    /// Each frame: for every PendingExplosion, deal Damage to all enemies within
    /// Radius world units, applying the same knockback as regular projectiles,
    /// then destroy the entity.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(ProjectileMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ExplosionSystem : ISystem
    {
        EntityQuery _enemyQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _enemyQuery = SystemAPI.QueryBuilder()
                .WithAll<EnemyTag, Health, LocalTransform, Knockback>()
                .WithNone<Downed>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_enemyQuery.IsEmpty) return;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            var enemyEntities    = _enemyQuery.ToEntityArray(Allocator.Temp);
            var enemyTransforms  = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var enemyHealths     = _enemyQuery.ToComponentDataArray<Health>(Allocator.Temp);
            var enemyKnockbacks  = _enemyQuery.ToComponentDataArray<Knockback>(Allocator.Temp);

            foreach (var (explosion, entity) in
                SystemAPI.Query<RefRO<PendingExplosion>>().WithEntityAccess())
            {
                float3 centre  = explosion.ValueRO.Position;
                float  radiusSq = explosion.ValueRO.Radius * explosion.ValueRO.Radius;
                float  dmg      = explosion.ValueRO.Damage;

                for (int i = 0; i < enemyEntities.Length; i++)
                {
                    float distSq = math.distancesq(centre, enemyTransforms[i].Position);
                    if (distSq > radiusSq) continue;

                    // Apply damage
                    var hp = enemyHealths[i];
                    hp.Current -= (int)math.round(dmg);
                    ecb.SetComponent(enemyEntities[i], hp);

                    // Knockback (same magnitude as a regular projectile hit ~8 u/s)
                    float3 awayDir = math.normalizesafe(enemyTransforms[i].Position - centre);
                    var kb = enemyKnockbacks[i];
                    kb.Velocity += new float2(awayDir.x, awayDir.y) * 8f;
                    ecb.SetComponent(enemyEntities[i], kb);
                }

                ecb.DestroyEntity(entity);
            }

            enemyEntities.Dispose();
            enemyTransforms.Dispose();
            enemyHealths.Dispose();
            enemyKnockbacks.Dispose();
        }
    }
}
