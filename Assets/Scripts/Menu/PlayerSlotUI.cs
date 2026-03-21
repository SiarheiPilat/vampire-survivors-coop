using TMPro;
using UnityEngine;

namespace VampireSurvivors.Menu
{
    /// <summary>
    /// Controls the visual state of one lobby player slot.
    /// LobbyManager drives state changes; this class only handles display.
    /// </summary>
    public class PlayerSlotUI : MonoBehaviour
    {
        [Header("Empty State")]
        [SerializeField] GameObject emptyPanel;

        [Header("Joined State")]
        [SerializeField] GameObject joinedPanel;
        [SerializeField] TMP_Text   playerLabel;        // "P1", "P2", …
        [SerializeField] TMP_Text   characterName;
        [SerializeField] TMP_Text   characterDescription; // optional — assign in Inspector
        [SerializeField] TMP_Text   customizationName;

        public int SlotIndex { get; set; }

        public void ShowEmpty()
        {
            emptyPanel.SetActive(true);
            joinedPanel.SetActive(false);
        }

        /// <param name="displayName">Pre-resolved display name (e.g. "Antonio").</param>
        /// <param name="description">Optional flavour / stat line. Ignored if field unassigned.</param>
        /// <param name="customizationIndex">0-based skin index.</param>
        public void ShowJoined(string displayName, string description, int customizationIndex)
        {
            emptyPanel.SetActive(false);
            joinedPanel.SetActive(true);
            playerLabel.text       = $"P{SlotIndex + 1}";
            characterName.text     = string.IsNullOrEmpty(displayName) ? "Unknown" : displayName;
            customizationName.text = $"Skin {customizationIndex + 1}";

            if (characterDescription != null)
                characterDescription.text = description ?? "";
        }
    }
}
