using UnityEngine;

namespace VampireSurvivors.Menu
{
    /// <summary>
    /// ScriptableObject that holds all playable character definitions.
    /// Assign the asset to LobbyManager via the Inspector.
    /// Create via: Assets → Create → VampireSurvivors → Character Registry
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterRegistry",
                     menuName  = "VampireSurvivors/Character Registry")]
    public class CharacterRegistry : ScriptableObject
    {
        public CharacterDefinition[] Characters = System.Array.Empty<CharacterDefinition>();

        /// <summary>Returns null if no character with the given id exists.</summary>
        public CharacterDefinition Find(string id)
        {
            foreach (var def in Characters)
                if (string.Equals(def.Id, id, System.StringComparison.OrdinalIgnoreCase))
                    return def;
            return null;
        }

        /// <summary>Returns the display name, or a capitalized version of id as fallback.</summary>
        public string GetDisplayName(string id)
        {
            var def = Find(id);
            if (def != null && !string.IsNullOrEmpty(def.DisplayName))
                return def.DisplayName;
            // Fallback: capitalize first letter of raw id
            if (string.IsNullOrEmpty(id)) return "Unknown";
            return char.ToUpper(id[0]) + (id.Length > 1 ? id[1..] : "");
        }

        /// <summary>Returns "" if no description exists for the given id.</summary>
        public string GetDescription(string id)
        {
            var def = Find(id);
            return def != null ? def.Description ?? "" : "";
        }

        public int Count => Characters.Length;

        public string IdAt(int index) => Characters[index].Id;
    }
}
