using System.Collections;
using TMPro;
using UnityEngine;
using VampireSurvivors.Menu;

/// <summary>
/// Displays a large centered stage name overlay at the start of a game session.
/// Fades in (0.4s), holds (1.6s), fades out (0.5s). Total ~2.5s.
///
/// Usage: GameSceneBootstrap calls StageBanner.Show(displayName) once.
/// Auto-creates its own Canvas + TMP_Text; no scene wiring needed.
/// </summary>
public class StageBanner : MonoBehaviour
{
    static StageBanner _instance;

    Canvas      _canvas;
    TMP_Text    _label;
    Coroutine   _anim;

    const float FadeInTime  = 0.4f;
    const float HoldTime    = 1.6f;
    const float FadeOutTime = 0.5f;

    /// <summary>
    /// Called by GameSceneBootstrap once the stage name is known.
    /// Creates the banner if it doesn't exist yet.
    /// </summary>
    public static void Show(string stageName)
    {
        if (_instance == null)
        {
            var go = new GameObject("[StageBanner]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<StageBanner>();
            _instance.Build();
        }

        _instance.Play(stageName);
    }

    void Build()
    {
        // Screen-space overlay canvas
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode    = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder  = 200;  // above pause menu (100), damage numbers (50)

        gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // TMP label — centred, large, semi-transparent shadow style
        var labelGo = new GameObject("BannerLabel");
        labelGo.transform.SetParent(_canvas.transform, false);

        _label = labelGo.AddComponent<TMP_Text>() as TMP_Text;
        if (_label == null)
            _label = labelGo.AddComponent<TextMeshProUGUI>();

        var rt          = labelGo.GetComponent<RectTransform>();
        rt.anchorMin    = new Vector2(0f, 0.4f);
        rt.anchorMax    = new Vector2(1f, 0.65f);
        rt.offsetMin    = Vector2.zero;
        rt.offsetMax    = Vector2.zero;

        _label.alignment        = TextAlignmentOptions.Center;
        _label.fontSize         = 64;
        _label.fontStyle        = FontStyles.Bold;
        _label.color            = new Color(1f, 1f, 1f, 0f); // start transparent
    }

    void Play(string stageName)
    {
        if (_anim != null) StopCoroutine(_anim);
        _label.text = stageName;
        _anim = StartCoroutine(AnimateBanner());
    }

    IEnumerator AnimateBanner()
    {
        // Fade in
        yield return Fade(0f, 1f, FadeInTime);

        // Hold
        yield return new WaitForSecondsRealtime(HoldTime);

        // Fade out
        yield return Fade(1f, 0f, FadeOutTime);

        _label.color = new Color(_label.color.r, _label.color.g, _label.color.b, 0f);
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(from, to, t / duration);
            _label.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
