using System;
using UnityEngine;

namespace VampireSurvivors.Menu
{
    /// <summary>
    /// Data for a single playable stage: identity, display info, and background colours.
    /// Serialised inside StageRegistry.
    /// </summary>
    [Serializable]
    public class StageDefinition
    {
        public string Id;
        public string DisplayName;
        [TextArea(1, 2)] public string Description;

        // Checkerboard tile colours for InfiniteBackground
        public Color TileColorA = new Color(0.07f, 0.11f, 0.07f, 1f);
        public Color TileColorB = new Color(0.10f, 0.15f, 0.10f, 1f);
    }
}
