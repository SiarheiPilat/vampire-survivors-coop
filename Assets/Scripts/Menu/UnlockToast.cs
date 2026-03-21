using System.Collections;
using TMPro;
using UnityEngine;

namespace VampireSurvivors.Menu
{
/// <summary>
/// Displays a brief "UNLOCKED: [Character Name]!" overlay whenever a character
/// is freshly unlocked (newly meets PersistentProgress.IsUnlocked conditions).
///
/// LobbyManager calls UnlockToast.CheckAndShow() on entry.
/// The toast shows one character at a time; queues extras if multiple unlock at once.
///
/// Auto-creates its own Canvas — no scene wiring needed.
/// </summary>
public class UnlockToast : MonoBehaviour
{
    static UnlockToast _instance;

    Canvas   _canvas;
    TMP_Text _label;
    TMP_Text _subLabel;

    readonly System.Collections.Generic.Queue<string> _queue = new();
    bool _playing;

    const float FadeInTime  = 0.3f;
    const float HoldTime    = 2.0f;
    const float FadeOutTime = 0.4f;

    // Persist which characters were unlocked last time so we can detect *new* unlocks
    const string PrefKey = "unlocked_chars_v1";

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Call from LobbyManager.Start() or after returning from a run.
    /// Compares current unlock state against saved state; shows a toast per new unlock.
    /// </summary>
    public static void CheckAndShow(CharacterRegistry registry)
    {
        if (registry == null) return;

        var prev    = LoadUnlocked();
        int newCount = 0;

        for (int i = 0; i < registry.Count; i++)
        {
            string id = registry.IdAt(i);
            if (!PersistentProgress.IsUnlocked(id)) continue; // still locked
            if (prev.Contains(id))                  continue; // already knew about this

            // Newly unlocked!
            newCount++;
            string displayName = registry.GetDisplayName(id);
            Enqueue($"UNLOCKED: {displayName}!");
        }

        if (newCount > 0) SaveUnlocked(registry);
    }

    // ── Private ─────────────────────────────────────────────────────────────

    static void Enqueue(string message)
    {
        EnsureInstance();
        _instance._queue.Enqueue(message);
        if (!_instance._playing)
            _instance.StartCoroutine(_instance.PlayQueue());
    }

    static void EnsureInstance()
    {
        if (_instance != null) return;
        var go = new GameObject("[UnlockToast]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<UnlockToast>();
        _instance.Build();
    }

    void Build()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 210;   // above StageBanner (200)

        gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();

        // Main label
        var mainGo      = new GameObject("ToastLabel");
        mainGo.transform.SetParent(_canvas.transform, false);
        _label          = mainGo.AddComponent<TextMeshProUGUI>();
        var rt          = mainGo.GetComponent<RectTransform>();
        rt.anchorMin    = new Vector2(0.1f, 0.65f);
        rt.anchorMax    = new Vector2(0.9f, 0.80f);
        rt.offsetMin    = rt.offsetMax = Vector2.zero;
        _label.alignment  = TextAlignmentOptions.Center;
        _label.fontSize   = 48;
        _label.fontStyle  = FontStyles.Bold;
        _label.color      = new Color(1f, 0.92f, 0.3f, 0f); // golden, start transparent

        // Sub-label (hint text below, smaller)
        var subGo       = new GameObject("ToastSub");
        subGo.transform.SetParent(_canvas.transform, false);
        _subLabel       = subGo.AddComponent<TextMeshProUGUI>();
        var rt2         = subGo.GetComponent<RectTransform>();
        rt2.anchorMin   = new Vector2(0.1f, 0.60f);
        rt2.anchorMax   = new Vector2(0.9f, 0.67f);
        rt2.offsetMin   = rt2.offsetMax = Vector2.zero;
        _subLabel.alignment = TextAlignmentOptions.Center;
        _subLabel.fontSize  = 22;
        _subLabel.color     = new Color(1f, 1f, 1f, 0f);
    }

    IEnumerator PlayQueue()
    {
        _playing = true;
        while (_queue.Count > 0)
        {
            string msg = _queue.Dequeue();
            _label.text    = msg;
            _subLabel.text = "New character available in the lobby!";

            yield return Fade(0f, 1f, FadeInTime);
            yield return new WaitForSecondsRealtime(HoldTime);
            yield return Fade(1f, 0f, FadeOutTime);

            // Brief gap between multiple toasts
            if (_queue.Count > 0)
                yield return new WaitForSecondsRealtime(0.3f);
        }
        _playing = false;
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a   = Mathf.Lerp(from, to, t / duration);
            _label.color    = new Color(1f, 0.92f, 0.3f, a);
            _subLabel.color = new Color(1f, 1f, 1f, a);
            yield return null;
        }
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ── PlayerPrefs persistence ──────────────────────────────────────────────

    static System.Collections.Generic.HashSet<string> LoadUnlocked()
    {
        var result = new System.Collections.Generic.HashSet<string>();
        string raw = PlayerPrefs.GetString(PrefKey, "");
        if (!string.IsNullOrEmpty(raw))
            foreach (var id in raw.Split(','))
                if (!string.IsNullOrEmpty(id)) result.Add(id);
        return result;
    }

    static void SaveUnlocked(CharacterRegistry registry)
    {
        var ids = new System.Collections.Generic.List<string>();
        for (int i = 0; i < registry.Count; i++)
        {
            string id = registry.IdAt(i);
            if (PersistentProgress.IsUnlocked(id)) ids.Add(id);
        }
        PlayerPrefs.SetString(PrefKey, string.Join(",", ids));
        PlayerPrefs.Save();
    }
}
} // namespace VampireSurvivors.Menu
