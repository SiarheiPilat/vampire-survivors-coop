using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pause menu overlay. Press Escape in the game scene to toggle.
/// Sets Time.timeScale = 0 while paused — DOTS systems use SystemAPI.Time.DeltaTime
/// which flows through Unity's timeScale, so the entire simulation halts.
///
/// Auto-creates itself via RuntimeInitializeOnLoadMethod. No scene wiring needed.
/// Only active in the game scene (scene index ≥ 3, i.e. 4_SampleScene).
/// </summary>
public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    bool       _isPaused;
    GameObject _panel;
    Canvas     _canvas;

    const int MainMenuSceneIndex = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("[PauseManager]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<PauseManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
        SetVisible(false);
    }

    void BuildUI()
    {
        // Root canvas — overlay, blocks input when visible
        var canvasGo = new GameObject("PauseCanvas");
        canvasGo.transform.SetParent(transform);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode    = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder  = 200; // on top of HUD (which is typically 0-100)
        canvasGo.AddComponent<GraphicRaycaster>();

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Semi-transparent dark overlay
        _panel = new GameObject("PausePanel");
        _panel.transform.SetParent(canvasGo.transform, false);

        var panelImg = _panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.75f);

        var panelRect = _panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        // "PAUSED" title
        var titleGo   = new GameObject("PauseTitle");
        titleGo.transform.SetParent(_panel.transform, false);
        var titleTmp  = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text      = "PAUSED";
        titleTmp.fontSize  = 80;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color     = Color.white;
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin        = new Vector2(0.5f, 0.6f);
        titleRect.anchorMax        = new Vector2(0.5f, 0.6f);
        titleRect.pivot            = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta        = new Vector2(600, 120);

        // Resume button
        AddButton(_panel, "Resume", new Vector2(0f, -60f), OnResumeClicked);

        // Quit to menu button
        AddButton(_panel, "Quit to Menu", new Vector2(0f, -160f), OnQuitClicked);
    }

    void AddButton(GameObject parent, string label, Vector2 offset, UnityEngine.Events.UnityAction onClick)
    {
        var btnGo = new GameObject($"Btn_{label}");
        btnGo.transform.SetParent(parent.transform, false);

        var img = btnGo.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        var btn = btnGo.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        var rect = btnGo.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0.5f);
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta        = new Vector2(320, 70);

        var textGo  = new GameObject("Label");
        textGo.transform.SetParent(btnGo.transform, false);
        var tmp     = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 32;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        var textRect  = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
    }

    void Update()
    {
        // Only allow pause in the game scene
        if (SceneManager.GetActiveScene().buildIndex < 3) return;

        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    public void TogglePause()
    {
        _isPaused = !_isPaused;
        SetVisible(_isPaused);
        Time.timeScale = _isPaused ? 0f : 1f;
    }

    public void Resume()
    {
        if (!_isPaused) return;
        TogglePause();
    }

    void SetVisible(bool visible)
    {
        _panel.SetActive(visible);
    }

    void OnResumeClicked() => Resume();

    void OnQuitClicked()
    {
        Time.timeScale = 1f;
        _isPaused      = false;
        SetVisible(false);
        SceneManager.LoadScene(MainMenuSceneIndex);
    }
}
