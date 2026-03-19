using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using VampireSurvivors.Components;

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
        EntityQuery _activePlayerQuery;  // PlayerTag + NOT Downed
        bool        _queryCreated;
        float       _elapsedTime;
        bool        _gameOver;
        bool        _hadPlayers;  // true once at least 1 player was alive

        readonly int[]   _lastLevels     = new int[4];
        readonly float[] _levelUpTimers  = new float[4];

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
            _queryCreated = true;

            if (gameOverPanel != null) gameOverPanel.SetActive(false);
        }

        void OnDisable()
        {
            if (_queryCreated)
            {
                _playerQuery.Dispose();
                _activePlayerQuery.Dispose();
                _queryCreated = false;
            }
        }

        void Update()
        {
            if (_gameOver) return;

            _elapsedTime += Time.deltaTime;
            UpdateTimer();
            TickLevelUpTimers();

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !_queryCreated) return;

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
    }
}
