using Unity.Entities;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Runs after all damage systems. Destroys any entity whose Health.Current
    /// has dropped to or below 0. Logs a message for player deaths.
    /// Not Burst-compiled — calls Debug.Log.
    /// </summary>
    [UpdateAfter(typeof(ContactDamageSystem))]
    [UpdateAfter(typeof(HitArcSystem))]
    public partial struct HealthSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (health, entity) in
                SystemAPI.Query<RefRO<Health>>().WithEntityAccess())
            {
                if (health.ValueRO.Current > 0) continue;

                if (SystemAPI.HasComponent<PlayerTag>(entity))
                {
                    var idx = SystemAPI.GetComponent<PlayerIndex>(entity);
                    Debug.Log($"[HealthSystem] Player {idx.Value} died.");
                }

                ecb.DestroyEntity(entity);
            }
        }
    }
}
