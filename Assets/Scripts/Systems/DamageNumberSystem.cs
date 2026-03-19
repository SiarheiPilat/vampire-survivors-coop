using Unity.Entities;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Bridges ECS damage events to the MonoBehaviour DamageNumberRenderer pool.
    /// Reads all DamageNumberEvent entities created by weapon systems this frame,
    /// calls DamageNumberRenderer.Spawn() for each, then destroys the event entity.
    /// Not Burst-compiled — calls managed MonoBehaviour API.
    /// </summary>
    [UpdateAfter(typeof(HealthSystem))]
    public partial struct DamageNumberSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var renderer = DamageNumberRenderer.Instance;
            if (renderer == null) return;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (evt, entity) in
                SystemAPI.Query<RefRO<DamageNumberEvent>>().WithEntityAccess())
            {
                renderer.Spawn(
                    new Vector3(evt.ValueRO.WorldPosition.x, evt.ValueRO.WorldPosition.y, 0f),
                    evt.ValueRO.Damage);
                ecb.DestroyEntity(entity);
            }
        }
    }
}
