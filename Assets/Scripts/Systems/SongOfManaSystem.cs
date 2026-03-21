using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Song of Mana — pulses a tall vertical column of mana centered on the player
    /// every Cooldown seconds, dealing damage to all enemies within the rectangular column.
    ///
    ///   Base: 10 dmg, 2.0 s CD, column 1.5 u wide × 6.0 u tall.
    ///   Evolved (Mannajja = Song of Mana + Skull O'Maniac):
    ///     40 dmg, 4.5 s CD, column 6.0 u wide × 8.0 u tall.
    ///
    /// Intentionally ignores ProjectileSpeedMult (wiki: "ignores Speed stat").
    /// Column width and height scale with player's AreaMult.
    /// Burst-compiled; IJobEntity runs single-threaded (.Run()) to avoid Health write races.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct SongOfManaSystem : ISystem
    {
        ComponentLookup<Health> _healthLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _healthLookup = state.GetComponentLookup<Health>(isReadOnly: false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _healthLookup.Update(ref state);

            float dt = SystemAPI.Time.DeltaTime;

            var enemyQuery = SystemAPI.QueryBuilder()
                .WithAll<EnemyTag, LocalTransform, Health>().Build();
            if (enemyQuery.IsEmpty) return;

            var enemyEntities   = enemyQuery.ToEntityArray(Allocator.TempJob);
            var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            new SongPulseJob
            {
                EnemyEntities   = enemyEntities,
                EnemyTransforms = enemyTransforms,
                HealthLookup    = _healthLookup,
                DeltaTime       = dt,
                Ecb             = ecb,
            }.Run();

            enemyEntities.Dispose();
            enemyTransforms.Dispose();
        }

        [BurstCompile]
        [WithAll(typeof(PlayerTag))]
        [WithNone(typeof(Downed))]
        partial struct SongPulseJob : IJobEntity
        {
            [ReadOnly] public NativeArray<Entity>         EnemyEntities;
            [ReadOnly] public NativeArray<LocalTransform> EnemyTransforms;

            [NativeDisableParallelForRestriction] public ComponentLookup<Health> HealthLookup;

            public float               DeltaTime;
            public EntityCommandBuffer Ecb;

            void Execute(ref SongOfManaState song, in LocalTransform transform, in PlayerStats stats)
            {
                song.Timer -= DeltaTime;
                if (song.Timer > 0f) return;

                song.Timer = song.Cooldown * stats.CooldownMult;

                int    damage     = (int)(song.Damage * stats.Might);
                float  halfW      = song.HalfWidth  * stats.AreaMult;
                float  halfH      = song.HalfHeight * stats.AreaMult;
                float2 playerPos  = transform.Position.xy;

                for (int i = 0; i < EnemyEntities.Length; i++)
                {
                    float2 diff = EnemyTransforms[i].Position.xy - playerPos;

                    // Rectangular column hit check
                    if (math.abs(diff.x) > halfW) continue;
                    if (math.abs(diff.y) > halfH) continue;

                    var hp = HealthLookup[EnemyEntities[i]];
                    hp.Current -= damage;
                    HealthLookup[EnemyEntities[i]] = hp;

                    var dmgEvt = Ecb.CreateEntity();
                    Ecb.AddComponent(dmgEvt, new DamageNumberEvent
                    {
                        WorldPosition = EnemyTransforms[i].Position,
                        Damage        = damage
                    });

                    // Gentle knockback: push horizontally (column hits from the side)
                    float2 pushDir = math.normalizesafe(new float2(diff.x, 0f));
                    if (math.lengthsq(pushDir) < 0.01f) pushDir = new float2(1f, 0f);
                    Ecb.SetComponent(EnemyEntities[i], new Knockback { Velocity = pushDir * 3f });
                }
            }
        }
    }
}
