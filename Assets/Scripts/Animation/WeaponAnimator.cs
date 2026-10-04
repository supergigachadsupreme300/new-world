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

    /// <summary>Normalized time of the hold plateau on charge/release tracks — the pose the arm
    /// samples (and holds) while a cast/draw is charging, and where the loose resumes from.</summary>
    private const float ChargeHoldT = 0.30f;

    /// <summary>Seconds the guard takes to raise into its hold pose (eased grab-in).</summary>
    private const float GuardRaiseTime = 0.18f;

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
        public float wrX, wrY;          // owner wrist flex / roll (additive)

        public PoseKey(float t, float shX, float shY, float shZ, float elX, float wrX = 0f, float wrY = 0f)
        {
            this.t = t;
            this.shX = shX; this.shY = shY; this.shZ = shZ;
            this.elX = elX;
            this.wrX = wrX; this.wrY = wrY;
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
    private static PoseKey K(float t, float shX, float shY, float shZ, float elX, float wrX, float wrY) => new PoseKey(t, shX, shY, shZ, elX, wrX, wrY);
    private static PoseKey[] T(params PoseKey[] keys) => keys;
    private static PoseKey[][] V(params PoseKey[][] sets) => sets;

    // Magic cast — ONE animation shared by every magic weapon: the casting hand raises up and
    // forward (palm presenting), holds, then a forward push beats out the spell at launch, then the
    // arm recovers. The t=0.30 plateau IS the live charge-hold pose (ChargeHoldT), so the raised
    // hand stays put until the spell is actually launched; the cast push only plays at launch.
    private static readonly PoseKey[] MagicCastKeys = T(
        K(0f, 0f, 0f, 0f, 0f),
        K(0.30f, -72f, 0f, 0f, -36f, 45f, 0f),  // raise hand up and forward, palm presenting
        K(0.60f, -72f, 0f, 0f, -36f, 45f, 0f),  // hold — persists while charging
        K(0.82f, -78f, 0f, 0f, -8f, 15f, 0f),   // cast: extend + push forward at launch
        K(1f, 0f, 0f, 0f, 0f));                 // recover

    // Shield bash — shared by every shield: a short forward jab, a lateral sweep bash, and an
    // overhead slam (heavy/finisher). The wrist drives the shield face into the strike.
    private static readonly PoseKey[][] ShieldBashKeys = V(
        T(K(0f, 0f, 0f, 0f, 0f), K(0.25f, -55f, 0f, 0f, -25f, 30f, 0f), K(0.45f, -20f, 0f, 0f, -5f, 80f, 0f), K(1f, 0f, 0f, 0f, 0f)),
        T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -60f, -40f, 0f, -20f, 30f, -50f), K(0.55f, -70f, 30f, 0f, -8f, 70f, -50f), K(1f, 0f, 0f, 0f, 0f)),
        T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -140f, 0f, 0f, -10f, 30f, 0f), K(0.60f, -45f, 0f, 0f, -6f, 60f, 0f), K(1f, 0f, 0f, 0f, 0f)));

    private static readonly PoseKey IdentityPose = new PoseKey(0f, 0f, 0f, 0f, 0f);

    // Defense guard hold-pose per weapon (the "defense" set alongside the attack/swing set). The
    // arms raise into a distinct blocking stance while RMB is held and hold it until the guard
    // drops. Tuned per class; t=1 is the settled hold. Magic/ranged weapons never block (their RMB
    // is charge/draw) so they fall back to IdentityPose and stay neutral.
    private static readonly System.Collections.Generic.Dictionary<string, PoseKey> GuardPoses =
        new System.Collections.Generic.Dictionary<string, PoseKey>
        {
            { "buckler",      new PoseKey(1f, -88f, 10f, 0f, -38f, -25f, 95f) },
            { "round_shield", new PoseKey(1f, -88f, 10f, 0f, -38f, -25f, 95f) },
            { "tower_shield", new PoseKey(1f, -92f, 8f, 0f, -22f, -18f, 95f) },
            { "iron_sword",   new PoseKey(1f, -40f, 0f, 0f, -50f, 8f, 0f) },
            { "dagger",       new PoseKey(1f, -30f, 8f, 0f, -58f, 15f, 0f) },
            { "greatsword",   new PoseKey(1f, -122f, 0f, 0f, -18f, 28f, 0f) },
            { "warhammer",    new PoseKey(1f, -52f, 0f, 0f, -55f, 55f, 0f) },
            { "greataxe",     new PoseKey(1f, -60f, 0f, 0f, -45f, 30f, 0f) },
            { "katana",       new PoseKey(1f, -24f, 0f, 0f, -38f, -12f, 0f) },
            { "lance",        new PoseKey(1f, -26f, 0f, 0f, -28f, 6f, 0f) },
            { "fist",         new PoseKey(1f, -72f, 0f, 0f, -68f, 82f, 0f) },
            { "gauntlets",    new PoseKey(1f, -72f, 0f, 0f, -68f, 82f, 0f) },
        };

    private static readonly System.Collections.Generic.Dictionary<string, WeaponAnimDef> Defs =
        new System.Collections.Generic.Dictionary<string, WeaponAnimDef>
        {
            // iron_sword — balanced 1H blade: slash L→R, slash R→L, overhead chop, forward thrust.
            {
                "iron_sword", new WeaponAnimDef(OffArm.None, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -75f, -55f, 0f, -15f, 45f, -90f), K(0.52f, -80f, 45f, 0f, -5f, 125f, -90f), K(1f, 0f, 0f, 0f, 0f)),   // 1. wind left/back → slash across right, wrist rolls into the cut
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -75f, 55f, 0f, -15f, 45f, 90f), K(0.52f, -80f, -45f, 0f, -5f, 125f, 90f), K(1f, 0f, 0f, 0f, 0f)),  // 2. wind right/back → slash across left, wrist rolls through
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -150f, 0f, 0f, 10f, 45f, 0f), K(0.68f, -65f, 0f, 0f, 6f, 125f, 0f), K(1f, 0f, 0f, 0f, 0f)),     // 3. overhead chop — wrist flexes into the swing then settles
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -65f, 0f, 0f, 6f, 125f, 0f), K(0.68f, -150f, 0f, 0f, 10f, 45f, 0f), K(1f, 0f, 0f, 0f, 0f))), // 4. forward thrust — wrist flattens as the arm extends
                    null, K_None, 0.30f, 0.50f, true)
            },

            // greatsword — heavy two-hander: overhead slam, low sweep, reverse-grip sweep, rising spin.
            {
                "greatsword", new WeaponAnimDef(OffArm.Mirror, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -150f, 0f, 0f, 8f, 60f, 0f), K(0.70f, -55f, 0f, 0f, 14f, 30f, 0f), K(1f, 0f, 0f, 0f, 0f)),       // 1. overhead slam — wrists flex through the smash
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -70f, -75f, 0f, 8f, 30f, -90f), K(0.70f, -80f, 35f, 0f, 12f, 20f, -90f), K(1f, 0f, 0f, 0f, 0f)),   // 2. low sweep from the left across to the right — wrists roll into the cut
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -100f, 70f, 0f, 8f, 40f, 90f), K(0.68f, -50f, -55f, 0f, -10f, 25f, 90f), K(1f, 0f, 0f, 0f, 0f)),// 3. reverse-grip right wind, hard backswing to the left — wrists roll through
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -120f, 30f, 0f, -30f, 50f, 0f), K(0.75f, -160f, 0f, 0f, -20f, 20f, 0f), K(1f, 0f, 0f, 0f, 0f))),// 4. rising spin uppercut (finisher) — wrists flex to whip the spin
                    null, K_None, 0.45f, 0.65f)
            },

            // dagger — fast stabs: low jab, high jab, quick double, lunging stab.
            {
                "dagger", new WeaponAnimDef(OffArm.None, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -75f, -55f, 0f, -15f, 45f, -90f), K(0.52f, -80f, 45f, 0f, -5f, 125f, -90f), K(1f, 0f, 0f, 0f, 0f)),   // 1. wind left/back → slash across right, wrist rolls into the cut
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -75f, 55f, 0f, -15f, 45f, 90f), K(0.52f, -80f, -45f, 0f, -5f, 125f, 90f), K(1f, 0f, 0f, 0f, 0f)),  // 2. wind right/back → slash across left, wrist rolls through
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -150f, 0f, 0f, 10f, 45f, 0f), K(0.68f, -65f, 0f, 0f, 6f, 125f, 0f), K(1f, 0f, 0f, 0f, 0f)),     // 3. overhead chop — wrist flexes into the swing then settles
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -65f, 0f, 0f, 6f, 125f, 0f), K(0.68f, -150f, 0f, 0f, 10f, 45f, 0f), K(1f, 0f, 0f, 0f, 0f))),// 4. lunging stab (finisher) — wrist flattens as the arm extends
                    null, K_None, 0.24f, 0.34f)
            },

            // katana — iaido draws: draw-slice, reverse draw, 360° spin, rising iai cuts.
            {
                "katana", new WeaponAnimDef(OffArm.Mirror, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.25f, -70f, 70f, 0f, -40f, 25f, -90f), K(0.88f, -60f, -200f, 0f, -10f, 15f, -90f), K(1f, 0f, 0f, 0f, 0f)),   // 1. sheathe draw-slice (deep elbow curl) — wrist rolls with the blade
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.25f, -70f, -70f, 0f, -40f, 25f, 90f), K(0.88f, -60f, 200f, 0f, -10f, 15f, 90f), K(1f, 0f, 0f, 0f, 0f)),  // 2. reverse (left) draw
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -85f, 90f, 0f, -30f, 35f, 90f), K(0.55f, -90f, -180f, 0f, -15f, 30f, 90f), K(0.85f, -70f, -260f, 0f, -8f, 20f, 60f), K(1f, 0f, 0f, 0f, 0f)), // 3. full 360° spinning sweep — wrist keeps the edge on the arc
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -120f, -50f, 0f, -20f, 45f, 0f), K(0.55f, -95f, 40f, 0f, -6f, 30f, 0f), K(0.75f, -135f, -30f, 0f, -14f, 20f, 0f), K(1f, 0f, 0f, 0f, 0f))), // 4. rising diagonal iai cuts — wrists flex through
                    null, K_None, 0.45f, 0.60f)
            },

            // greataxe — brutal cleaves: overhand, chest sweep, diagonal chop, 360° spin cleave.
            {
                "greataxe", new WeaponAnimDef(OffArm.Mirror, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -120f, 0f, 0f, 8f, 65f, 0f), K(0.70f, -65f, 0f, 0f, 12f, 30f, 0f), K(1f, 0f, 0f, 0f, 0f)),       // 1. overhand cleave — wrists flex into the chop
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -45f, -60f, 0f, 10f, 25f, -90f), K(0.75f, -75f, 60f, 0f, 6f, 20f, -90f), K(1f, 0f, 0f, 0f, 0f)),    // 2. chest-level backswing sweep left→right — wrists roll into the cut
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -90f, 55f, 0f, 8f, 50f, 45f), K(0.70f, -70f, -50f, 0f, 10f, 35f, 45f), K(1f, 0f, 0f, 0f, 0f)),   // 3. diagonal shoulder chop right→left — wrists roll down the angle
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -85f, 80f, 0f, 6f, 40f, 90f), K(0.55f, -80f, -160f, 0f, 4f, 30f, 90f), K(0.80f, -70f, -200f, 0f, 8f, 20f, 60f), K(1f, 0f, 0f, 0f, 0f))), // 4. 360° spin cleave (finisher) — wrists roll through the spin
                    null, K_None, 0.40f, 0.60f)
            },

            // lance — mounted-style: low thrust, high lunge, couched charge, overhead riposte.
            {
                "lance", new WeaponAnimDef(OffArm.Mirror, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, 20f, 0f, 0f, 38f, 35f, 0f), K(0.75f, -65f, 0f, 0f, -10f, 85f, 0f), K(1f, 0f, 0f, 0f, 0f)),     // 1. pull back → low thrust — wrist drives the tip down
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, 10f, 15f, 0f, 42f, 35f, 0f), K(0.75f, -75f, 0f, 0f, -14f, 90f, 0f), K(1f, 0f, 0f, 0f, 0f)),   // 2. high lunge
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -20f, 0f, 0f, 30f, 20f, 0f), K(0.45f, -20f, 0f, 0f, 34f, 20f, 0f), K(0.80f, -70f, 0f, 0f, -6f, 90f, 0f), K(1f, 0f, 0f, 0f, 0f)), // 3. couched charge (hold then drive) — wrist from hold to snap
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -120f, 0f, 0f, 18f, 55f, 0f), K(0.60f, -70f, 0f, 0f, -16f, 85f, 0f), K(1f, 0f, 0f, 0f, 0f))),  // 4. overhead-to-thrust riposte (finisher)
                    null, K_None, 0.45f, 0.60f)
            },

            // fist — innate bare fists: same boxing chain as gauntlets (jab, cross, double, uppercut),
            // dual fists alternate hands off-phase and hold the ready guard at idle. No weapon visual.
            {
                "fist", new WeaponAnimDef(OffArm.None, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -45f, 0f, 0f, -90f, 70f, 20f), K(0.48f, -105f, 0f, 0f, 0f, 95f, 20f), K(1f, 0f, 0f, 0f, 0f)),    // 1. straight jab — fist snaps with the wrist
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, 0f, 0f, 90f, -90f, 70f, 20f), K(0.48f, 0f, -90f, 45f,-90f, 95f, 20f), K(1f, 0f, 0f, 0f, 0f)), // 2. side cross — wrist rolls through the hook
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -45f, 0f, 0f, -90f, 70f, 20f), K(0.48f, -105f, 0f, 0f, 0f, 95f, 20f), K(1f, 0f, 0f, 0f, 0f)), // 3. quick double jab — two wrist snaps
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.01f, 360f, 0f, 0f, 0f, 70f, 20f), K(0.99f, 0f, 0f, 0f, 0f, 95f, 20f), K(1f, 0f, 0f, 0f, 0f))), // 4. heavy uppercut (finisher) — wrist cocks then drives
                    null, K_Dual, 0.26f, 0.36f)
            },

            // gauntlets — boxer chain: jab, cross, double, uppercut (dual-wield alternates hands).
            {
                "gauntlets", new WeaponAnimDef(OffArm.None, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -72f, 0f, 0f, -90f, 70f, 20f), K(0.48f, -72f, 0f, 0f, 0f, 95f, 20f), K(1f, 0f, 0f, 0f, 0f)),    // 1. straight jab — fist snaps with the wrist
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -72f, 0f, 0f, -90f, 70f, 20f), K(0.48f, -72f, 0f, 0f,-90f, 95f, 20f), K(1f, 0f, 0f, 0f, 0f)), // 2. side cross — wrist rolls through the hook
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -72f, 0f, 0f, -90f, 70f, 20f), K(0.48f, -72f, 0f, 0f, 0f, 95f, 20f), K(1f, 0f, 0f, 0f, 0f)), // 3. quick double jab — two wrist snaps
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -72f, 0f, 0f, -90f, 70f, 20f), K(0.48f, -72f, 0f, 0f, 0f, 95f, 20f), K(1f, 0f, 0f, 0f, 0f))), // 4. heavy uppercut (finisher) — wrist cocks then drives
                    null, K_Dual, 0.26f, 0.36f)
            },


            // longbow — one aim-draw-loose: draw to full hold, loose on the shot, recover.
            {
                "longbow", new WeaponAnimDef(OffArm.Asym,
                    V(T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -25f, -20f, 0f, -120f, 10f, 0f), K(0.60f, -25f, -20f, 0f, -120f, 10f, 0f), K(0.80f, -55f, 0f, 0f, -15f, 70f, 0f), K(1f, 0f, 0f, 0f, 0f))),
                    V(T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -85f, 0f, 0f, -6f), K(0.60f, -85f, 0f, 0f, -6f), K(0.80f, -82f, 0f, 0f, -4f), K(1f, 0f, 0f, 0f, 0f))),
                    K_None, 0.50f, 0.80f)
            },

            // throwing_hammer — one cocked charge: wind back, hold, whip-release, recover.
            {
                "throwing_hammer", new WeaponAnimDef(OffArm.None, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.30f, -150f, 0f, 0f, -60f, 30f, -70f), K(0.58f, -150f, 0f, 0f, -55f, 30f, -70f), K(0.85f, -55f, 0f, 0f, -8f, 60f, 0f), K(1f, 0f, 0f, 0f, 0f))),
                    null, K_None, 0.32f, 0.45f)
            },

            // warhammer — slow crushing: telegraphed slam, side smashes, two-handed ground pound.
            {
                "warhammer", new WeaponAnimDef(OffArm.Mirror, V(
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.40f, -155f, 0f, 0f, 6f, 45f, 0f), K(0.55f, -155f, 0f, 0f, 6f, 45f, 0f), K(0.72f, -65f, 0f, 0f, 16f, 30f, 0f), K(0.86f, -72f, 0f, 0f, 22f, 25f, 0f), K(1f, 0f, 0f, 0f, 0f)),  // 1. telegraphed overhead slam — wrists held, flex on impact
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.40f, -130f, -60f, 0f, 8f, 30f, -50f), K(0.55f, -130f, -60f, 0f, 8f, 30f, -50f), K(0.72f, -70f, -10f, 0f, 14f, 25f, -50f), K(1f, 0f, 0f, 0f, 0f)),            // 2. left-side smash — wrists roll into the crush
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.40f, -130f, 60f, 0f, 8f, 30f, 50f), K(0.55f, -130f, 60f, 0f, 8f, 30f, 50f), K(0.72f, -70f, 10f, 0f, 14f, 25f, 50f), K(1f, 0f, 0f, 0f, 0f)),            // 3. right backhand smash
                    T(K(0f, 0f, 0f, 0f, 0f), K(0.35f, -155f, 0f, 0f, 6f, 40f, 0f), K(0.50f, -155f, 0f, 0f, 6f, 40f, 0f), K(0.70f, -20f, 0f, 0f, 20f, 55f, 0f), K(0.82f, -15f, 0f, 0f, 26f, 45f, 0f), K(1f, 0f, 0f, 0f, 0f))), // 4. two-handed ground pound (finisher) — wrists flex into the pound
                    null, K_None, 0.55f, 0.75f)
            },

            // staff / holy_book / bone_wand / control_orb / lute — ALL magic weapons share the one
            // magic cast (MagicCastKeys): raise hand forward, hold while charging, cast at launch.
            // Each keeps its own cast speed (TimeLight/TimeHeavy) and weapon-local magic accent.
            {
                "staff", new WeaponAnimDef(OffArm.None, V(MagicCastKeys), null, K_Staff, 0.42f, 0.62f)
            },

            {
                "holy_book", new WeaponAnimDef(OffArm.None, V(MagicCastKeys), null, K_Book, 0.44f, 0.64f)
            },

            {
                "bone_wand", new WeaponAnimDef(OffArm.None, V(MagicCastKeys), null, K_Wand, 0.38f, 0.56f)
            },

            {
                "control_orb", new WeaponAnimDef(OffArm.None, V(MagicCastKeys), null, K_Orb, 0.46f, 0.66f)
            },

            {
                "lute", new WeaponAnimDef(OffArm.None, V(MagicCastKeys), null, K_Lute, 0.42f, 0.60f)
            },

            // buckler / round_shield / tower_shield — one shared bash track (quick jab, lateral
            // sweep, overhead slam); the shield's heavier tiers just move at their own speed.
            {
                "buckler", new WeaponAnimDef(OffArm.None, ShieldBashKeys, null, K_None, 0.28f, 0.40f)
            },

            {
                "round_shield", new WeaponAnimDef(OffArm.None, ShieldBashKeys, null, K_None, 0.32f, 0.46f)
            },

            {
                "tower_shield", new WeaponAnimDef(OffArm.None, ShieldBashKeys, null, K_None, 0.38f, 0.55f)
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

    // Per-phase base: where the weapon sits when THIS phase began. Derived from the authored rest
    // below - never from a live animated transform.
    private Vector3 _basePos;
    private Vector3 _baseEuler;
    private Vector3 _baseScale;

    // The AUTHORED rest pose, and the one value that must never be sampled from an animated frame.
    // 1im: every animated frame writes `_baseEuler + aEuler` onto the transform, so a phase that
    // began while the previous phase's pose was still applied used to capture that accent offset as
    // its new rest - and since End/StopSway/AbandonSway all restore TO the base, the error could
    // never unwind and the weapon stayed permanently rolled. Only the magic weapons could drift:
    // they are the only defs with a rotation accent (staff 14, book 16, wand 10, orb 30 degrees),
    // while every melee/ranged/shield def is K_None or a no-op.
    private bool _restValid;
    private Vector3 _restPos;
    private Quaternion _restRot;
    private Vector3 _restScale;

    private Quaternion _ownerShBase;
    private Quaternion _ownerElBase;
    private Quaternion _ownerWrBase;
    private Quaternion _otherShBase;
    private Quaternion _otherElBase;
    private Quaternion _otherWrBase;

    private bool _active;
    private bool _heavy;
    private float _t;
    private float _duration;
    private bool _offHand;

    private PlayerAnimator _playerAnim;
    private Transform _ownerShoulder;
    private Transform _ownerElbow;
    private Transform _ownerWrist;
    private Transform _otherShoulder;
    private Transform _otherElbow;
    private Transform _otherWrist;

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

    // Live charge-hold: the arm holds the weapon's charge pose indefinitely while _charging,
    // and the weapon-local accent ramps with _chargeLevel (0..1) as the cast/draw builds.
    private bool _charging;
    private float _chargeLevel;

    // Defense guard hold: the arms ease into the weapon's guard pose and hold (a blocking stance)
    // while CombatController keeps blocking. Mirrors the charge hold; only one phase owns the arms.
    private bool _guarding;
    private float _guardT;
    private PoseKey _guardPose;
    private PoseKey[] _guardTrack;

    /// <summary>True when THIS rig currently holds an AcquireArms claim on the player arms.</summary>
    private bool _ownsArms;

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

        // Abandon any lingering hold (sway / charge / guard) on this rig. The arms stay owned by
        // the new phase instead of releasing and re-acquiring, so spam can never leak arm owners.
        AbandonSway();
        _charging = false;
        _guarding = false;

        DetectStrike(_set);
        _sweepScale = SlashSweepScale(_set);

        _heavy = heavy;
        float speed = SpeedScale();
        _duration = Mathf.Max(0.001f, (heavy ? _def.TimeHeavy : _def.TimeLight) * BaseSwingTimeScale / speed);
        _t = 0f;
        _active = true;

        CaptureRest();

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

    /// <summary>
    /// Enter the weapon's charge-hold: the arm eases to the track's hold plateau and stays there
    /// (driving <see cref="SetChargeLevel"/> scales the accent). Ends with <see cref="EndCharge"/>:
    /// fire resumes the track through the loose/recover tail; cancel settles straight back.
    /// </summary>
    public void PlayCharge()
    {
        _set = _def.Owner != null && _def.Owner.Length > 0 ? _def.Owner[0] : null;
        _otherSet = _def.Other != null && _def.Other.Length > 0 ? _def.Other[0] : null;

        AbandonSway();
        _active = false;
        _guarding = false;

        _chargeLevel = 0f;
        _heavy = false;
        float speed = SpeedScale();
        _duration = Mathf.Max(0.001f, _def.TimeLight * BaseSwingTimeScale / speed);

        CaptureRest();

        _charging = true;
    }

    /// <summary>Set the live charge level (0..1) while charging — drives the weapon accent.</summary>
    public void SetChargeLevel(float level) => _chargeLevel = Mathf.Clamp01(level);

    /// <summary>End the charge-hold. <paramref name="fire"/> resumes the release tail of the track
    /// (loose → recover); otherwise the arms settle straight back to the rest pose.</summary>
    public void EndCharge(bool fire)
    {
        if (!_charging) return;
        _charging = false;
        if (fire)
        {
            _active = true;
            _t = ChargeHoldT * _duration;
        }
        else
        {
            End();
        }
    }

    /// <summary>
    /// Enter the weapon's defense guard (RMB hold): the arms ease from rest into the weapon's guard
    /// pose and hold it while blocking — the "defense" animation set that pairs with the attack
    /// swings. Ends with <see cref="EndGuard"/> (guard release), which settles the arms back so the
    /// ready sway / PlayerAnimator retakes them.
    /// </summary>
    public void PlayGuard()
    {
        if (_guarding) return;
        AbandonSway();
        _active = false;
        _charging = false;

        _guardPose = _weaponId != null && GuardPoses.TryGetValue(_weaponId, out var g) ? g : IdentityPose;
        _guardTrack = T(IdentityPose, _guardPose);
        _guardT = 0f;

        CaptureRest();

        _guarding = true;
    }

    /// <summary>Drop the guard hold: arms settle straight back to rest (sway retakes them).</summary>
    public void EndGuard()
    {
        if (!_guarding) return;
        End();
    }

    private void Update()
    {
        if (_active)
        {
            UpdateAttack();
            return;
        }
        if (_charging)
        {
            UpdateCharge();
            return;
        }
        if (_guarding)
        {
            UpdateGuard();
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
        float m = _offHand ? -1f : 1f;

        PoseKey k = Sample(_set, t);

        // Blade lead: rotate the weapon INTO the swing so the tip leads the arc instead of being
        // dragged along at a fixed grip angle. A subtle angle follows the shoulder track (the blade
        // turns with the horizontal yaw and dives with the vertical pitch; elbow-driven thrusts
        // barely move their axes so jabs stay tight), plus a strike snap that briefly whips the
        // blade through the hit along the swing's travel direction. Tracks return to zero on
        // recovery, so the lead eases back to the grip.
        Vector3 leadEuler = Vector3.zero;

        // Impact pulse: right after the strike the arm rebounds briefly (a recoil kick opposite the
        // swing's travel) and the blade shoves forward a touch, then eases into follow-through.
        _pulse = _impactT > 0f && t >= _impactT
            ? Mathf.Min(1f, Mathf.Exp(-(t - _impactT) * 18f))
            : 0f;

        ApplyPose(k, m, h, _pulse, t);

        // The weapon rides the hand; only magic focuses add a small local accent.
        ApplyAccent(_def.Accent, t, out Vector3 aEuler, out Vector3 aPos, out float aScale);
        aEuler.y *= m; aEuler.z *= m; aPos.x *= m;
        transform.localRotation = Quaternion.Euler(_baseEuler + aEuler + leadEuler);
        transform.localPosition = _basePos + aPos + new Vector3(0f, 0f, 0.02f * h * _pulse);
        transform.localScale = _baseScale * aScale;

        if (_t >= _duration)
            End();
    }

    /// <summary>Hold the charge plateau: the arm sits at the track's hold pose while the weapon
    /// accent (staff arc / book raise / wand pulse / orb sweep / bow draw) ramps with charge.</summary>
    private void UpdateCharge()
    {
        float t = ChargeHoldT;
        float m = _offHand ? -1f : 1f;
        PoseKey k = Sample(_set, t);
        ApplyPose(k, m, 1f, 0f, t);

        ApplyAccent(_def.Accent, Mathf.Lerp(0.25f, 0.65f, _chargeLevel), out Vector3 aEuler, out Vector3 aPos, out float aScale);
        aEuler.y *= m; aEuler.z *= m; aPos.x *= m;
        transform.localRotation = Quaternion.Euler(_baseEuler + aEuler);
        transform.localPosition = _basePos + aPos;
        transform.localScale = _baseScale * aScale;
    }

    /// <summary>Raise into the guard hold: the arm eases from rest to the weapon's guard pose and
    /// holds it (a blocking stance) until <see cref="EndGuard"/> drops it. No weapon accent — the
    /// weapon simply rides the guarding arm.</summary>
    private void UpdateGuard()
    {
        _guardT = Mathf.MoveTowards(_guardT, 1f, Time.deltaTime / GuardRaiseTime);
        float m = _offHand ? -1f : 1f;
        PoseKey k = Sample(_guardTrack, _guardT);
        ApplyPose(k, m, 1f, 0f, _guardT);
        if (_playerAnim != null) _playerAnim.PingArms();

        transform.localRotation = Quaternion.Euler(_baseEuler);
        transform.localPosition = _basePos;
        transform.localScale = _baseScale;
    }

    /// <summary>Apply the shoulder/elbow/wrist that the attack and charge phases share — arm (owner),
    /// support arm by def mode, and the reflected left-hand mirror. Pulse = impact recoil (0 in idle
    /// holds), h = heavy amplification, t = normalized time (needed for Asym support-arm sampling).</summary>
    private void ApplyPose(PoseKey k, float m, float h, float pulse, float t)
    {
        if (_playerAnim != null) _playerAnim.PingArms();
        Vector3 sh = new Vector3(ClampShX(k.shX), k.shY * m, k.shZ * m) * h;
        float el = k.elX * h;
        if (pulse > 0f)
        {
            sh += new Vector3(0f, -_strikeDir * 6f * h * pulse * m, 0f);
            el += 4f * h * pulse;
        }

        if (_ownerShoulder != null)
            _ownerShoulder.localRotation = _ownerShBase * Quaternion.Euler(sh);
        if (_ownerElbow != null)
            _ownerElbow.localRotation = _ownerElBase * Quaternion.Euler(el, 0f, 0f);

        // Wrist rotation: flex (wrX) bends the palm, roll (wrY) twists the blade in hand. The
        // roll mirrors on the left hand so both hands twist the blade the same way.
        Vector3 wr = new Vector3(k.wrX, k.wrY * m, 0f) * h;
        if (_ownerWrist != null)
            _ownerWrist.localRotation = _ownerWrBase * Quaternion.Euler(wr);

        switch (_def.Mode)
        {
            case OffArm.Mirror:
                // Two-hand grip: the support arm mirrors the swing (yaw flipped side-to-side).
                if (_otherShoulder != null)
                    _otherShoulder.localRotation = _otherShBase * Quaternion.Euler(ClampShX(k.shX) * h, -k.shY * h * m, k.shZ * h * m);
                if (_otherElbow != null)
                    _otherElbow.localRotation = _otherElBase * Quaternion.Euler(el, 0f, 0f);
                if (_otherWrist != null)
                    _otherWrist.localRotation = _otherWrBase * Quaternion.Euler(-wr);
                break;

            case OffArm.Asym:
                PoseKey ok = Sample(_otherSet, t);
                if (_otherShoulder != null)
                    _otherShoulder.localRotation = _otherShBase * Quaternion.Euler(ClampShX(ok.shX) * h, ok.shY * h * m, ok.shZ * h * m);
                if (_otherElbow != null)
                    _otherElbow.localRotation = _otherElBase * Quaternion.Euler(ok.elX * h, 0f, 0f);
                if (_otherWrist != null)
                    _otherWrist.localRotation = _otherWrBase * Quaternion.Euler(ok.wrX * h, ok.wrY * h * m, 0f);
                break;
        }
    }

    /// <summary>Claim the player arms ONCE per phase (balanced with exactly one release in End).</summary>
    private void Acquire()
    {
        if (_ownsArms) return;
        _ownsArms = true;
        if (_playerAnim != null) _playerAnim.AcquireArms();
    }

    /// <summary>Release the arm claim held by this rig. Safe to call when not owning.</summary>
    private void Release()
    {
        if (!_ownsArms) return;
        _ownsArms = false;
        if (_playerAnim != null) _playerAnim.ReleaseArms();
    }

    /// <summary>Restore the arm pivots to their rest pose. The model's rest is local identity
    /// (see PlayerAnimator), so phases restore to identity rather than an arbitrary live capture —
    /// a polluted capture can never bake an altered pose into the model.</summary>
    private void RestoreArms()
    {
        if (_ownerShoulder != null) _ownerShoulder.localRotation = _ownerShBase;
        if (_ownerElbow != null) _ownerElbow.localRotation = _ownerElBase;
        if (_ownerWrist != null) _ownerWrist.localRotation = _ownerWrBase;
        if (_otherShoulder != null) _otherShoulder.localRotation = _otherShBase;
        if (_otherElbow != null) _otherElbow.localRotation = _otherElBase;
        if (_otherWrist != null) _otherWrist.localRotation = _otherWrBase;
    }

    /// <summary>
    /// (1im) The ONE place the authored rest is written. Call only while no phase owns the
    /// transform, i.e. while the live pose genuinely IS the rest rather than an animated offset on
    /// top of it.
    /// </summary>
    private void SyncRestFromIdle()
    {
        _restPos = transform.localPosition;
        _restRot = transform.localRotation;
        _restScale = transform.localScale;
        _restValid = true;
    }

    /// <summary>
    /// (1im) Put the transform back on the authored rest. Restores nothing when the rest is not yet
    /// known, so a first capture taken before it exists degrades to a plain live sample rather than
    /// snapping the weapon to identity.
    /// </summary>
    private void RestoreAuthoredRest()
    {
        if (!_restValid) return;
        transform.localRotation = _restRot;
        transform.localPosition = _restPos;
        transform.localScale = _restScale;
    }

    /// <summary>
    /// Capture the per-phase rest state (weapon transform + arm pivots) and claim the arms. The arm
    /// pivots' rest is always local identity (see <see cref="RestoreArms"/>); the weapon's rest is
    /// the authored one below, NOT a fresh sample.
    /// <para>
    /// 1im: this used to re-sample the live transform, which is what let the magic weapons drift.
    /// Because every animated frame is written as `_baseEuler + aEuler`, a phase that began while
    /// the previous phase was still on screen captured that accent offset as its own rest - and since
    /// End/StopSway/AbandonSway all restore TO `_baseEuler`, each cast compounded the last and the
    /// weapon stayed rolled forever. The rest is now re-sampled only while idle (which keeps
    /// re-parenting onto a hand harmless, because a re-parent rewrites the local pose and an idle
    /// rig is by definition sitting on that new pose) and restored otherwise, so the capture can
    /// never read its own output.
    /// </para>
    /// </summary>
    private void CaptureRest()
    {
        if (!_active && !_charging && !_guarding && !_swayActive)
            SyncRestFromIdle();
        RestoreAuthoredRest();

        _basePos = transform.localPosition;
        _baseEuler = transform.localRotation.eulerAngles;
        _baseScale = transform.localScale;

        ResolvePivots();

        _ownerShBase = Quaternion.identity;
        _ownerElBase = Quaternion.identity;
        _ownerWrBase = Quaternion.identity;
        _otherShBase = Quaternion.identity;
        _otherElBase = Quaternion.identity;
        _otherWrBase = Quaternion.identity;

        Acquire();
    }

    /// <summary>End whatever phase is active: restore weapon + arms to rest and release ownership.</summary>
    private void End()
    {
        transform.localRotation = Quaternion.Euler(_baseEuler);
        transform.localPosition = _basePos;
        transform.localScale = _baseScale;
        RestoreArms();
        Release();
        _active = false;
        _charging = false;
        _guarding = false;
        _swayActive = false;
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

    /// <summary>Recursively find a descendant transform by exact name (arm chains vary per model).</summary>
    private static Transform FindNamedDescendant(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var t = FindNamedDescendant(root.GetChild(i), name);
            if (t != null) return t;
        }
        return null;
    }

    private void OnDisable()
    {
        // Fully tear the active phase down (balanced release) so the player arms always return to
        // rest — stale attack/charge/guard holds must never leak their arm ownership.
        if (_ownsArms)
            End();
        // 1im: a rig that was disabled may have been re-parented or re-posed meanwhile, so the rest
        // authored before that is stale. Drop it and let the next idle frame re-author it.
        _restValid = false;
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

        // The owner wrist is the Hand bone the rig hangs from; the support wrist is the opposite
        // hand, found below the other shoulder (Hand → Elbow → Shoulder chain varies per model).
        _ownerWrist = parent;
        _ownerShoulder = FindOwnerShoulder(parent);
        _ownerElbow = FindOwnerElbow(parent);
        _otherShoulder = _playerAnim != null ? (_offHand ? _playerAnim.ShoulderR : _playerAnim.ShoulderL) : null;
        _otherElbow = _playerAnim != null ? (_offHand ? _playerAnim.ElbowR : _playerAnim.ElbowL) : null;
        _otherWrist = FindNamedDescendant(_otherShoulder, _offHand ? "HandR" : "HandL");
    }

    /// <summary>True when the sway can safely own the arms: drawn, transition settled, player standing.</summary>
    private bool ShouldSway()
    {
        if (_playerAnim == null || _playerAnim.Controller == null) return false;
        var pc = _playerAnim.Controller;
        if (pc.IsSitting || pc.IsMoving) return false;
        if (_stow == null || !_stow.IsDrawn || _stow.IsBusy) return false;
        ResolvePivots();
        return _ownerShoulder != null;
    }

    private void StartSway()
    {
        ResolvePivots();
        if (_ownerShoulder == null || _ownsArms) return;
        _swayShBase = Quaternion.identity;
        _swayElBase = Quaternion.identity;
        if (_def.Mode == OffArm.Mirror)
        {
            _swayOtherShBase = Quaternion.identity;
            _swayOtherElBase = Quaternion.identity;
        }
        // 1im: same rule as CaptureRest - never re-base from a live frame. The sway only ever starts
        // from an idle rig, so the live pose IS the authored rest, but routing it through the shared
        // helper keeps one place that writes the rest and one place that reads it.
        SyncRestFromIdle();
        _basePos = _restPos;
        _baseEuler = _restRot.eulerAngles;
        _baseScale = _restScale;
        _swayGuard = 0f;
        _swayActive = true;
        Acquire();
    }

    private void StopSway()
    {
        if (!_swayActive) return;
        _swayActive = false;
        transform.localRotation = Quaternion.Euler(_baseEuler);
        transform.localPosition = _basePos;
        transform.localScale = _baseScale;
        Release();
    }

    /// <summary>Drop an active ready sway WITHOUT releasing the arms (a newer phase — attack, charge
    /// or guard — is taking them over): restore the weapon transform to the sway's captured base so
    /// the live swing accent never leaks into the new phase, but keep the single arm claim.</summary>
    private void AbandonSway()
    {
        if (!_swayActive) return;
        _swayActive = false;
        transform.localRotation = Quaternion.Euler(_baseEuler);
        transform.localPosition = _basePos;
        transform.localScale = _baseScale;
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
        if (_playerAnim != null) _playerAnim.PingArms();
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
            float sm = _offHand ? -1f : 1f;
            aEuler.y *= sm; aEuler.z *= sm; aPos.x *= sm;
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
                    Mathf.Lerp(track[i].elX, track[i + 1].elX, u),
                    Mathf.Lerp(track[i].wrX, track[i + 1].wrX, u),
                    Mathf.Lerp(track[i].wrY, track[i + 1].wrY, u));
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