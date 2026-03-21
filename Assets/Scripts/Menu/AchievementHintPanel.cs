using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VampireSurvivors.Menu
{
    /// <summary>
    /// Auto-created in the Lobby scene.
    /// Shows a small panel at the bottom of the screen indicating
    /// the player's nearest character unlock goal.
    ///
    /// e.g.  "Next unlock: Mortaccio — Kill 347 more enemies"
    ///       "All characters unlocked!"
    ///
    /// Uses RuntimeInitializeOnLoadMethod so no scene wiring is required.
    /// </summary>
    public class AchievementHintPanel : MonoBehaviour
    {
        static AchievementHintPanel s_Instance;

        TMP_Text _hintText;

        // Character unlock order we scan to find the nearest goal
        static readonly string[] s_UnlockOrder =
        {
            "mortaccio", "dommario", "giovanna", "krochi",
            "pugnala", "yattacavallo", "poppea", "clerici", "bianzi",
        };

        static string DisplayName(string id) => id switch
        {
            "mortaccio"    => "Mortaccio",
            "yattacavallo" => "Yatta Cavallo",
            "krochi"       => "Krochi",
            "dommario"     => "Dommario",
            "giovanna"     => "Giovanna",
            "pugnala"      => "Pugnala",
            "poppea"       => "Poppea",
            "clerici"      => "Clerici",
            "bianzi"       => "Bi-An Zi",
            _              => id,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (!SceneManager.GetActiveScene().name.Contains("Lobby")) return;
            if (s_Instance != null) return;

            var go = new GameObject("AchievementHintPanel");
            DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<AchievementHintPanel>();
        }

        void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            BuildUI();
            RefreshHint();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (s_Instance == this) s_Instance = null;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.name.Contains("Lobby"))
                Destroy(gameObject);
        }

        void BuildUI()
        {
            var canvasGo = new GameObject("AchievementCanvas");
            canvasGo.transform.SetParent(transform);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;

            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode         = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode     = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Semi-transparent dark strip along the bottom
            var panelGo  = new GameObject("HintPanel");
            panelGo.transform.SetParent(canvasGo.transform, false);

            var panelImg = panelGo.AddComponent<UnityEngine.UI.Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.55f);

            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin        = new Vector2(0f, 0f);
            panelRect.anchorMax        = new Vector2(1f, 0f);
            panelRect.pivot            = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta        = new Vector2(0f, 56f);

            var textGo = new GameObject("HintText");
            textGo.transform.SetParent(panelGo.transform, false);

            _hintText           = textGo.AddComponent<TextMeshProUGUI>();
            _hintText.alignment = TextAlignmentOptions.Center;
            _hintText.fontSize  = 22f;
            _hintText.color     = new Color(1f, 0.92f, 0.6f, 1f);
            _hintText.enableWordWrapping = false;

            var textRect           = textGo.GetComponent<RectTransform>();
            textRect.anchorMin     = Vector2.zero;
            textRect.anchorMax     = Vector2.one;
            textRect.offsetMin     = new Vector2(12f, 4f);
            textRect.offsetMax     = new Vector2(-12f, -4f);
        }

        void RefreshHint()
        {
            if (_hintText == null) return;

            string bestId   = null;
            float  bestRatio = -1f;

            foreach (var id in s_UnlockOrder)
            {
                if (PersistentProgress.IsUnlocked(id)) continue;

                float ratio = UnlockRatio(id);
                if (ratio > bestRatio)
                {
                    bestRatio = ratio;
                    bestId    = id;
                }
            }

            if (bestId == null)
            {
                _hintText.text = "All characters unlocked!";
                return;
            }

            string hint    = PersistentProgress.UnlockHint(bestId);
            _hintText.text = $"Next unlock: <b>{DisplayName(bestId)}</b> \u2014 {hint}";
        }

        static float UnlockRatio(string id) => id switch
        {
            "mortaccio"    => Mathf.Clamp01(PersistentProgress.TotalKills     / 500f),
            "yattacavallo" => Mathf.Clamp01(PersistentProgress.TotalKills     / 2000f),
            "krochi"       => Mathf.Clamp01(PersistentProgress.BestSurviveMin / 10f),
            "dommario"     => Mathf.Clamp01(PersistentProgress.TotalGold      / 1000f),
            "giovanna"     => Mathf.Clamp01(PersistentProgress.BestLevel      / 10f),
            "pugnala"      => Mathf.Clamp01(PersistentProgress.BestLevel      / 15f),
            "poppea"       => Mathf.Clamp01(PersistentProgress.BestSurviveMin / 20f),
            "clerici"      => Mathf.Clamp01(PersistentProgress.BestSurviveMin / 25f),
            "bianzi"       => Mathf.Clamp01(PersistentProgress.OrologionCount / 5f),
            _              => 1f,
        };
    }
}
