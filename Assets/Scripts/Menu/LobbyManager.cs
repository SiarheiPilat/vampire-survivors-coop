using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VampireSurvivors.Menu
{
    /// <summary>
    /// Manages the lobby: device join/leave, character/customization cycling,
    /// PLAY button enable state, and Settings navigation.
    ///
    /// Character cycling: Up/Down on stick/dpad, dead zone 0.5, repeat 0.25s.
    /// Customization cycling: Left/Right, same thresholds.
    /// Leave slot: UI.Cancel (B / Circle / Escape).
    /// </summary>
    public class LobbyManager : MonoBehaviour
    {
        [SerializeField] PlayerSlotUI[] slots;          // 4 slots, assigned in Inspector
        [SerializeField] Button         playButton;
        [SerializeField] GameObject     settingsPanel;

        // Characters available — replace with CharacterRegistry ScriptableObject later
        static readonly string[] Characters = { "antonio", "imelda", "pasqualina", "gennaro" };

        // Per-slot state
        readonly InputDevice[] _slotDevice = new InputDevice[4];
        readonly string[]      _slotChar   = new string[4];
        readonly int[]         _slotCustom = new int[4];

        // Per-slot input repeat timers
        readonly float[] _repeatTimerV = new float[4]; // vertical (char cycling)
        readonly float[] _repeatTimerH = new float[4]; // horizontal (customization)

        const float DeadZone      = 0.5f;
        const float RepeatInterval = 0.25f;

        void Awake()
        {
            // Destroy any stale GameSession on lobby re-entry (e.g. returning from game).
            // P1 is NOT pre-filled — they must press a button to join like every other player.
            GameSession.Instance?.DestroySelf();
        }

        void OnEnable()
        {
            InputSystem.onAnyButtonPress    += OnAnyButton;
            InputSystem.onDeviceChange      += OnDeviceChange;
        }

        void OnDisable()
        {
            InputSystem.onAnyButtonPress    -= OnAnyButton;
            InputSystem.onDeviceChange      -= OnDeviceChange;
        }

        void Start()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].SlotIndex = i;
                slots[i].ShowEmpty();
            }
            RefreshPlayButton();
            settingsPanel.SetActive(false);
        }

        // ── Join ────────────────────────────────────────────────────────────────

        void OnAnyButton(InputControl control)
        {
            var device = control.device;

            // Already in a slot?
            if (SlotOf(device) >= 0) return;

            // Find first empty slot
            int slot = -1;
            for (int i = 0; i < 4; i++)
                if (_slotDevice[i] == null) { slot = i; break; }
            if (slot < 0) return; // lobby full

            JoinSlot(slot, device);
        }

        void JoinSlot(int slot, InputDevice device)
        {
            _slotDevice[slot] = device;

            // Load saved selection for this device
            var desc = device.description;
            var key  = DeviceSaveData.BuildKey(desc.product, desc.manufacturer,
                                               desc.serial, slot);
            DeviceSaveData.Load(key, out var charId, out var customIdx);

            _slotChar[slot]   = charId;
            _slotCustom[slot] = customIdx;

            slots[slot].ShowJoined(charId, customIdx);
            RefreshPlayButton();
        }

        // ── Leave ───────────────────────────────────────────────────────────────

        void LeaveSlot(int slot)
        {
            _slotDevice[slot] = null;
            slots[slot].ShowEmpty();
            RefreshPlayButton();
        }

        // ── Disconnect ──────────────────────────────────────────────────────────

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change != InputDeviceChange.Disconnected) return;
            int slot = SlotOf(device);
            if (slot >= 0) LeaveSlot(slot);
        }

        // ── Input polling ───────────────────────────────────────────────────────

        void Update()
        {
            for (int i = 0; i < 4; i++)
            {
                var device = _slotDevice[i];
                if (device == null) continue;

                if (device is not Gamepad gp) continue;

                // Leave slot — UI.Cancel (B / Circle)
                if (gp.buttonEast.wasPressedThisFrame)
                {
                    LeaveSlot(i);
                    continue;
                }

                float v = gp.leftStick.ReadValue().y;
                if (Mathf.Abs(v) < DeadZone) v = gp.dpad.ReadValue().y;

                float h = gp.leftStick.ReadValue().x;
                if (Mathf.Abs(h) < DeadZone) h = gp.dpad.ReadValue().x;

                // Vertical — cycle character
                if (Mathf.Abs(v) >= DeadZone)
                {
                    if (_repeatTimerV[i] <= 0f)
                    {
                        CycleCharacter(i, v > 0 ? -1 : 1);
                        _repeatTimerV[i] = RepeatInterval;
                    }
                    else _repeatTimerV[i] -= Time.unscaledDeltaTime;
                }
                else _repeatTimerV[i] = 0f;

                // Horizontal — cycle customization
                if (Mathf.Abs(h) >= DeadZone)
                {
                    if (_repeatTimerH[i] <= 0f)
                    {
                        CycleCustomization(i, h > 0 ? 1 : -1);
                        _repeatTimerH[i] = RepeatInterval;
                    }
                    else _repeatTimerH[i] -= Time.unscaledDeltaTime;
                }
                else _repeatTimerH[i] = 0f;

                // PLAY — any button on the south face button
                if (gp.buttonSouth.wasPressedThisFrame && AnySlotFilled())
                    StartGame();
            }
        }

        void CycleCharacter(int slot, int dir)
        {
            int idx = System.Array.IndexOf(Characters, _slotChar[slot]);
            idx = (idx + dir + Characters.Length) % Characters.Length;
            _slotChar[slot] = Characters[idx];
            SaveSlot(slot);
            slots[slot].ShowJoined(_slotChar[slot], _slotCustom[slot]);
        }

        void CycleCustomization(int slot, int dir)
        {
            const int MaxCustomizations = 4; // placeholder count
            _slotCustom[slot] = (_slotCustom[slot] + dir + MaxCustomizations) % MaxCustomizations;
            SaveSlot(slot);
            slots[slot].ShowJoined(_slotChar[slot], _slotCustom[slot]);
        }

        void SaveSlot(int slot)
        {
            var device = _slotDevice[slot];
            if (device == null) return;
            var desc = device.description;
            var key  = DeviceSaveData.BuildKey(desc.product, desc.manufacturer,
                                               desc.serial, slot);
            DeviceSaveData.Save(key, _slotChar[slot], _slotCustom[slot]);
        }

        // ── PLAY ────────────────────────────────────────────────────────────────

        public void OnPlayButtonClicked() => StartGame();

        void StartGame()
        {
            if (!AnySlotFilled()) return;

            // Build GameSession
            var go      = new GameObject("GameSession");
            var session = go.AddComponent<GameSession>();

            for (int i = 0; i < 4; i++)
            {
                if (_slotDevice[i] == null) continue;
                session.Slots[i] = new GameSession.SlotData
                {
                    Filled             = true,
                    DeviceId           = _slotDevice[i].deviceId,
                    CharacterId        = _slotChar[i],
                    CustomizationIndex = _slotCustom[i],
                };
            }

            SceneManager.LoadScene("SampleScene");
        }

        // ── Settings ────────────────────────────────────────────────────────────

        public void OnSettingsButtonClicked() => settingsPanel.SetActive(true);
        public void OnSettingsClose()         => settingsPanel.SetActive(false);

        // ── Helpers ─────────────────────────────────────────────────────────────

        int SlotOf(InputDevice device)
        {
            for (int i = 0; i < 4; i++)
                if (_slotDevice[i] == device) return i;
            return -1;
        }

        bool AnySlotFilled()
        {
            foreach (var d in _slotDevice)
                if (d != null) return true;
            return false;
        }

        void RefreshPlayButton() => playButton.interactable = AnySlotFilled();
    }
}
