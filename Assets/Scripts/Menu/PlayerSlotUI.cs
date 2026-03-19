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
        [SerializeField] TMP_Text   emptyLabel;   // "Press any button to join"

        [Header("Joined State")]
        [SerializeField] GameObject joinedPanel;
        [SerializeField] TMP_Text   playerLabel;  // "P1", "P2", …
        [SerializeField] TMP_Text   characterName;
        [SerializeField] TMP_Text   customizationName;

        public int SlotIndex { get; set; }

        public void ShowEmpty()
        {
            emptyPanel.SetActive(true);
            joinedPanel.SetActive(false);
        }

        public void ShowJoined(string character, int customizationIndex)
        {
            emptyPanel.SetActive(false);
            joinedPanel.SetActive(true);
            playerLabel.text      = $"P{SlotIndex + 1}";
            characterName.text    = char.ToUpper(character[0]) + character[1..];
            customizationName.text = $"Skin {customizationIndex + 1}";
        }
    }
}
