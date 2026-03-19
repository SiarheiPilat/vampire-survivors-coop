# Menu Scene Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a three-screen menu flow (Splash → Press to Start → Lobby) with local co-op device assignment, per-device character/customization memory, and handoff into the game scene.

**Architecture:** Four Unity scenes chained via `SceneManager.LoadScene`. A `GameSession` DontDestroyOnLoad singleton carries device-to-slot assignments from Lobby into Game. `PlayerInputSystem` is updated to look up devices by ID (via `AssignedDeviceId` component) rather than `Gamepad.all` index, so the lobby's device assignment is honoured at runtime.

**Tech Stack:** Unity 6 · DOTS/ECS (Entities 1.3.14) · Unity Input System 1.18.0 · uGUI (Canvas) · PlayerPrefs · C# 9

---

## File Map

| Action | Path | Purpose |
|--------|------|---------|
| Modify | `Assets/Scripts/Components/PlayerComponents.cs` | Add `AssignedDeviceId` struct; fix stale `PlayerIndex` comment |
| Modify | `Assets/Scripts/Authoring/PlayerAuthoring.cs` | Baker adds `AssignedDeviceId { Value = 0 }` so all entities always have the component |
| Modify | `Assets/Scripts/Systems/PlayerInputSystem.cs` | Look up device by `AssignedDeviceId.deviceId` instead of `Gamepad.all[i]`; keep keyboard fallback |
| Create | `Assets/Scripts/Menu/DeviceSaveData.cs` | Pure C# — PlayerPrefs key generation + JSON save/load |
| Create | `Assets/Scripts/Menu/GameSession.cs` | DontDestroyOnLoad singleton — carries slot assignments Lobby → Game |
| Create | `Assets/Scripts/Menu/SplashController.cs` | Logo fade-in/out, auto-advance, skip-on-any-button |
| Create | `Assets/Scripts/Menu/PressToStartController.cs` | 0.3s cooldown, first-device claim, advance to Lobby |
| Create | `Assets/Scripts/Menu/PlayerSlotUI.cs` | Visual state for one lobby slot (empty / joined) |
| Create | `Assets/Scripts/Menu/LobbyManager.cs` | Device assignment, character/customization cycling, PLAY |
| Create | `Assets/Scripts/Menu/SettingsPanel.cs` | Volume sliders, PlayerPrefs persistence |
| Create | `Assets/Scripts/MonoBehaviours/GameSceneBootstrap.cs` | Reads GameSession, stamps `AssignedDeviceId` onto player entities |
| Create | `Assets/Tests/EditMode/MenuTests.asmdef` | Assembly def for EditMode tests |
| Create | `Assets/Tests/EditMode/DeviceSaveDataTests.cs` | Tests for key generation and JSON round-trip |
| Create | `Assets/Tests/EditMode/GameSessionTests.cs` | Tests for slot add/remove/count logic |
| Create | `Assets/Scenes/SplashScene.unity` | Scene: Camera + Canvas + SplashController |
| Create | `Assets/Scenes/PressToStartScene.unity` | Scene: Camera + Canvas + PressToStartController |
| Create | `Assets/Scenes/LobbyScene.unity` | Scene: Camera + Canvas + LobbyManager + 4× PlayerSlotUI + SettingsPanel |

---

## Task 1: Add `AssignedDeviceId` component and fix stale comment

**Files:**
- Modify: `Assets/Scripts/Components/PlayerComponents.cs`

- [ ] **Open** `Assets/Scripts/Components/PlayerComponents.cs`

- [ ] **Replace** the stale `PlayerIndex` doc comment and add `AssignedDeviceId` at the bottom of the file:

```csharp
using Unity.Entities;
using Unity.Mathematics;

namespace VampireSurvivors.Components
{
    /// <summary>Zero-size marker — identifies all player entities.</summary>
    public struct PlayerTag : IComponentData { }

    /// <summary>
    /// Slot index (0–3) assigned at bake time or by GameSceneBootstrap.
    /// NOT a Gamepad.all index — use AssignedDeviceId for device lookup.
    /// </summary>
    public struct PlayerIndex : IComponentData
    {
        public byte Value;
    }

    /// <summary>
    /// Set by GameSceneBootstrap from GameSession. Stores InputDevice.deviceId
    /// so PlayerInputSystem can look up the correct device regardless of
    /// Gamepad.all connection order.
    /// Absent on entities created from baked PlayerAuthoring (dev-only path).
    /// </summary>
    public struct AssignedDeviceId : IComponentData
    {
        public int Value; // InputDevice.deviceId
    }

    /// <summary>Current frame's movement input — written by PlayerInputSystem, read by PlayerMovementSystem.</summary>
    public struct MoveInput : IComponentData
    {
        public float2 Value;
    }

    /// <summary>Base movement speed in world units per second.</summary>
    public struct MoveSpeed : IComponentData
    {
        public float Value;
    }

    /// <summary>Player stats — components are live now, systems come later.</summary>
    public struct PlayerStats : IComponentData
    {
        public int Hp;
        public int MaxHp;
        public int Level;
        public float Xp;
        public float XpToNextLevel;
    }
}
```

- [ ] **Save** the file and confirm Unity compiles with no errors (check Console)

- [ ] **Also update `PlayerAuthoring.cs` Baker** — add `AssignedDeviceId` with sentinel value `0` so every baked entity always has the component. Open `Assets/Scripts/Authoring/PlayerAuthoring.cs` and add to the `Baker.Bake` method:

```csharp
AddComponent(entity, new AssignedDeviceId { Value = 0 }); // 0 = unassigned; set by GameSceneBootstrap when coming from lobby
```

Place it after the existing `AddComponent` calls. This ensures `PlayerInputSystem`'s query matches baked entities and the `Value == 0` dev fallback path is reachable.

- [ ] **Confirm** Unity compiles with no errors

- [ ] **Commit**
```bash
git add Assets/Scripts/Components/PlayerComponents.cs Assets/Scripts/Authoring/PlayerAuthoring.cs
git commit -m "feat: add AssignedDeviceId component; bake with Value=0 sentinel on all player entities"
```

---

## Task 2: Update `PlayerInputSystem` to use `AssignedDeviceId`

**Files:**
- Modify: `Assets/Scripts/Systems/PlayerInputSystem.cs`

- [ ] **Replace** the file contents:

```csharp
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Reads input for each player entity and writes it into MoveInput.
    ///
    /// Device lookup priority:
    ///   1. If the entity has AssignedDeviceId (set by GameSceneBootstrap from
    ///      GameSession), look up that specific device via InputSystem.GetDeviceById.
    ///   2. Otherwise fall back to Gamepad.all[PlayerIndex] — used during dev when
    ///      the game scene is loaded directly without going through the lobby.
    ///
    /// Keyboard fallback (always active regardless of device assignment):
    ///   Player 0 — WASD
    ///   Player 1 — Arrow keys
    ///
    /// Not Burst-compiled — reads managed Input System APIs.
    /// Runs before PlayerMovementSystem.
    /// </summary>
    [UpdateBefore(typeof(PlayerMovementSystem))]
    public partial struct PlayerInputSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var keyboard = Keyboard.current;

            foreach (var (index, assignedDevice, moveInput) in
                SystemAPI.Query<RefRO<PlayerIndex>, RefRO<AssignedDeviceId>, RefRW<MoveInput>>()
                         .WithOptions(EntityQueryOptions.Default))
            {
                int i = index.ValueRO.Value;
                float2 dir = float2.zero;

                // Keyboard fallback
                if (keyboard != null)
                {
                    if (i == 0)
                    {
                        if (keyboard.wKey.isPressed) dir.y += 1f;
                        if (keyboard.sKey.isPressed) dir.y -= 1f;
                        if (keyboard.aKey.isPressed) dir.x -= 1f;
                        if (keyboard.dKey.isPressed) dir.x += 1f;
                    }
                    else if (i == 1)
                    {
                        if (keyboard.upArrowKey.isPressed)    dir.y += 1f;
                        if (keyboard.downArrowKey.isPressed)  dir.y -= 1f;
                        if (keyboard.leftArrowKey.isPressed)  dir.x -= 1f;
                        if (keyboard.rightArrowKey.isPressed) dir.x += 1f;
                    }
                }

                // Assigned device (from GameSession via GameSceneBootstrap)
                int deviceId = assignedDevice.ValueRO.Value;
                if (deviceId != 0)
                {
                    var device = InputSystem.GetDeviceById(deviceId);
                    if (device is Gamepad gamepad)
                        dir += (float2)gamepad.leftStick.ReadValue();
                }
                else if (Gamepad.all.Count > i)
                {
                    // Dev fallback: no session, use Gamepad.all[i]
                    dir += (float2)Gamepad.all[i].leftStick.ReadValue();
                }

                moveInput.ValueRW.Value = math.lengthsq(dir) > 1f ? math.normalize(dir) : dir;
            }
        }
    }
}
```

> **Note:** `AssignedDeviceId.Value == 0` is the sentinel for "not assigned" — `InputDevice.deviceId` starts at 1 in the Input System.

- [ ] **Save** and confirm no compile errors in Unity Console

- [ ] **Test manually:** enter Play mode with the baked players — WASD/arrows should still work (dev path via `Gamepad.all[i]`)

- [ ] **Commit**
```bash
git add Assets/Scripts/Systems/PlayerInputSystem.cs
git commit -m "feat: update PlayerInputSystem to use AssignedDeviceId for device lookup"
```

---

## Task 3: `DeviceSaveData` — pure C# save/load helper

**Files:**
- Create: `Assets/Scripts/Menu/DeviceSaveData.cs`
- Create: `Assets/Tests/EditMode/MenuTests.asmdef`
- Create: `Assets/Tests/EditMode/DeviceSaveDataTests.cs`

- [ ] **Create the assembly definition** `Assets/Tests/EditMode/MenuTests.asmdef`:
```json
{
    "name": "MenuTests",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false,
    "optionalUnityReferences": ["TestAssemblies"]
}
```

- [ ] **Write the failing tests** in `Assets/Tests/EditMode/DeviceSaveDataTests.cs`:

```csharp
using NUnit.Framework;
using VampireSurvivors.Menu;

namespace Tests.EditMode
{
    public class DeviceSaveDataTests
    {
        [Test]
        public void Key_UsesSerial_WhenNonEmpty()
        {
            var key = DeviceSaveData.BuildKey("Xbox Controller", "Microsoft", "SN-1234");
            Assert.AreEqual("SN-1234", key);
        }

        [Test]
        public void Key_UsesCombined_WhenSerialEmpty()
        {
            var key = DeviceSaveData.BuildKey("DualSense", "Sony", "");
            Assert.AreEqual("DualSense|Sony", key);
        }

        [Test]
        public void Key_UsesSlotFallback_WhenAllEmpty()
        {
            var key = DeviceSaveData.BuildKey("", "", "", slotFallback: 2);
            Assert.AreEqual("slot_2", key);
        }

        [Test]
        public void RoundTrip_SaveAndLoad()
        {
            const string key = "test_device_key";
            DeviceSaveData.Save(key, "antonio", 3);
            DeviceSaveData.Load(key, out var charId, out var customIdx);
            Assert.AreEqual("antonio", charId);
            Assert.AreEqual(3, customIdx);
            UnityEngine.PlayerPrefs.DeleteKey(key); // cleanup
        }

        [Test]
        public void Load_ReturnsDefaults_WhenKeyMissing()
        {
            DeviceSaveData.Load("__nonexistent_key__", out var charId, out var customIdx);
            Assert.AreEqual("antonio", charId);
            Assert.AreEqual(0, customIdx);
        }
    }
}
```

- [ ] **Run tests** — expect FAIL (class not yet defined):
  In Unity: Window > General > Test Runner > EditMode > Run All

- [ ] **Create** `Assets/Scripts/Menu/DeviceSaveData.cs`:

```csharp
using UnityEngine;

namespace VampireSurvivors.Menu
{
    /// <summary>
    /// Builds PlayerPrefs keys from InputDevice.description fields and
    /// saves/loads character+customization selections per device.
    /// </summary>
    public static class DeviceSaveData
    {
        private static readonly string[] ValidCharacterIds =
            { "antonio", "imelda", "pasqualina", "gennaro" };

        /// <summary>
        /// Build a stable PlayerPrefs key for a device.
        /// Priority: serial > product|manufacturer > slot_N
        /// </summary>
        public static string BuildKey(string product, string manufacturer,
                                      string serial, int slotFallback = 0)
        {
            if (!string.IsNullOrEmpty(serial))
                return serial;
            var combined = $"{product}|{manufacturer}";
            if (combined.Length > 1) // at least one non-empty
                return combined;
            return $"slot_{slotFallback}";
        }

        public static void Save(string key, string characterId, int customizationIndex)
        {
            var json = JsonUtility.ToJson(new SavePayload
            {
                characterId        = characterId,
                customizationIndex = customizationIndex,
            });
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }

        public static void Load(string key, out string characterId, out int customizationIndex)
        {
            characterId        = ValidCharacterIds[0];
            customizationIndex = 0;

            var json = PlayerPrefs.GetString(key, null);
            if (string.IsNullOrEmpty(json)) return;

            var payload = JsonUtility.FromJson<SavePayload>(json);
            if (payload == null) return;

            characterId        = payload.characterId;
            customizationIndex = payload.customizationIndex;
        }

        [System.Serializable]
        private class SavePayload
        {
            public string characterId;
            public int    customizationIndex;
        }
    }
}
```

- [ ] **Run tests again** — expect all PASS

- [ ] **Commit**
```bash
git add Assets/Scripts/Menu/ Assets/Tests/
git commit -m "feat: add DeviceSaveData with key generation and PlayerPrefs persistence"
```

---

## Task 4: `GameSession` singleton

**Files:**
- Create: `Assets/Scripts/Menu/GameSession.cs`
- Create: `Assets/Tests/EditMode/GameSessionTests.cs`

- [ ] **Write failing tests** in `Assets/Tests/EditMode/GameSessionTests.cs`:

```csharp
using NUnit.Framework;
using VampireSurvivors.Menu;

namespace Tests.EditMode
{
    public class GameSessionTests
    {
        [Test]
        public void SlotCount_StartsAtZero()
        {
            var slots = new GameSession.SlotData[4];
            int count = 0;
            foreach (var s in slots)
                if (s.Filled) count++;
            Assert.AreEqual(0, count);
        }

        [Test]
        public void SlotData_StoresCharacterAndCustomization()
        {
            var slot = new GameSession.SlotData
            {
                Filled             = true,
                CharacterId        = "imelda",
                CustomizationIndex = 2,
                DeviceId           = 42,
            };
            Assert.IsTrue(slot.Filled);
            Assert.AreEqual("imelda", slot.CharacterId);
            Assert.AreEqual(2, slot.CustomizationIndex);
            Assert.AreEqual(42, slot.DeviceId);
        }
    }
}
```

- [ ] **Run tests** — expect PASS (pure struct, no MonoBehaviour dependency)

- [ ] **Create** `Assets/Scripts/Menu/GameSession.cs`:

```csharp
using UnityEngine;

namespace VampireSurvivors.Menu
{
    /// <summary>
    /// DontDestroyOnLoad singleton that carries player slot assignments
    /// from LobbyScene into GameScene.
    ///
    /// Lifecycle: created by LobbyManager before loading GameScene.
    /// Destroyed by LobbyManager.Awake on re-entry to LobbyScene.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        [System.Serializable]
        public struct SlotData
        {
            public bool   Filled;
            public int    DeviceId;           // InputDevice.deviceId (0 = unassigned)
            public string CharacterId;
            public int    CustomizationIndex;
        }

        public SlotData[] Slots { get; } = new SlotData[4];
        public int FilledCount
        {
            get
            {
                int n = 0;
                foreach (var s in Slots) if (s.Filled) n++;
                return n;
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void DestroySelf() => Destroy(gameObject);
    }
}
```

- [ ] **Verify** Unity compiles with no errors

- [ ] **Commit**
```bash
git add Assets/Scripts/Menu/GameSession.cs Assets/Tests/EditMode/GameSessionTests.cs
git commit -m "feat: add GameSession DontDestroyOnLoad singleton"
```

---

## Task 5: `SplashScene` — logo screen

**Files:**
- Create: `Assets/Scripts/Menu/SplashController.cs`
- Create: `Assets/Scenes/SplashScene.unity` (created in Editor)

- [ ] **Create** `Assets/Scripts/Menu/SplashController.cs`:

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VampireSurvivors.Menu
{
    /// <summary>
    /// Fades the logo in, holds, fades out, then loads PressToStartScene.
    /// Any button press after fade-in skips the hold.
    /// </summary>
    public class SplashController : MonoBehaviour
    {
        [SerializeField] CanvasGroup logoGroup;
        [SerializeField] float       fadeInDuration  = 0.8f;
        [SerializeField] float       holdDuration    = 1.5f;
        [SerializeField] float       fadeOutDuration = 0.6f;
        [SerializeField] string      nextScene       = "PressToStartScene";

        bool _skipRequested;

        void OnEnable()  => InputSystem.onAnyButtonPress.CallOnce(_ => _skipRequested = true);
        void OnDisable() { }

        IEnumerator Start()
        {
            logoGroup.alpha = 0f;
            yield return Fade(0f, 1f, fadeInDuration);

            _skipRequested = false; // reset — only skip during hold
            float elapsed = 0f;
            while (elapsed < holdDuration && !_skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return Fade(1f, 0f, fadeOutDuration);
            SceneManager.LoadScene(nextScene);
        }

        IEnumerator Fade(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed         += Time.unscaledDeltaTime;
                logoGroup.alpha  = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            logoGroup.alpha = to;
        }
    }
}
```

- [ ] **Create SplashScene in Unity Editor:**
  1. File > New Scene (Basic URP 2D)
  2. Save as `Assets/Scenes/SplashScene.unity`
  3. Delete the default `Main Camera` if present; add a new Camera: GameObject > Camera, set Projection = Orthographic
  4. Create a Canvas: GameObject > UI > Canvas. Set Canvas Scaler to Scale With Screen Size, Reference Resolution 1920×1080
  5. Inside Canvas, create a Panel (name it `LogoPanel`), add a `CanvasGroup` component
  6. Inside LogoPanel, add a UI > Text (TMP) element with the game title as placeholder (replace with Image asset later)
  7. Create empty GameObject `SplashController`, add `SplashController` script, drag `LogoPanel`'s CanvasGroup into the `Logo Group` field
  8. Set `Next Scene` to `PressToStartScene`

- [ ] **Enter Play mode in SplashScene** — logo should fade in, hold ~1.5s, fade out. Press any key during hold to skip. Console should show no errors.

- [ ] **Commit**
```bash
git add Assets/Scripts/Menu/SplashController.cs Assets/Scenes/SplashScene.unity
git commit -m "feat: add SplashScene with fade-in/hold/fade-out logo"
```

---

## Task 6: `PressToStartScene` — device claim

**Files:**
- Create: `Assets/Scripts/Menu/PressToStartController.cs`
- Create: `Assets/Scenes/PressToStartScene.unity` (created in Editor)

- [ ] **Create** `Assets/Scripts/Menu/PressToStartController.cs`:

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
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
```

- [ ] **Create PressToStartScene in Unity Editor:**
  1. File > New Scene (Basic URP 2D), save as `Assets/Scenes/PressToStartScene.unity`
  2. Add Camera (Orthographic), Canvas (Scale With Screen Size 1920×1080)
  3. Inside Canvas, add a Text (TMP): "Press any button to start"
  4. Create empty GameObject `PressToStartController`, attach `PressToStartController` script
  5. Set `Next Scene` to `LobbyScene`

- [ ] **Enter Play mode** — wait 0.3s then press any gamepad button or spacebar. Should load LobbyScene (which doesn't exist yet, so you'll get a "Scene not found" error — that's expected at this stage)

- [ ] **Commit**
```bash
git add Assets/Scripts/Menu/PressToStartController.cs Assets/Scenes/PressToStartScene.unity
git commit -m "feat: add PressToStartScene with 0.3s cooldown and P1 device claim"
```

---

## Task 7: `PlayerSlotUI` — single slot visual

**Files:**
- Create: `Assets/Scripts/Menu/PlayerSlotUI.cs`

- [ ] **Create** `Assets/Scripts/Menu/PlayerSlotUI.cs`:

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VampireSurvivors.Menu;

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
```

- [ ] **Confirm** Unity compiles with no errors (no scene setup yet)

- [ ] **Commit**
```bash
git add Assets/Scripts/Menu/PlayerSlotUI.cs
git commit -m "feat: add PlayerSlotUI empty/joined visual state"
```

---

## Task 8: `LobbyManager` — device assignment and input polling

**Files:**
- Create: `Assets/Scripts/Menu/LobbyManager.cs`

- [ ] **Create** `Assets/Scripts/Menu/LobbyManager.cs`:

```csharp
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
```

- [ ] **Confirm** Unity compiles with no errors

- [ ] **Commit**
```bash
git add Assets/Scripts/Menu/LobbyManager.cs
git commit -m "feat: add LobbyManager with device join/leave, character cycling, PLAY"
```

---

## Task 9: `SettingsPanel` — volume sliders

**Files:**
- Create: `Assets/Scripts/Menu/SettingsPanel.cs`

- [ ] **Create** `Assets/Scripts/Menu/SettingsPanel.cs`:

```csharp
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace VampireSurvivors.Menu
{
    /// <summary>
    /// Settings panel: master/music/sfx volume sliders.
    /// Persisted in PlayerPrefs. Back button calls LobbyManager.OnSettingsClose.
    /// Focus order (tvOS): MasterVolume → MusicVolume → SFXVolume → Back.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] Slider masterSlider;
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;

        // Optional: wire up an AudioMixer for real volume control
        // [SerializeField] AudioMixer audioMixer;

        const string KeyMaster = "vol_master";
        const string KeyMusic  = "vol_music";
        const string KeySFX    = "vol_sfx";

        void OnEnable()
        {
            masterSlider.value = PlayerPrefs.GetFloat(KeyMaster, 100f);
            musicSlider.value  = PlayerPrefs.GetFloat(KeyMusic,  100f);
            sfxSlider.value    = PlayerPrefs.GetFloat(KeySFX,    100f);

            masterSlider.onValueChanged.AddListener(OnMasterChanged);
            musicSlider.onValueChanged.AddListener(OnMusicChanged);
            sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        }

        void OnDisable()
        {
            masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
            musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            sfxSlider.onValueChanged.RemoveListener(OnSFXChanged);
        }

        void OnMasterChanged(float v) { PlayerPrefs.SetFloat(KeyMaster, v); PlayerPrefs.Save(); }
        void OnMusicChanged(float v)  { PlayerPrefs.SetFloat(KeyMusic,  v); PlayerPrefs.Save(); }
        void OnSFXChanged(float v)    { PlayerPrefs.SetFloat(KeySFX,    v); PlayerPrefs.Save(); }
    }
}
```

- [ ] **Commit**
```bash
git add Assets/Scripts/Menu/SettingsPanel.cs
git commit -m "feat: add SettingsPanel with volume sliders and PlayerPrefs persistence"
```

---

## Task 10: Build LobbyScene in Unity Editor

**Files:**
- Create: `Assets/Scenes/LobbyScene.unity` (created in Editor)

- [ ] **Create the scene:** File > New Scene (Basic URP 2D), save as `Assets/Scenes/LobbyScene.unity`

- [ ] **Add Camera:** GameObject > Camera, Projection = Orthographic, Position (0, 0, -10)

- [ ] **Add Canvas:** GameObject > UI > Canvas, Canvas Scaler = Scale With Screen Size, Reference 1920×1080

- [ ] **Add title text:** Inside Canvas, add UI > Text (TMP), text = game title, top-center anchored

- [ ] **Add 4 slot panels:** Inside Canvas, create 4 child panels side by side (name them `Slot0`–`Slot3`). Each slot needs:
  - Child: `EmptyPanel` with a Text "Press button to join"
  - Child: `JoinedPanel` with Text elements for player label, character name, customization name
  - Attach `PlayerSlotUI` script; wire all fields in the Inspector

- [ ] **Add PLAY button:** Bottom-center, attach `LobbyManager.OnPlayButtonClicked` to its onClick

- [ ] **Add Settings button:** Top-right corner, attach `LobbyManager.OnSettingsButtonClicked`

- [ ] **Add SettingsPanel:** Child of Canvas, initially inactive. Contains three Sliders + a Back button wired to `LobbyManager.OnSettingsClose`. Attach `SettingsPanel` script, wire sliders.

- [ ] **Add LobbyManager:** Empty GameObject `LobbyManager`, attach `LobbyManager` script. Wire all 4 `PlayerSlotUI` components, PLAY button, and Settings panel in the Inspector.

- [ ] **Enter Play mode in LobbyScene:** Press a gamepad button — a slot should fill. Press Up/Down to cycle characters. PLAY button should become interactable.

- [ ] **Commit**
```bash
git add Assets/Scenes/LobbyScene.unity
git commit -m "feat: build LobbyScene with slots, PLAY button, and Settings panel"
```

---

## Task 11: `GameSceneBootstrap` — handoff from session to ECS

**Files:**
- Create: `Assets/Scripts/MonoBehaviours/GameSceneBootstrap.cs`

- [ ] **Create** `Assets/Scripts/MonoBehaviours/GameSceneBootstrap.cs`:

```csharp
using Unity.Entities;
using UnityEngine;
using VampireSurvivors.Components;
using VampireSurvivors.Menu;

namespace VampireSurvivors.MonoBehaviours
{
    /// <summary>
    /// Runs at GameScene startup. If a GameSession exists (i.e. the player came
    /// through the lobby), stamps AssignedDeviceId onto each player entity so
    /// PlayerInputSystem uses the correct device.
    ///
    /// If no GameSession exists (direct scene load during dev), does nothing —
    /// baked PlayerAuthoring entities with the keyboard/Gamepad.all fallback
    /// remain intact.
    /// </summary>
    public class GameSceneBootstrap : MonoBehaviour
    {
        void Start()
        {
            var session = GameSession.Instance;
            if (session == null)
            {
                Debug.Log("[GameSceneBootstrap] No GameSession — using baked entities (dev mode).");
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;

            // Find player entities by PlayerIndex
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerIndex>());
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

            foreach (var entity in entities)
            {
                var playerIndex = em.GetComponentData<PlayerIndex>(entity);
                int slot        = playerIndex.Value;

                if (slot >= session.Slots.Length || !session.Slots[slot].Filled)
                    continue;

                var deviceId = session.Slots[slot].DeviceId;

                if (em.HasComponent<AssignedDeviceId>(entity))
                    em.SetComponentData(entity, new AssignedDeviceId { Value = deviceId });
                else
                    em.AddComponentData(entity, new AssignedDeviceId { Value = deviceId });
            }

            Debug.Log($"[GameSceneBootstrap] Stamped {session.FilledCount} device assignment(s).");
        }
    }
}
```

- [ ] **Add to SampleScene:** In Unity, open `SampleScene`, create an empty GameObject named `GameSceneBootstrap`, attach the script

- [ ] **Test full flow:**
  1. Start from SplashScene
  2. Press button on PressToStart
  3. Join in lobby with a controller
  4. Press PLAY — game scene loads
  5. Console should show `[GameSceneBootstrap] Stamped 1 device assignment(s).`
  6. Controller should drive the correct player entity

- [ ] **Commit**
```bash
git add Assets/Scripts/MonoBehaviours/GameSceneBootstrap.cs Assets/Scenes/SampleScene.unity
git commit -m "feat: add GameSceneBootstrap to stamp AssignedDeviceId on player entities"
```

---

## Task 12: Wire build settings and fix `PressToStartController`

The `PressToStartController` creates a `GameSession` for P1, then navigates to LobbyScene where `LobbyManager.Awake` destroys it. That's intentional — Lobby rebuilds from scratch so P1 must re-press to join (keeps the join flow consistent for all players). But the scene name in `PressToStartController.nextScene` must match exactly.

- [ ] **Open** File > Build Settings

- [ ] **Add scenes in order:**
  1. `Assets/Scenes/SplashScene.unity` (index 0)
  2. `Assets/Scenes/PressToStartScene.unity` (index 1)
  3. `Assets/Scenes/LobbyScene.unity` (index 2)
  4. `Assets/Scenes/SampleScene.unity` (index 3)

- [ ] **Verify scene name strings** in each controller match exactly:
  - `SplashController.nextScene` = `"PressToStartScene"`
  - `PressToStartController.nextScene` = `"LobbyScene"`
  - `LobbyManager` hardcodes `"SampleScene"` — confirm it matches

- [ ] **Run the full flow end-to-end** from SplashScene: logo → press to start → lobby → play → game. No scene-not-found errors.

- [ ] **Commit**
```bash
git commit -am "chore: add all menu scenes to Build Settings"
```

---

## Task 13: Update `TODO.md`

- [ ] **Open** `TODO.md` at the project root and mark completed items:

```markdown
- [x] SubScene setup — Player_0–3 baked into ECS entities via Players.unity SubScene
- [x] Input routing — WASD (P0), Arrow keys (P1), Gamepad[n] for all players
- [x] Main menu / character select  ← mark complete
```

- [ ] **Add new known follow-ups:**

```markdown
- [ ] Wire Apple TV Remote asset into LobbyManager and PressToStartController
- [ ] Replace placeholder "Skin N" customization with real skin data
- [ ] Replace static Characters array in LobbyManager with CharacterRegistry ScriptableObject
- [ ] Add back-navigation from GameScene to LobbyScene (pause menu or death screen)
```

- [ ] **Commit**
```bash
git add TODO.md
git commit -m "chore: update TODO after menu scene implementation"
```
