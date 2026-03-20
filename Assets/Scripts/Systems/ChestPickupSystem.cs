using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Walk-over chest collection. Any player within CollectRadius (0.6u) of a Chest entity
    /// collects it and receives one of four randomised rewards:
    ///   40% → Gold bonus  (100–200 gold added to SharedGold)
    ///   30% → Full HP restore for the collecting player
    ///   20% → XP burst  (+100 XP for the collecting player)
    ///   10% → Invincibility surge  (Invincible.Timer = 8s for the collecting player)
    /// The chest entity is destroyed after collection.
    /// Not Burst-compiled — modifies a managed singleton (SharedGold write-back).
    /// </summary>
    [UpdateAfter(typeof(XpGemSystem))]
    public partial struct ChestPickupSystem : ISystem
    {
        const float CollectRadius = 0.6f;

        ComponentLookup<LocalTransform> _transformLookup;
        ComponentLookup<Health>         _healthLookup;
        ComponentLookup<Invincible>     _invincibleLookup;
        ComponentLookup<PlayerStats>    _statLookup;

        public void OnCreate(ref SystemState state)
        {
            _transformLookup  = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
            _healthLookup     = state.GetComponentLookup<Health>(isReadOnly: false);
            _invincibleLookup = state.GetComponentLookup<Invincible>(isReadOnly: false);
            _statLookup       = state.GetComponentLookup<PlayerStats>(isReadOnly: false);
        }

        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _invincibleLookup.Update(ref state);
            _statLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            bool hasRunStats = SystemAPI.TryGetSingletonEntity<SharedGold>(out var runStatsEntity);
            var  runStats    = hasRunStats ? SystemAPI.GetSingleton<SharedGold>() : default;
            bool goldDirty   = false;

            foreach (var (chestRef, chestTransform, chestEntity) in
                SystemAPI.Query<RefRW<Chest>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                float3 chestPos = chestTransform.ValueRO.Position;

                foreach (var (playerTransform, playerEntity) in
                    SystemAPI.Query<RefRO<LocalTransform>>()
                        .WithAll<PlayerTag>().WithNone<Downed>().WithEntityAccess())
                {
                    float dist = math.distance(
                        playerTransform.ValueRO.Position.xy, chestPos.xy);
                    if (dist > CollectRadius) continue;

                    // Use the chest's own RNG for reward selection
                    ref var rng = ref chestRef.ValueRW.Rng;
                    float roll = rng.NextFloat();

                    if (roll < 0.40f)
                    {
                        // 40%: Gold bonus 100–200
                        int bonus = rng.NextInt(100, 201);
                        if (hasRunStats)
                        {
                            runStats.Total += bonus;
                            goldDirty = true;
                        }
                    }
                    else if (roll < 0.70f)
                    {
                        // 30%: Full HP restore
                        if (_healthLookup.HasComponent(playerEntity))
                        {
                            var hp = _healthLookup[playerEntity];
                            hp.Current = hp.Max;
                            _healthLookup[playerEntity] = hp;
                        }
                    }
                    else if (roll < 0.90f)
                    {
                        // 20%: XP burst +100
                        if (_statLookup.HasComponent(playerEntity))
                        {
                            var ps = _statLookup[playerEntity];
                            ps.Xp += 100f;
                            _statLookup[playerEntity] = ps;
                        }
                    }
                    else
                    {
                        // 10%: Invincibility surge 8s
                        if (_invincibleLookup.HasComponent(playerEntity))
                        {
                            var inv = _invincibleLookup[playerEntity];
                            inv.Timer = math.max(inv.Timer, 8f);
                            _invincibleLookup[playerEntity] = inv;
                        }
                    }

                    ecb.DestroyEntity(chestEntity);
                    break; // only one player collects per chest
                }
            }

            if (goldDirty && hasRunStats)
                state.EntityManager.SetComponentData(runStatsEntity, runStats);
        }
    }
}
