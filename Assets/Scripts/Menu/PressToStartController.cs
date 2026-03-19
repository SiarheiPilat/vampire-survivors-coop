using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;

namespace VampireSurvivors.Menu
{
    /// <summary>
    /// Waits for the first device press after a 0.3s cooldown (prevents
    /// spurious tvOS Remote events from triggering on scene load).
    /// Assigns that device as P1 slot in a new GameSession, then loads LobbyScene.
    /// </summary>
    public class PressToStartController : MonoBehaviour
    {
        [SerializeField] string nextScene = "LobbyScene";

        const float CooldownSeconds = 0.3f;
        bool        _ready;

        IEnumerator Start()
        {
            // Kill any stale session on arrival from game
            GameSession.Instance?.DestroySelf();

            yield return new WaitForSecondsRealtime(CooldownSeconds);
            _ready = true;
            InputSystem.onAnyButtonPress.CallOnce(OnAnyButton);
        }

        void OnAnyButton(InputControl control)
        {
            if (!_ready) return;

            // Create GameSession and claim P1 slot
            var sessionGo = new GameObject("GameSession");
            var session   = sessionGo.AddComponent<GameSession>();

            var device = control.device;
            var desc   = device.description;
            var key    = DeviceSaveData.BuildKey(desc.product, desc.manufacturer, desc.serial, 0);
            DeviceSaveData.Load(key, out var charId, out var customIdx);

            session.Slots[0] = new GameSession.SlotData
            {
                Filled             = true,
                DeviceId           = device.deviceId,
                CharacterId        = charId,
                CustomizationIndex = customIdx,
            };

            SceneManager.LoadScene(nextScene);
        }
    }
}
