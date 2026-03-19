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
        bool        _hadPlayers;  // true once at least 1 player was alive

        readonly int[]   _lastLevels     = new int[4];
        readonly float[] _levelUpTimers  = new float[4];

        // Upgrade-choice panel (created programmatically)
        GameObject _upgradePanel;
        TMP_Text   _upgradeTitle;
        bool       _upgradeShowing;
        Entity     _pendingUpgradeEntity;

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
            if (_gameOver) return;

            _elapsedTime += Time.unscaledDeltaTime; // unscaled so timer advances while paused
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
        }

        void TriggerGameOver()
        {
            _gameOver = true;
            if (gameOverPanel != null)  gameOverPanel.SetActive(true);
            if (gameOverTimeText != null)
            {
                int total = Mathf.FloorToInt(_elapsedTime);
                gameOverTimeText.text = $"Survived  {total / 60:00}:{total % 60:00}";
            }
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
            titleRt.anchoredPosition = new Vector2(0f, 140f);
            titleRt.sizeDelta        = new Vector2(700f, 60f);
            _upgradeTitle = titleGO.AddComponent<TextMeshProUGUI>();
            _upgradeTitle.fontSize  = 34;
            _upgradeTitle.alignment = TextAlignmentOptions.Center;
            _upgradeTitle.color     = new Color(1f, 0.95f, 0.1f);

            // Five upgrade buttons
            string[] labels = {
                "Spinach\n+10% Might (weapon damage)",
                "Pummarola\n+0.2 HP/s regen",
                "Armor\n+1 flat damage reduction",
                "Empty Tome\n-8% weapon cooldown",
                "Crown\n+8% XP gain"
            };
            float[] yPos = { 95f, 30f, -35f, -100f, -165f };
            for (int i = 0; i < 5; i++)
            {
                int capturedIdx = i;

                var btnGO = new GameObject($"UpgradeBtn{i}");
                btnGO.transform.SetParent(_upgradePanel.transform, false);
                var btnRt = btnGO.AddComponent<RectTransform>();
                btnRt.anchoredPosition = new Vector2(0f, yPos[i]);
                btnRt.sizeDelta        = new Vector2(480f, 75f);

                var btnImg = btnGO.AddComponent<Image>();
                btnImg.color = new Color(0.15f, 0.15f, 0.45f, 1f);

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
                var lbl = lblGO.AddComponent<TextMeshProUGUI>();
                lbl.text      = labels[i];
                lbl.fontSize  = 22;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.color     = Color.white;
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
            _upgradePanel.SetActive(true);
            Time.timeScale = 0f;
        }

        void ApplyUpgrade(World world, int choiceIndex)
        {
            if (_pendingUpgradeEntity == Entity.Null) return;
            if (!world.EntityManager.Exists(_pendingUpgradeEntity)) return;
            if (!world.EntityManager.HasComponent<PlayerStats>(_pendingUpgradeEntity)) return;

            var stats = world.EntityManager.GetComponentData<PlayerStats>(_pendingUpgradeEntity);
            int pidx  = world.EntityManager.GetComponentData<PlayerIndex>(_pendingUpgradeEntity).Value;

            switch (choiceIndex)
            {
                case 0:
                    stats.Might += 0.1f;
                    Debug.Log($"[HUDManager] P{pidx} chose Spinach — Might = {stats.Might:F1}x");
                    break;
                case 1:
                    stats.HpRegen += 0.2f;
                    Debug.Log($"[HUDManager] P{pidx} chose Pummarola — HpRegen = {stats.HpRegen:F1}/s");
                    break;
                case 2:
                    stats.Armor += 1;
                    Debug.Log($"[HUDManager] P{pidx} chose Armor — Armor = {stats.Armor}");
                    break;
                case 3:
                    stats.CooldownMult = Mathf.Max(0.5f, stats.CooldownMult * 0.92f);
                    Debug.Log($"[HUDManager] P{pidx} chose Empty Tome — CooldownMult = {stats.CooldownMult:F3}×");
                    break;
                case 4:
                    stats.XpMult *= 1.08f;
                    Debug.Log($"[HUDManager] P{pidx} chose Crown — XpMult = {stats.XpMult:F3}×");
                    break;
            }

            world.EntityManager.SetComponentData(_pendingUpgradeEntity, stats);
            world.EntityManager.RemoveComponent<UpgradeChoicePending>(_pendingUpgradeEntity);
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
