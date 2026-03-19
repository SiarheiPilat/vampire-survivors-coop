using UnityEngine;

/// <summary>
/// Renders an infinite tiling background by repositioning a 5×5 grid of
/// sprite quads as the camera moves. Each tile is 10×10 world units.
/// Two alternating dark colors create a subtle checkerboard that gives
/// players a visual reference for movement speed.
///
/// Auto-creates itself at scene load via RuntimeInitializeOnLoadMethod.
/// No scene wiring or prefab needed.
/// </summary>
public class InfiniteBackground : MonoBehaviour
{
    const int   GridSize  = 5;         // 5×5 = 25 tiles
    const float TileSize  = 10f;       // world units per tile
    const float ZDepth    = 5f;        // behind all game entities

    // Two alternating tile colours — dark muted greens
    static readonly Color ColA = new Color(0.07f, 0.11f, 0.07f, 1f);
    static readonly Color ColB = new Color(0.10f, 0.15f, 0.10f, 1f);

    SpriteRenderer[,] _tiles;
    Camera            _cam;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("[InfiniteBackground]");
        DontDestroyOnLoad(go);
        go.AddComponent<InfiniteBackground>();
    }

    void Awake()
    {
        // Build a 1×1 white sprite for tinting
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);

        _tiles = new SpriteRenderer[GridSize, GridSize];

        int half = GridSize / 2;

        for (int r = 0; r < GridSize; r++)
        {
            for (int c = 0; c < GridSize; c++)
            {
                var tileGo = new GameObject($"BgTile_{r}_{c}");
                tileGo.transform.SetParent(transform);
                tileGo.transform.localScale = new Vector3(TileSize, TileSize, 1f);
                tileGo.transform.position   = new Vector3(
                    (c - half) * TileSize,
                    (r - half) * TileSize,
                    ZDepth);

                var sr             = tileGo.AddComponent<SpriteRenderer>();
                sr.sprite          = sprite;
                sr.color           = ((r + c) % 2 == 0) ? ColA : ColB;
                sr.sortingOrder    = -100;
                _tiles[r, c]       = sr;
            }
        }
    }

    void LateUpdate()
    {
        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam == null) return;
        }

        float camX = _cam.transform.position.x;
        float camY = _cam.transform.position.y;

        // Snap to the nearest tile-grid origin around the camera
        float snapX = Mathf.Round(camX / TileSize) * TileSize;
        float snapY = Mathf.Round(camY / TileSize) * TileSize;

        int half = GridSize / 2;

        for (int r = 0; r < GridSize; r++)
        {
            for (int c = 0; c < GridSize; c++)
            {
                _tiles[r, c].transform.position = new Vector3(
                    snapX + (c - half) * TileSize,
                    snapY + (r - half) * TileSize,
                    ZDepth);
            }
        }
    }
}
