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
                    // Whip is already baked as WeaponState — just apply the Might bonus
                    var antonioStats = em.GetComponentData<PlayerStats>(entity);
                    antonioStats.Might = 1.1f;
                    em.SetComponentData(entity, antonioStats);
                    Debug.Log($"[GameSceneBootstrap] P{slot} Antonio: Whip, Might=1.1");
                    break;

                case "imelda":
                    // Replace Whip with Magic Wand; +10% XP gain
                    if (em.HasComponent<WeaponState>(entity))
                        em.RemoveComponent<WeaponState>(entity);
                    em.AddComponentData(entity, new MagicWandState
                    {
                        Timer    = 0f,
                        Cooldown = 0.5f,
                        Damage   = 10f,
                        Speed    = 10f,
                        MaxRange = 15f
                    });
                    var imeldaStats = em.GetComponentData<PlayerStats>(entity);
                    imeldaStats.XpMult = 1.1f;
                    em.SetComponentData(entity, imeldaStats);
                    Debug.Log($"[GameSceneBootstrap] P{slot} Imelda: MagicWand, XpMult=1.1");
                    break;

                case "pasqualina":
                    // Runetracer not yet implemented — give Magic Wand, no bonus
                    if (em.HasComponent<WeaponState>(entity))
                        em.RemoveComponent<WeaponState>(entity);
                    em.AddComponentData(entity, new MagicWandState
                    {
                        Timer    = 0f,
                        Cooldown = 0.5f,
                        Damage   = 10f,
                        Speed    = 10f,
                        MaxRange = 15f
                    });
                    Debug.Log($"[GameSceneBootstrap] P{slot} Pasqualina: MagicWand (Runetracer TBD)");
                    break;

                case "gennaro":
                    // Replace Whip with Knife; wiki: +1 projectile count (deferred)
                    if (em.HasComponent<WeaponState>(entity))
                        em.RemoveComponent<WeaponState>(entity);
                    em.AddComponentData(entity, new KnifeState
                    {
                        Timer    = 0f,
                        Cooldown = 0.35f,
                        Damage   = 10f,
                        Speed    = 15f,
                        MaxRange = 12f
                    });
                    Debug.Log($"[GameSceneBootstrap] P{slot} Gennaro: Knife");
                    break;

                default:
                    Debug.LogWarning($"[GameSceneBootstrap] Unknown character '{charId}' for P{slot} — keeping Whip.");
                    break;
            }
        }
    }
}
