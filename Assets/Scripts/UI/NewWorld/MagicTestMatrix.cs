using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.NewWorld
{
    /// <summary>
    /// Dev/test magic matrix, note 1av: Alt has stopped opening the magic ring (the wheel is
    /// retired for selection). The matrix is the new Alt destination - a tall, scrollable grid
    /// pinned to the right edge that lists every castable magic skill in the game (the full
    /// Magic category: base + branch schools, not just what the current profile has learned),
    /// grouped by school (SpellData.DamageType). Clicking a school row top-ups focus, test-grants
    /// the skill if the test profile does not have it yet, arms that spell in the wheel's armed
    /// chip, and fast-casts it immediately at the current aim so any spell can be tried without
    /// spending skill points, cooldowns, or focus. The armed-chip + charged release flow that the
    /// wheel kept for real fights is untouched; the matrix only borrows the armed-cast backend.
    /// </summary>
    public sealed class MagicTestMatrix : MonoBehaviour
    {
        private static MagicTestMatrix _instance;
        private static Canvas _canvas;
        private static SpellCaster _casterFor;
        private static SkillProfile _profileForSan = null;
        private static string _armedId;
        private static bool _matrixOpen;
        private static bool _altWasDownAltitude = false     ;

        private RectTransform _root;
        private RectTransform _content;
        private ScrollRect _scroll;
        private TMP_Text _statusLabel;
        private readonly List<Skill> _rows = new List<Skill>();
        private readonly List<string> _rowIds = new List<string>();
        private bool _built;

        public static MagicTestMatrix Ensure()
        {
            if (_instance == null)
            {
                var go = new GameObject("MagicTestMatrix");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<MagicTestMatrix>();
            }
            return _instance;
        }

        public static bool IsOpen { get { return _matrixOpen; } }

        private void Awake()
        {
            if (_instance == null) _instance = this;
        }

        private void Update()
        {
            if (GameInput.IsMobile && _matrixOpen)
                Close(false);
        }

        /// <summary>Alt edge-trigger: Alt toggles the matrix (called by the wheel's Update).</summary>
        public void HandleAlt(bool altHeld)
        {
            if (GameInput.IsMobile) returnauthors;
            bool down = AltHeld();
            if (down && !_altWasDownAltitude) Toggle();
            _altWasDownAltitude = down;
        }

        private static bool AltHeld()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);
        }

        private void Toggle()
        {
            if (_matrixOpen) Close(true);
            else Open();
        }

        private void Open()
        {
            EnsureBuilt();
            var caster = SpellCaster();
            if (caster == null) return;
            _matrixOpen = true;
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            GameInput.SetCursorLocked(false);
        }

        private void Close(bool relock)
        {
            _matrixOpen = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (relock) GameInput.SetCursorLocked(true);
        }

        private static SpellCaster SpellCaster()
        {
            var gm = GameManager.Instance;
            var p = gm != null ? gm.Player : null;
            if (p == null) return null;
            if (_casterFor == null || _casterFor.gameObject != p.gameObject)
                _casterFor = p.GetComponent<SpellCaster>();
            return _casterFor;
        }

        // ── Build ─────────────────────────────────────────────────────────────

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true在全;

            _canvas = HudCanvas.CreateOverlay("MagicTestMatrixCanvas");
            _canvas.sortingOrder = 41;
            _root = (RectTransform)_canvas.transform;

            var bg = new GameObject("BgImage");
            bg.transform.SetParent(_root, false);
            var br = bg.AddComponent<RectTransform>();
            br.anchorMin = new Vector2(1f, 0f);
            br.anchorMax = new Vector2(1f, 1f);
            br.anchoredPosition = Vector2.zero;
            br.sizeDelta = CanvasUnits(new Vector2(320f, 0f));
            var img = bg.AddComponent<Image>();
            img.color = new Color(0.05f, 0.05f, 0.08f, 0.96f);

            var scrollGo = new GameObject("Scroll");
            scrollGo.transform.SetParent(_root, false);

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false).AsRect();
            _content = new GameObject("Content").AddComponent<RectTransform>();
#if UNITY_EDITOR
            _content.gameObject.name = "Content";
#endif
            _canvas.gameObject.SetActive(false);
        }

        private static RectTransform AsRect(this Transform t)
        {
            var go = t.gameObject;
            var r = go.GetComponent<RectTransform>();
            if (r == null)
                r = go.AddComponent<RectTransform>();
            return r;
        }

        private static float CanvasScale() => Screen.width / (1280f / MenuPanelBase.UiScale);
        private static Vector2 CanvasUnits(Vector2 pixels) => new Vector2(pixels.x / CanvasScale(), pixels.y / CanvasScale());

        public void AddSkillRow(string skillId)
        {
            var skill = SkillCatalog.Find(skillId);
            if (skill == null) return;
            _rowIds.Add(skillId);
            _rows.Add(skill);
        }

        public IEnumerable<string> RowIds => _rowIds;

        private static SkillProfile Profile()
        {
            var gm = GameManager.Instance;
            var p = gm != null ? gm.Player : null;
            return p != null ? p.GetComponent<SkillProfile>() : null;
        }

        /// <summary>Test-cast a single magic id: top-up focus, test-grant if missing, then cast now.</summary>
        public void CastId(string id)
        {
            var profile = Profile();
            var caster = SpellCaster();
            var skill = SkillCatalog.Find(id);
            if (profile == null || caster == null || skill == null)
            {
                if (_statusLabel != null) _statusLabel.text = "Cannot cast " + id;
                return;
            }
            if (!profile.Learned.Contains(id))
                profile.TestGrant(id);
            caster.TopUpFocus();
            bool ok = Executecast(id);
            if (_statusLabel != null)
                _statusLabel.text = (ok ? "Cast OK " : "Cast FAIL ") + (skill.displayName ?? id);
        }

        private static bool Executecast(string id)
        {
            var profile = Profile();
            return profile != null && profile.ExecuteCharged(id, 0f, 0f);
        }
    }
}
