using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
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
        [SerializeField] PlayerSlotUI[]    slots;              // 4 slots, assigned in Inspector
        [SerializeField] Button            playButton;
        [SerializeField] GameObject        settingsPanel;
        [SerializeField] CharacterRegistry characterRegistry;  // assign CharacterRegistry.asset in Inspector
        [SerializeField] StageRegistry     stageRegistry;      // assign StageRegistry.asset in Inspector
        [SerializeField] TMPro.TMP_Text    stageNameText;      // optional label showing current stage

        // Fallback list used when no CharacterRegistry asset is assigned
        static readonly string[] s_FallbackIds =
        {
            "antonio", "imelda", "pasqualina", "gennaro", "arca", "porta", "lama",
            "mortaccio", "yattacavallo", "krochi", "dommario", "giovanna",
            "pugnala", "poppea", "clerici", "bianzi",
        };

        int   CharacterCount => characterRegistry != null ? characterRegistry.Count : s_FallbackIds.Length;
        string IdAt(int i)   => characterRegistry != null ? characterRegistry.IdAt(i) : s_FallbackIds[i];

        // Stage selection (shared for all players)
        int _stageIndex = 0;
        int StageCount => stageRegistry != null ? stageRegistry.Count : 3;
        string CurrentStageId => stageRegistry != null && stageRegistry.At(_stageIndex) != null
            ? stageRegistry.At(_stageIndex).Id
            : (_stageIndex == 1 ? "inlaid_library" : _stageIndex == 2 ? "dairy_plant" : "mad_forest");
        string CurrentStageName => stageRegistry != null && stageRegistry.At(_stageIndex) != null
            ? stageRegistry.At(_stageIndex).DisplayName
            : (new[] { "Mad Forest", "Inlaid Library", "Dairy Plant" })[System.Math.Min(_stageIndex, 2)];

        // Per-slot state
        readonly InputDevice[] _slotDevice = new InputDevice[4];
        readonly string[]      _slotChar   = new string[4];
        readonly int[]         _slotCustom = new int[4];

        // Per-slot input repeat timers
        readonly float[] _repeatTimerV = new float[4]; // vertical (char cycling)
        readonly float[] _repeatTimerH = new float[4]; // horizontal (customization)

        IDisposable _anyButtonSub;

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
            _anyButtonSub = InputSystem.onAnyButtonPress.Call(OnAnyButton);
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        void OnDisable()
        {
            _anyButtonSub?.Dispose();
            _anyButtonSub = null;
            InputSystem.onDeviceChange -= OnDeviceChange;
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
            RefreshStageDisplay();
        }

        void CycleStage(int dir)
        {
            _stageIndex = (_stageIndex + dir + StageCount) % StageCount;
            RefreshStageDisplay();
        }

        void RefreshStageDisplay()
        {
            if (stageNameText != null)
                stageNameText.text = $"Stage: {CurrentStageName}";
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

            // If saved character is now locked (progress reset?), fall back to Antonio
            _slotChar[slot]   = PersistentProgress.IsUnlocked(charId) ? charId : "antonio";
            _slotCustom[slot] = customIdx;

            RefreshSlotDisplay(slot);
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
            // Keyboard back-navigation: Escape with no joined players → PressToStart
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame &&
                !AnySlotFilled())
            {
                SceneManager.LoadScene("2_PressToStartScene");
                return;
            }

            // Stage cycling — keyboard Q/E (available to any human at the keyboard)
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.qKey.wasPressedThisFrame) CycleStage(-1);
                if (kb.eKey.wasPressedThisFrame) CycleStage(+1);
            }

            for (int i = 0; i < 4; i++)
            {
                var device = _slotDevice[i];
                if (device == null) continue;

                if (device is not Gamepad gp) continue;

                // Leave slot — B/Circle. If already empty (no players at all) go back to PressToStart.
                if (gp.buttonEast.wasPressedThisFrame)
                {
                    if (!AnySlotFilled())
                    {
                        SceneManager.LoadScene("2_PressToStartScene");
                        return;
                    }
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

                // Stage cycling — LB/RB on first filled slot only
                if (i == FirstFilledSlot())
                {
                    if (gp.leftShoulder.wasPressedThisFrame)  CycleStage(-1);
                    if (gp.rightShoulder.wasPressedThisFrame) CycleStage(+1);
                }

                // PLAY — any button on the south face button
                if (gp.buttonSouth.wasPressedThisFrame && AnySlotFilled())
                    StartGame();
            }
        }

        void CycleCharacter(int slot, int dir)
        {
            int idx = IndexOfId(_slotChar[slot]);
            // Cycle but skip locked characters (wrap up to CharacterCount iterations max)
            for (int tries = 0; tries < CharacterCount; tries++)
            {
                idx = (idx + dir + CharacterCount) % CharacterCount;
                if (PersistentProgress.IsUnlocked(IdAt(idx))) break;
            }
            _slotChar[slot] = IdAt(idx);
            SaveSlot(slot);
            RefreshSlotDisplay(slot);
        }

        void CycleCustomization(int slot, int dir)
        {
            const int MaxCustomizations = 4; // placeholder count
            _slotCustom[slot] = (_slotCustom[slot] + dir + MaxCustomizations) % MaxCustomizations;
            SaveSlot(slot);
            RefreshSlotDisplay(slot);
        }

        void RefreshSlotDisplay(int slot)
        {
            string id   = _slotChar[slot];
            string name = characterRegistry != null
                ? characterRegistry.GetDisplayName(id)
                : (string.IsNullOrEmpty(id) ? "Unknown" : char.ToUpper(id[0]) + (id.Length > 1 ? id[1..] : ""));
            string desc = characterRegistry != null ? characterRegistry.GetDescription(id) : "";
            slots[slot].ShowJoined(name, desc, _slotCustom[slot]);
        }

        int IndexOfId(string id)
        {
            for (int i = 0; i < CharacterCount; i++)
                if (string.Equals(IdAt(i), id, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            return 0;
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

            session.StageId = CurrentStageId;

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

            SceneManager.LoadScene("4_SampleScene");
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

        int FirstFilledSlot()
        {
            for (int i = 0; i < 4; i++)
                if (_slotDevice[i] != null) return i;
            return -1;
        }

        void RefreshPlayButton() => playButton.interactable = AnySlotFilled();
    }
}
