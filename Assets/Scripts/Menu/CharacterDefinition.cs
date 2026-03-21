using System;
using UnityEngine;

namespace VampireSurvivors.Menu
{
    /// <summary>
    /// Data for a single playable character shown in the lobby.
    /// Serialized inside CharacterRegistry ScriptableObject.
    /// </summary>
    [Serializable]
    public class CharacterDefinition
    {
        [Tooltip("Internal id used by GameSceneBootstrap (lowercase, e.g. 'antonio')")]
        public string Id;

        [Tooltip("Display name shown in the lobby slot")]
        public string DisplayName;

        [Tooltip("Short flavour text / stat summary shown in the lobby slot")]
        [TextArea(1, 3)]
        public string Description;
    }
}
