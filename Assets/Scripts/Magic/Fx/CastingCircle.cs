using UnityEngine;

/// <summary>
/// Halo-style casting circle that wraps around the held magic weapon while a spell is being
/// aimed/charged (driven by <see cref="PlayerController"/>). The ring plane lies perpendicular
/// to the weapon's up axis so it tilts with the weapon like a halo, growing brighter and
/// spinning faster as the charge level (0..1) builds. Released on cast with a quick outward
/// ring burst. Prefab-free, built from a translucent disc + LineRenderer rings. Pure
/// visual: no colliders and nothing blocking gameplay.
///
/// <para><b>1if: the halo family is per-spell.</b> There are seven <see cref="SpellCastStyle"/>
/// families and this component draws all of them. They are built ONCE in <see cref="Build"/> and
/// then only toggled in <see cref="Apply"/> — never rebuilt per style change, which is what makes
/// it safe to call <see cref="Show(Vector3, float, in SpellLook)"/> from the aim frame.</para>
///
/// <para><b>Why one GameObject per renderer.</b> Unity permits a single <c>Renderer</c> component
/// per GameObject, so the second <c>LineRenderer</c> this class wants is on its own child. The
/// per-style groups below each own their own children for the same reason; that is also why the
/// group <c>GameObject</c>s exist at all, since <c>SetActive</c> is the cheapest way to switch a
/// whole family off.</para>
///
/// <para><b>The identity-less overloads are kept.</b> <see cref="Show(Transform, float, Color)"/>
/// and <see cref="Burst(float, Color, Vector3)"/> are still used by the preview and ranged-draw
/// paths, which have a colour but no <see cref="SpellData"/>; they draw the plain
/// <see cref="SpellCastStyle.Circle"/> family.</para>
/// </summary>
public sealed class CastingCircle : MonoBehaviour
{
    private static CastingCircle _instance;

    // Segment counts are named constants, not inline literals, because the build pass and the
    // per-frame Apply pass BOTH read them — a length that disagreed between the two is a ring that
    // is shaped one way and coloured another (rule 10: one named constant per shared metric).
    private const int OuterSegments = 48;
    private const int InnerSegments = 36;
    private const int HexSegments = 6;
    private const int ArcSegments = 24;
    private const int WaveSegments = 24;
    private const int RuneTicks = 8;
    private const float BaseRadius = 0.35f;
    private const float FullRadius = 0.75f;

    private Transform _disc;
    private Renderer _discRenderer;
    private Material _discMat;
    private LineRenderer _outerRing;
    private Material _outerMat;
    private LineRenderer _innerRing;
    private Material _innerMat;

    // --- 1if per-style groups. Each is a child GameObject toggled by Apply(). ---
    private GameObject _runeGroup;
    private Transform _runeTicks;
    private Material _runeMat;
    private LineRenderer _hexRing;
    private Material _hexMat;
    private LineRenderer _crossA;
    private LineRenderer _crossB;
    private Material _crossMatA;
    private Material _crossMatB;
    private LineRenderer _arc;
    private Material _arcMat;
    private LineRenderer _waveB;
    private LineRenderer _waveC;
    private Material _waveMatB;
    private Material _waveMatC;

    private bool _active;
    private Transform _anchor;
    private float _charge;
    private Color _color = Color.white;
    private SpellCastStyle _style = SpellCastStyle.Circle;
    private float _scale = 1f;
    private float _tempo = 1f;
    private float _spin;
    private float _pulse;

    // ---------------------------------------------------------------- 1jt: ground anchoring
    //
    // The two `Show(Transform, ...)` overloads below hang the halo off the held magic WEAPON, and
    // that is still correct for their three callers (the cast preview, the ranged-draw accent, and
    // the no-spell-armed release). 1jt adds a third mode for the actual per-spell charge, which
    // belongs on the ground at the player's feet or in front of them (SpellCastAnchor) instead of
    // under the staff.
    //
    // Why a flag rather than an overload on `_anchor`: `Update` RE-DERIVES the transform from
    // `_anchor` every single frame, so simply passing a different position to Show would be
    // overwritten before the next repaint. The mode has to survive into Update, which means it has
    // to be state. `_anchor` is deliberately NOT reused to carry a position — one field meaning
    // "a transform to follow" and one meaning "a world point we were handed" is exactly the aliasing
    // that produced 1jq's ArgumentOutOfRange.

    /// <summary>1jt: true when the halo is placed by <see cref="_groundPos"/> instead of by
    /// <see cref="_anchor"/>. Set by the <see cref="ShowGround"/> overloads and cleared by both
    /// <see cref="Show(Transform,float,Color)"/> and <see cref="Show(Transform,float,in SpellLook)"/>.</summary>
    private bool _grounded;

    /// <summary>1jt: the world point the halo sits on, refreshed by the caller each frame so it
    /// tracks the player while a charge is held.</summary>
    private Vector3 _groundPos;

    /// <summary>1jt: the surface normal the halo is laid flat against. Always <see cref="Vector3.up"/>
    /// for a ground halo — it is not a slope-aligned disc, deliberately: a halo that tilts with a
    /// hillside reads as falling over, and the ground probe returns a height, not a normal.</summary>
    private Vector3 _groundUp = Vector3.up;

    /// <summary>Ensure the singleton exists and returns it (builds on first access).</summary>
    public static CastingCircle Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("CastingCircle");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<CastingCircle>();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null) _instance = this;
        Build();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>Show/refresh the halo around the anchor weapon with a charge level (0..1).</summary>
    public void Show(Transform anchor, float charge, Color color)
    {
        // No spell behind this colour — draw the plain family rather than inventing an identity.
        if (anchor == null) { Hide(); return; }
        _active = true;
        _grounded = false;
        _anchor = anchor;
        _charge = Mathf.Clamp01(charge);
        _color = color;
        _style = SpellCastStyle.Circle;
        _scale = 1f;
        _tempo = 1f;
        transform.position = anchor.position + anchor.up * 0.05f;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, anchor.up);
        gameObject.SetActive(true);
        Apply();
    }

    /// <summary>1if: the per-spell path — colour, family, size and tempo all come from the look.</summary>
    public void Show(Transform anchor, float charge, in SpellLook look)
    {
        if (anchor == null) { Hide(); return; }
        _active = true;
        _grounded = false;
        _anchor = anchor;
        _charge = Mathf.Clamp01(charge);
        _color = look.Core;
        _style = look.Cast == SpellCastStyle.Inherit ? SpellCastStyle.Circle : look.Cast;
        _scale = Mathf.Max(0.2f, look.Scale);
        _tempo = Mathf.Max(0.2f, look.Tempo);
        transform.position = anchor.position + anchor.up * 0.05f;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, anchor.up);
        gameObject.SetActive(true);
        Apply();
    }

    /// <summary>1jt: the plain-family ground halo — no spell behind it, so no identity to invent.</summary>
    public void ShowGround(Vector3 groundPos, float charge, Color color)
    {
        _active = true;
        _grounded = true;
        _groundPos = groundPos;
        _groundUp = Vector3.up;
        _anchor = null;
        _charge = Mathf.Clamp01(charge);
        _color = color;
        _style = SpellCastStyle.Circle;
        _scale = 1f;
        _tempo = 1f;
        transform.position = _groundPos;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, _groundUp);
        gameObject.SetActive(true);
        Apply();
    }

    /// <summary>1jt: the per-spell ground halo — the charge circle for a real armed spell, placed by
    /// the caller at the resolved <see cref="SpellLook.CastAnchor"/> point rather than under the
    /// weapon. Colour, family, size and tempo all come from the look, exactly as the weapon-anchored
    /// overload takes them; only the placement differs.
    /// <para>The caller re-sends <paramref name="groundPos"/> every frame while charging, so the halo
    /// follows a walking player instead of being left behind at the cast origin.</para></summary>
    public void ShowGround(Vector3 groundPos, float charge, in SpellLook look)
    {
        _active = true;
        _grounded = true;
        _groundPos = groundPos;
        _groundUp = Vector3.up;
        _anchor = null;
        _charge = Mathf.Clamp01(charge);
        _color = look.Core;
        _style = look.Cast == SpellCastStyle.Inherit ? SpellCastStyle.Circle : look.Cast;
        _scale = Mathf.Max(0.2f, look.Scale);
        _tempo = Mathf.Max(0.2f, look.Tempo);
        transform.position = _groundPos;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, _groundUp);
        gameObject.SetActive(true);
        Apply();
    }

    /// <summary>One-shot expanding ring at the current anchor, sized by the charge level.</summary>
    public void Burst(float radius, Color color, Vector3 upDir)
        => Burst(radius, color, upDir, 1f);

    /// <summary>1if: the per-spell burst, taking the look's size multiplier.</summary>
    public void Burst(float radius, Color color, Vector3 upDir, float scaleMul)
    {
        // 1jt: in ground mode the release ring must come off the same point the halo was charging on,
        // or it fires from the staff and reads as two different spells. Weapon mode is unchanged.
        Vector3 at = _grounded
            ? _groundPos
            : (_anchor != null ? _anchor.position : transform.position);
        SkillFx.RingFlash(at, upDir, color, Mathf.Max(radius, 0.4f), 0.35f, scaleMul);
    }

    /// <summary>1jt: the per-spell burst at an EXPLICIT world point, for a halo that was charging on
    /// the ground rather than on the weapon. Takes the point rather than reading it out of state so
    /// the caller cannot accidentally burst at last frame's halo position if the player has moved
    /// since — the release ring belongs at the cast, not at wherever the circle drifted to.
    /// <para><c>upDir</c> is passed separately from <paramref name="at"/> because the ground halo is
    /// always flat (see <see cref="_groundUp"/>), so the two are independent facts and reading the
    /// normal out of state would make them look coupled when they are not.</para></summary>
    public void Burst(float radius, Color color, Vector3 upDir, float scaleMul, Vector3 at)
    {
        SkillFx.RingFlash(at, upDir, color, Mathf.Max(radius, 0.4f), 0.35f, scaleMul);
    }

    /// <summary>Hide the halo immediately (aim cancelled, cast released, no magic, etc.).</summary>
    public void Hide()
    {
        if (!_active) return;
        _active = false;
        _grounded = false;
        _anchor = null;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_active) return;
        if (!_grounded && _anchor == null) { Hide(); return; }
        if (_grounded)
        {
            transform.position = _groundPos;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, _groundUp);
        }
        else
        {
            transform.position = _anchor.position + _anchor.up * 0.05f;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, _anchor.up);
        }

        _spin += Time.deltaTime * (18f + _charge * 60f) * _tempo;
        _pulse += Time.deltaTime * 2.4f * _tempo;
        Apply();
    }

    /// <summary>1jd: the ten pieces moved to <see cref="CastingCircleModelBuilder"/> (under
    /// <c>Models/Magic/</c>). This method only unpacks one record per piece into the fields
    /// <see cref="Apply"/> drives, so every handle keeps its exact declared type and the per-frame
    /// colour/spin/vertex code is untouched. The segment and tick counts are passed IN rather than
    /// duplicated in the builder: <c>Apply</c> loops over these same numbers.</summary>
    private void Build()
    {
        CastingCircleModelBuilder.Circle circle = CastingCircleModelBuilder.Build(
            transform, OuterSegments, InnerSegments, HexSegments, ArcSegments, WaveSegments, RuneTicks);

        _disc = circle.Disc.Root.transform;
        _discRenderer = circle.Disc.Renderer;
        _discMat = circle.Disc.Material;

        _outerRing = circle.OuterRing.Line;
        _outerMat = circle.OuterRing.Material;

        _innerRing = circle.InnerRing.Line;
        _innerMat = circle.InnerRing.Material;

        _runeGroup = circle.Rune.Root;
        _runeTicks = circle.Rune.Root.transform;
        _runeMat = circle.Rune.Material;

        _hexRing = circle.HexRing.Line;
        _hexMat = circle.HexRing.Material;

        _crossA = circle.CrossA.Line;
        _crossMatA = circle.CrossA.Material;
        _crossB = circle.CrossB.Line;
        _crossMatB = circle.CrossB.Material;

        _arc = circle.Arc.Line;
        _arcMat = circle.Arc.Material;

        _waveB = circle.WaveB.Line;
        _waveMatB = circle.WaveB.Material;
        _waveC = circle.WaveC.Line;
        _waveMatC = circle.WaveC.Material;
    }

    private void Apply()
    {
        float r = Mathf.Lerp(BaseRadius, FullRadius, _charge) * _scale;
        float a = Mathf.Lerp(0.35f, 1f, _charge);
        float pulse = 1f + 0.03f * Mathf.Sin(_pulse);

        // --- 1if: which family is on. Exactly one of the seven is active; the shared disc and
        // outer ring stay on for every style, so no family is ever an empty circle.
        bool wantInner = _style == SpellCastStyle.Circle || _style == SpellCastStyle.Rune;
        bool wantRune = _style == SpellCastStyle.Rune;
        bool wantHex = _style == SpellCastStyle.HexRing;
        bool wantCross = _style == SpellCastStyle.Cross;
        bool wantArc = _style == SpellCastStyle.Arc;
        bool wantWaves = _style == SpellCastStyle.Wave;
        // Circle = the original disc+outer+inner. Halo = disc+outer only, i.e. the soft filled
        // disc with a bright rim and no spinning inner ring.
        if (_disc != null)
            _disc.localScale = new Vector3(r * 2f * pulse, 0.02f, r * 2f * pulse);
        if (_discMat != null)
            _discMat.color = new Color(_color.r, _color.g, _color.b, 0.05f + 0.08f * _charge);
        if (_innerRing != null && _innerRing.gameObject.activeSelf != wantInner)
            _innerRing.gameObject.SetActive(wantInner);
        if (_runeGroup != null && _runeGroup.activeSelf != wantRune)
            _runeGroup.SetActive(wantRune);
        if (_hexRing != null && _hexRing.gameObject.activeSelf != wantHex)
            _hexRing.gameObject.SetActive(wantHex);
        if (_crossA != null && _crossA.gameObject.activeSelf != wantCross)
            _crossA.gameObject.SetActive(wantCross);
        // Both spokes are the same family: toggling only CrossA left CrossB on screen after the
        // style moved on, and a build that creates it active means "never switched" is visible
        // rather than hypothetical.
        if (_crossB != null && _crossB.gameObject.activeSelf != wantCross)
            _crossB.gameObject.SetActive(wantCross);
        if (_arc != null && _arc.gameObject.activeSelf != wantArc)
            _arc.gameObject.SetActive(wantArc);
        if (_waveB != null && _waveB.gameObject.activeSelf != wantWaves)
            _waveB.gameObject.SetActive(wantWaves);
        if (_waveC != null && _waveC.gameObject.activeSelf != wantWaves)
            _waveC.gameObject.SetActive(wantWaves);

        if (_outerRing != null)
            for (int i = 0; i < OuterSegments; i++)
            {
                float ang = (i / (float)OuterSegments) * Mathf.PI * 2f;
                _outerRing.SetPosition(i, new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r));
            }
        if (_outerMat != null)
            _outerMat.color = new Color(_color.r, _color.g, _color.b, a);

        // Rune ticks: eight short radial marks sitting on the inner ring's radius.
        //
        // The childCount guard is not defensive noise, it is the fix for a real per-frame throw.
        // Build() assigns _runeTicks unconditionally but only CREATES the ticks inside
        // `if (shader != null)`, so when both Shader.Find calls return null (a stripped
        // URP project's Sprites/Default and Unlit/Color) _runeTicks is an EMPTY transform and
        // GetChild(i) throws ArgumentOutOfRangeException every frame — the circle draws nothing and
        // the console fills. The null check alone passed `while the ticks were missing`; the
        // invariant is "as many children as we are about to index", so it is counted.
        if (wantRune && _runeTicks != null && _runeTicks.childCount >= RuneTicks)
        {
            float ir = r * 0.86f;
            for (int i = 0; i < RuneTicks; i++)
            {
                var tick = _runeTicks.GetChild(i);
                float ang = _spin * Mathf.Deg2Rad + (i / (float)RuneTicks) * Mathf.PI * 2f;
                tick.localPosition = new Vector3(Mathf.Cos(ang) * ir, 0f, Mathf.Sin(ang) * ir);
                tick.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f);
            }
            if (_runeMat != null)
                _runeMat.color = new Color(_color.r, _color.g, _color.b, a * 0.9f);
        }

        if (wantInner && _innerRing != null)
        {
            float ir = r * 0.72f * pulse;
            for (int i = 0; i < InnerSegments; i++)
            {
                float ang = _spin * Mathf.Deg2Rad + (i / (float)InnerSegments) * Mathf.PI * 2f;
                _innerRing.SetPosition(i, new Vector3(Mathf.Cos(ang) * ir, 0f, Mathf.Sin(ang) * ir));
            }
        }
        if (_innerMat != null && wantInner)
            _innerMat.color = new Color(_color.r, _color.g, _color.b, a * 0.8f);

        if (wantHex && _hexRing != null)
        {
            // Flat-top hexagon: angles offset 30 degrees so the flat sides face the rim.
            for (int i = 0; i < HexSegments; i++)
            {
                float ang = (i / (float)HexSegments) * Mathf.PI * 2f + Mathf.PI / 6f;
                _hexRing.SetPosition(i, new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r));
            }
            if (_hexMat != null)
                _hexMat.color = new Color(_color.r, _color.g, _color.b, a);
        }

        if (wantCross)
        {
            float cr = r * 0.95f;
            _crossA.SetPosition(0, new Vector3(-cr, 0f, 0f));
            _crossA.SetPosition(1, new Vector3(cr, 0f, 0f));
            _crossB.SetPosition(0, new Vector3(0f, 0f, -cr));
            _crossB.SetPosition(1, new Vector3(0f, 0f, cr));
            if (_crossMatA != null) _crossMatA.color = new Color(_color.r, _color.g, _color.b, a);
            if (_crossMatB != null) _crossMatB.color = new Color(_color.r, _color.g, _color.b, a);
        }

        if (wantArc && _arc != null)
        {
            // Sweep grows with charge, so a partial cast shows a partial halo.
            float sweep = Mathf.Lerp(0.5f, 2.4f, _charge);
            float start = _spin * Mathf.Deg2Rad;
            for (int i = 0; i < ArcSegments; i++)
            {
                float t = i / (float)(ArcSegments - 1);
                float ang = start + t * sweep;
                float rr = r * (0.8f + 0.2f * Mathf.Sin(t * Mathf.PI));
                _arc.SetPosition(i, new Vector3(Mathf.Cos(ang) * rr, 0f, Mathf.Sin(ang) * rr));
            }
            if (_arcMat != null)
                _arcMat.color = new Color(_color.r, _color.g, _color.b, a);
        }

        if (wantWaves)
        {
            // Two waves chasing outward, phase-shifted by the pulse. Phase is a function of
            // _pulse, so a slower tempo (lower _tempo) visibly slows the ripples.
            DrawWave(_waveB, _waveMatB, r, 0f, a);
            DrawWave(_waveC, _waveMatC, r, Mathf.PI, a);
        }
    }

    private void DrawWave(LineRenderer lr, Material mat, float r, float phase, float a)
    {
        if (lr == null) return;
        // Sawtooth: one wave crosses the halo then restarts, rather than shrinking to nothing.
        float t = Mathf.Repeat((_pulse * 0.5f) + phase / (Mathf.PI * 2f), 1f);
        float rr = r * Mathf.Lerp(0.25f, 1.15f, t);
        float fade = (1f - Mathf.Abs(t * 2f - 1f)) * 0.9f;
        for (int i = 0; i < WaveSegments; i++)
        {
            float ang = (i / (float)WaveSegments) * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * rr, 0f, Mathf.Sin(ang) * rr));
        }
        if (mat != null)
            mat.color = new Color(_color.r, _color.g, _color.b, a * fade);
    }
}