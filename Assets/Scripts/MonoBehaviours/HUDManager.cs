using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using VampireSurvivors.Components;
using VampireSurvivors.Menu;
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

        [Header("Score Screen")]
        [SerializeField] VampireSurvivors.Menu.CharacterRegistry characterRegistry;

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
            Candelabrador, Spellbinder, Attractorb, Wings,
            WandAmount, KnifeAmount, FireAmount, LightningAmount, WhipAmount, AxeAmount, HolyWaterAmount, BoneAmount, RunetracerAmount,
            HolyWandEvolution,     // Magic Wand + Empty Tome
            SoulEaterEvolution,    // Garlic + Pummarola
            HeavenSwordEvolution,  // Cross + Clover
            ThousandEdgeEvolution, // Knife + Bracer
            BloodyTearEvolution,   // Whip + Hollow Heart
            ThunderLoopEvolution,  // Lightning Ring + Duplicator
            OsoleMeeoEvolution,    // Fire Wand + Candelabrador
            UnholyVespersEvolution,// King Bible + Spellbinder
            NoFutureEvolution,     // Runetracer + Armor
            DeathSpiralEvolution,  // Axe + Candelabrador
            HellfireEvolution,     // Fire Wand + Spinach
            LaBorraEvolution,      // Holy Water + Attractorb
            GattiAmariAmount,      // Gatti Amari +1 cat
            StoneMask,             // passive: +10% gold earnings
            ViciousHungerEvolution,   // Gatti Amari + Stone Mask
            Tiragisu,                 // passive: +1 ReviveStocks
            SilverRing,               // passive: +5% Duration +5% Area
            GoldRing,                 // passive: +5% Curse
            InfiniteCorridorEvolution,// Clock Lancet + Silver Ring + Gold Ring
            PhieraAmount,             // Phiera Der Tuphello +1 bullet/dir
            EightAmount,              // Eight The Sparrow +1 bullet/dir
            PhieraggiEvolution,       // Phiera + Eight + Tiragisú → 8-direction rapid fire
            SkullOManiac,             // passive: +10% Curse (harder enemies, more XP)
            MannajjaEvolution,        // Song of Mana + Skull O'Maniac → wide column, 40 dmg
            MetaglioLeft,             // passive: +0.1 HpRegen, +5% MaxHp (9 levels)
            MetaglioRight,            // passive: +5% Curse (9 levels)
            CrimsonShroudEvolution,   // Laurel + Metaglio Left + Right → damage cap 10, AoE retaliation
            PeachoneAmount,           // Peachone +1 egg/volley
            EbonyWingsAmount,         // Ebony Wings +1 bat/volley
            VandalierEvolution,       // Peachone + Ebony Wings → 15 dmg, 0.7s CD, fires both directions
        }
        readonly UpgradeType[] _currentChoices = new UpgradeType[3];
        readonly TMP_Text[]    _btnLabels       = new TMP_Text[3];

        static readonly (UpgradeType type, string label)[] k_WeaponUpgrades =
        {
            (UpgradeType.WandAmount,       "Magic Wand +1 shot\nFire an extra Wand projectile"),
            (UpgradeType.KnifeAmount,      "Knife +1 blade\nThrow an extra Knife per volley"),
            (UpgradeType.FireAmount,       "Fire Wand +1 flame\nLaunch an extra fireball per burst"),
            (UpgradeType.LightningAmount,  "Lightning Ring +1 strike\nHit an extra enemy per activation"),
            (UpgradeType.WhipAmount,       "Whip +1 arc\nSwing an extra Whip in a new direction"),
            (UpgradeType.AxeAmount,        "Axe +1 blade\nThrow an extra Axe per volley"),
            (UpgradeType.HolyWaterAmount,  "Holy Water +1 flask\nThrow an extra flask per volley"),
            (UpgradeType.BoneAmount,       "Bone +1 bone\nFire an extra bouncing Bone per volley"),
            (UpgradeType.RunetracerAmount,  "Runetracer +1 tracer\nFire an extra bouncing Runetracer"),
            (UpgradeType.GattiAmariAmount, "Gatti Amari +1 cat\nSummon an extra wandering cat"),
        };
        static readonly (UpgradeType type, string label)[] k_PassiveUpgrades =
        {
            (UpgradeType.Spinach,       "Spinach\n+10% Might (weapon damage)"),
            (UpgradeType.Pummarola,     "Pummarola\n+0.2 HP/s regen"),
            (UpgradeType.Armor,         "Armor\n+1 flat damage reduction"),
            (UpgradeType.EmptyTome,     "Empty Tome\n-8% weapon cooldown"),
            (UpgradeType.Crown,         "Crown\n+8% XP gain"),
            (UpgradeType.Clover,        "Clover\n+10% Luck (better drops)"),
            (UpgradeType.Bracer,        "Bracer\n+10% projectile speed"),
            (UpgradeType.HollowHeart,   "Hollow Heart\n+10% Max HP"),
            (UpgradeType.Duplicator,    "Duplicator\n+1 Amount to all weapons"),
            (UpgradeType.Candelabrador, "Candelabrador\n+10% Area (weapon range/radius)"),
            (UpgradeType.Spellbinder,   "Spellbinder\n+10% Duration (effect lifetimes)"),
            (UpgradeType.Attractorb,    "Attractorb\n+30% XP magnet radius"),
            (UpgradeType.Wings,         "Wings\n+10% movement speed"),
            (UpgradeType.StoneMask,  "Stone Mask\n+10% gold earnings"),
            (UpgradeType.Tiragisu,   "Tiragisú\n+1 Revival (auto-revive stock)"),
            (UpgradeType.SilverRing,    "Silver Ring\n+5% Duration and Area"),
            (UpgradeType.GoldRing,      "Gold Ring\n+5% Curse (harder but more rewarding)"),
            (UpgradeType.SkullOManiac,  "Skull O'Maniac\n+10% Curse (enemies hit harder, drop more XP)"),
            (UpgradeType.MetaglioLeft,  "Metaglio Left\n+0.1 HP/s Regen, +5% Max HP"),
            (UpgradeType.MetaglioRight, "Metaglio Right\n+5% Curse (dark power, harder enemies)"),
        };

        static readonly (UpgradeType type, string label)[] k_WeaponUpgradesExtra =
        {
            (UpgradeType.PhieraAmount,      "Phiera +1 bullet\nFire an extra bullet in each of the 4 directions"),
            (UpgradeType.EightAmount,       "Eight +1 bullet\nFire an extra bullet in each of the 4 diagonal directions"),
            (UpgradeType.PeachoneAmount,    "Peachone +1 egg\nFire an extra rotating egg per volley"),
            (UpgradeType.EbonyWingsAmount,  "Ebony Wings +1 bat\nFire an extra rotating bat per volley"),
        };

        // Gold display (created programmatically)
        EntityQuery _sharedGoldQuery;
        TMP_Text    _goldText;

        // Revive stocks display (one label per player panel, created programmatically)
        readonly TMP_Text[] _stocksTexts = new TMP_Text[4];

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
            CreateReviveStocksTexts();
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

            var entities    = _playerQuery.ToEntityArray(Allocator.Temp);
            var indices     = _playerQuery.ToComponentDataArray<PlayerIndex>(Allocator.Temp);
            var stats       = _playerQuery.ToComponentDataArray<PlayerStats>(Allocator.Temp);
            var healths     = _playerQuery.ToComponentDataArray<Health>(Allocator.Temp);
            int playerCount = indices.Length;

            for (int i = 0; i < playerCount; i++)
            {
                int slot = indices[i].Value;
                if (slot < 0 || slot >= 4) continue;
                seen[slot] = true;

                int stocks = 0;
                if (world.EntityManager.HasComponent<ReviveStocks>(entities[i]))
                    stocks = world.EntityManager.GetComponentData<ReviveStocks>(entities[i]).Count;

                UpdatePanel(slot, healths[i], stats[i], stocks);
            }

            entities.Dispose();
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

        void UpdatePanel(int slot, Health health, PlayerStats stats, int reviveStocks = 0)
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

            // Revive stocks — show "☠×N" when N > 0 (only Krochi starts with stocks)
            if (_stocksTexts[slot] != null)
            {
                if (reviveStocks > 0)
                {
                    _stocksTexts[slot].text = $"\u2620\u00d7{reviveStocks}";
                    _stocksTexts[slot].gameObject.SetActive(true);
                }
                else
                {
                    _stocksTexts[slot].gameObject.SetActive(false);
                }
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
            SaveRunProgress();
            if (gameOverPanel == null) return;
            gameOverPanel.SetActive(true);

            if (gameOverTimeText != null)
            {
                int total = Mathf.FloorToInt(_elapsedTime);
                gameOverTimeText.text = $"Survived  {total / 60:00}:{total % 60:00}";
            }

            BuildScoreScreen(gameOverPanel, victory: false);
        }

        void TriggerVictory()
        {
            _victory = true;
            Time.timeScale = 0f;
            SaveRunProgress();

            if (gameOverPanel == null) return;
            gameOverPanel.SetActive(true);

            if (gameOverTimeText != null)
                gameOverTimeText.text = "YOU SURVIVED 30 MINUTES!";

            BuildScoreScreen(gameOverPanel, victory: true);

            var bg = gameOverPanel.GetComponent<UnityEngine.UI.Image>();
            if (bg != null) bg.color = new Color(0.05f, 0.25f, 0.05f, 0.92f);
        }

        /// <summary>Reads ECS run stats and persists them to PlayerPrefs via PersistentProgress.</summary>
        void SaveRunProgress()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;

            // Gather team stats from SharedGold singleton
            int kills = 0, gold = 0;
            using var gq  = em.CreateEntityQuery(ComponentType.ReadOnly<SharedGold>());
            if (gq.CalculateEntityCount() > 0)
            {
                using var arr = gq.ToComponentDataArray<SharedGold>(Allocator.Temp);
                kills = arr[0].EnemiesKilled;
                gold  = arr[0].Total;
            }

            // Best level across all active players
            int maxLevel = 0;
            using var pq = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerStats>(),
                ComponentType.ReadOnly<PlayerTag>());
            if (pq.CalculateEntityCount() > 0)
            {
                using var stats = pq.ToComponentDataArray<PlayerStats>(Allocator.Temp);
                foreach (var s in stats)
                    if (s.Level > maxLevel) maxLevel = s.Level;
            }

            PersistentProgress.SaveRunStats(kills, gold, _elapsedTime, maxLevel);
        }

        /// <summary>
        /// Populates the game-over / victory panel with per-player rows + team stats.
        /// Layout (top-to-bottom from panel center):
        ///   y=-40  Per-player rows: "P1 — CharName  Lv X"
        ///   …      (one row per filled slot, spaced 38 px)
        ///   gap    Divider line
        ///   …      Team stats: Enemies Killed / Gold Earned
        /// </summary>
        void BuildScoreScreen(GameObject panel, bool victory)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;

            // --- Gather per-player level data (indexed by PlayerIndex.Value 0-3) ---
            var playerLevels = new int[4];
            using (var pq = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<PlayerIndex>(),
                ComponentType.ReadOnly<PlayerStats>()))
            {
                using var pEntities = pq.ToEntityArray(Unity.Collections.Allocator.Temp);
                for (int i = 0; i < pEntities.Length; i++)
                {
                    var idx   = em.GetComponentData<PlayerIndex>(pEntities[i]);
                    var stats = em.GetComponentData<PlayerStats>(pEntities[i]);
                    if (idx.Value < 4) playerLevels[idx.Value] = stats.Level;
                }
            }

            // --- Per-player rows ---
            float yOffset = -40f;
            const float rowStep = 38f;

            var session = VampireSurvivors.Menu.GameSession.Instance;
            for (int i = 0; i < 4; i++)
            {
                bool filled = session != null && session.Slots[i].Filled;
                if (!filled && playerLevels[i] == 0) continue;

                string charId      = session != null ? session.Slots[i].CharacterId : "";
                string charDisplay = characterRegistry != null
                    ? characterRegistry.GetDisplayName(charId)
                    : (string.IsNullOrEmpty(charId) ? $"P{i + 1}"
                       : char.ToUpper(charId[0]) + (charId.Length > 1 ? charId[1..] : ""));
                int lv = playerLevels[i] > 0 ? playerLevels[i] : 1;

                AddStatLine(panel,
                    $"P{i + 1}  —  {charDisplay,-14}  Lv {lv,2}",
                    yOffset,
                    fontSize: 20f,
                    color: victory ? new Color(0.6f, 1f, 0.6f) : new Color(0.85f, 0.85f, 1f));
                yOffset -= rowStep;
            }

            // Gap between player rows and team stats
            yOffset -= 12f;

            // --- Team stats ---
            using var gq = em.CreateEntityQuery(ComponentType.ReadOnly<SharedGold>());
            if (gq.CalculateEntityCount() > 0)
            {
                using var arr = gq.ToComponentDataArray<SharedGold>(Unity.Collections.Allocator.Temp);
                var gs = arr[0];
                AddStatLine(panel, $"Enemies Killed    {gs.EnemiesKilled}", yOffset);
                yOffset -= rowStep;
                AddStatLine(panel, $"Gold Earned       {gs.Total}", yOffset);
            }
        }

        static void AddStatLine(GameObject panel, string text, float yOffset,
                                float fontSize = 22f, Color? color = null)
        {
            var go  = new GameObject("StatLine");
            go.transform.SetParent(panel.transform, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(460f, 36f);
            rt.anchoredPosition = new Vector2(0f, yOffset);
            var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.color     = color ?? Color.white;
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

        // ─── Revive Stocks (per panel) ──────────────────────────────────────

        void CreateReviveStocksTexts()
        {
            for (int i = 0; i < 4; i++)
            {
                if (panelRoots[i] == null) continue;

                var go = new GameObject("ReviveStocksText");
                go.transform.SetParent(panelRoots[i].transform, false);
                var rt = go.AddComponent<RectTransform>();
                // Anchor to bottom-right corner of the panel
                rt.anchorMin        = new Vector2(1f, 0f);
                rt.anchorMax        = new Vector2(1f, 0f);
                rt.pivot            = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-4f, 4f);
                rt.sizeDelta        = new Vector2(70f, 22f);

                _stocksTexts[i] = go.AddComponent<TextMeshProUGUI>();
                _stocksTexts[i].fontSize  = 16;
                _stocksTexts[i].alignment = TextAlignmentOptions.Right;
                _stocksTexts[i].color     = new Color(0.9f, 0.3f, 0.3f);
                go.SetActive(false);
            }
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

            // Build pool: all passives (excluding capped/irrelevant ones) + weapon upgrades
            var playerStatsPre = em.GetComponentData<PlayerStats>(_pendingUpgradeEntity);
            var e0             = _pendingUpgradeEntity;

            // ── Weapon-presence helpers (used to gate passives) ──────────────
            // Projectile weapons: fire individual bullets/projectiles from source
            bool hasProjectileWeapon =
                em.HasComponent<MagicWandState>(e0)   || em.HasComponent<KnifeState>(e0)     ||
                em.HasComponent<FireWandState>(e0)    || em.HasComponent<LightningRingState>(e0) ||
                em.HasComponent<CrossState>(e0)       || em.HasComponent<AxeState>(e0)        ||
                em.HasComponent<HolyWaterState>(e0)   || em.HasComponent<RunetracerState>(e0) ||
                em.HasComponent<BoneState>(e0)        || em.HasComponent<PhieraState>(e0)     ||
                em.HasComponent<EightSparrowState>(e0)|| em.HasComponent<PeachoneState>(e0)   ||
                em.HasComponent<EbonyWingsState>(e0);

            // Area weapons: deal damage via radius/zone
            bool hasAreaWeapon =
                em.HasComponent<WeaponState>(e0)      || em.HasComponent<GarlicState>(e0)     ||
                em.HasComponent<KingBibleState>(e0)   || em.HasComponent<HolyWaterState>(e0)  ||
                em.HasComponent<GattiAmariState>(e0)  || em.HasComponent<SongOfManaState>(e0) ||
                em.HasComponent<LaurelState>(e0);

            // Duration weapons: have a meaningful lifetime/duration stat
            bool hasDurationWeapon =
                em.HasComponent<HolyWaterState>(e0)   || em.HasComponent<KingBibleState>(e0)  ||
                em.HasComponent<GattiAmariState>(e0)  || em.HasComponent<SongOfManaState>(e0) ||
                em.HasComponent<LaurelState>(e0);

            var pool = new System.Collections.Generic.List<(UpgradeType type, string label)>();
            foreach (var (t, l) in k_PassiveUpgrades)
            {
                // Filter out Metaglio at max stacks (9)
                if (t == UpgradeType.MetaglioLeft  && playerStatsPre.MetaglioLeftStacks  >= 9) continue;
                if (t == UpgradeType.MetaglioRight && playerStatsPre.MetaglioRightStacks >= 9) continue;

                // Gate weapon-synergy passives — only offer if player has a weapon that benefits
                if (t == UpgradeType.Bracer       && !hasProjectileWeapon) continue;
                if (t == UpgradeType.Candelabrador && !hasAreaWeapon)       continue;
                if (t == UpgradeType.Spellbinder  && !hasDurationWeapon)   continue;

                // SilverRing / GoldRing — evolution gate for Clock Lancet; require ClockLancet
                if (t == UpgradeType.SilverRing && !em.HasComponent<ClockLancetState>(e0)) continue;
                if (t == UpgradeType.GoldRing   && !em.HasComponent<ClockLancetState>(e0)) continue;

                pool.Add((t, l));
            }

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
                            var fw2 = em.GetComponentData<FireWandState>(_pendingUpgradeEntity);
                            curAmt = fw2.Amount;
                            canAdd = curAmt < 5 && !fw2.IsEvolved && !fw2.IsHellfire;
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
                            var axe2 = em.GetComponentData<AxeState>(_pendingUpgradeEntity);
                            curAmt = Unity.Mathematics.math.max(1, axe2.Amount);
                            canAdd = curAmt < 5 && !axe2.IsEvolved;
                        }
                        break;
                    case UpgradeType.HolyWaterAmount:
                        if (em.HasComponent<HolyWaterState>(_pendingUpgradeEntity))
                        {
                            var hw2 = em.GetComponentData<HolyWaterState>(_pendingUpgradeEntity);
                            curAmt = Unity.Mathematics.math.max(1, hw2.Amount);
                            canAdd = curAmt < 5 && !hw2.IsEvolved;
                        }
                        break;
                    case UpgradeType.BoneAmount:
                        if (em.HasComponent<BoneState>(_pendingUpgradeEntity))
                        {
                            curAmt = Unity.Mathematics.math.max(1, em.GetComponentData<BoneState>(_pendingUpgradeEntity).Amount);
                            canAdd = curAmt < 5;
                        }
                        break;
                    case UpgradeType.RunetracerAmount:
                        if (em.HasComponent<RunetracerState>(_pendingUpgradeEntity))
                        {
                            var rt2 = em.GetComponentData<RunetracerState>(_pendingUpgradeEntity);
                            curAmt = Unity.Mathematics.math.max(1, rt2.Amount);
                            canAdd = curAmt < 5 && !rt2.IsEvolved;
                        }
                        break;
                    case UpgradeType.GattiAmariAmount:
                        if (em.HasComponent<GattiAmariState>(_pendingUpgradeEntity))
                        {
                            var ga2 = em.GetComponentData<GattiAmariState>(_pendingUpgradeEntity);
                            curAmt = Unity.Mathematics.math.max(1, ga2.Amount);
                            canAdd = curAmt < 3 && !ga2.IsEvolved;
                        }
                        break;
                }
                if (canAdd) pool.Add((type, label + $"  ({curAmt}→{curAmt + 1})"));
            }

            // ── Extra weapon upgrades (Phiera, Eight, etc.) ──────────────────
            foreach (var (type, label) in k_WeaponUpgradesExtra)
            {
                bool canAdd = false;
                int  curAmt = 1;
                switch (type)
                {
                    case UpgradeType.PhieraAmount:
                        if (em.HasComponent<PhieraState>(_pendingUpgradeEntity))
                        {
                            var ph2 = em.GetComponentData<PhieraState>(_pendingUpgradeEntity);
                            curAmt = Unity.Mathematics.math.max(1, ph2.Amount);
                            canAdd = curAmt < 3 && !ph2.IsEvolved;
                        }
                        break;
                    case UpgradeType.EightAmount:
                        if (em.HasComponent<EightSparrowState>(_pendingUpgradeEntity))
                        {
                            var es2 = em.GetComponentData<EightSparrowState>(_pendingUpgradeEntity);
                            curAmt = Unity.Mathematics.math.max(1, es2.Amount);
                            canAdd = curAmt < 3 && !es2.IsEvolved;
                        }
                        break;
                    case UpgradeType.PeachoneAmount:
                        if (em.HasComponent<PeachoneState>(_pendingUpgradeEntity))
                        {
                            var pc = em.GetComponentData<PeachoneState>(_pendingUpgradeEntity);
                            curAmt = Unity.Mathematics.math.max(1, pc.Amount);
                            canAdd = curAmt < 3 && !pc.IsEvolved;
                        }
                        break;
                    case UpgradeType.EbonyWingsAmount:
                        if (em.HasComponent<EbonyWingsState>(_pendingUpgradeEntity))
                        {
                            var ew = em.GetComponentData<EbonyWingsState>(_pendingUpgradeEntity);
                            curAmt = Unity.Mathematics.math.max(1, ew.Amount);
                            canAdd = curAmt < 3 && !ew.IsEvolved;
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

            // O'Sole Meeo = Fire Wand + Candelabrador (AreaMult > 1 means candelabrador was taken)
            if (em.HasComponent<FireWandState>(_pendingUpgradeEntity))
            {
                var fw = em.GetComponentData<FireWandState>(_pendingUpgradeEntity);
                if (!fw.IsEvolved && playerStats.AreaMult > 1.0f)
                    pool.Add((UpgradeType.OsoleMeeoEvolution,
                        "★ O'Sole Meeo\nFire Wand + Candelabrador — 8 fireballs, 20 dmg, 0.4s CD"));
            }

            // Death Spiral = Axe + Candelabrador (AreaMult > 1 means candelabrador was taken)
            if (em.HasComponent<AxeState>(_pendingUpgradeEntity))
            {
                var axe2 = em.GetComponentData<AxeState>(_pendingUpgradeEntity);
                if (!axe2.IsEvolved && playerStats.AreaMult > 1.0f)
                    pool.Add((UpgradeType.DeathSpiralEvolution,
                        "★ Death Spiral\nAxe + Candelabrador — 9 piercing scythes, 60 dmg, 4s CD"));
            }

            // Hellfire = Fire Wand + Spinach (Might > 1 means spinach was taken)
            if (em.HasComponent<FireWandState>(_pendingUpgradeEntity))
            {
                var fw3 = em.GetComponentData<FireWandState>(_pendingUpgradeEntity);
                if (!fw3.IsEvolved && !fw3.IsHellfire && playerStats.Might > 1.0f)
                    pool.Add((UpgradeType.HellfireEvolution,
                        "★ Hellfire\nFire Wand + Spinach — 2 slow pierce meteors, 100 dmg, 3s CD"));
            }

            // La Borra = Holy Water + Attractorb (MagnetRadiusMult > 1 means attractorb was taken)
            if (em.HasComponent<HolyWaterState>(_pendingUpgradeEntity))
            {
                var hw3 = em.GetComponentData<HolyWaterState>(_pendingUpgradeEntity);
                if (!hw3.IsEvolved && playerStats.MagnetRadiusMult > 1.0f)
                    pool.Add((UpgradeType.LaBorraEvolution,
                        "★ La Borra\nHoly Water + Attractorb — puddles follow player, 40 dmg, 4 flasks"));
            }

            // Infinite Corridor = Clock Lancet + Silver Ring + Gold Ring
            if (em.HasComponent<ClockLancetState>(_pendingUpgradeEntity))
            {
                var cl = em.GetComponentData<ClockLancetState>(_pendingUpgradeEntity);
                if (!cl.IsEvolved && playerStats.SilverRingStacks > 0 && playerStats.GoldRingStacks > 0)
                    pool.Add((UpgradeType.InfiniteCorridorEvolution,
                        "★ Infinite Corridor\nClock Lancet + Silver Ring + Gold Ring — halves all enemy HP/s"));
            }

            // Phieraggi = Phiera + Eight The Sparrow + Tiragisú (ReviveStocks present = Tiragisú taken)
            if (em.HasComponent<PhieraState>(_pendingUpgradeEntity) &&
                em.HasComponent<EightSparrowState>(_pendingUpgradeEntity))
            {
                var phEvo = em.GetComponentData<PhieraState>(_pendingUpgradeEntity);
                if (!phEvo.IsEvolved && em.HasComponent<ReviveStocks>(_pendingUpgradeEntity))
                    pool.Add((UpgradeType.PhieraggiEvolution,
                        "★ Phieraggi\nPhiera + Eight + Tiragisú — 8-way rapid fire, 0.35s CD"));
            }

            // Mannajja = Song of Mana + Skull O'Maniac (SkullOManiacStacks > 0)
            if (em.HasComponent<SongOfManaState>(_pendingUpgradeEntity))
            {
                var som = em.GetComponentData<SongOfManaState>(_pendingUpgradeEntity);
                if (!som.IsEvolved && playerStats.SkullOManiacStacks > 0)
                    pool.Add((UpgradeType.MannajjaEvolution,
                        "★ Mannajja\nSong of Mana + Skull O'Maniac — 40 dmg, 6u×8u column, 4.5s CD"));
            }

            // Crimson Shroud = Laurel + Metaglio Left + Metaglio Right
            if (em.HasComponent<LaurelState>(_pendingUpgradeEntity))
            {
                var ls = em.GetComponentData<LaurelState>(_pendingUpgradeEntity);
                if (!ls.IsEvolved &&
                    playerStats.MetaglioLeftStacks  > 0 &&
                    playerStats.MetaglioRightStacks > 0)
                    pool.Add((UpgradeType.CrimsonShroudEvolution,
                        "★ Crimson Shroud\nLaurel + Metaglio L+R — cap dmg at 10, AoE retaliation 2u"));
            }

            // Vandalier = Peachone + Ebony Wings (no passive required — just need both weapons)
            if (em.HasComponent<PeachoneState>(_pendingUpgradeEntity) &&
                em.HasComponent<EbonyWingsState>(_pendingUpgradeEntity))
            {
                var pcEvo = em.GetComponentData<PeachoneState>(_pendingUpgradeEntity);
                if (!pcEvo.IsEvolved)
                    pool.Add((UpgradeType.VandalierEvolution,
                        "★ Vandalier\nPeachone + Ebony Wings — 15 dmg, 0.7s CD, fires both CW+CCW"));
            }

            // Vicious Hunger = Gatti Amari + Stone Mask (GoldMult > 1 means stone mask was taken)
            if (em.HasComponent<GattiAmariState>(_pendingUpgradeEntity))
            {
                var ga = em.GetComponentData<GattiAmariState>(_pendingUpgradeEntity);
                if (!ga.IsEvolved && playerStats.GoldMult > 1.0f)
                    pool.Add((UpgradeType.ViciousHungerEvolution,
                        "★ Vicious Hunger\nGatti Amari + Stone Mask — 2 giant cats, 30 dmg, 1.5u AoE, 7s"));
            }

            // NO FUTURE = Runetracer + Armor (Armor > 0 means armor was taken)
            if (em.HasComponent<RunetracerState>(_pendingUpgradeEntity))
            {
                var rt2 = em.GetComponentData<RunetracerState>(_pendingUpgradeEntity);
                if (!rt2.IsEvolved && playerStats.Armor > 0)
                    pool.Add((UpgradeType.NoFutureEvolution,
                        "★ NO FUTURE\nRunetracer + Armor — 3 tracers, 30 dmg, explode on expire"));
            }

            // Unholy Vespers = King Bible + Spellbinder (DurationMult > 1 means spellbinder was taken)
            if (em.HasComponent<KingBibleState>(_pendingUpgradeEntity))
            {
                var kb = em.GetComponentData<KingBibleState>(_pendingUpgradeEntity);
                if (!kb.IsEvolved && playerStats.DurationMult > 1.0f)
                    pool.Add((UpgradeType.UnholyVespersEvolution,
                        "★ Unholy Vespers\nKing Bible + Spellbinder — 30 dmg, r=1.75u, 3 bibles"));
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
                        if (!w.IsEvolved && !w.IsHellfire) { w.Amount++; em.SetComponentData(_pendingUpgradeEntity, w); }
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
                        if (!w.IsEvolved) { w.Amount = Unity.Mathematics.math.max(1, w.Amount) + 1; em.SetComponentData(_pendingUpgradeEntity, w); }
                    }
                    if (em.HasComponent<HolyWaterState>(_pendingUpgradeEntity))
                    {
                        var w = em.GetComponentData<HolyWaterState>(_pendingUpgradeEntity);
                        if (!w.IsEvolved) { w.Amount = Unity.Mathematics.math.max(1, w.Amount) + 1; em.SetComponentData(_pendingUpgradeEntity, w); }
                    }
                    if (em.HasComponent<BoneState>(_pendingUpgradeEntity))
                    {
                        var w = em.GetComponentData<BoneState>(_pendingUpgradeEntity);
                        w.Amount = Unity.Mathematics.math.max(1, w.Amount) + 1;
                        em.SetComponentData(_pendingUpgradeEntity, w);
                    }
                    if (em.HasComponent<RunetracerState>(_pendingUpgradeEntity))
                    {
                        var w = em.GetComponentData<RunetracerState>(_pendingUpgradeEntity);
                        w.Amount = Unity.Mathematics.math.max(1, w.Amount) + 1;
                        em.SetComponentData(_pendingUpgradeEntity, w);
                    }
                    if (em.HasComponent<GattiAmariState>(_pendingUpgradeEntity))
                    {
                        var w = em.GetComponentData<GattiAmariState>(_pendingUpgradeEntity);
                        if (!w.IsEvolved) { w.Amount = Unity.Mathematics.math.max(1, w.Amount) + 1; em.SetComponentData(_pendingUpgradeEntity, w); }
                    }
                    if (em.HasComponent<PhieraState>(_pendingUpgradeEntity))
                    {
                        var w = em.GetComponentData<PhieraState>(_pendingUpgradeEntity);
                        if (!w.IsEvolved) { w.Amount = Unity.Mathematics.math.max(1, w.Amount) + 1; em.SetComponentData(_pendingUpgradeEntity, w); }
                    }
                    if (em.HasComponent<EightSparrowState>(_pendingUpgradeEntity))
                    {
                        var w = em.GetComponentData<EightSparrowState>(_pendingUpgradeEntity);
                        if (!w.IsEvolved) { w.Amount = Unity.Mathematics.math.max(1, w.Amount) + 1; em.SetComponentData(_pendingUpgradeEntity, w); }
                    }
                    if (em.HasComponent<PeachoneState>(_pendingUpgradeEntity))
                    {
                        var w = em.GetComponentData<PeachoneState>(_pendingUpgradeEntity);
                        if (!w.IsEvolved) { w.Amount = Unity.Mathematics.math.max(1, w.Amount) + 1; em.SetComponentData(_pendingUpgradeEntity, w); }
                    }
                    if (em.HasComponent<EbonyWingsState>(_pendingUpgradeEntity))
                    {
                        var w = em.GetComponentData<EbonyWingsState>(_pendingUpgradeEntity);
                        if (!w.IsEvolved) { w.Amount = Unity.Mathematics.math.max(1, w.Amount) + 1; em.SetComponentData(_pendingUpgradeEntity, w); }
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
                case UpgradeType.BoneAmount:
                    if (em.HasComponent<BoneState>(_pendingUpgradeEntity))
                    {
                        var bone2 = em.GetComponentData<BoneState>(_pendingUpgradeEntity);
                        bone2.Amount = Unity.Mathematics.math.max(1, bone2.Amount) + 1;
                        em.SetComponentData(_pendingUpgradeEntity, bone2);
                        Debug.Log($"[HUDManager] P{pidx} chose Bone +1 — Amount = {bone2.Amount}");
                    }
                    break;
                case UpgradeType.RunetracerAmount:
                    if (em.HasComponent<RunetracerState>(_pendingUpgradeEntity))
                    {
                        var rt2 = em.GetComponentData<RunetracerState>(_pendingUpgradeEntity);
                        rt2.Amount = Unity.Mathematics.math.max(1, rt2.Amount) + 1;
                        em.SetComponentData(_pendingUpgradeEntity, rt2);
                        Debug.Log($"[HUDManager] P{pidx} chose Runetracer +1 — Amount = {rt2.Amount}");
                    }
                    break;
                case UpgradeType.GattiAmariAmount:
                    if (em.HasComponent<GattiAmariState>(_pendingUpgradeEntity))
                    {
                        var gatti = em.GetComponentData<GattiAmariState>(_pendingUpgradeEntity);
                        gatti.Amount = Unity.Mathematics.math.max(1, gatti.Amount) + 1;
                        em.SetComponentData(_pendingUpgradeEntity, gatti);
                        Debug.Log($"[HUDManager] P{pidx} chose Gatti Amari +1 — Amount = {gatti.Amount}");
                    }
                    break;
                case UpgradeType.Candelabrador:
                    stats.AreaMult *= 1.1f;
                    Debug.Log($"[HUDManager] P{pidx} chose Candelabrador — AreaMult = {stats.AreaMult:F3}×");
                    break;
                case UpgradeType.Spellbinder:
                    stats.DurationMult *= 1.1f;
                    Debug.Log($"[HUDManager] P{pidx} chose Spellbinder — DurationMult = {stats.DurationMult:F3}×");
                    break;
                case UpgradeType.Attractorb:
                    stats.MagnetRadiusMult *= 1.3f;
                    Debug.Log($"[HUDManager] P{pidx} chose Attractorb — MagnetRadiusMult = {stats.MagnetRadiusMult:F2}×");
                    break;
                case UpgradeType.Wings:
                    stats.SpeedMult += 0.1f;
                    Debug.Log($"[HUDManager] P{pidx} chose Wings — SpeedMult = {stats.SpeedMult:F2}×");
                    break;
                case UpgradeType.StoneMask:
                    stats.GoldMult += 0.1f;
                    Debug.Log($"[HUDManager] P{pidx} chose Stone Mask — GoldMult = {stats.GoldMult:F2}×");
                    break;
                case UpgradeType.Tiragisu:
                {
                    // Grant +1 ReviveStock — add the component if the player doesn't have it yet
                    if (em.HasComponent<ReviveStocks>(_pendingUpgradeEntity))
                    {
                        var stocks = em.GetComponentData<ReviveStocks>(_pendingUpgradeEntity);
                        stocks.Count++;
                        em.SetComponentData(_pendingUpgradeEntity, stocks);
                        Debug.Log($"[HUDManager] P{pidx} chose Tiragisú — ReviveStocks = {stocks.Count}");
                    }
                    else
                    {
                        em.AddComponentData(_pendingUpgradeEntity, new ReviveStocks { Count = 1 });
                        Debug.Log($"[HUDManager] P{pidx} chose Tiragisú — ReviveStocks = 1 (first revival)");
                    }
                    break;
                }

                case UpgradeType.SilverRing:
                    stats.SilverRingStacks++;
                    stats.DurationMult *= 1.05f;
                    stats.AreaMult     *= 1.05f;
                    Debug.Log($"[HUDManager] P{pidx} chose Silver Ring — Duration×{stats.DurationMult:F3} Area×{stats.AreaMult:F3}");
                    break;
                case UpgradeType.GoldRing:
                    stats.GoldRingStacks++;
                    stats.Curse += 0.05f;
                    Debug.Log($"[HUDManager] P{pidx} chose Gold Ring — Curse = {stats.Curse:F2}");
                    break;
                case UpgradeType.SkullOManiac:
                    stats.SkullOManiacStacks++;
                    stats.Curse += 0.1f;
                    Debug.Log($"[HUDManager] P{pidx} chose Skull O'Maniac — Curse = {stats.Curse:F2} (stacks={stats.SkullOManiacStacks})");
                    break;
                case UpgradeType.InfiniteCorridorEvolution:
                    if (em.HasComponent<ClockLancetState>(_pendingUpgradeEntity))
                    {
                        var cl      = em.GetComponentData<ClockLancetState>(_pendingUpgradeEntity);
                        cl.IsEvolved = true;
                        cl.Cooldown  = 1.0f;  // wiki: 1.0s CD
                        em.SetComponentData(_pendingUpgradeEntity, cl);
                        Debug.Log($"[HUDManager] P{pidx} evolved Clock Lancet → Infinite Corridor");
                    }
                    break;
                case UpgradeType.PhieraAmount:
                    if (em.HasComponent<PhieraState>(_pendingUpgradeEntity))
                    {
                        var ph = em.GetComponentData<PhieraState>(_pendingUpgradeEntity);
                        ph.Amount = Unity.Mathematics.math.max(1, ph.Amount) + 1;
                        em.SetComponentData(_pendingUpgradeEntity, ph);
                        Debug.Log($"[HUDManager] P{pidx} chose Phiera +1 — Amount = {ph.Amount}");
                    }
                    break;
                case UpgradeType.EightAmount:
                    if (em.HasComponent<EightSparrowState>(_pendingUpgradeEntity))
                    {
                        var es = em.GetComponentData<EightSparrowState>(_pendingUpgradeEntity);
                        es.Amount = Unity.Mathematics.math.max(1, es.Amount) + 1;
                        em.SetComponentData(_pendingUpgradeEntity, es);
                        Debug.Log($"[HUDManager] P{pidx} chose Eight +1 — Amount = {es.Amount}");
                    }
                    break;
                case UpgradeType.PeachoneAmount:
                    if (em.HasComponent<PeachoneState>(_pendingUpgradeEntity))
                    {
                        var pc = em.GetComponentData<PeachoneState>(_pendingUpgradeEntity);
                        pc.Amount = Unity.Mathematics.math.max(1, pc.Amount) + 1;
                        em.SetComponentData(_pendingUpgradeEntity, pc);
                        Debug.Log($"[HUDManager] P{pidx} chose Peachone +1 — Amount = {pc.Amount}");
                    }
                    break;
                case UpgradeType.EbonyWingsAmount:
                    if (em.HasComponent<EbonyWingsState>(_pendingUpgradeEntity))
                    {
                        var ew = em.GetComponentData<EbonyWingsState>(_pendingUpgradeEntity);
                        ew.Amount = Unity.Mathematics.math.max(1, ew.Amount) + 1;
                        em.SetComponentData(_pendingUpgradeEntity, ew);
                        Debug.Log($"[HUDManager] P{pidx} chose Ebony Wings +1 — Amount = {ew.Amount}");
                    }
                    break;
                case UpgradeType.VandalierEvolution:
                    if (em.HasComponent<PeachoneState>(_pendingUpgradeEntity) &&
                        em.HasComponent<EbonyWingsState>(_pendingUpgradeEntity))
                    {
                        // Peachone handles all shots (CW + CCW), 15 dmg, 0.7s CD
                        var pcEvo       = em.GetComponentData<PeachoneState>(_pendingUpgradeEntity);
                        pcEvo.IsEvolved = true;
                        pcEvo.Damage    = 15f;   // wiki: +50% dmg
                        pcEvo.Cooldown  = 0.7f;  // wiki: 0.7s CD (half of 1.4s)
                        em.SetComponentData(_pendingUpgradeEntity, pcEvo);
                        var ewEvo       = em.GetComponentData<EbonyWingsState>(_pendingUpgradeEntity);
                        ewEvo.IsEvolved = true;
                        em.SetComponentData(_pendingUpgradeEntity, ewEvo);
                        Debug.Log($"[HUDManager] P{pidx} evolved Peachone+EbonyWings → Vandalier (15 dmg, 0.7s CD, both dirs)");
                    }
                    break;
                case UpgradeType.PhieraggiEvolution:
                    if (em.HasComponent<PhieraState>(_pendingUpgradeEntity) &&
                        em.HasComponent<EightSparrowState>(_pendingUpgradeEntity))
                    {
                        // Phiera fires all 8 directions at 0.35s CD; Eight goes silent
                        var phEvo        = em.GetComponentData<PhieraState>(_pendingUpgradeEntity);
                        phEvo.IsEvolved  = true;
                        phEvo.Cooldown   = 0.35f;  // wiki: Phieraggi 0.35s CD
                        em.SetComponentData(_pendingUpgradeEntity, phEvo);
                        var esEvo        = em.GetComponentData<EightSparrowState>(_pendingUpgradeEntity);
                        esEvo.IsEvolved  = true;
                        em.SetComponentData(_pendingUpgradeEntity, esEvo);
                        Debug.Log($"[HUDManager] P{pidx} evolved Phiera+Eight → Phieraggi (8-way, 0.35s CD)");
                    }
                    break;

                case UpgradeType.ViciousHungerEvolution:
                    if (em.HasComponent<GattiAmariState>(_pendingUpgradeEntity))
                    {
                        var ga       = em.GetComponentData<GattiAmariState>(_pendingUpgradeEntity);
                        ga.IsEvolved  = true;
                        ga.Damage     = 30f;   // wiki: 30 dmg base
                        ga.Cooldown   = 8.0f;  // wiki: 8s CD (much slower, fires 2 giants)
                        ga.CatLifetime = 7.0f; // wiki: 7s duration
                        em.SetComponentData(_pendingUpgradeEntity, ga);
                        Debug.Log($"[HUDManager] P{pidx} evolved Gatti Amari → Vicious Hunger");
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

                case UpgradeType.OsoleMeeoEvolution:
                    if (em.HasComponent<FireWandState>(_pendingUpgradeEntity))
                    {
                        var fw       = em.GetComponentData<FireWandState>(_pendingUpgradeEntity);
                        fw.IsEvolved = true;
                        fw.Amount    = 8;
                        fw.Damage    = 20f;
                        fw.Cooldown  = 0.4f; // wiki: same CD, just more fireballs
                        em.SetComponentData(_pendingUpgradeEntity, fw);
                        Debug.Log($"[HUDManager] P{pidx} evolved Fire Wand → O'Sole Meeo");
                    }
                    break;

                case UpgradeType.UnholyVespersEvolution:
                    if (em.HasComponent<KingBibleState>(_pendingUpgradeEntity))
                    {
                        var kb       = em.GetComponentData<KingBibleState>(_pendingUpgradeEntity);
                        kb.IsEvolved = true;
                        kb.Damage    = 30f;
                        kb.Radius    = 1.75f;
                        kb.Count     = 3;
                        kb.Spawned   = false; // triggers re-spawn with new stats
                        em.SetComponentData(_pendingUpgradeEntity, kb);
                        Debug.Log($"[HUDManager] P{pidx} evolved King Bible → Unholy Vespers");
                    }
                    break;

                case UpgradeType.NoFutureEvolution:
                    if (em.HasComponent<RunetracerState>(_pendingUpgradeEntity))
                    {
                        var rt3      = em.GetComponentData<RunetracerState>(_pendingUpgradeEntity);
                        rt3.IsEvolved = true;
                        rt3.Damage    = 30f;          // wiki: +20 over base 10
                        rt3.Speed     = 11f;           // wiki: ~140% of base 8
                        rt3.Amount    = Unity.Mathematics.math.max(rt3.Amount, 3); // wiki: +2 amount (min 3)
                        rt3.Bounces   = 5;             // more bounces for extended lifetime
                        rt3.Cooldown  = 0.35f;         // keep same CD
                        em.SetComponentData(_pendingUpgradeEntity, rt3);
                        Debug.Log($"[HUDManager] P{pidx} evolved Runetracer → NO FUTURE");
                    }
                    break;

                case UpgradeType.DeathSpiralEvolution:
                    if (em.HasComponent<AxeState>(_pendingUpgradeEntity))
                    {
                        var axe3      = em.GetComponentData<AxeState>(_pendingUpgradeEntity);
                        axe3.IsEvolved = true;
                        axe3.Damage    = 60f;    // wiki: 60 dmg
                        axe3.Cooldown  = 4.0f;   // wiki: 4.0s CD (slower, but fires 9 at once)
                        axe3.Gravity   = 0f;     // no gravity needed when evolved
                        em.SetComponentData(_pendingUpgradeEntity, axe3);
                        Debug.Log($"[HUDManager] P{pidx} evolved Axe → Death Spiral");
                    }
                    break;

                case UpgradeType.HellfireEvolution:
                    if (em.HasComponent<FireWandState>(_pendingUpgradeEntity))
                    {
                        var fw4       = em.GetComponentData<FireWandState>(_pendingUpgradeEntity);
                        fw4.IsHellfire = true;
                        fw4.Damage     = 100f;  // wiki: 100 dmg
                        fw4.Speed      = 1.5f;  // wiki: very slow (speed ~1)
                        fw4.Amount     = 2;     // wiki: 2 meteors per volley
                        fw4.MaxRange   = 25f;   // long range since slow
                        fw4.Cooldown   = 3.0f;  // wiki: 3.0s CD
                        em.SetComponentData(_pendingUpgradeEntity, fw4);
                        Debug.Log($"[HUDManager] P{pidx} evolved Fire Wand → Hellfire");
                    }
                    break;

                case UpgradeType.MetaglioLeft:
                {
                    if (stats.MetaglioLeftStacks < 9)
                    {
                        stats.MetaglioLeftStacks++;
                        stats.HpRegen += 0.1f;                               // +0.1 Recovery
                        if (em.HasComponent<Health>(_pendingUpgradeEntity))  // +5% Max HP
                        {
                            var hp    = em.GetComponentData<Health>(_pendingUpgradeEntity);
                            int bonus = Mathf.Max(1, hp.Max / 20); // 5% of current max
                            hp.Max   += bonus;
                            em.SetComponentData(_pendingUpgradeEntity, hp);
                            stats.MaxHpBonus += bonus; // track for display
                            Debug.Log($"[HUDManager] P{pidx} chose Metaglio Left (x{stats.MetaglioLeftStacks}) — HpRegen={stats.HpRegen:F1}/s MaxHp={hp.Max}");
                        }
                    }
                    break;
                }
                case UpgradeType.MetaglioRight:
                    if (stats.MetaglioRightStacks < 9)
                    {
                        stats.MetaglioRightStacks++;
                        stats.Curse += 0.05f;
                        Debug.Log($"[HUDManager] P{pidx} chose Metaglio Right (x{stats.MetaglioRightStacks}) — Curse={stats.Curse:F2}");
                    }
                    break;
                case UpgradeType.CrimsonShroudEvolution:
                    if (em.HasComponent<LaurelState>(_pendingUpgradeEntity))
                    {
                        var ls              = em.GetComponentData<LaurelState>(_pendingUpgradeEntity);
                        ls.IsEvolved        = true;
                        ls.Cooldown         = 8.0f;    // wiki: 8.0s CD (faster than base 10s)
                        ls.MaxDamageCap     = 10;      // wiki: caps incoming damage at 10 per hit
                        ls.RetaliationDamage = 30f;    // AoE explosion on each pulse
                        ls.RetaliationRadius = 2.0f;   // wiki: Area 2
                        em.SetComponentData(_pendingUpgradeEntity, ls);
                        Debug.Log($"[HUDManager] P{pidx} evolved Laurel → Crimson Shroud (dmg cap 10, 2u AoE, 8s CD)");
                    }
                    break;

                case UpgradeType.MannajjaEvolution:
                    if (em.HasComponent<SongOfManaState>(_pendingUpgradeEntity))
                    {
                        var som       = em.GetComponentData<SongOfManaState>(_pendingUpgradeEntity);
                        som.IsEvolved  = true;
                        som.Damage     = 40f;    // wiki: 40 dmg
                        som.Cooldown   = 4.5f;   // wiki: 4.5s CD (slower but massive AoE)
                        som.HalfWidth  = 3.0f;   // full width 6.0u — wiki: +325% area = 4.25× base (≈6u wide)
                        som.HalfHeight = 4.0f;   // full height 8.0u
                        em.SetComponentData(_pendingUpgradeEntity, som);
                        Debug.Log($"[HUDManager] P{pidx} evolved Song of Mana → Mannajja (6u×8u, 40 dmg, 4.5s CD)");
                    }
                    break;

                case UpgradeType.LaBorraEvolution:
                    if (em.HasComponent<HolyWaterState>(_pendingUpgradeEntity))
                    {
                        var hw4          = em.GetComponentData<HolyWaterState>(_pendingUpgradeEntity);
                        hw4.IsEvolved    = true;
                        hw4.Damage       = 40f;   // wiki: 40 dmg/tick
                        hw4.Cooldown     = 4.0f;  // wiki: 4.0s CD
                        hw4.Amount       = 4;     // wiki: 4 puddles per throw
                        hw4.Radius       = 3.0f;  // wiki: 200% area ≈ 2× radius (base 1.5)
                        hw4.PuddleLifetime = 4.0f; // wiki: 4.0s duration
                        em.SetComponentData(_pendingUpgradeEntity, hw4);
                        Debug.Log($"[HUDManager] P{pidx} evolved Holy Water → La Borra");
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
