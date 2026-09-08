using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-weapon attack animation — a unique visible "using" motion for every one of the 15
/// weapons ("animation pack lives on the weapon", Phase 10).
///
/// Mounted on each weapon rig by <see cref="WeaponRigBuilder"/>. Plays a windup → strike/charge
/// → recover limb pose-track that matches <see cref="CombatController"/>'s attack timing, and
/// reports the actual duration it will run so the controller's action lock stays in sync.
///
/// The arm is the animation: each weapon is a keyframed pose track driving the owning shoulder +
/// elbow (and, for two-hand grips, the supporting arm) so the blade swings WITH the arm — no more
/// blade-only "wiggle". The weapon rides the hand at its equipped rest pose; only magic focuses
/// keep small local accents (orb arc, scale pulse, strum) on top.
///
/// The arms are fully owned while attacking: this component sets
/// <see cref="PlayerAnimator.SuppressArms"/> so the walk/idle animator can't overwrite the swing
/// mid-attack, then restores the captured base pose on recovery. Rest poses are re-captured at the
/// start of every attack, so re-parenting onto a hand (WeaponRigBuilder's ReparentToHands) never
/// breaks the animation.
///
/// Block weapons are authored +Y-up with the grip at the base; swings rotate the arm (and therefore
/// the blade) from the shoulder while the elbow flexes for punches, draws, stabs and thrusts.
/// </summary>
public sealed class WeaponAnimator : MonoBehaviour
{
    private const int K_None = -1;   // no local weapon accent (arm carries the motion)
    private const int K_Staff = 0;   // staff — steady raise + arc pulse
    private const int K_Book = 1;    // holy_book — two-hand raise + chant sway
    private const int K_Wand = 2;    // bone_wand — quick raise + size pulse
    private const int K_Orb = 3;     // control_orb — raised arm + wide sweeping arc
    private const int K_Lute = 4;    // lute — held at the side, string strum
    private const int K_Dual = 5;    // gauntlets — alternate hands off-phase

    // Side-slash blade lead: during a horizontal sweep the blade first pitches tip-DOWN and then
    // turns SIDEWAYS into the swing's travel, and that pose is HELD rigid from the hand through
    // the cut — a clean slashing line. Tune the degrees; flip SlashYawFlip if the tip reads
    // backwards (pointing away from the travel) instead of leading.
    private const float SlashDownDeg = 80f;
    private const float SlashYawDeg = 70f;
    private const float SlashYawFlip = 1f;

    /// <summary>Multiplier on the authored swing tempos (TimeLight/TimeHeavy). >1 slows the base
    /// attack rate; the AttackSpeed stat divides this further, so a value of 1 AttackSpeed now
    /// plays at <see cref="BaseSwingTimeScale"/> × the authored beat.</summary>
    private const float BaseSwingTimeScale = 10f;

    /// <summary>Capped attack-speed scale from the player's stats (1 → authored tempo).</summary>
    private float SpeedScale()
    {
        var stats = GetComponentInParent<PlayerStats>();
        return stats != null ? stats.AttackSpeedScale : 1f;
    }

    private enum OffArm
    {
        None,    // weapon in one hand only (sword, dagger, hammer, casters, gauntlets each hand)
        Mirror,  // two-hand grip — support arm copies the swing (greatsword, greataxe, warhammer, lance, katana)
        Asym     // the two arms play different tracks (longbow: bow arm vs draw arm)
    }

    /// <summary>One keyframe of the owner arm's pose track (angles in degrees, additive).</summary>
    private struct PoseKey
    {
        public float t;                 // normalized time 0..1
        public float shX, shY, shZ;     // owner shoulder pitch / yaw / roll (additive)
        public float elX;               // owner elbow flex (additive)

        public PoseKey(float t, float shX, float shY, float shZ, float elX)
        {
            this.t = t;
            this.shX = shX; this.shY = shY; this.shZ = shZ;
            this.elX = elX;
        }
    }

    private struct WeaponAnimDef
    {
        public OffArm Mode;
        public PoseKey[][] Owner;   // one track per combo variant (throwing arm)
        public PoseKey[][] Other;   // support-arm tracks (Mode == Asym only), same variant count
        public int Accent;          // K_* accent kind
        public float TimeLight;     // light-attack duration
        public float TimeHeavy;     // heavy-attack duration
        public bool SlashLead;      // side slashes lead tip-down → sideways (slashing line)

        public WeaponAnimDef(OffArm mode, PoseKey[][] owner, PoseKey[][] other, int accent,
            float light, float heavy, bool slashLead = false)
        {
            Mode = mode;
            Owner = owner;
            Other = other;
            Accent = accent;
            TimeLight = light;
            TimeHeavy = heavy;
            SlashLead = slashLead;
        }

        /// <summary>Number of distinct combo swings authored for this weapon (>= 1).</summary>
        public int VariantCount => Owner != null && Owner.Length > 0 ? Owner.Length : 1;
    }

    // ── Pose-track authoring helpers ───────────────────────────────────────
    private static PoseKey K(float t, float shX, float shY, float shZ, float elX) => new PoseKey(t, shX, shY, shZ, elX);
    private static PoseKey[] T(params PoseKey[] keys) => keys;
    private static PoseKey[][] V(params PoseKey[][] sets) => sets;

    private static readonly System.Collections.Generic.Dictionary<string, WeaponAnimDef> Defs =
        new System.Collections.Generic.Dictionary<string, WeaponAnimDef>
        {
            // iron_sword — balanced 1H blade: slash L→R, slash R→L, overhead chop, forward thrust.
            {
                "iron_sword", new WeaponAnimDef(OffArm.None, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -75f, -55f, 0f, -15f), K(0.62f, -80f, 45f, 0f, -5f), K(1f, 0f, 0f, 0f, 0f)),   // 1. wind left/back → slash across to the right
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -75f, 55f, 0f, -15f), K(0.62f, -80f, -45f, 0f, -5f), K(1f, 0f, 0f, 0f, 0f)),  // 2. wind right/back → slash across to the left
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -150f, 0f, 0f, 10f), K(0.68f, -65f, 0f, 0f, 6f), K(1f, 0f, 0f, 0f, 0f)),     // 3. overhead chop
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.38f, -88f, -12f, 0f, -42f), K(0.58f, -62f, -8f, 0f, -4f), K(1f, 0f, 0f, 0f, 0f))), // 4. forward thrust (elbow extends)
                    null, K_None, 0.30f, 0.50f, true)
            },

            // greatsword — heavy two-hander: overhead slam, low sweep, reverse-grip sweep, rising spin.
            {
                "greatsword", new WeaponAnimDef(OffArm.Mirror, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -150f, 0f, 0f, 8f), K(0.70f, -55f, 0f, 0f, 14f), K(1f, 0f, 0f, 0f, 0f)),       // 1. overhead slam
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -70f, -75f, 0f, 8f), K(0.70f, -80f, 35f, 0f, 12f), K(1f, 0f, 0f, 0f, 0f)),   // 2. low sweep from the left across to the right
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -100f, 70f, 0f, 18f), K(0.68f, -70f, -55f, 0f, -10f), K(1f, 0f, 0f, 0f, 0f)),// 3. reverse-grip right wind, hard backswing to the left
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -120f, 30f, 0f, -30f), K(0.75f, -160f, 0f, 0f, -20f), K(1f, 0f, 0f, 0f, 0f))),// 4. rising spin uppercut (finisher)
                    null, K_None, 0.45f, 0.65f)
            },

            // dagger — fast stabs: low jab, high jab, quick double, lunging stab.
            {
                "dagger", new WeaponAnimDef(OffArm.None, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.20f, -58f, 6f, 0f, -28f), K(0.34f, 12f, 0f, 0f, 18f), K(1f, 0f, 0f, 0f, 0f)),    // 1. low jab
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.20f, -70f, 6f, 0f, -24f), K(0.34f, 10f, 0f, 0f, 20f), K(1f, 0f, 0f, 0f, 0f)),    // 2. high jab
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.20f, -58f, 6f, 0f, -28f), K(0.34f, 12f, 0f, 0f, 18f), K(0.55f, -58f, 6f, 0f, -28f), K(0.70f, 12f, 0f, 0f, 18f), K(1f, 0f, 0f, 0f, 0f)), // 3. quick double jab
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.42f, -60f, 0f, 0f, -45f), K(0.62f, -48f, 0f, 0f, -10f), K(1f, 0f, 0f, 0f, 0f))),// 4. lunging stab (finisher)
                    null, K_None, 0.24f, 0.34f)
            },

            // katana — iaido draws: draw-slice, reverse draw, 360° spin, rising iai cuts.
            {
                "katana", new WeaponAnimDef(OffArm.Mirror, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.25f, -70f, 70f, 0f, -40f), K(0.88f, -60f, -200f, 0f, -10f), K(1f, 0f, 0f, 0f, 0f)),   // 1. sheathe draw-slice (deep elbow curl)
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.25f, -70f, -70f, 0f, -40f), K(0.88f, -60f, 200f, 0f, -10f), K(1f, 0f, 0f, 0f, 0f)),  // 2. reverse (left) draw
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -85f, 90f, 0f, -30f), K(0.55f, -90f, -180f, 0f, -15f), K(0.85f, -70f, -260f, 0f, -8f), K(1f, 0f, 0f, 0f, 0f)), // 3. full 360° spinning sweep
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -120f, -50f, 0f, -20f), K(0.55f, -95f, 40f, 0f, -6f), K(0.75f, -135f, -30f, 0f, -14f), K(1f, 0f, 0f, 0f, 0f))), // 4. rising diagonal iai cuts
                    null, K_None, 0.45f, 0.60f)
            },

            // greataxe — brutal cleaves: overhand, chest sweep, diagonal chop, 360° spin cleave.
            {
                "greataxe", new WeaponAnimDef(OffArm.Mirror, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -120f, 0f, 0f, 8f), K(0.70f, -65f, 0f, 0f, 12f), K(1f, 0f, 0f, 0f, 0f)),       // 1. overhand cleave
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -45f, -60f, 0f, 10f), K(0.75f, -75f, 60f, 0f, 6f), K(1f, 0f, 0f, 0f, 0f)),    // 2. chest-level backswing sweep left→right
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -90f, 55f, 0f, 8f), K(0.70f, -70f, -50f, 0f, 10f), K(1f, 0f, 0f, 0f, 0f)),   // 3. diagonal shoulder chop right→left
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -85f, 80f, 0f, 6f), K(0.55f, -80f, -160f, 0f, 4f), K(0.80f, -70f, -200f, 0f, 8f), K(1f, 0f, 0f, 0f, 0f))), // 4. 360° spin cleave (finisher)
                    null, K_None, 0.40f, 0.60f)
            },

            // lance — mounted-style: low thrust, high lunge, couched charge, overhead riposte.
            {
                "lance", new WeaponAnimDef(OffArm.Mirror, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, 20f, 0f, 0f, 38f), K(0.75f, -65f, 0f, 0f, -10f), K(1f, 0f, 0f, 0f, 0f)),     // 1. pull back → low thrust
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, 10f, 15f, 0f, 42f), K(0.75f, -75f, 0f, 0f, -14f), K(1f, 0f, 0f, 0f, 0f)),   // 2. high lunge
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -20f, 0f, 0f, 30f), K(0.45f, -20f, 0f, 0f, 34f), K(0.80f, -70f, 0f, 0f, -6f), K(1f, 0f, 0f, 0f, 0f)), // 3. couched charge (hold then drive)
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -120f, 0f, 0f, 18f), K(0.60f, -70f, 0f, 0f, -16f), K(1f, 0f, 0f, 0f, 0f))),  // 4. overhead-to-thrust riposte (finisher)
                    null, K_None, 0.32f, 0.48f)
            },

            // gauntlets — boxer chain: jab, cross, double, uppercut (dual-wield alternates hands).
            {
                "gauntlets", new WeaponAnimDef(OffArm.None, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -72f, 0f, 0f, -24f), K(0.48f, -24f, 0f, 0f, 34f), K(1f, 0f, 0f, 0f, 0f)),    // 1. straight jab
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -65f, 28f, 0f, -22f), K(0.48f, -20f, 0f, 0f, 30f), K(1f, 0f, 0f, 0f, 0f)), // 2. side cross
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -72f, 0f, 0f, -24f), K(0.48f, -24f, 0f, 0f, 34f), K(0.72f, -72f, 0f, 0f, -24f), K(1f, 0f, 0f, 0f, 0f)), // 3. quick double jab
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -140f, -10f, 0f, -35f), K(0.55f, -60f, 8f, 0f, -18f), K(1f, 0f, 0f, 0f, 0f))), // 4. heavy uppercut (finisher)
                    null, K_Dual, 0.26f, 0.36f)
            },

            // longbow — archer shots: snap, aimed draw-hold, step release, rapid double (asym arms).
            {
                "longbow", new WeaponAnimDef(OffArm.Asym,
                    // Owner (right) = the draw hand.
                    V(
                        T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -20f, -15f, 0f, -90f), K(0.55f, -30f, -15f, 0f, -60f), K(0.75f, -55f, 0f, 0f, -15f), K(1f, 0f, 0f, 0f, 0f)),   // 1. quick snap shot
                        T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -25f, -20f, 0f, -120f), K(0.65f, -25f, -20f, 0f, -120f), K(0.80f, -55f, 0f, 0f, -15f), K(1f, 0f, 0f, 0f, 0f)),   // 2. aimed draw-hold-loose
                        T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -25f, -25f, 0f, -110f), K(0.55f, -30f, -25f, 0f, -110f), K(0.72f, -50f, 10f, 0f, -20f), K(1f, 0f, 0f, 0f, 0f)),  // 3. step-forward release
                        T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -22f, -15f, 0f, -95f), K(0.45f, -35f, -15f, 0f, -60f), K(0.60f, -50f, 10f, 0f, -15f), K(0.70f, -25f, -15f, 0f, -90f), K(0.85f, -35f, -15f, 0f, -60f), K(1f, 0f, 0f, 0f, 0f))), // 4. rapid double shot
                    // Other (left) = the bow arm, extended toward the target.
                    V(
                        T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -85f, 0f, 0f, -6f), K(0.75f, -82f, 0f, 0f, -4f), K(1f, 0f, 0f, 0f, 0f)),
                        T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -85f, 0f, 0f, -6f), K(0.65f, -85f, 0f, 0f, -6f), K(0.80f, -82f, 0f, 0f, -4f), K(1f, 0f, 0f, 0f, 0f)),
                        T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -85f, 5f, 0f, -6f), K(0.40f, -82f, 5f, 0f, -4f), K(0.72f, -80f, 0f, 0f, -4f), K(1f, 0f, 0f, 0f, 0f)),
                        T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -84f, 0f, 0f, -6f), K(0.60f, -80f, 0f, 0f, -4f), K(0.70f, -84f, 0f, 0f, -6f), K(1f, 0f, 0f, 0f, 0f))),
                    K_None, 0.50f, 0.80f)
            },

            // throwing_hammer — windmills: underhand lob, overhand toss, side skip, full windmill.
            {
                "throwing_hammer", new WeaponAnimDef(OffArm.None, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -95f, 0f, 0f, -45f), K(0.70f, -40f, 0f, 0f, -5f), K(1f, 0f, 0f, 0f, 0f)),      // 1. underhand lob
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.32f, -145f, 0f, 0f, -80f), K(0.70f, -50f, 0f, 0f, -5f), K(1f, 0f, 0f, 0f, 0f)),   // 2. overhand toss (elbow cocked)
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -100f, 55f, 0f, -20f), K(0.70f, -45f, 10f, 0f, -6f), K(1f, 0f, 0f, 0f, 0f)), // 3. side skip throw
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -160f, 0f, 0f, -70f), K(0.55f, -150f, 0f, 0f, -40f), K(0.75f, -55f, 0f, 0f, -8f), K(1f, 0f, 0f, 0f, 0f))),  // 4. full windmill overhead (finisher)
                    null, K_None, 0.32f, 0.45f)
            },

            // warhammer — slow crushing: telegraphed slam, side smashes, two-handed ground pound.
            {
                "warhammer", new WeaponAnimDef(OffArm.Mirror, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.40f, -155f, 0f, 0f, 6f), K(0.55f, -155f, 0f, 0f, 6f), K(0.72f, -65f, 0f, 0f, 16f), K(0.86f, -72f, 0f, 0f, 22f), K(1f, 0f, 0f, 0f, 0f)),  // 1. telegraphed overhead slam
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.40f, -130f, -60f, 0f, 8f), K(0.55f, -130f, -60f, 0f, 8f), K(0.72f, -70f, -10f, 0f, 14f), K(1f, 0f, 0f, 0f, 0f)),            // 2. left-side smash
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.40f, -130f, 60f, 0f, 8f), K(0.55f, -130f, 60f, 0f, 8f), K(0.72f, -70f, 10f, 0f, 14f), K(1f, 0f, 0f, 0f, 0f)),            // 3. right backhand smash
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -155f, 0f, 0f, 6f), K(0.50f, -155f, 0f, 0f, 6f), K(0.70f, -20f, 0f, 0f, 20f), K(0.82f, -15f, 0f, 0f, 26f), K(1f, 0f, 0f, 0f, 0f))), // 4. two-handed ground pound (finisher)
                    null, K_None, 0.55f, 0.75f)
            },

            // staff — caster: raised channel, angled sweeps, full overhead arc slam.
            {
                "staff", new WeaponAnimDef(OffArm.None, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -60f, 0f, 0f, -18f), K(0.70f, -60f, 0f, 0f, -18f), K(1f, 0f, 0f, 0f, 0f)),     // 1. raised channel
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -60f, -35f, 0f, -18f), K(0.70f, -55f, -35f, 0f, -18f), K(1f, 0f, 0f, 0f, 0f)), // 2. angled sweep left
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -60f, 35f, 0f, -18f), K(0.70f, -55f, 35f, 0f, -18f), K(1f, 0f, 0f, 0f, 0f)),  // 3. angled sweep right
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -135f, 0f, 0f, 6f), K(0.60f, -100f, 0f, 0f, -6f), K(0.80f, -55f, 0f, 0f, -12f), K(1f, 0f, 0f, 0f, 0f))), // 4. full overhead arc slam (finisher)
                    null, K_Staff, 0.42f, 0.62f)
            },

            // holy_book — tome chants: single raise, open-page, side tilt, beatific wide raise.
            {
                "holy_book", new WeaponAnimDef(OffArm.None, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -45f, 0f, 0f, -26f), K(0.70f, -45f, 0f, 0f, -26f), K(1f, 0f, 0f, 0f, 0f)),       // 1. single raise
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -55f, 18f, 0f, -34f), K(0.55f, -52f, 22f, 0f, -32f), K(0.75f, -58f, 14f, 0f, -36f), K(1f, 0f, 0f, 0f, 0f)), // 2. open-page two-hand raise
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -45f, -30f, 0f, -30f), K(0.70f, -42f, -28f, 0f, -30f), K(1f, 0f, 0f, 0f, 0f)), // 3. side-tilt chant
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -80f, 0f, 0f, -40f), K(0.60f, -85f, 0f, 0f, -44f), K(1f, 0f, 0f, 0f, 0f))),    // 4. beatific wide raise (finisher)
                    null, K_Book, 0.44f, 0.64f)
            },

            // bone_wand — quick flicks: up, side, downward point, wide swirl.
            {
                "bone_wand", new WeaponAnimDef(OffArm.None, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -70f, 0f, 0f, -14f), K(0.70f, -70f, 0f, 0f, -14f), K(1f, 0f, 0f, 0f, 0f)),      // 1. up flick
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -70f, -40f, 0f, -14f), K(0.70f, -68f, -40f, 0f, -14f), K(1f, 0f, 0f, 0f, 0f)), // 2. side flick left
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -45f, 0f, 0f, -10f), K(0.70f, -40f, 0f, 0f, -10f), K(1f, 0f, 0f, 0f, 0f)),    // 3. downward point
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -85f, 55f, 0f, -18f), K(0.55f, -70f, -30f, 0f, -14f), K(0.75f, -80f, 60f, 0f, -16f), K(1f, 0f, 0f, 0f, 0f))), // 4. wide swirling flick (finisher)
                    null, K_Wand, 0.38f, 0.56f)
            },

            // control_orb — orbiting arcs: low, high wide, figure-eight, grand circle.
            {
                "control_orb", new WeaponAnimDef(OffArm.None, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -80f, 10f, 0f, -12f), K(0.70f, -80f, 10f, 0f, -12f), K(1f, 0f, 0f, 0f, 0f)),       // 1. low arc
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -95f, -55f, 0f, -14f), K(0.70f, -90f, -55f, 0f, -14f), K(1f, 0f, 0f, 0f, 0f)),  // 2. high wide arc left
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -85f, 45f, 0f, -10f), K(0.55f, -75f, -35f, 0f, -12f), K(0.75f, -88f, 45f, 0f, -10f), K(1f, 0f, 0f, 0f, 0f)), // 3. figure-eight sweep
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -160f, 0f, 0f, -10f), K(0.60f, -120f, 0f, 0f, -6f), K(0.85f, -80f, 0f, 0f, -12f), K(1f, 0f, 0f, 0f, 0f))),   // 4. grand full circle (finisher)
                    null, K_Orb, 0.46f, 0.66f)
            },

            // lute — bard strums: single, double, side tilt, flourish.
            {
                "lute", new WeaponAnimDef(OffArm.None, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -30f, 15f, 0f, -34f), K(0.70f, -30f, 15f, 0f, -34f), K(1f, 0f, 0f, 0f, 0f)),   // 1. single strum
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -30f, 15f, 0f, -34f), K(0.50f, -28f, 12f, 0f, -30f), K(0.70f, -32f, 18f, 0f, -36f), K(1f, 0f, 0f, 0f, 0f)), // 2. double strum
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -30f, -25f, 0f, -32f), K(0.70f, -28f, -28f, 0f, -34f), K(1f, 0f, 0f, 0f, 0f)), // 3. side-tilt strum
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -40f, 30f, 0f, -38f), K(0.55f, -30f, 10f, 0f, -30f), K(0.75f, -45f, 35f, 0f, -40f), K(1f, 0f, 0f, 0f, 0f))), // 4. flourish strum (finisher)
                    null, K_Lute, 0.42f, 0.60f)
            },
        };

    private static readonly WeaponAnimDef FallbackDef = new WeaponAnimDef(OffArm.None,
        V(
            T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -75f, 55f, 0f, -15f), K(0.62f, -80f, -45f, 0f, -5f), K(1f, 0f, 0f, 0f, 0f))),
        null, K_None, 0.30f, 0.50f);

    private string _weaponId;
    private WeaponAnimDef _def;
    private PoseKey[] _set;       // currently selected combo variant (owner/throwing arm)
    private PoseKey[] _otherSet;  // currently selected combo variant (support arm, Asym only)

    // Rest pose snapshots, re-captured each attack (re-parent safe).
    private Vector3 _basePos;
    private Vector3 _baseEuler;
    private Vector3 _baseScale;
    private Quaternion _ownerShBase;
    private Quaternion _ownerElBase;
    private Quaternion _otherShBase;
    private Quaternion _otherElBase;

    private bool _active;
    private bool _heavy;
    private float _t;
    private float _duration;
    private bool _offHand;

    private PlayerAnimator _playerAnim;
    private Transform _ownerShoulder;
    private Transform _ownerElbow;
    private Transform _otherShoulder;
    private Transform _otherElbow;

    // Ready-stance sway — keeps the drawn weapon alive while the player stands still in combat.
    private WeaponStowAnimator _stow;
    private Quaternion _swayShBase;
    private Quaternion _swayElBase;
    private Quaternion _swayOtherShBase;
    private Quaternion _swayOtherElBase;
    private float _swayGuard;
    private bool _swayActive;

    // Impact pulse — strike time/direction auto-detected from the track, plus recoil decay.
    private float _impactT = 0.6f;
    private float _strikeDir = 1f;
    private float _pulse;

    // 1 when the current swing sweeps horizontally (yaw-dominant = side slash) — gates the
    // tip-down → sideways slash lead so chops and thrusts keep their own motion.
    private float _sweepScale;

    private static readonly HashSet<string> _leadLogged = new HashSet<string>();

    private void OnEnable()
    {
        var host = GetComponent<WeaponRigHost>();
        var data = host != null ? host.Data : null;
        _weaponId = data != null ? data.id : null;
        _def = _weaponId != null && Defs.TryGetValue(_weaponId, out var d) ? d : FallbackDef;

        _playerAnim = GetComponentInParent<PlayerAnimator>();
        _stow = GetComponentInParent<WeaponStowAnimator>();
    }

    /// <summary>
    /// Kick off an attack visual. Returns the duration <see cref="CombatController"/> should keep
    /// the player locked for, so slower weapons (bow draw, warhammer windup) stay in sync.
    /// </summary>
    public float PlayAttack(bool heavy, int variant = 0)
    {
        // Combo-variant selection cycles through the authored swing set; lone tracks stay put.
        int count = _def.VariantCount;
        int index = ((variant % count) + count) % count;
        _set = _def.Owner != null && _def.Owner.Length > 0 ? _def.Owner[index] : null;
        _otherSet = _def.Other != null && _def.Other.Length > 0 ? _def.Other[index] : null;

        // Hand the arms over from the ready sway to the attack cleanly.
        StopSway();
        DetectStrike(_set);
        _sweepScale = SlashSweepScale(_set);

        _heavy = heavy;
        float speed = SpeedScale();
        _duration = Mathf.Max(0.001f, (heavy ? _def.TimeHeavy : _def.TimeLight) * BaseSwingTimeScale / speed);
        _t = 0f;
        _active = true;

        // Rest poses freshly captured so re-parenting onto a hand (ReparentToHands) is harmless.
        _basePos = transform.localPosition;
        _baseEuler = transform.localRotation.eulerAngles;
        _baseScale = transform.localScale;

        ResolvePivots();

        _ownerShBase = _ownerShoulder != null ? _ownerShoulder.localRotation : Quaternion.identity;
        _ownerElBase = _ownerElbow != null ? _ownerElbow.localRotation : Quaternion.identity;
        _otherShBase = _otherShoulder != null ? _otherShoulder.localRotation : Quaternion.identity;
        _otherElBase = _otherElbow != null ? _otherElbow.localRotation : Quaternion.identity;

        if (_playerAnim != null) _playerAnim.AcquireArms();

        if (WeaponRigBuilder.LogRigging && _weaponId != null && _leadLogged.Add(_weaponId))
        {
            var l = LeadScale();
            Debug.Log("[WeaponAnim] blade-lead live for '" + _weaponId + "': pitch " + l.pitch.ToString("0.00") +
                " yaw " + l.yaw.ToString("0.00") + " strike snap " + l.snapDeg.ToString("0") + "\u00B0" +
                (_def.SlashLead
                    ? ", slash-lead ON (down " + SlashDownDeg.ToString("0") + "\u00B0 side " + SlashYawDeg.ToString("0") + "\u00B0)"
                    : ", slash-lead off"));
        }
        return _duration;
    }

    private void Update()
    {
        if (_active)
        {
            UpdateAttack();
            return;
        }
        UpdateSway();
    }

    private void UpdateAttack()
    {
        _t += Time.deltaTime;
        float t = Mathf.Clamp01(_t / _duration);

        // Dual-wield gauntlets alternate hands by half a phase so the punches land one-two.
        if (_def.Accent == K_Dual && _offHand) t = Mathf.Repeat(t + 0.5f, 1f);

        float h = _heavy ? 1.15f : 1f;

        PoseKey k = Sample(_set, t);
        Vector3 sh = new Vector3(ClampShX(k.shX), k.shY, k.shZ) * h;
        float el = k.elX * h;

        // Blade lead: rotate the weapon INTO the swing so the tip leads the arc instead of being
        // dragged along at a fixed grip angle. A subtle angle follows the shoulder track (the blade
        // turns with the horizontal yaw and dives with the vertical pitch; elbow-driven thrusts
        // barely move their axes so jabs stay tight), plus a strike snap that briefly whips the
        // blade through the hit along the swing's travel direction. Tracks return to zero on
        // recovery, so the lead eases back to the grip.
        BladeLead lead = LeadScale();
        float snap = _strikeDir * lead.snapDeg * h * Mathf.Exp(-Mathf.Abs(t - _impactT) * 8f)
            * (_def.SlashLead && _sweepScale > 0f ? 0.3f : 1f);
        Vector3 leadEuler = new Vector3(k.shX * lead.pitch, k.shY * lead.yaw + snap, 0f);

        // Side-slash two-stage lead: the blade pitches tip-DOWN ~90° during the windup, then turns
        // ~90° SIDEWAYS into the swing's travel, and HOLDS that pose straight from the hand through
        // the sweep — a clean slashing line instead of a vertical blade wobbling sideways. Released
        // only right at recovery. Chops and thrusts (sweepScale 0) are untouched.
        if (_def.SlashLead && _sweepScale > 0f)
        {
            float hold = 1f - Ease(Seg(t, 0.85f, 0.97f));
            float down = hold * Ease(Seg(t, 0.05f, 0.35f));
            float side = hold * Ease(Seg(t, 0.30f, 0.50f));
            leadEuler.x += -SlashDownDeg * down;
            leadEuler.y += _strikeDir * SlashYawFlip * SlashYawDeg * side;
        }

        // Impact pulse: right after the strike the arm rebounds briefly (a recoil kick opposite the
        // swing's travel) and the blade shoves forward a touch, then eases into follow-through.
        _pulse = _impactT > 0f && t >= _impactT
            ? Mathf.Min(1f, Mathf.Exp(-(t - _impactT) * 18f))
            : 0f;
        if (_pulse > 0f)
        {
            sh += new Vector3(0f, -_strikeDir * 6f * h * _pulse, 0f);
            el += 4f * h * _pulse;
        }

        if (_ownerShoulder != null)
            _ownerShoulder.localRotation = _ownerShBase * Quaternion.Euler(sh);
        if (_ownerElbow != null)
            _ownerElbow.localRotation = _ownerElBase * Quaternion.Euler(el, 0f, 0f);

        switch (_def.Mode)
        {
            case OffArm.Mirror:
                // Two-hand grip: the support arm mirrors the swing (yaw flipped side-to-side).
                if (_otherShoulder != null)
                    _otherShoulder.localRotation = _otherShBase * Quaternion.Euler(ClampShX(k.shX) * h, -k.shY * h, k.shZ * h);
                if (_otherElbow != null)
                    _otherElbow.localRotation = _otherElBase * Quaternion.Euler(el, 0f, 0f);
                break;

            case OffArm.Asym:
                PoseKey ok = Sample(_otherSet, t);
                if (_otherShoulder != null)
                    _otherShoulder.localRotation = _otherShBase * Quaternion.Euler(ClampShX(ok.shX) * h, ok.shY * h, ok.shZ * h);
                if (_otherElbow != null)
                    _otherElbow.localRotation = _otherElBase * Quaternion.Euler(ok.elX * h, 0f, 0f);
                break;
        }

        // The weapon rides the hand; only magic focuses add a small local accent.
        ApplyAccent(_def.Accent, t, out Vector3 aEuler, out Vector3 aPos, out float aScale);
        transform.localRotation = Quaternion.Euler(_baseEuler + aEuler + leadEuler);
        transform.localPosition = _basePos + aPos + new Vector3(0f, 0f, 0.02f * h * _pulse);
        transform.localScale = _baseScale * aScale;

        if (_t >= _duration)
            End();
    }

    private void End()
    {
        _active = false;
        transform.localRotation = Quaternion.Euler(_baseEuler);
        transform.localPosition = _basePos;
        transform.localScale = _baseScale;
        if (_ownerShoulder != null) _ownerShoulder.localRotation = _ownerShBase;
        if (_ownerElbow != null) _ownerElbow.localRotation = _ownerElBase;
        if (_otherShoulder != null) _otherShoulder.localRotation = _otherShBase;
        if (_otherElbow != null) _otherElbow.localRotation = _otherElBase;
        if (_playerAnim != null) _playerAnim.ReleaseArms();
    }

    /// <summary>
    /// From a hand-pivoted weapon rig, climb to the arm's shoulder pivot (rig -> HandR/L ->
    /// ElbowR/L -> ShoulderR/L) regardless of how deep the arm chain is.
    /// </summary>
    private static Transform FindOwnerShoulder(Transform from)
    {
        var p = from;
        while (p != null)
        {
            if (p.name.StartsWith("Shoulder")) return p;
            p = p.parent;
        }
        return null;
    }

    /// <summary>Climb to the arm's elbow pivot the same way as <see cref="FindOwnerShoulder"/>.</summary>
    private static Transform FindOwnerElbow(Transform from)
    {
        var p = from;
        while (p != null)
        {
            if (p.name.StartsWith("Elbow")) return p;
            p = p.parent;
        }
        return null;
    }

    private void OnDisable()
    {
        StopSway();
        if (_playerAnim != null) _playerAnim.ReleaseArms();
    }

    // ──────────────────────────────────────────────────────────
    //  Ready-stance sway + strike detection
    // ──────────────────────────────────────────────────────────

    /// <summary>Resolve the arm chain (rig -> HandL/R -> ElbowL/R -> ShoulderL/R) fresh each frame.</summary>
    private void ResolvePivots()
    {
        if (_playerAnim == null) _playerAnim = GetComponentInParent<PlayerAnimator>();
        var parent = transform.parent;
        _offHand = parent != null && parent.name == "HandL";
        _ownerShoulder = FindOwnerShoulder(parent);
        _ownerElbow = FindOwnerElbow(parent);
        _otherShoulder = _playerAnim != null ? (_offHand ? _playerAnim.ShoulderR : _playerAnim.ShoulderL) : null;
        _otherElbow = _playerAnim != null ? (_offHand ? _playerAnim.ElbowR : _playerAnim.ElbowL) : null;
    }

    /// <summary>True when the sway can safely own the arms: drawn, transition settled, player standing.</summary>
    private bool ShouldSway()
    {
        if (_playerAnim == null || _playerAnim.Controller == null) return false;
        var pc = _playerAnim.Controller;
        if (pc.IsSitting || pc.IsRiding || pc.IsMoving) return false;
        if (_stow == null || !_stow.IsDrawn || _stow.IsBusy) return false;
        ResolvePivots();
        return _ownerShoulder != null;
    }

    private void StartSway()
    {
        ResolvePivots();
        if (_ownerShoulder == null) return;
        _swayShBase = _ownerShoulder.localRotation;
        _swayElBase = _ownerElbow != null ? _ownerElbow.localRotation : Quaternion.identity;
        if (_def.Mode == OffArm.Mirror)
        {
            _swayOtherShBase = _otherShoulder != null ? _otherShoulder.localRotation : Quaternion.identity;
            _swayOtherElBase = _otherElbow != null ? _otherElbow.localRotation : Quaternion.identity;
        }
        _basePos = transform.localPosition;
        _baseEuler = transform.localRotation.eulerAngles;
        _baseScale = transform.localScale;
        _swayGuard = 0f;
        _swayActive = true;
        if (_playerAnim != null) _playerAnim.AcquireArms();
    }

    private void StopSway()
    {
        if (!_swayActive) return;
        _swayActive = false;
        transform.localRotation = Quaternion.Euler(_baseEuler);
        transform.localPosition = _basePos;
        transform.localScale = _baseScale;
        if (_playerAnim != null) _playerAnim.ReleaseArms();
    }

    private void UpdateSway()
    {
        if (_swayActive && !ShouldSway())
        {
            StopSway();
            return;
        }
        if (!_swayActive)
        {
            if (!ShouldSway()) return;
            StartSway();
        }
        _swayGuard = Mathf.MoveTowards(_swayGuard, 1f, Time.deltaTime / 0.25f);

        float t = Time.time;
        float phase = _offHand ? 0.5f : 0f;
        Vector3 sh = new Vector3(
            SwayGuardPitch() * _swayGuard + Mathf.Sin(t * 1.6f + phase * 1.3f) * 2.5f,
            Mathf.Sin(t * 1.1f + phase) * 1.8f,
            Mathf.Sin(t * 0.8f + phase * 0.7f) * 0.8f);
        float el = Mathf.Sin(t * 1.35f + phase * 0.5f) * 1.2f;

        if (_ownerShoulder != null)
            _ownerShoulder.localRotation = _swayShBase * Quaternion.Euler(sh);
        if (_ownerElbow != null)
            _ownerElbow.localRotation = _swayElBase * Quaternion.Euler(el, 0f, 0f);
        if (_def.Mode == OffArm.Mirror)
        {
            if (_otherShoulder != null)
                _otherShoulder.localRotation = _swayOtherShBase * Quaternion.Euler(sh.x, -sh.y, sh.z);
            if (_otherElbow != null)
                _otherElbow.localRotation = _swayOtherElBase * Quaternion.Euler(el, 0f, 0f);
        }

        // Magic focuses stay lit while armed: loop the weapon-local accent with a slow soft pulse.
        if (_def.Accent != K_None && _def.Accent != K_Dual)
        {
            float s = 0.5f - 0.5f * Mathf.Cos(t * 0.9f);
            ApplyAccent(_def.Accent, 0.35f + 0.35f * s, out Vector3 aEuler, out Vector3 aPos, out float aScale);
            transform.localRotation = Quaternion.Euler(_baseEuler + aEuler);
            transform.localPosition = _basePos + aPos;
            transform.localScale = _baseScale * aScale;
        }
    }

    /// <summary>Idle shoulder-pitch guard so blades hold a ready stance rather than hanging limp.</summary>
    private float SwayGuardPitch()
    {
        switch (_def.Accent)
        {
            case K_None:
            case K_Dual:
                return -14f;   // melee: ready guard
            case K_Lute:
                return -8f;    // held at the side, gently lifted
            default:
                return 0f;     // casters keep a neutral stance
        }
    }

    private struct BladeLead
    {
        public float pitch;
        public float yaw;
        public float snapDeg;

        public BladeLead(float pitch, float yaw, float snapDeg)
        {
            this.pitch = pitch;
            this.yaw = yaw;
            this.snapDeg = snapDeg;
        }
    }

    /// <summary>
    /// Weapon-local blade-lead: how strongly the held weapon rotates into the swing pattern
    /// (pitch = follows the vertical arc, yaw = follows the horizontal arc), plus strike-snap
    /// degrees whipped through the hit. One-hand blades lash tip-first; two-handers get dragged
    /// more; casters and the longbow stay neutral (their accents already own the weapon look).
    /// </summary>
    private BladeLead LeadScale()
    {
        if (_def.Accent == K_None || _def.Accent == K_Dual)
            return _def.Mode == OffArm.Mirror
                ? new BladeLead(0.10f, 0.14f, 16f)
                : new BladeLead(0.15f, 0.20f, 22f);
        if (_def.Accent == K_Lute)
            return new BladeLead(0.12f, 0.16f, 14f);
        return new BladeLead(0f, 0f, 0f);
    }

    /// <summary>Shoulder pitch clamp — authored high-raises can swing the arm behind the head.</summary>
    private static float ClampShX(float v) => Mathf.Clamp(v, -150f, 40f);

    /// <summary>
    /// 1 when a track sweeps horizontally (shoulder-yaw motion dominates = side slash), else 0 —
    /// gates the tip-down → sideways slash lead so overhead chops and elbow thrusts keep their own
    /// motion. A small yaw floor avoids classifying near-straight jabs as slashes.
    /// </summary>
    private static float SlashSweepScale(PoseKey[] track)
    {
        if (track == null || track.Length < 2) return 0f;
        float dY = 0f, dX = 0f, dE = 0f;
        for (int i = 0; i < track.Length - 1; i++)
        {
            dY += Mathf.Abs(track[i + 1].shY - track[i].shY);
            dX += Mathf.Abs(track[i + 1].shX - track[i].shX);
            dE += Mathf.Abs(track[i + 1].elX - track[i].elX);
        }
        return dY >= 0.8f * (dX + dE) && dY > 5f ? 1f : 0f;
    }

    /// <summary>Auto-detect the strike moment of a swing = midpoint of its largest single segment.</summary>
    private void DetectStrike(PoseKey[] track)
    {
        _impactT = 0.6f;
        _strikeDir = 1f;
        if (track == null || track.Length < 2) return;
        float best = -1f;
        for (int i = 0; i < track.Length - 1; i++)
        {
            PoseKey a = track[i];
            PoseKey b = track[i + 1];
            float mag = Mathf.Abs(b.shX - a.shX) + Mathf.Abs(b.shY - a.shY)
                      + Mathf.Abs(b.shZ - a.shZ) + Mathf.Abs(b.elX - a.elX);
            if (mag > best)
            {
                best = mag;
                _impactT = Mathf.Lerp(a.t, b.t, 0.5f);
                float dY = b.shY - a.shY;
                float dX = b.shX - a.shX;
                _strikeDir = Mathf.Abs(dY) > Mathf.Abs(dX) ? (dY >= 0f ? 1f : -1f) : (dX >= 0f ? 1f : -1f);
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    //  Pose-track playback
    // ──────────────────────────────────────────────────────────

    private static PoseKey Sample(PoseKey[] track, float t)
    {
        if (track == null || track.Length == 0) return new PoseKey(0f, 0f, 0f, 0f, 0f);
        if (track.Length == 1) return track[0];
        if (t <= track[0].t) return track[0];
        if (t >= track[track.Length - 1].t) return track[track.Length - 1];

        for (int i = 0; i < track.Length - 1; i++)
        {
            if (t < track[i + 1].t)
            {
                float u = Ease(Seg(t, track[i].t, track[i + 1].t));
                return new PoseKey(
                    t,
                    Mathf.Lerp(track[i].shX, track[i + 1].shX, u),
                    Mathf.Lerp(track[i].shY, track[i + 1].shY, u),
                    Mathf.Lerp(track[i].shZ, track[i + 1].shZ, u),
                    Mathf.Lerp(track[i].elX, track[i + 1].elX, u));
            }
        }
        return track[track.Length - 1];
    }

    /// <summary>
    /// Small weapon-local accent per magic weapon on top of the arm choreography: an arc (staff /
    /// orb), a chant sway (book), a size pulse (wand) or a string strum (lute). Melee returns 0.
    /// </summary>
    private static void ApplyAccent(int accent, float t, out Vector3 euler, out Vector3 pos, out float scale)
    {
        euler = Vector3.zero;
        pos = Vector3.zero;
        scale = 1f;
        if (accent == K_None) return;

        float pulse = Mathf.Sin(Mathf.PI * Seg(t, 0.35f, 0.70f));

        switch (accent)
        {
            case K_Staff:
                euler = new Vector3(0f, 0f, 14f * pulse);
                pos = new Vector3(0f, 0.02f * Seg(t, 0.35f, 0.70f), 0f);
                break;
            case K_Book:
                euler = new Vector3(0f, 16f * pulse, 0f);
                pos = new Vector3(0f, 0.10f * Seg(t, 0.35f, 0.70f), 0f);
                break;
            case K_Wand:
                euler = new Vector3(0f, 0f, 10f * pulse);
                scale = 1f + 0.26f * pulse;
                break;
            case K_Orb:
                euler = new Vector3(0f, 0f, 30f * pulse);
                pos = new Vector3(0f, 0.16f * Seg(t, 0.35f, 0.70f), 0f);
                break;
            case K_Lute:
                scale = 1f + 0.08f * Mathf.Sin(Mathf.PI * 4f * Seg(t, 0f, 1f));
                break;
        }
    }

    private static float Ease(float t) => t * t * (3f - 2f * t);

    private static float Seg(float t, float a, float b) => Mathf.InverseLerp(a, b, t);
}