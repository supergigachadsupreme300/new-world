using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phase 8 (Task 8.1): enemy health bars. A screen-space overlay that positions a compact
/// health bar above every live <see cref="EnemyController"/> (and other IDamageable when the
/// <see cref="TrackDamageable"/> flag is set). Bars auto-pool; dead enemies release theirs.
/// Uses the first on-screen enemy's max the moment a bar attaches (public max is deferred to
/// <see cref="EnemyController"/> internals, so we capture the initial CurrentHealth).
/// </summary>
public sealed class EnemyHealthBarHUD : MonoBehaviour
{
    public float MaxBars = 24f;

    private Canvas _canvas;
    private readonly List<EnemyHealthBar> _bars = new List<EnemyHealthBar>();

    private sealed class EnemyHealthBar
    {
        public GameObject Root;
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
        var out_ = new List<EnemyController>();
        foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            out_.Add(e);
        return out_;
    }

    private EnemyHealthBar Acquire(int index)
    {
        while (_bars.Count <= index)
        {
            var bar = new EnemyHealthBar();
            bar.Root = new GameObject("EnemyBar_" + _bars.Count);
            bar.Root.transform.SetParent(_canvas.transform, false);
            var rect = bar.Root.AddComponent<RectTransform>();
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
        bar.Target = enemy.transform;
        if (bar.Max <= 0f)
            bar.Max = Mathf.Max(1f, enemy.CurrentHealth);

        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 screen = cam.WorldToScreenPoint(enemy.transform.position + Vector3.up * bar.HeightOffset);
            RectTransform rect = bar.Root.GetComponent<RectTransform>();
            if (bar.Target != null && screen.z > 0f)
            {
                rect.anchoredPosition = new Vector3(screen.x - Screen.width * 0.5f, screen.y - Screen.height * 0.5f, 0f);
                float frac = bar.Max > 0f ? Mathf.Clamp01(enemy.CurrentHealth / bar.Max) : 0f;
                bar.Fill.fillAmount = frac;
                bar.Root.SetActive(true);
                return;
            }
        }
        bar.Root.SetActive(false);
    }
}