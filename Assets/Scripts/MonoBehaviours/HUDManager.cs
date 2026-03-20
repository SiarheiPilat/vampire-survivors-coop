using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using VampireSurvivors.Components;
using VampireSurvivors.Systems;

namespace VampireSurvivors.MonoBehaviours
{
    /// <summary>
    /// Polls the ECS world each frame and updates HUD elements:
    /// per-player HP bar, XP bar, level text, plus a top-center elapsed timer.
    ///
    /// Attach to the HUD GameObject in 4_SampleScene.
    /// Wire the four panel roots and their child Image/TMP_Text refs via the Inspector arrays.
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        [Header("Player Panels (index 0-3)")]
        [SerializeField] GameObject[] panelRoots   = new GameObject[4];
        [SerializeField] Image[]      hpFillImages = new Image[4];
        [SerializeField] Image[]      xpFillImages = new Image[4];
        [SerializeField] TMP_Text[]   levelTexts   = new TMP_Text[4];
        [SerializeField] TMP_Text[]   playerLabels = new TMP_Text[4];

        [Header("Timer")]
        [SerializeField] TMP_Text timerText;

        [Header("Game Over")]
        [SerializeField] GameObject gameOverPanel;
        [SerializeField] TMP_Text   gameOverTimeText;

        static readonly Color HpColorHigh   = new Color(0.20f, 0.80f, 0.20f, 1f);
        static readonly Color HpColorMid    = new Color(0.90f, 0.80f, 0.10f, 1f);
        static readonly Color HpColorLow    = new Color(0.85f, 0.15f, 0.15f, 1f);
        static readonly Color LevelUpColor  = new Color(1.00f, 0.95f, 0.10f, 1f);
        static readonly Color LevelTextNorm = Color.white;

        const float LevelUpFlashDuration = 1.5f;

        EntityQuery _playerQuery;
        EntityQuery _activePlayerQuery;   // PlayerTag + NOT Downed
        EntityQuery _upgradePendingQuery; // PlayerTag + UpgradeChoicePending
        bool        _queryCreated;
        float       _elapsedTime;
        bool        _gameOver;
        bool        _victory;
        bool        _hadPlayers;  // true once at least 1 player was alive

        const float RunDuration = 1800f; // 30 minutes in seconds

        readonly int[]   _lastLevels     = new int[4];
        readonly float[] _levelUpTimers  = new float[4];

        // Upgrade-choice panel (created programmatically)
        GameObject _upgradePanel;
        TMP_Text   _upgradeTitle;
        bool       _upgradeShowing;
        Entity     _pendingUpgradeEntity;

        // Dynamic 3-choice upgrade system
        enum UpgradeType
        {
            Spinach, Pummarola, Armor, EmptyTome, Crown, Clover, Bracer, HollowHeart, Duplicator,
            WandAmount, KnifeAmount, FireAmount, LightningAmount, WhipAmount, AxeAmount, HolyWaterAmount,
            HolyWandEvolution,     // Magic Wand + Empty Tome
            SoulEaterEvolution,    // Garlic + Pummarola
            HeavenSwordEvolution,  // Cross + Clover
            ThousandEdgeEvolution, // Knife + Bracer
            BloodyTearEvolution,   // Whip + Hollow Heart
            ThunderLoopEvolution,  // Lightning Ring + Duplicator
        }
        readonly UpgradeType[] _currentChoices = new UpgradeType[3];
        readonly TMP_Text[]    _btnLabels       = new TMP_Text[3];

        static readonly (UpgradeType type, string label)[] k_WeaponUpgrades =
        {
            (UpgradeType.WandAmount,      "Magic Wand +1 shot\nFire an extra Wand projectile"),
            (UpgradeType.KnifeAmount,     "Knife +1 blade\nThrow an extra Knife per volley"),
            (UpgradeType.FireAmount,      "Fire Wand +1 flame\nLaunch an extra fireball per burst"),
            (UpgradeType.LightningAmount, "Lightning Ring +1 strike\nHit an extra enemy per activation"),
            (UpgradeType.WhipAmount,      "Whip +1 arc\nSwing an extra Whip in a new direction"),
            (UpgradeType.AxeAmount,       "Axe +1 blade\nThrow an extra Axe per volley"),
            (UpgradeType.HolyWaterAmount, "Holy Water +1 flask\nThrow an extra flask per volley"),
        };
        static readonly (UpgradeType type, string label)[] k_PassiveUpgrades =
        {
            (UpgradeType.Spinach,     "Spinach\n+10% Might (weapon damage)"),
            (UpgradeType.Pummarola,   "Pummarola\n+0.2 HP/s regen"),
            (UpgradeType.Armor,       "Armor\n+1 flat damage reduction"),
            (UpgradeType.EmptyTome,   "Empty Tome\n-8% weapon cooldown"),
            (UpgradeType.Crown,       "Crown\n+8% XP gain"),
            (UpgradeType.Clover,      "Clover\n+10% Luck (better drops)"),
            (UpgradeType.Bracer,      "Bracer\n+10% projectile speed"),
            (UpgradeType.HollowHeart, "Hollow Heart\n+10% Max HP"),
            (UpgradeType.Duplicator,  "Duplicator\n+1 Amount to all weapons"),
        };

        // Gold display (created programmatically)
        EntityQuery _sharedGoldQuery;
        TMP_Text    _goldText;

        // Revive progress display
        EntityQuery  _reviveProgressQuery;
        GameObject   _reviveBar;
        TMP_Text     _reviveText;
        Image        _reviveFill;

        void Start()
        {
            for (int i = 0; i < 4; i++)
            {
                if (playerLabels[i] != null) playerLabels[i].text = $"P{i + 1}";
                if (panelRoots[i] != null)   panelRoots[i].SetActive(false);
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            _playerQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<PlayerIndex>(),
                ComponentType.ReadOnly<PlayerStats>(),
                ComponentType.ReadOnly<Health>()
            );
            _activePlayerQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.Exclude<Downed>()
            );
            _upgradePendingQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<PlayerIndex>(),
                ComponentType.ReadOnly<UpgradeChoicePending>()
            );
            _queryCreated = true;

            _sharedGoldQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SharedGold>()
            );
            _reviveProgressQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ReviveProgress>(),
                ComponentType.ReadOnly<PlayerIndex>()
            );

            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            CreateUpgradePanel();
            CreateGoldDisplay();
            CreateReviveBar();
        }

        void OnDisable()
        {
            if (_queryCreated)
            {
                _playerQuery.Dispose();
                _activePlayerQuery.Dispose();
                _upgradePendingQuery.Dispose();
                _sharedGoldQuery.Dispose();
                _reviveProgressQuery.Dispose();
                _queryCreated = false;
            }
        }

        void Update()
        {
            if (_gameOver || _victory) return;

            _elapsedTime += Time.unscaledDeltaTime; // unscaled so timer advances while paused

            // 30-minute win condition
            if (_elapsedTime >= RunDuration)
            {
                TriggerVictory();
                return;
            }

            UpdateTimer();
            TickLevelUpTimers();

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !_queryCreated) return;

            HandleUpgradeChoices(world);
            UpdateGoldDisplay();
            UpdateReviveBar();
            if (_upgradeShowing) return; // freeze HUD updates while choice panel is visible

            bool[] seen = new bool[4];

            var indices     = _playerQuery.ToComponentDataArray<PlayerIndex>(Allocator.Temp);
            var stats       = _playerQuery.ToComponentDataArray<PlayerStats>(Allocator.Temp);
            var healths     = _playerQuery.ToComponentDataArray<Health>(Allocator.Temp);
            int playerCount = indices.Length;

            for (int i = 0; i < playerCount; i++)
            {
                int slot = indices[i].Value;
                if (slot < 0 || slot >= 4) continue;
                seen[slot] = true;
                UpdatePanel(slot, healths[i], stats[i]);
            }

            indices.Dispose();
            stats.Dispose();
            healths.Dispose();

            for (int slot = 0; slot < 4; slot++)
                if (panelRoots[slot] != null && !seen[slot])
                    panelRoots[slot].SetActive(false);

            // Game-over: all players downed
            if (playerCount > 0) _hadPlayers = true;
            if (_hadPlayers && _activePlayerQuery.CalculateEntityCount() == 0)
                TriggerGameOver();
        }

        void UpdatePanel(int slot, Health health, PlayerStats stats)
        {
            if (panelRoots[slot] == null) return;
            panelRoots[slot].SetActive(true);

            if (hpFillImages[slot] != null)
            {
                float hpRatio = health.Max > 0
                    ? Mathf.Clamp01((float)health.Current / health.Max)
                    : 0f;
                hpFillImages[slot].fillAmount = hpRatio;
                hpFillImages[slot].color      = HpColor(hpRatio);
            }

            if (xpFillImages[slot] != null)
            {
                float xpRatio = stats.XpToNextLevel > 0f
                    ? Mathf.Clamp01(stats.Xp / stats.XpToNextLevel)
                    : 0f;
                xpFillImages[slot].fillAmount = xpRatio;
            }

            if (levelTexts[slot] != null)
            {
                // Detect level-up and start flash
                if (stats.Level > _lastLevels[slot] && _lastLevels[slot] > 0)
                    _levelUpTimers[slot] = LevelUpFlashDuration;
                _lastLevels[slot] = stats.Level;

                levelTexts[slot].text  = _levelUpTimers[slot] > 0f
                    ? $"LEVEL UP!"
                    : $"Lv {stats.Level}";
                levelTexts[slot].color = _levelUpTimers[slot] > 0f
                    ? LevelUpColor
                    : LevelTextNorm;
            }
        }

        void TickLevelUpTimers()
        {
            for (int i = 0; i < 4; i++)
                if (_levelUpTimers[i] > 0f)
                    _levelUpTimers[i] = Mathf.Max(0f, _levelUpTimers[i] - Time.deltaTime);
        }

        void UpdateTimer()
        {
            if (timerText == null) return;
            int total      = Mathf.FloorToInt(_elapsedTime);
            timerText.text = $"{total / 60:00}:{total % 60:00}";

            // Warn when < 5 minutes remain (yellow flash)
            float remaining = RunDuration - _elapsedTime;
            if (remaining <= 300f)
                timerText.color = (Mathf.FloorToInt(remaining) % 2 == 0)
                    ? new Color(1f, 0.9f, 0.1f) : Color.white;
            else
                timerText.color = Color.white;
        }

        void TriggerGameOver()
        {
            _gameOver = true;
            if (gameOverPanel == null) return;
            gameOverPanel.SetActive(true);

            if (gameOverTimeText != null)
            {
                int total = Mathf.FloorToInt(_elapsedTime);
                gameOverTimeText.text = $"Survived  {total / 60:00}:{total % 60:00}";
            }

            // Append run stats (kills, gold) as text lines inside the game-over panel
            var goWorld = World.DefaultGameObjectInjectionWorld;
            if (goWorld != null)
            {
                using var gq = goWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<SharedGold>());
                if (gq.CalculateEntityCount() > 0)
                {
                    using var arr = gq.ToComponentDataArray<SharedGold>(Unity.Collections.Allocator.Temp);
                    var gs = arr[0];
                    AddStatLine(gameOverPanel, $"Enemies Killed  {gs.EnemiesKilled}", -50f);
                    AddStatLine(gameOverPanel, $"Gold Earned  {gs.Total}", -90f);
                }
            }
        }

        void TriggerVictory()
        {
            _victory = true;
            Time.timeScale = 0f; // pause the game on victory

            // Re-use the game-over panel as the victory panel (swap text)
            if (gameOverPanel == null) return;
            gameOverPanel.SetActive(true);

            if (gameOverTimeText != null)
                gameOverTimeText.text = "YOU SURVIVED 30 MINUTES!";

            // Append stats
            var goWorld = World.DefaultGameObjectInjectionWorld;
            if (goWorld != null)
            {
                using var gq = goWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<SharedGold>());
                if (gq.CalculateEntityCount() > 0)
                {
                    using var arr = gq.ToComponentDataArray<SharedGold>(Unity.Collections.Allocator.Temp);
                    var gs = arr[0];
                    AddStatLine(gameOverPanel, $"Enemies Killed  {gs.EnemiesKilled}", -50f);
                    AddStatLine(gameOverPanel, $"Gold Earned  {gs.Total}", -90f);
                }
            }

            // Tint the panel green for victory
            var bg = gameOverPanel.GetComponent<UnityEngine.UI.Image>();
            if (bg != null) bg.color = new Color(0.05f, 0.25f, 0.05f, 0.92f);
        }

        static void AddStatLine(GameObject panel, string text, float yOffset)
        {
            var go  = new GameObject("StatLine");
            go.transform.SetParent(panel.transform, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(400f, 36f);
            rt.anchoredPosition = new Vector2(0f, yOffset);
            var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = 22f;
            tmp.color     = Color.white;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
        }

        static Color HpColor(float ratio)
        {
            if (ratio > 0.5f)  return HpColorHigh;
            if (ratio > 0.25f) return HpColorMid;
            return HpColorLow;
        }

        // ─── Revive Progress Bar ────────────────────────────────────────────

        void CreateReviveBar()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            _reviveBar = new GameObject("ReviveBar");
            _reviveBar.transform.SetParent(canvas.transform, false);
            var rt = _reviveBar.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0f);
            rt.anchorMax        = new Vector2(0.5f, 0f);
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 20f);
            rt.sizeDelta        = new Vector2(300f, 40f);

            // Background
            var bgImg = _reviveBar.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

            // Fill bar child
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(_reviveBar.transform, false);
            var fillRt = fillGO.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(2f, -2f);
            fillRt.sizeDelta = new Vector2(0f, -4f);
            _reviveFill = fillGO.AddComponent<Image>();
            _reviveFill.color = new Color(0.1f, 0.8f, 0.3f);

            // Label
            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(_reviveBar.transform, false);
            var lblRt = lblGO.AddComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero;
            lblRt.offsetMax = Vector2.zero;
            _reviveText = lblGO.AddComponent<TextMeshProUGUI>();
            _reviveText.fontSize  = 18;
            _reviveText.alignment = TextAlignmentOptions.Center;
            _reviveText.color     = Color.white;

            _reviveBar.SetActive(false);
        }

        void UpdateReviveBar()
        {
            if (_reviveBar == null || !_queryCreated) return;

            if (_reviveProgressQuery.CalculateEntityCount() == 0)
            {
                _reviveBar.SetActive(false);
                return;
            }

            var progresses = _reviveProgressQuery.ToComponentDataArray<ReviveProgress>(Allocator.Temp);
            var indices    = _reviveProgressQuery.ToComponentDataArray<PlayerIndex>(Allocator.Temp);

            // Show bar for the first in-progress revive
            float ratio = Mathf.Clamp01(progresses[0].Timer / ReviveSystem.ReviveDuration);
            int   slot  = indices[0].Value;
            progresses.Dispose();
            indices.Dispose();

            _reviveBar.SetActive(true);

            if (_reviveFill != null)
            {
                var fillRt = _reviveFill.GetComponent<RectTransform>();
                var barRt  = _reviveBar.GetComponent<RectTransform>();
                float maxW = barRt.sizeDelta.x - 4f;
                fillRt.sizeDelta = new Vector2(maxW * ratio, fillRt.sizeDelta.y);
            }

            if (_reviveText != null)
                _reviveText.text = $"Reviving P{slot + 1}... {Mathf.FloorToInt(ratio * 100)}%";
        }

        // ─── Gold Display ───────────────────────────────────────────────────

        void CreateGoldDisplay()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            var go = new GameObject("GoldText");
            go.transform.SetParent(canvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 1f);
            rt.anchorMax        = new Vector2(0.5f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(100f, -8f); // top-center, right of timer
            rt.sizeDelta        = new Vector2(160f, 36f);

            _goldText = go.AddComponent<TextMeshProUGUI>();
            _goldText.fontSize  = 22;
            _goldText.alignment = TextAlignmentOptions.Left;
            _goldText.color     = new Color(1f, 0.85f, 0.1f);
            _goldText.text      = "G: 0";
        }

        void UpdateGoldDisplay()
        {
            if (_goldText == null || !_queryCreated) return;
            if (_sharedGoldQuery.CalculateEntityCount() == 0) return;

            var golds = _sharedGoldQuery.ToComponentDataArray<SharedGold>(Allocator.Temp);
            _goldText.text = $"G: {golds[0].Total}";
            golds.Dispose();
        }

        // ─── Upgrade Choice Panel ───────────────────────────────────────────

        void CreateUpgradePanel()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            // Semi-transparent full-screen overlay
            _upgradePanel = new GameObject("UpgradeChoicePanel");
            _upgradePanel.transform.SetParent(canvas.transform, false);
            var rt = _upgradePanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var bg = _upgradePanel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.78f);

            // Title
            var titleGO = new GameObject("UpgradeTitle");
            titleGO.transform.SetParent(_upgradePanel.transform, false);
            var titleRt = titleGO.AddComponent<RectTransform>();
            titleRt.anchoredPosition = new Vector2(0f, 120f);
            titleRt.sizeDelta        = new Vector2(700f, 60f);
            _upgradeTitle = titleGO.AddComponent<TextMeshProUGUI>();
            _upgradeTitle.fontSize  = 34;
            _upgradeTitle.alignment = TextAlignmentOptions.Center;
            _upgradeTitle.color     = new Color(1f, 0.95f, 0.1f);

            // 3 dynamic upgrade buttons, larger than before
            float[] yPos = { 40f, -60f, -160f };
            for (int i = 0; i < 3; i++)
            {
                int capturedIdx = i;

                var btnGO = new GameObject($"UpgradeBtn{i}");
                btnGO.transform.SetParent(_upgradePanel.transform, false);
                var btnRt = btnGO.AddComponent<RectTransform>();
                btnRt.anchoredPosition = new Vector2(0f, yPos[i]);
                btnRt.sizeDelta        = new Vector2(540f, 85f);

                var btnImg = btnGO.AddComponent<Image>();
                btnImg.color = new Color(0.12f, 0.12f, 0.42f, 1f);

                var btn = btnGO.AddComponent<Button>();
                btn.targetGraphic = btnImg;

                var colors = btn.colors;
                colors.highlightedColor = new Color(0.25f, 0.25f, 0.70f, 1f);
                colors.pressedColor     = new Color(0.35f, 0.35f, 0.90f, 1f);
                btn.colors = colors;

                btn.onClick.AddListener(() =>
                {
                    var w = World.DefaultGameObjectInjectionWorld;
                    if (w != null) ApplyUpgrade(w, capturedIdx);
                });

                var lblGO = new GameObject("Label");
                lblGO.transform.SetParent(btnGO.transform, false);
                var lblRt = lblGO.AddComponent<RectTransform>();
                lblRt.anchorMin = Vector2.zero;
                lblRt.anchorMax = Vector2.one;
                lblRt.offsetMin = Vector2.zero;
                lblRt.offsetMax = Vector2.zero;
                _btnLabels[i] = lblGO.AddComponent<TextMeshProUGUI>();
                _btnLabels[i].text      = "...";
                _btnLabels[i].fontSize  = 22;
                _btnLabels[i].alignment = TextAlignmentOptions.Center;
                _btnLabels[i].color     = Color.white;
            }

            _upgradePanel.SetActive(false);
        }

        void HandleUpgradeChoices(World world)
        {
            if (_upgradePanel == null) return;

            if (_upgradeShowing)
            {
                // Dismiss if entity was destroyed unexpectedly
                if (_pendingUpgradeEntity == Entity.Null ||
                    !world.EntityManager.Exists(_pendingUpgradeEntity) ||
                    !world.EntityManager.HasComponent<UpgradeChoicePending>(_pendingUpgradeEntity))
                {
                    DismissUpgradePanel();
                }
                return;
            }

            if (_upgradePendingQuery.CalculateEntityCount() == 0) return;

            // Show panel for first pending player (one at a time)
            var entities = _upgradePendingQuery.ToEntityArray(Allocator.Temp);
            var indices  = _upgradePendingQuery.ToComponentDataArray<PlayerIndex>(Allocator.Temp);
            _pendingUpgradeEntity = entities[0];
            int slot = indices[0].Value;
            entities.Dispose();
            indices.Dispose();

            _upgradeShowing = true;
            if (_upgradeTitle != null)
                _upgradeTitle.text = $"P{slot + 1}  LEVEL UP!\nChoose an upgrade:";

            BuildUpgradeChoices(world);

            _upgradePanel.SetActive(true);
            Time.timeScale = 0f;
        }

        void BuildUpgradeChoices(World world)
        {
            var em = world.EntityManager;

            // Build pool: all 6 passives + weapon amount upgrades for weapons the player has
            var pool = new System.Collections.Generic.List<(UpgradeType type, string label)>(k_PassiveUpgrades);

            foreach (var (type, label) in k_WeaponUpgrades)
            {
                bool canAdd = false;
                int  curAmt = 1;
                switch (type)
                {
                    case UpgradeType.WandAmount:
                        if (em.HasComponent<MagicWandState>(_pendingUpgradeEntity))
                        {
                            curAmt = em.GetComponentData<MagicWandState>(_pendingUpgradeEntity).Amount;
                            canAdd = curAmt < 5;
                        }
                        break;
                    case UpgradeType.KnifeAmount:
                        if (em.HasComponent<KnifeState>(_pendingUpgradeEntity))
                        {
                            curAmt = em.GetComponentData<KnifeState>(_pendingUpgradeEntity).Amount;
                            canAdd = curAmt < 5;
                        }
                        break;
                    case UpgradeType.FireAmount:
                        if (em.HasComponent<FireWandState>(_pendingUpgradeEntity))
                        {
                            curAmt = em.GetComponentData<FireWandState>(_pendingUpgradeEntity).Amount;
                            canAdd = curAmt < 5;
                        }
                        break;
                    case UpgradeType.LightningAmount:
                        if (em.HasComponent<LightningRingState>(_pendingUpgradeEntity))
                        {
                            curAmt = em.GetComponentData<LightningRingState>(_pendingUpgradeEntity).Amount;
                            canAdd = curAmt < 5;
                        }
                        break;
                    case UpgradeType.WhipAmount:
                        if (em.HasComponent<WeaponState>(_pendingUpgradeEntity))
                        {
                            var ws2 = em.GetComponentData<WeaponState>(_pendingUpgradeEntity);
                            curAmt = Unity.Mathematics.math.max(1, ws2.Amount);
                            canAdd = curAmt < 5 && !ws2.IsEvolved;
                        }
                        break;
                    case UpgradeType.AxeAmount:
                        if (em.HasComponent<AxeState>(_pendingUpgradeEntity))
                        {
                            curAmt = Unity.Mathematics.math.max(1, em.GetComponentData<AxeState>(_pendingUpgradeEntity).Amount);
                            canAdd = curAmt < 5;
                        }
                        break;
                    case UpgradeType.HolyWaterAmount:
                        if (em.HasComponent<HolyWaterState>(_pendingUpgradeEntity))
                        {
                            curAmt = Unity.Mathematics.math.max(1, em.GetComponentData<HolyWaterState>(_pendingUpgradeEntity).Amount);
                            canAdd = curAmt < 5;
                        }
                        break;
                }
                if (canAdd) pool.Add((type, label + $"  ({curAmt}→{curAmt + 1})"));
            }

            // ── Evolution checks ─────────────────────────────────────────────
            var playerStats = em.GetComponentData<PlayerStats>(_pendingUpgradeEntity);

            // Holy Wand = Magic Wand + Empty Tome (CooldownMult < 1 means tome was taken)
            if (em.HasComponent<MagicWandState>(_pendingUpgradeEntity))
            {
                var ws = em.GetComponentData<MagicWandState>(_pendingUpgradeEntity);
                if (!ws.IsEvolved && playerStats.CooldownMult < 1.0f)
                    pool.Add((UpgradeType.HolyWandEvolution,
                        "★ Holy Wand\nMagic Wand + Empty Tome — 7 bolts, 20 dmg, tight fan"));
            }

            // Soul Eater = Garlic + Pummarola (HpRegen > 0 means pummarola was taken)
            if (em.HasComponent<GarlicState>(_pendingUpgradeEntity))
            {
                var gs = em.GetComponentData<GarlicState>(_pendingUpgradeEntity);
                if (!gs.IsEvolved && playerStats.HpRegen > 0f)
                    pool.Add((UpgradeType.SoulEaterEvolution,
                        "★ Soul Eater\nGarlic + Pummarola — r=3.5u, 25 dmg, heals 2 HP/pulse"));
            }

            // Heaven Sword = Cross + Clover (Luck > 0 means clover was taken)
            if (em.HasComponent<CrossState>(_pendingUpgradeEntity))
            {
                var cs = em.GetComponentData<CrossState>(_pendingUpgradeEntity);
                if (!cs.IsEvolved && playerStats.Luck > 0f)
                    pool.Add((UpgradeType.HeavenSwordEvolution,
                        "★ Heaven Sword\nCross + Clover — 2 swords, 200 dmg, piercing, 2.5s CD"));
            }

            // Bloody Tear = Whip + Hollow Heart (MaxHpBonus > 0 means hollow heart was taken)
            if (em.HasComponent<WeaponState>(_pendingUpgradeEntity))
            {
                var ws = em.GetComponentData<WeaponState>(_pendingUpgradeEntity);
                if (!ws.IsEvolved && playerStats.MaxHpBonus > 0)
                    pool.Add((UpgradeType.BloodyTearEvolution,
                        "★ Bloody Tear\nWhip + Hollow Heart — 20 dmg, heals 1 HP/enemy hit"));
            }

            // Thousand Edge = Knife + Bracer (ProjectileSpeedMult > 1 means bracer was taken)
            if (em.HasComponent<KnifeState>(_pendingUpgradeEntity))
            {
                var ks = em.GetComponentData<KnifeState>(_pendingUpgradeEntity);
                if (!ks.IsEvolved && playerStats.ProjectileSpeedMult > 1.0f)
                    pool.Add((UpgradeType.ThousandEdgeEvolution,
                        "★ Thousand Edge\nKnife + Bracer — 5 blades, 15 dmg, 0.15s CD, speed 20"));
            }

            // Thunder Loop = Lightning Ring + Duplicator (DuplicatorStacks > 0 means duplicator was taken)
            if (em.HasComponent<LightningRingState>(_pendingUpgradeEntity))
            {
                var lr = em.GetComponentData<LightningRingState>(_pendingUpgradeEntity);
                if (!lr.IsEvolved && playerStats.DuplicatorStacks > 0)
                    pool.Add((UpgradeType.ThunderLoopEvolution,
                        "★ Thunder Loop\nLightning Ring + Duplicator — 65 dmg, 6 targets, 0.5s CD"));
            }

            // Fisher-Yates shuffle using UnityEngine.Random (unscaled, so fine while paused)
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            // Pick up to 3
            int count = Mathf.Min(3, pool.Count);
            for (int i = 0; i < count; i++)
            {
                _currentChoices[i] = pool[i].type;
                if (_btnLabels[i] != null) _btnLabels[i].text = pool[i].label;
            }
        }

        void ApplyUpgrade(World world, int choiceIndex)
        {
            if (_pendingUpgradeEntity == Entity.Null) return;
            if (!world.EntityManager.Exists(_pendingUpgradeEntity)) return;
            if (!world.EntityManager.HasComponent<PlayerStats>(_pendingUpgradeEntity)) return;
            if (choiceIndex < 0 || choiceIndex >= 3) return;

            var em    = world.EntityManager;
            var stats = em.GetComponentData<PlayerStats>(_pendingUpgradeEntity);
            int pidx  = em.GetComponentData<PlayerIndex>(_pendingUpgradeEntity).Value;

            switch (_currentChoices[choiceIndex])
            {
                case UpgradeType.Spinach:
                    stats.Might += 0.1f;
                    Debug.Log($"[HUDManager] P{pidx} chose Spinach — Might = {stats.Might:F1}x");
                    break;
                case UpgradeType.Pummarola:
                    stats.HpRegen += 0.2f;
                    Debug.Log($"[HUDManager] P{pidx} chose Pummarola — HpRegen = {stats.HpRegen:F1}/s");
                    break;
                case UpgradeType.Armor:
                    stats.Armor += 1;
                    Debug.Log($"[HUDManager] P{pidx} chose Armor — Armor = {stats.Armor}");
                    break;
                case UpgradeType.EmptyTome:
                    stats.CooldownMult = Mathf.Max(0.5f, stats.CooldownMult * 0.92f);
                    Debug.Log($"[HUDManager] P{pidx} chose Empty Tome — CooldownMult = {stats.CooldownMult:F3}×");
                    break;
                case UpgradeType.Crown:
                    stats.XpMult *= 1.08f;
                    Debug.Log($"[HUDManager] P{pidx} chose Crown — XpMult = {stats.XpMult:F3}×");
                    break;
                case UpgradeType.Clover:
                    stats.Luck += 0.1f;
                    Debug.Log($"[HUDManager] P{pidx} chose Clover — Luck = {stats.Luck:F1}");
                    break;
                case UpgradeType.Bracer:
                    stats.ProjectileSpeedMult *= 1.1f;
                    Debug.Log($"[HUDManager] P{pidx} chose Bracer — ProjectileSpeedMult = {stats.ProjectileSpeedMult:F3}×");
                    break;
                case UpgradeType.HollowHeart:
                {
                    if (em.HasComponent<Health>(_pendingUpgradeEntity))
                    {
                        var hp     = em.GetComponentData<Health>(_pendingUpgradeEntity);
                        int bonus  = Mathf.Max(1, hp.Max / 10); // +10% of current max
                        hp.Max    += bonus;
                        hp.Current = Mathf.Min(hp.Max, hp.Current + bonus); // also heal the bonus
                        em.SetComponentData(_pendingUpgradeEntity, hp);
                        stats.MaxHpBonus += bonus;
                        Debug.Log($"[HUDManager] P{pidx} chose Hollow Heart — MaxHp = {hp.Max} (+{bonus})");
                    }
                    break;
                }
                case UpgradeType.Duplicator:
                {
                    // +1 Amount to every weapon the player currently owns
                    stats.DuplicatorStacks++;
                    if (em.HasComponent<WeaponState>(_pendingUpgradeEntity))
                    {
                        var w = em.GetComponentData<WeaponState>(_pendingUpgradeEntity);
                        w.Amount = Unity.Mathematics.math.max(1, w.Amount) + 1;
                        em.SetComponentData(_pendingUpgradeEntity, w);
                    }
                    if (em.HasComponent<MagicWandState>(_pendingUpgradeEntity))
                    {
                        var w = em.GetComponentData<MagicWandState>(_pendingUpgradeEntity);
                        w.Amount++;
                        em.SetComponentData(_pendingUpgradeEntity, w);
                    }
                    if (em.HasComponent<KnifeState>(_pendingUpgradeEntity))
                    {
                        var w = em.GetComponentData<KnifeState>(_pendingUpgradeEntity);
                        w.Amount++;
                        em.SetComponentData(_pendingUpgradeEntity, w);
                    }
                    if (em.HasComponent<FireWandState>(_pendingUpgradeEntity))
                    {
                        var w = em.GetComponentData<FireWandState>(_pendingUpgradeEntity);
                        w.Amount++;
                        em.SetComponentData(_pendingUpgradeEntity, w);
                    }
                    if (em.HasComponent<LightningRingState>(_pendingUpgradeEntity))
                    {
                        var w = em.GetComponentData<LightningRingState>(_pendingUpgradeEntity);
                        w.Amount++;
                        em.SetComponentData(_pendingUpgradeEntity, w);
                    }
                    if (em.HasComponent<AxeState>(_pendingUpgradeEntity))
                    {
                        var w = em.GetComponentData<AxeState>(_pendingUpgradeEntity);
                        w.Amount = Unity.Mathematics.math.max(1, w.Amount) + 1;
                        em.SetComponentData(_pendingUpgradeEntity, w);
                    }
                    if (em.HasComponent<HolyWaterState>(_pendingUpgradeEntity))
                    {
                        var w = em.GetComponentData<HolyWaterState>(_pendingUpgradeEntity);
                        w.Amount = Unity.Mathematics.math.max(1, w.Amount) + 1;
                        em.SetComponentData(_pendingUpgradeEntity, w);
                    }
                    Debug.Log($"[HUDManager] P{pidx} chose Duplicator — +1 Amount to all weapons (stacks={stats.DuplicatorStacks})");
                    break;
                }
                case UpgradeType.WandAmount:
                    if (em.HasComponent<MagicWandState>(_pendingUpgradeEntity))
                    {
                        var wand = em.GetComponentData<MagicWandState>(_pendingUpgradeEntity);
                        wand.Amount++;
                        em.SetComponentData(_pendingUpgradeEntity, wand);
                        Debug.Log($"[HUDManager] P{pidx} chose Wand +1 — Amount = {wand.Amount}");
                    }
                    break;
                case UpgradeType.KnifeAmount:
                    if (em.HasComponent<KnifeState>(_pendingUpgradeEntity))
                    {
                        var knife = em.GetComponentData<KnifeState>(_pendingUpgradeEntity);
                        knife.Amount++;
                        em.SetComponentData(_pendingUpgradeEntity, knife);
                        Debug.Log($"[HUDManager] P{pidx} chose Knife +1 — Amount = {knife.Amount}");
                    }
                    break;
                case UpgradeType.FireAmount:
                    if (em.HasComponent<FireWandState>(_pendingUpgradeEntity))
                    {
                        var fire = em.GetComponentData<FireWandState>(_pendingUpgradeEntity);
                        fire.Amount++;
                        em.SetComponentData(_pendingUpgradeEntity, fire);
                        Debug.Log($"[HUDManager] P{pidx} chose FireWand +1 — Amount = {fire.Amount}");
                    }
                    break;
                case UpgradeType.LightningAmount:
                    if (em.HasComponent<LightningRingState>(_pendingUpgradeEntity))
                    {
                        var ring = em.GetComponentData<LightningRingState>(_pendingUpgradeEntity);
                        ring.Amount++;
                        em.SetComponentData(_pendingUpgradeEntity, ring);
                        Debug.Log($"[HUDManager] P{pidx} chose Lightning Ring +1 — Amount = {ring.Amount}");
                    }
                    break;
                case UpgradeType.WhipAmount:
                    if (em.HasComponent<WeaponState>(_pendingUpgradeEntity))
                    {
                        var ws2 = em.GetComponentData<WeaponState>(_pendingUpgradeEntity);
                        ws2.Amount = Unity.Mathematics.math.max(1, ws2.Amount) + 1;
                        em.SetComponentData(_pendingUpgradeEntity, ws2);
                        Debug.Log($"[HUDManager] P{pidx} chose Whip +1 — Amount = {ws2.Amount}");
                    }
                    break;
                case UpgradeType.AxeAmount:
                    if (em.HasComponent<AxeState>(_pendingUpgradeEntity))
                    {
                        var axe2 = em.GetComponentData<AxeState>(_pendingUpgradeEntity);
                        axe2.Amount = Unity.Mathematics.math.max(1, axe2.Amount) + 1;
                        em.SetComponentData(_pendingUpgradeEntity, axe2);
                        Debug.Log($"[HUDManager] P{pidx} chose Axe +1 — Amount = {axe2.Amount}");
                    }
                    break;
                case UpgradeType.HolyWaterAmount:
                    if (em.HasComponent<HolyWaterState>(_pendingUpgradeEntity))
                    {
                        var hw2 = em.GetComponentData<HolyWaterState>(_pendingUpgradeEntity);
                        hw2.Amount = Unity.Mathematics.math.max(1, hw2.Amount) + 1;
                        em.SetComponentData(_pendingUpgradeEntity, hw2);
                        Debug.Log($"[HUDManager] P{pidx} chose Holy Water +1 — Amount = {hw2.Amount}");
                    }
                    break;

                case UpgradeType.HolyWandEvolution:
                    if (em.HasComponent<MagicWandState>(_pendingUpgradeEntity))
                    {
                        var wand = em.GetComponentData<MagicWandState>(_pendingUpgradeEntity);
                        wand.IsEvolved = true;
                        wand.Amount    = 7;
                        wand.Damage    = 20f;
                        wand.Cooldown  = 0.25f;
                        wand.Speed     = 14f;
                        em.SetComponentData(_pendingUpgradeEntity, wand);
                        Debug.Log($"[HUDManager] P{pidx} evolved Magic Wand → Holy Wand");
                    }
                    break;

                case UpgradeType.SoulEaterEvolution:
                    if (em.HasComponent<GarlicState>(_pendingUpgradeEntity))
                    {
                        var garlic = em.GetComponentData<GarlicState>(_pendingUpgradeEntity);
                        garlic.IsEvolved    = true;
                        garlic.Range        = 3.5f;
                        garlic.Damage       = 25f;
                        garlic.HealPerPulse = 2f;
                        em.SetComponentData(_pendingUpgradeEntity, garlic);
                        Debug.Log($"[HUDManager] P{pidx} evolved Garlic → Soul Eater");
                    }
                    break;

                case UpgradeType.BloodyTearEvolution:
                    if (em.HasComponent<WeaponState>(_pendingUpgradeEntity))
                    {
                        var ws        = em.GetComponentData<WeaponState>(_pendingUpgradeEntity);
                        ws.IsEvolved  = true;
                        ws.Damage     = 20f;       // double from base 10
                        ws.HealPerHit = 1f;        // 1 HP healed per enemy struck
                        ws.SwingCooldown = 0.45f;  // slightly faster
                        em.SetComponentData(_pendingUpgradeEntity, ws);
                        Debug.Log($"[HUDManager] P{pidx} evolved Whip → Bloody Tear");
                    }
                    break;

                case UpgradeType.ThousandEdgeEvolution:
                    if (em.HasComponent<KnifeState>(_pendingUpgradeEntity))
                    {
                        var knife       = em.GetComponentData<KnifeState>(_pendingUpgradeEntity);
                        knife.IsEvolved = true;
                        knife.Amount    = 5;
                        knife.Damage    = 15f;
                        knife.Speed     = 20f;
                        knife.Cooldown  = 0.15f;
                        knife.MaxRange  = 15f;
                        em.SetComponentData(_pendingUpgradeEntity, knife);
                        Debug.Log($"[HUDManager] P{pidx} evolved Knife → Thousand Edge");
                    }
                    break;

                case UpgradeType.HeavenSwordEvolution:
                    if (em.HasComponent<CrossState>(_pendingUpgradeEntity))
                    {
                        var cross       = em.GetComponentData<CrossState>(_pendingUpgradeEntity);
                        cross.IsEvolved    = true;
                        cross.Count        = 2;
                        cross.Damage       = 200f;
                        cross.Speed        = 20f;
                        cross.Cooldown     = 2.5f;
                        cross.TurnDistance = 0f;  // no boomerang return
                        em.SetComponentData(_pendingUpgradeEntity, cross);
                        Debug.Log($"[HUDManager] P{pidx} evolved Cross → Heaven Sword");
                    }
                    break;

                case UpgradeType.ThunderLoopEvolution:
                    if (em.HasComponent<LightningRingState>(_pendingUpgradeEntity))
                    {
                        var lr      = em.GetComponentData<LightningRingState>(_pendingUpgradeEntity);
                        lr.IsEvolved = true;
                        lr.Damage    = 65f;
                        lr.Amount    = 6;
                        lr.Cooldown  = 0.5f;
                        em.SetComponentData(_pendingUpgradeEntity, lr);
                        Debug.Log($"[HUDManager] P{pidx} evolved Lightning Ring → Thunder Loop");
                    }
                    break;
            }

            em.SetComponentData(_pendingUpgradeEntity, stats);
            em.RemoveComponent<UpgradeChoicePending>(_pendingUpgradeEntity);
            DismissUpgradePanel();
        }

        void DismissUpgradePanel()
        {
            _upgradeShowing       = false;
            _pendingUpgradeEntity = Entity.Null;
            if (_upgradePanel != null) _upgradePanel.SetActive(false);
            Time.timeScale = 1f;
        }
    }
}
