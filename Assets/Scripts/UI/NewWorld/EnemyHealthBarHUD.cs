using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phase 8 (Task 8.1): enemy health bars. A screen-space overlay that positions a compact
/// health bar above every live <see cref="EnemyController"/>. Bars auto-pool; dead enemies
/// release theirs. Each bar anchors to its enemy's actual model head (highest renderer bounds)
/// instead of a fixed offset, so small enemies (slime, bat) don't get bars floating far above.
/// Uses the first on-screen enemy's max the moment a bar attaches (public max is deferred to
/// <see cref="EnemyController"/> internals, so we capture the initial CurrentHealth).
/// </summary>
public sealed class EnemyHealthBarHUD : MonoBehaviour
{
    public float MaxBars = 24f;

    private Canvas _canvas;
    private readonly List<EnemyHealthBar> _bars = new List<EnemyHealthBar>();
    private Camera _cam;

    // Scanned enemy pool, refreshed periodically instead of every frame so a fully
    // streamed world (tens of thousands of objects) is never swept per-frame.
    private readonly List<EnemyController> _enemies = new List<EnemyController>();
    private float _scanTimer;
    private const float ScanInterval = 0.5f;

    private sealed class EnemyHealthBar
    {
        public GameObject Root;
        public RectTransform Rect;
        public Image Fill;
        public Transform Target;
        public float Max;
        public float HeightOffset = 2.2f;
        public float BarWidth = 120f;

        public bool IsUsed => Target != null && Target.gameObject.activeInHierarchy;
    }

    private void OnEnable()
    {
        _canvas = HudCanvas.CreateOverlay("EnemyHealthBarCanvas");
    }

    private void Update()
    {
        if (_canvas == null) return;
        _canvas.gameObject.SetActive(GameManager.Instance != null && GameManager.Instance.InGame);
        if (!_canvas.gameObject.activeSelf) return;

        if (_scanTimer > 0f)
            _scanTimer -= Time.deltaTime;

        var enemies = AllEnemies();
        int used = 0;

        for (int i = 0; i < enemies.Count && used < MaxBars; i++)
        {
            var e = enemies[i];
            if (e == null || e.IsDead) continue;

            EnemyHealthBar bar = Acquire(used);
            used++;
            Attach(bar, e);
        }

        // Release surplus bars.
        for (int i = used; i < _bars.Count; i++)
            _bars[i].Target = null;
    }

    private List<EnemyController> AllEnemies()
    {
        if (_scanTimer > 0f)
            return _enemies;

        _scanTimer = ScanInterval;
        _enemies.Clear();
        foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            _enemies.Add(e);
        return _enemies;
    }

    private EnemyHealthBar Acquire(int index)
    {
        while (_bars.Count <= index)
        {
            var bar = new EnemyHealthBar();
            bar.Root = new GameObject("EnemyBar_" + _bars.Count);
            bar.Root.transform.SetParent(_canvas.transform, false);
            var rect = bar.Root.AddComponent<RectTransform>();
            bar.Rect = rect;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(bar.BarWidth, 8f);
            var bg = bar.Root.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.7f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(bar.Root.transform, false);
            var fr = fill.AddComponent<RectTransform>();
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(1f, 1f);
            fr.offsetMax = new Vector2(-1f, -1f);
            var fi = fill.AddComponent<Image>();
            fi.type = Image.Type.Filled;
            fi.fillMethod = Image.FillMethod.Horizontal;
            fi.fillAmount = 1f;
            fi.color = new Color(0.85f, 0.2f, 0.18f);
            bar.Fill = fi;
            _bars.Add(bar);
        }
        return _bars[index];
    }

    private void Attach(EnemyHealthBar bar, EnemyController enemy)
    {
        // Bars are pooled: (re)initialise max and head height when a different enemy takes over the bar.
        if (bar.Target != enemy.transform)
        {
            bar.Target = enemy.transform;
            bar.Max = Mathf.Max(1f, enemy.CurrentHealth);
            bar.HeightOffset = ComputeHeadOffset(enemy);
        }

        if (_cam == null)
            _cam = Camera.main;
        if (_cam == null)
        {
            SetShown(bar, false);
            return;
        }

        Vector3 screen = _cam.WorldToScreenPoint(enemy.transform.position + Vector3.up * bar.HeightOffset);
        bool shown = screen.z > 0f;
        SetShown(bar, shown);
        if (!shown) return;

        bar.Rect.anchoredPosition = new Vector3(screen.x - Screen.width * 0.5f, screen.y - Screen.height * 0.5f, 0f);
        float frac = bar.Max > 0f ? Mathf.Clamp01(enemy.CurrentHealth / bar.Max) : 0f;
        if (Mathf.Abs(bar.Fill.fillAmount - frac) > 0.0005f)
            bar.Fill.fillAmount = frac;
    }

    private static void SetShown(EnemyHealthBar bar, bool shown)
    {
        if (bar.Root.activeSelf != shown)
            bar.Root.SetActive(shown);
    }

    private static float ComputeHeadOffset(EnemyController enemy)
    {
        // Default height above the root (used only when the model is unavailable).
        const float Fallback = 2.2f;
        const float Margin = 0.25f;

        if (enemy == null || enemy.ModelRoot == null)
            return Fallback;

        var renderers = enemy.ModelRoot.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return Fallback;

        float rootY = enemy.transform.position.y;
        float top = renderers[0].bounds.max.y;
        for (int i = 1; i < renderers.Length; i++)
        {
            float t = renderers[i].bounds.max.y;
            if (t > top)
                top = t;
        }

        return top - rootY + Margin;
    }
}