using Unity.Entities;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Creates the SharedGold singleton entity exactly once at world startup.
    /// Disables itself immediately so it never runs again.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct SharedGoldBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var e = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent(e, new SharedGold { Total = 0 });
            state.Enabled = false;
        }
    }
}
