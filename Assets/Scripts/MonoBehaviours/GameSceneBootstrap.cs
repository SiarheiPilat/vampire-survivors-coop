using Unity.Entities;
using UnityEngine;
using VampireSurvivors.Components;
using VampireSurvivors.Menu;

namespace VampireSurvivors.MonoBehaviours
{
    /// <summary>
    /// Runs at GameScene startup. If a GameSession exists (i.e. the player came
    /// through the lobby), stamps AssignedDeviceId onto each player entity so
    /// PlayerInputSystem uses the correct device.
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

            // Find player entities by PlayerIndex
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerIndex>());
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

                var deviceId = session.Slots[slot].DeviceId;

                if (em.HasComponent<AssignedDeviceId>(entity))
                    em.SetComponentData(entity, new AssignedDeviceId { Value = deviceId });
                else
                    em.AddComponentData(entity, new AssignedDeviceId { Value = deviceId });
            }

            Debug.Log($"[GameSceneBootstrap] Stamped {session.FilledCount} device assignment(s).");
        }
    }
}
