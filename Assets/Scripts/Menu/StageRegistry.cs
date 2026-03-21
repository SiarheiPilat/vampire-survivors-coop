using UnityEngine;

namespace VampireSurvivors.Menu
{
    /// <summary>
    /// ScriptableObject catalogue of all playable stages.
    /// Assign the asset to LobbyManager and GameSceneBootstrap in the Inspector.
    ///
    /// Create via:  Assets ▸ Create ▸ VampireSurvivors ▸ Stage Registry
    /// </summary>
    [CreateAssetMenu(menuName = "VampireSurvivors/Stage Registry", fileName = "StageRegistry")]
    public class StageRegistry : ScriptableObject
    {
        public StageDefinition[] Stages;

        public int Count => Stages?.Length ?? 0;

        public StageDefinition At(int index)
            => (Stages != null && index >= 0 && index < Stages.Length) ? Stages[index] : null;

        public StageDefinition Find(string id)
        {
            if (Stages == null || string.IsNullOrEmpty(id)) return null;
            foreach (var s in Stages)
                if (string.Equals(s.Id, id, System.StringComparison.OrdinalIgnoreCase))
                    return s;
            return null;
        }

        public int IndexOf(string id)
        {
            if (Stages == null) return 0;
            for (int i = 0; i < Stages.Length; i++)
                if (string.Equals(Stages[i].Id, id, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            return 0;
        }
    }
}
