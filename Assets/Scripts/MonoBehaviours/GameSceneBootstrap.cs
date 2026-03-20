using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VampireSurvivors.Components;
using VampireSurvivors.Menu;

namespace VampireSurvivors.MonoBehaviours
{
    /// <summary>
    /// Runs at GameScene startup. If a GameSession exists (i.e. the player came
    /// through the lobby), stamps AssignedDeviceId onto each player entity and
    /// configures the character's starting weapon + stat bonus.
    ///
    /// Character mapping (wiki-accurate base stats):
    ///   antonio    → Whip  (WeaponState already baked), Might +10% (1.1)
    ///   imelda     → Magic Wand (removes Whip), XpMult +10% (1.1)
    ///   pasqualina → Magic Wand (removes Whip), no bonus (Runetracer TBD)
    ///   gennaro    → Knife (removes Whip), no bonus
    ///
    /// If no GameSession exists (direct scene load during dev), does nothing —
    /// baked PlayerAuthoring entities with the keyboard/Gamepad.all fallback
    /// remain intact.
    /// </summary>
    public class GameSceneBootstrap : MonoBehaviour
    {
        void Start()
        {
            var session = GameSession.Instance;
            if (session == null)
            {
                Debug.Log("[GameSceneBootstrap] No GameSession — using baked entities (dev mode).");
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;

            using var query    = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerIndex>());
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

            foreach (var entity in entities)
            {
                var playerIndex = em.GetComponentData<PlayerIndex>(entity);
                int slot        = playerIndex.Value;

                if (slot >= session.Slots.Length || !session.Slots[slot].Filled)
                {
                    em.DestroyEntity(entity);
                    continue;
                }

                // Stamp device ID
                var deviceId = session.Slots[slot].DeviceId;
                if (em.HasComponent<AssignedDeviceId>(entity))
                    em.SetComponentData(entity, new AssignedDeviceId { Value = deviceId });
                else
                    em.AddComponentData(entity, new AssignedDeviceId { Value = deviceId });

                // Apply character-specific starting weapon and stat bonus
                string charId = session.Slots[slot].CharacterId ?? "antonio";
                ApplyCharacter(em, entity, charId, slot);
            }

            Debug.Log($"[GameSceneBootstrap] Stamped {session.FilledCount} device assignment(s).");
        }

        static void ApplyCharacter(EntityManager em, Entity entity, string charId, int slot)
        {
            switch (charId)
            {
                case "antonio":
                    SetBaseStats(em, entity, hp: 120, speed: 7.0f);
                    var antonioStats = em.GetComponentData<PlayerStats>(entity);
                    antonioStats.Might = 1.1f;
                    em.SetComponentData(entity, antonioStats);
                    Debug.Log($"[GameSceneBootstrap] P{slot} Antonio: Whip, HP=120, Might=1.1");
                    break;

                case "imelda":
                    SetBaseStats(em, entity, hp: 100, speed: 7.0f);
                    if (em.HasComponent<WeaponState>(entity))
                        em.RemoveComponent<WeaponState>(entity);
                    em.AddComponentData(entity, new MagicWandState
                    {
                        Timer    = 0f,
                        Cooldown = 0.5f,
                        Damage   = 10f,
                        Speed    = 10f,
                        MaxRange = 15f,
                        Amount   = 1
                    });
                    var imeldaStats = em.GetComponentData<PlayerStats>(entity);
                    imeldaStats.XpMult = 1.1f;
                    em.SetComponentData(entity, imeldaStats);
                    Debug.Log($"[GameSceneBootstrap] P{slot} Imelda: MagicWand, HP=100, XpMult=1.1");
                    break;

                case "pasqualina":
                    SetBaseStats(em, entity, hp: 130, speed: 7.0f);
                    if (em.HasComponent<WeaponState>(entity))
                        em.RemoveComponent<WeaponState>(entity);
                    em.AddComponentData(entity, new RunetracerState
                    {
                        Timer    = 0f,
                        Cooldown = 0.35f,
                        Damage   = 10f,
                        Speed    = 8f,
                        MaxRange = 10f,
                        Bounces  = 3,
                        Amount   = 1
                    });
                    Debug.Log($"[GameSceneBootstrap] P{slot} Pasqualina: Runetracer, HP=130");
                    break;

                case "gennaro":
                    SetBaseStats(em, entity, hp: 100, speed: 7.7f);
                    if (em.HasComponent<WeaponState>(entity))
                        em.RemoveComponent<WeaponState>(entity);
                    em.AddComponentData(entity, new KnifeState
                    {
                        Timer    = 0f,
                        Cooldown = 0.35f,
                        Damage   = 10f,
                        Speed    = 15f,
                        MaxRange = 12f,
                        Amount   = 1
                    });
                    Debug.Log($"[GameSceneBootstrap] P{slot} Gennaro: Knife, HP=100, Speed=7.7");
                    break;

                case "arca":
                    // Wiki: Garlic starter, HP 130, Speed 7.0, -5% cooldown bonus
                    SetBaseStats(em, entity, hp: 130, speed: 7.0f);
                    if (em.HasComponent<WeaponState>(entity))
                        em.RemoveComponent<WeaponState>(entity);
                    em.AddComponentData(entity, new GarlicState
                    {
                        Timer    = 0f,
                        Cooldown = 1.5f,
                        Damage   = 10f,
                        Range    = 1.5f
                    });
                    var arcaStats = em.GetComponentData<PlayerStats>(entity);
                    arcaStats.CooldownMult = 0.95f; // -5% cooldown at start
                    em.SetComponentData(entity, arcaStats);
                    Debug.Log($"[GameSceneBootstrap] P{slot} Arca: Garlic, HP=130, CooldownMult=0.95");
                    break;

                case "porta":
                    // Wiki: Lightning Ring starter, HP 100, Speed 7.5, no stat bonus
                    SetBaseStats(em, entity, hp: 100, speed: 7.5f);
                    if (em.HasComponent<WeaponState>(entity))
                        em.RemoveComponent<WeaponState>(entity);
                    em.AddComponentData(entity, new LightningRingState
                    {
                        Timer    = 0f,
                        Cooldown = 0.6f,
                        Damage   = 40f,
                        Amount   = 1,
                        Rng      = Unity.Mathematics.Random.CreateFromIndex((uint)(slot * 97 + 13))
                    });
                    Debug.Log($"[GameSceneBootstrap] P{slot} Porta: Lightning Ring, HP=100, Speed=7.5");
                    break;

                case "lama":
                    // Wiki: Axe starter, HP 130, Speed 6.5, Might +10%
                    SetBaseStats(em, entity, hp: 130, speed: 6.5f);
                    if (em.HasComponent<WeaponState>(entity))
                        em.RemoveComponent<WeaponState>(entity);
                    em.AddComponentData(entity, new AxeState
                    {
                        Timer    = 0f,
                        Cooldown = 1.25f,
                        Damage   = 20f,
                        Speed    = 15f,
                        Gravity  = 12f,
                        MaxRange = 10f,
                        Amount   = 1
                    });
                    var lamaStats = em.GetComponentData<PlayerStats>(entity);
                    lamaStats.Might = 1.1f;
                    em.SetComponentData(entity, lamaStats);
                    Debug.Log($"[GameSceneBootstrap] P{slot} Lama: Axe, HP=130, Might=1.1");
                    break;

                case "mortaccio":
                    // Wiki: Bone starter, HP 100, Speed 7.0, no stat bonus
                    SetBaseStats(em, entity, hp: 100, speed: 7.0f);
                    if (em.HasComponent<WeaponState>(entity))
                        em.RemoveComponent<WeaponState>(entity);
                    em.AddComponentData(entity, new BoneState
                    {
                        Timer    = 0f,
                        Cooldown = 0.5f,
                        Damage   = 30f,
                        Speed    = 8f,
                        MaxRange = 12f,
                        Bounces  = 2,
                        Amount   = 1
                    });
                    Debug.Log($"[GameSceneBootstrap] P{slot} Mortaccio: Bone, HP=100, Speed=7.0");
                    break;

                case "yattacavallo":
                    // Wiki: Santa Water / Holy Water starter, HP 100, Speed 7.0, no stat bonus
                    SetBaseStats(em, entity, hp: 100, speed: 7.0f);
                    if (em.HasComponent<WeaponState>(entity))
                        em.RemoveComponent<WeaponState>(entity);
                    em.AddComponentData(entity, new HolyWaterState
                    {
                        Timer          = 0f,
                        Cooldown       = 6.0f,
                        Damage         = 20f,
                        Speed          = 8f,
                        MaxRange       = 4f,
                        Radius         = 1.5f,
                        PuddleLifetime = 5.0f,
                        TickCooldown   = 0.5f,
                        Amount         = 1,
                        Rng            = Unity.Mathematics.Random.CreateFromIndex((uint)(slot * 53 + 7))
                    });
                    Debug.Log($"[GameSceneBootstrap] P{slot} Yatta Cavallo: Holy Water, HP=100, Speed=7.0");
                    break;

                default:
                    Debug.LogWarning($"[GameSceneBootstrap] Unknown character '{charId}' for P{slot} — keeping Whip.");
                    break;
            }
        }
        static void SetBaseStats(EntityManager em, Entity entity, int hp, float speed)
        {
            em.SetComponentData(entity, new Health { Current = hp, Max = hp });
            var ps = em.GetComponentData<PlayerStats>(entity);
            ps.Hp    = hp;
            ps.MaxHp = hp;
            em.SetComponentData(entity, ps);
            if (em.HasComponent<MoveSpeed>(entity))
                em.SetComponentData(entity, new MoveSpeed { Value = speed });
        }
    }
}
