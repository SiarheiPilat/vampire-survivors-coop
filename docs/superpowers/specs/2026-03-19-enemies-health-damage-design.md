# Enemies, Health, Damage & Whip — Design Spec

**Goal:** First playable combat loop — enemies spawn, chase players, deal contact damage, and die when hit by the Whip weapon.

**Architecture:** Pure ECS/DOTS. New components in `EnemyComponents.cs`. Five new Burst-compiled systems. One MonoBehaviour spawner. Three enemy prefabs baked via `EnemyAuthoring`. Whip is the first weapon, hardcoded to all player entities.

---

## Components

### `EnemyComponents.cs`
```csharp
EnemyTag          : IComponentData  // zero-size marker
EnemyStats        : IComponentData  { float MoveSpeed; int ContactDamage; int XpValue; }
Health            : IComponentData  { int Current; int Max; }
Invincible        : IComponentData  { float Timer; }  // contact-damage cooldown
WeaponState       : IComponentData  { float SwingTimer; float SwingCooldown; float Damage; float Range; float ArcDegrees; }
HitArc            : IComponentData  { float Damage; float2 Direction; float Range; float ArcDegrees; }
```

`Health` is added to **both** enemy and player entities. `PlayerStats.Hp/MaxHp` remain untouched (used for level/XP later). `WeaponState` is added to player entities by `GameSceneBootstrap`. `HitArc` lives on transient entities that exist for one frame only.

---

## Enemy Stats (from wiki)

| Enemy    | HP  | Speed | Contact Dmg | XP |
|----------|-----|-------|-------------|----|
| Bat      | 10  | 2.5   | 10          | 1  |
| Zombie   | 40  | 1.0   | 20          | 5  |
| Skeleton | 75  | 1.8   | 25          | 10 |

---

## Whip Stats (Antonio starter, from wiki)

- Damage: 10 per swing
- Cooldown: 0.5s
- Arc: 120° in front of last movement direction
- Range: 1.5 units

---

## Systems

### `EnemyMovementSystem` (Burst)
For each entity with `EnemyTag + EnemyStats + LocalTransform`: find nearest player (`PlayerTag + LocalTransform`), move toward it at `EnemyStats.MoveSpeed * dt`.

Uses `NativeArray` of player positions built once per frame. O(enemies × players) — fine for current scale.

### `ContactDamageSystem` (Burst)
For each enemy: check distance to each player. If within `ContactRadius = 0.5f` and player's `Invincible.Timer <= 0`: subtract `EnemyStats.ContactDamage` from player `Health.Current`, set player `Invincible.Timer = 1.0f`.

### `InvincibilitySystem` (Burst)
Tick `Invincible.Timer` down by `dt`. Clamp to 0.

### `WhipSystem` (Burst)
For each player with `WeaponState + MoveInput + LocalTransform`:
- Tick `SwingTimer` down by dt
- When `SwingTimer <= 0`: spawn a `HitArc` entity with the player's current facing direction (last non-zero `MoveInput`, default right), reset `SwingTimer = SwingCooldown`

### `HitArcSystem` (Burst)
For each `HitArc` entity:
- For each enemy within `Range` of arc origin (stored as a component on the HitArc entity alongside `LocalTransform`): if angle to enemy is within `ArcDegrees/2`, subtract `Damage` from enemy `Health.Current`
- Destroy the HitArc entity

### `HealthSystem`
Runs after all damage systems. For each entity with `Health.Current <= 0`:
- If enemy: `DestroyEntity`
- If player: `DestroyEntity` + `Debug.Log("[HealthSystem] Player {i} died.")` (death screen is future work)

---

## Enemy Authoring

`EnemyAuthoring.cs` — single MonoBehaviour with fields: `int hp`, `float moveSpeed`, `int contactDamage`, `int xpValue`. Baker adds: `EnemyTag`, `EnemyStats`, `Health { Current=hp, Max=hp }`, `LocalTransform`.

Three prefabs: `Assets/Prefabs/Enemies/Bat.prefab`, `Zombie.prefab`, `Skeleton.prefab`. Each is a Quad with a colored material (red=Bat, green=Zombie, blue=Skeleton) — replace with sprites later.

---

## Enemy Spawner

`EnemySpawner.cs` — MonoBehaviour on a scene GameObject in `4_SampleScene`. Fields:
- `GameObject[] enemyPrefabs` — [Bat, Zombie, Skeleton]
- `float spawnInterval = 3f`
- `int minBurst = 5`, `maxBurst = 8`
- Spawn weights: Bat 60%, Zombie 25%, Skeleton 15%
- Spawn radius: 12 units from centroid of all players

Uses `ConvertToEntity` pattern: instantiates a prefab, lets Unity bake it at runtime. Actually uses `EntityManager.Instantiate` on pre-baked entity prefabs stored via `PrefabReference` components.

Actually: simpler approach — use `GameObject.Instantiate` on the prefab GameObjects. Unity's baking pipeline converts them. Wait — at runtime we can't bake. Instead, store `Entity` prefab references baked ahead of time via a `SpawnerAuthoring` component that holds the three prefab references.

**`SpawnerAuthoring.cs`**: Baker bakes each prefab reference and stores them in a `SpawnerData` IComponentData (using `BlobAssetReference` or three separate `EnemyPrefabRef` components).

Simplest: three separate components `BatPrefab`, `ZombiePrefab`, `SkeletonPrefab` each `struct { Entity Value; }`, added by the Baker to a single spawner entity. The `EnemySpawnerSystem` (non-Burst, accesses managed camera data) reads the player centroid, picks a random off-screen point, and calls `em.Instantiate`.

---

## GameSceneBootstrap Changes

Add `WeaponState { SwingTimer = 0, SwingCooldown = 0.5f, Damage = 10, Range = 1.5f, ArcDegrees = 120f }` and `Health { Current = 100, Max = 100 }` to each filled player entity.

---

## File Map

| Action | Path |
|--------|------|
| Create | `Assets/Scripts/Components/EnemyComponents.cs` |
| Modify | `Assets/Scripts/Authoring/PlayerAuthoring.cs` — add Health |
| Modify | `Assets/Scripts/MonoBehaviours/GameSceneBootstrap.cs` — add WeaponState + Health to players |
| Create | `Assets/Scripts/Authoring/EnemyAuthoring.cs` |
| Create | `Assets/Scripts/Authoring/SpawnerAuthoring.cs` |
| Create | `Assets/Scripts/Systems/EnemyMovementSystem.cs` |
| Create | `Assets/Scripts/Systems/ContactDamageSystem.cs` |
| Create | `Assets/Scripts/Systems/InvincibilitySystem.cs` |
| Create | `Assets/Scripts/Systems/WhipSystem.cs` |
| Create | `Assets/Scripts/Systems/HitArcSystem.cs` |
| Create | `Assets/Scripts/Systems/HealthSystem.cs` |
| Create | `Assets/Prefabs/Enemies/Bat.prefab` (via Unity) |
| Create | `Assets/Prefabs/Enemies/Zombie.prefab` (via Unity) |
| Create | `Assets/Prefabs/Enemies/Skeleton.prefab` (via Unity) |
| Modify | `Assets/Scenes/4_SampleScene.unity` — add EnemySpawner GameObject |
