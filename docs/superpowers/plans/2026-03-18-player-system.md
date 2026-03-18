# Player System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement 4-player local co-op movement — each gamepad drives one colored quad entity, a shared camera follows the centroid of all players with dynamic zoom.

**Architecture:** Full DOTS authoring pipeline. Components baked from `PlayerAuthoring` scene GameObjects. `PlayerInputSystem` (unmanaged, reads `Gamepad.all`) writes `MoveInput` each frame; `PlayerMovementSystem` (Burst, IJobEntity) consumes it to move `LocalTransform`. Camera is a MonoBehaviour that queries ECS each `LateUpdate`.

**Tech Stack:** Unity 6000.3.8f1, DOTS/ECS 1.3.14, entities.graphics 1.4.18, Burst 1.8.21, Unity Input System 1.18.0, URP 2D

---

> **Note on testing:** Unity game systems don't have traditional unit tests — the verification loop is Play Mode. Each task ends with a compile-time check (ensure no errors in Console); the final task is a Play Mode verification checklist. Gamepad simulation via `InputTestFixture` is out of scope for this milestone.

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `Assets/Scripts/Components/PlayerComponents.cs` | **Create** | All 5 ECS component structs |
| `Assets/Scripts/Authoring/PlayerAuthoring.cs` | **Create** | MonoBehaviour + Baker, stamps components + mesh onto entity |
| `Assets/Scripts/Systems/PlayerInputSystem.cs` | **Create** | Reads gamepads, writes `MoveInput` per player entity |
| `Assets/Scripts/Systems/PlayerMovementSystem.cs` | **Create** | Burst job: `MoveInput` × `MoveSpeed` × dt → `LocalTransform` |
| `Assets/Scripts/MonoBehaviours/CameraFollow.cs` | **Create** | Centroid follow + orthographic zoom |
| `Assets/Scripts/ECSBootstrap.cs` | **Modify** | Disable (comment out `Start` body) |
| `Assets/Materials/` | **Create (Editor)** | 4 colored URP materials: Red, Blue, Green, Yellow |
| `Assets/Scenes/SampleScene.unity` | **Modify (Editor)** | Add 4 `Player_N` GameObjects + wire `CameraFollow` |

---

## Task 1: ECS Components

**Files:**
- Create: `Assets/Scripts/Components/PlayerComponents.cs`

- [ ] **Step 1: Create the components file**

Create `Assets/Scripts/Components/PlayerComponents.cs` with this exact content:

```csharp
using Unity.Entities;
using Unity.Mathematics;

namespace VampireSurvivors.Components
{
    /// <summary>Zero-size marker — identifies all player entities.</summary>
    public struct PlayerTag : IComponentData { }

    /// <summary>Maps this entity to Gamepad.all[Value].</summary>
    public struct PlayerIndex : IComponentData
    {
        public byte Value;
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

- [ ] **Step 2: Verify — no compile errors in Unity Console**

Switch to Unity. Check Console (Window > General > Console). Expected: no errors. If there are errors, fix before continuing.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Components/PlayerComponents.cs
git commit -m "feat: add player ECS component structs"
```

---

## Task 2: Player Authoring + Baker

**Files:**
- Create: `Assets/Scripts/Authoring/PlayerAuthoring.cs`

- [ ] **Step 1: Create the authoring file**

Create `Assets/Scripts/Authoring/PlayerAuthoring.cs`:

```csharp
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using VampireSurvivors.Components;

namespace VampireSurvivors.Authoring
{
    /// <summary>
    /// Add to a scene GameObject. Baker converts it to a player ECS entity at bake time.
    /// Assign playerMesh (a Quad mesh asset) and playerMaterials (4 colored materials) in the Inspector.
    /// </summary>
    public class PlayerAuthoring : MonoBehaviour
    {
        [Header("Player Config")]
        public byte playerIndex;
        public float moveSpeed = 7f;
        public int maxHp = 100;

        [Header("Visuals")]
        [Tooltip("Assign a quad mesh (e.g. Unity built-in Quad)")]
        public Mesh playerMesh;
        [Tooltip("4 materials: index 0=Red, 1=Blue, 2=Green, 3=Yellow")]
        public Material[] playerMaterials = new Material[4];

        class Baker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new PlayerTag());
                AddComponent(entity, new PlayerIndex { Value = authoring.playerIndex });
                AddComponent(entity, new MoveInput { Value = float2.zero });
                AddComponent(entity, new MoveSpeed { Value = authoring.moveSpeed });
                AddComponent(entity, new PlayerStats
                {
                    Hp        = authoring.maxHp,
                    MaxHp     = authoring.maxHp,
                    Level     = 1,
                    Xp        = 0f,
                    XpToNextLevel = 100f
                });

                // Colored quad visual
                var material = authoring.playerMaterials[authoring.playerIndex];
                var desc = new RenderMeshDescription(ShadowCastingMode.Off, receiveShadows: false);
                var rma  = new RenderMeshArray(new[] { material }, new[] { authoring.playerMesh });
                RenderMeshUtility.AddComponents(
                    entity, this, desc, rma,
                    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
            }
        }
    }
}
```

- [ ] **Step 2: Verify — no compile errors in Unity Console**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Authoring/PlayerAuthoring.cs
git commit -m "feat: add PlayerAuthoring MonoBehaviour and Baker"
```

---

## Task 3: Player Input System

**Files:**
- Create: `Assets/Scripts/Systems/PlayerInputSystem.cs`

- [ ] **Step 1: Create the input system**

Create `Assets/Scripts/Systems/PlayerInputSystem.cs`:

```csharp
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Reads left-stick input from each connected gamepad and writes it into MoveInput.
    /// Not Burst-compiled — reads managed Gamepad.all.
    /// Runs before PlayerMovementSystem (same SimulationSystemGroup, UpdateBefore enforced).
    /// </summary>
    [UpdateBefore(typeof(PlayerMovementSystem))]
    public partial struct PlayerInputSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (index, moveInput) in
                SystemAPI.Query<RefRO<PlayerIndex>, RefRW<MoveInput>>())
            {
                int i = index.ValueRO.Value;
                if (Gamepad.all.Count > i)
                {
                    // Vector2 implicitly converts to float2
                    moveInput.ValueRW.Value = Gamepad.all[i].leftStick.ReadValue();
                }
                else
                {
                    // Gamepad not connected — player stays still
                    moveInput.ValueRW.Value = float2.zero;
                }
            }
        }
    }
}
```

- [ ] **Step 2: Verify — compile error is expected here (forward reference)**

`[UpdateBefore(typeof(PlayerMovementSystem))]` references a type that doesn't exist yet. Unity will show a compile error. **Do not fix it — proceed immediately to Task 4.** The error resolves itself once `PlayerMovementSystem` is created.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Systems/PlayerInputSystem.cs
git commit -m "feat: add PlayerInputSystem (gamepad → MoveInput)"
```

---

## Task 4: Player Movement System

**Files:**
- Create: `Assets/Scripts/Systems/PlayerMovementSystem.cs`

- [ ] **Step 1: Create the movement system**

Create `Assets/Scripts/Systems/PlayerMovementSystem.cs`:

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Moves player entities based on MoveInput and MoveSpeed.
    /// Burst-compiled — no managed references.
    /// </summary>
    [BurstCompile]
    public partial struct PlayerMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new MoveJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct MoveJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(in MoveInput input, in MoveSpeed speed, ref LocalTransform transform)
            {
                // Move in XY plane only; Z stays 0
                transform.Position += new float3(input.Value * speed.Value * DeltaTime, 0f);
            }
        }
    }
}
```

- [ ] **Step 2: Verify — no compile errors**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Systems/PlayerMovementSystem.cs
git commit -m "feat: add Burst-compiled PlayerMovementSystem (MoveInput → LocalTransform)"
```

---

## Task 5: Camera Follow

**Files:**
- Create: `Assets/Scripts/MonoBehaviours/CameraFollow.cs`

- [ ] **Step 1: Create the camera script**

Create `Assets/Scripts/MonoBehaviours/CameraFollow.cs`:

```csharp
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.MonoBehaviours
{
    /// <summary>
    /// Attach to Main Camera. Each LateUpdate, queries all player positions from ECS,
    /// lerps the camera to their centroid, and adjusts orthographic size based on spread.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] float followSpeed  = 5f;
        [SerializeField] float baseSize     = 5f;
        [SerializeField] float zoomFactor   = 0.4f;
        [SerializeField] float maxSize      = 12f;

        Camera      _cam;
        EntityQuery _playerQuery;

        void Start()
        {
            _cam = GetComponent<Camera>();

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            _playerQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
        }

        void OnDisable()
        {
            if (_playerQuery.IsCreated)
                _playerQuery.Dispose();
        }

        void LateUpdate()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !_playerQuery.IsCreated) return;

            var transforms = _playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            if (transforms.Length == 0)
            {
                transforms.Dispose();
                return;
            }

            // Centroid
            float3 centroid = float3.zero;
            for (int i = 0; i < transforms.Length; i++)
                centroid += transforms[i].Position;
            centroid /= transforms.Length;

            // Spread — max distance from centroid to any player
            float maxDist = 0f;
            for (int i = 0; i < transforms.Length; i++)
            {
                float d = math.distance(transforms[i].Position.xy, centroid.xy);
                if (d > maxDist) maxDist = d;
            }

            transforms.Dispose(); // MUST dispose before returning

            // Move camera toward centroid
            var targetPos = new Vector3(centroid.x, centroid.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, targetPos, followSpeed * Time.deltaTime);

            // Zoom out as players spread apart
            float targetSize = math.min(baseSize + maxDist * zoomFactor, maxSize);
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, targetSize, followSpeed * Time.deltaTime);
        }
    }
}
```

- [ ] **Step 2: Verify — no compile errors**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/MonoBehaviours/CameraFollow.cs
git commit -m "feat: add CameraFollow (centroid lerp + dynamic zoom)"
```

---

## Task 6: Disable ECSBootstrap

**Files:**
- Modify: `Assets/Scripts/ECSBootstrap.cs`

- [ ] **Step 1: Comment out the Start body**

Open `Assets/Scripts/ECSBootstrap.cs`. Replace the `Start()` body so the file looks like this:

```csharp
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// DISABLED — replaced by PlayerAuthoring + Baker.
/// Left in place for reference; body is commented out.
/// </summary>
public class ECSBootstrap : MonoBehaviour
{
    void Start()
    {
        // Disabled — see PlayerAuthoring.cs
    }
}
```

- [ ] **Step 2: Verify — no compile errors**

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/ECSBootstrap.cs
git commit -m "chore: disable ECSBootstrap (replaced by PlayerAuthoring)"
```

---

## Task 7: Create Player Materials (Unity Editor — manual)

> All steps in this task happen inside the Unity Editor. No code files are created.

- [ ] **Step 1: Open the Materials folder**

In the Project window, navigate to (or create) `Assets/Materials/`.

- [ ] **Step 2: Create 4 materials**

Right-click in the folder > Create > Material. Repeat 4 times. Name them:
- `PlayerMat_Red`
- `PlayerMat_Blue`
- `PlayerMat_Green`
- `PlayerMat_Yellow`

- [ ] **Step 3: Set shader and color on each material**

For each material:
1. Select it in the Project window
2. In the Inspector, set Shader to: `Universal Render Pipeline/2D/Sprite-Unlit-Default`
3. Set the `Base Color` (the color swatch at the top) to:
   - `PlayerMat_Red` → bright red (R:255 G:50 B:50)
   - `PlayerMat_Blue` → bright blue (R:50 G:120 B:255)
   - `PlayerMat_Green` → bright green (R:50 G:220 B:80)
   - `PlayerMat_Yellow` → bright yellow (R:255 G:220 B:50)

- [ ] **Step 4: Verify**

Each material should show its color in the Inspector preview sphere.

---

## Task 8: Scene Wiring (Unity Editor — manual)

> All steps happen in the Unity Editor in `SampleScene`.

- [ ] **Step 1: Disable the ECSBootstrap GameObject**

In the Hierarchy, find the GameObject that has `ECSBootstrap` attached. Uncheck the checkbox next to its name to disable it (or you can remove the component — the script is already neutered so either is fine).

- [ ] **Step 2: Create Player_0**

In the Hierarchy: right-click > Create Empty. Name it `Player_0`. In the Inspector:
- Set Position to `(-3, 0, 0)`
- Add Component > `PlayerAuthoring`
- Set `Player Index` = `0`
- Set `Move Speed` = `7`
- Set `Max Hp` = `100`
- Set `Player Mesh`: click the field, search for `Quad`, select the Unity built-in Quad mesh
- Expand `Player Materials` (size = 4): assign `PlayerMat_Red` to slot 0, `PlayerMat_Blue` to slot 1, `PlayerMat_Green` to slot 2, `PlayerMat_Yellow` to slot 3

- [ ] **Step 3: Create Player_1, Player_2, Player_3**

Duplicate `Player_0` three times (Ctrl+D × 3). Rename them `Player_1`, `Player_2`, `Player_3`. Set:

| Object | Position | Player Index |
|--------|----------|-------------|
| Player_1 | `(-1, 0, 0)` | 1 |
| Player_2 | `(1, 0, 0)` | 2 |
| Player_3 | `(3, 0, 0)` | 3 |

The `Player Materials` array and `Player Mesh` are already copied from the duplicate — no need to reassign.

- [ ] **Step 4: Add CameraFollow to Main Camera**

Select `Main Camera` in the Hierarchy. Add Component > `CameraFollow`. Leave all fields at defaults (Follow Speed: 5, Base Size: 5, Zoom Factor: 0.4, Max Size: 12).

Confirm in the Inspector:
- `Projection` = `Orthographic`, `Size` = `5`
- Transform `Position Z` = `-10` (Unity's default — if it's not -10, set it now; at Z=0 the camera sits on the same plane as the quads and nothing renders)

- [ ] **Step 5: Save the scene**

File > Save (Ctrl+S).

---

## Task 9: Play Mode Verification

- [ ] **Step 1: Enter Play Mode**

Press the Play button. Check the Console — there should be zero errors. If you see errors, do not proceed; fix them first.

- [ ] **Step 2: Verify entities in DOTS Hierarchy**

Window > DOTS > Entities Hierarchy. You should see 4 player entities, each with `PlayerTag`, `PlayerIndex`, `MoveInput`, `MoveSpeed`, `PlayerStats`, `LocalTransform`.

- [ ] **Step 3: Verify visual**

In the Game view you should see 4 colored quads spread horizontally — red, blue, green, yellow from left to right.

- [ ] **Step 4: Verify single-gamepad movement**

Connect one gamepad. Push the left stick. The corresponding colored quad should move. The other quads stay still. Camera should smoothly follow.

- [ ] **Step 5: Verify camera zoom**

Move one player far from the others. Camera orthographic size should increase. Release the stick and bring them back together — size should decrease back toward 5.

- [ ] **Step 6: Verify 2-controller movement (if you have 2 controllers)**

Connect a second gamepad. Both players should move independently. Camera tracks centroid.

- [ ] **Step 7: Commit final**

```bash
git add Assets/Materials/ Assets/Scenes/SampleScene.unity
git commit -m "feat: wire player system scene — 4 players, materials, CameraFollow"
```

---

## Done

At this point you have:
- 4 ECS player entities, each mapped to a gamepad by index
- Burst-compiled movement at 60fps
- Camera that follows the group centroid and zooms dynamically
- Foundation ready for enemy entities, weapons, and XP in the next milestone
