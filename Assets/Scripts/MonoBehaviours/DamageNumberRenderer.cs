using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Pooled world-space damage number display.
/// Auto-creates itself at scene load via RuntimeInitializeOnLoadMethod — no scene wiring needed.
///
/// Pool: 64 TextMeshPro world-space labels.
/// Each label floats 1.5 units upward over 0.7 s and fades out.
/// Color: white (≤10 dmg), yellow (≤30 dmg), orange (>30 dmg).
/// </summary>
public class DamageNumberRenderer : MonoBehaviour
{
    public static DamageNumberRenderer Instance { get; private set; }

    const int   PoolSize      = 64;
    const float FloatDistance = 1.5f;
    const float Duration      = 0.7f;

    readonly Queue<TextMeshPro> _pool = new Queue<TextMeshPro>(PoolSize);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("[DamageNumberRenderer]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<DamageNumberRenderer>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        for (int i = 0; i < PoolSize; i++)
            _pool.Enqueue(CreateLabel());
    }

    TextMeshPro CreateLabel()
    {
        var go = new GameObject("DmgNum");
        go.transform.SetParent(transform);
        go.SetActive(false);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.fontSize     = 3f;
        tmp.alignment    = TextAlignmentOptions.Center;
        tmp.fontStyle    = FontStyles.Bold;
        tmp.sortingOrder = 10;
        return tmp;
    }

    public void Spawn(Vector3 worldPos, int damage)
    {
        var tmp = _pool.Count > 0 ? _pool.Dequeue() : CreateLabel();
        tmp.text  = damage.ToString();
        tmp.color = DamageColor(damage);
        tmp.gameObject.SetActive(true);
        tmp.transform.position = worldPos;
        StartCoroutine(Animate(tmp, worldPos));
    }

    static Color DamageColor(int damage)
    {
        if (damage > 30) return new Color(1f, 0.45f, 0f);   // orange
        if (damage > 10) return new Color(1f, 0.95f, 0.1f); // yellow
        return Color.white;
    }

    IEnumerator Animate(TextMeshPro tmp, Vector3 startPos)
    {
        float elapsed = 0f;
        while (elapsed < Duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / Duration;
            tmp.transform.position = startPos + Vector3.up * (FloatDistance * t);
            var c = tmp.color;
            c.a       = 1f - t;
            tmp.color = c;
            yield return null;
        }
        tmp.gameObject.SetActive(false);
        _pool.Enqueue(tmp);
    }
}
