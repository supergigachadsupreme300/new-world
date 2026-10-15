using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1l97 — jointed ragdoll state for the player.
///
/// The player model is a PROCEDURAL hierarchy (see <see cref="PlayerModelBuilder"/>): a plain
/// transform tree of pivots carrying primitive-mesh parts, with no skinned mesh, no bones and no
/// Animator. So there is no separate ragdoll model to swap in — the rig is attached to the SAME
/// pivots a built model already has, one Rigidbody + BoxCollider + CharacterJoint per bone, chained
/// from the model root down to the hands and feet.
///
/// The PlayerModel root STAYS PARENTED to the player while ragdolled. Two reasons: the recovery
/// path rebuilds the model wholesale, and the cutscene system's control-restore looks the model up
/// by NAME under the player (a detached model would make that lookup fail, and that path lives in
/// Legacy/ where the fence is read-only, rule 18).
///
/// Camera: the pivot is driven to the ragdoll's upper body every frame while down, which is what
/// makes CameraModeSwitch follow the flop — it reads that pivot each LateUpdate. Position only:
/// the boom's horizontal direction is built from the pivot's FORWARD, and feeding it a tumbling
/// body would swing the camera wildly.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerRagdoll : MonoBehaviour
{
    [Tooltip("1l97: seconds a heavy-hit ragdoll lasts before the model is rebuilt and control comes back. Death ragdolls ignore this - they stay down (see Enter's stayDown).")]
    public float RecoverSeconds = 1.6f;

    [Tooltip("1l97: velocity-change (m/s) put into the upper body by the hit that knocked the player down, on top of the attacker's own force. Raise to make knockdowns read as a launch; lower to keep them as a collapse.")]
    public float KnockdownImpulse = 4f;

    /// <summary>True while the model is physics-driven and the player has no control.</summary>
    public bool IsRagdolled { get; private set; }

    private sealed class BoneRig
    {
        public Transform Transform;
        public Rigidbody Body;
    }

    private PlayerController _pc;
    private CharacterController _controller;
    private CameraModeSwitch _camSwitch;
    private PlayerAnimator _animator;
    private Transform _modelRoot;
    private Transform _followBone;
    private readonly List<BoneRig> _bones = new List<BoneRig>();
    private HashSet<Transform> _boneSet;
    private bool _rigBuilt;
    private bool _stayDown;
    private float _recoverAt;
    private bool _wasFirstPerson;
    private Vector3 _pivotLocal;

    private PlayerController PC
    {
        get
        {
            if (_pc == null) _pc = GetComponent<PlayerController>();
            return _pc;
        }
    }

    /// <summary>Enter the ragdoll. Escalates a recoverable knockdown to permanent when a later hit
    /// while already down is lethal; re-entry while already down is otherwise a no-op, so a second
    /// hit cannot restart the recovery timer.</summary>
    public void Enter(Vector3 hitDir, float force, bool stayDown)
    {
        if (IsRagdolled)
        {
            if (stayDown) _stayDown = true;
            return;
        }

        var pc = PC;
        if (pc == null || pc.IsSitting) return;

        EnsureRig();
        if (_modelRoot == null) return;

        IsRagdolled = true;
        _stayDown = stayDown;
        _recoverAt = Time.time + RecoverSeconds;

        _animator = _modelRoot.GetComponent<PlayerAnimator>();
        if (_animator != null) _animator.enabled = false;

        _controller = GetComponent<CharacterController>();
        if (_controller != null) _controller.enabled = false;

        _camSwitch = GetComponent<CameraModeSwitch>();
        _wasFirstPerson = _camSwitch != null && _camSwitch.IsFirstPerson;
        if (_camSwitch != null) _camSwitch.SetThird();

        _followBone = FindUpperBody();

        // 1l97: keep the pivot's authored offset, because Update overwrites its WORLD position while
        // the body is down and nothing else ever puts it back - the pivot's offset is created once
        // in PlayerController.SetupPlayerCamera and this is the same offset, remembered rather than
        // re-spelled (a second literal here would drift from the first the next time it moves).
        var pivot = pc.PlayerCameraPivot;
        if (pivot != null) _pivotLocal = pivot.localPosition;

        pc.EnableInput(false);
        ApplyImpulse(hitDir, force);
    }

    /// <summary>Leave the ragdoll: put the player where the body landed, then rebuild a pristine
    /// model (the whole rig dies with the old model) and give back animator, movement and camera.</summary>
    public void Exit()
    {
        if (!IsRagdolled) return;
        IsRagdolled = false;

        var pc = PC;
        if (pc != null)
            pc.TeleportTo(ComputeLanding());

        if (_controller != null)
        {
            _controller.enabled = true;
            Physics.SyncTransforms();
        }

        if (pc != null)
            pc.RebuildPlayerModel();

        // Restore the pivot AFTER the teleport above, so the remembered LOCAL offset lands at the
        // right world point under the player's new position.
        if (pc != null && pc.PlayerCameraPivot != null)
            pc.PlayerCameraPivot.localPosition = _pivotLocal;

        _modelRoot = null;
        _followBone = null;
        _bones.Clear();
        _boneSet = null;
        _rigBuilt = false;
        _animator = null;

        if (pc != null)
            pc.EnableInput(true);

        if (_camSwitch != null)
        {
            if (_wasFirstPerson) _camSwitch.SetFirst();
            _camSwitch = null;
        }
        _wasFirstPerson = false;
    }

    private void Update()
    {
        if (!IsRagdolled) return;

        var pc = PC;
        if (pc != null && _followBone != null && pc.PlayerCameraPivot != null)
            pc.PlayerCameraPivot.position = _followBone.position;

        if (!_stayDown && Time.time >= _recoverAt)
            Exit();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  RIG
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Attach the physics rig to the current model's pivots. Idempotent for the life of a
    /// model: the caller clears the flag when it rebuilds the model.</summary>
    private void EnsureRig()
    {
        if (_rigBuilt) return;

        var root = transform.Find("PlayerModel");
        if (root == null) return;
        _modelRoot = root;

        var torso = root.Find("Torso");
        var shoulderL = torso != null ? torso.Find("ShoulderL") : null;
        var shoulderR = torso != null ? torso.Find("ShoulderR") : null;
        var elbowL = shoulderL != null ? shoulderL.Find("ElbowL") : null;
        var elbowR = shoulderR != null ? shoulderR.Find("ElbowR") : null;
        var hipL = root.Find("HipL");
        var hipR = root.Find("HipR");
        var kneeL = hipL != null ? hipL.Find("KneeL") : null;
        var kneeR = hipR != null ? hipR.Find("KneeR") : null;

        // Parent before child, so a joint's connected body already exists when it is wired.
        var order = new List<(Transform bone, Transform parent, float mass)>();
        order.Add((root, null, 9f));
        if (torso != null) order.Add((torso, root, 14f));
        if (shoulderL != null) order.Add((shoulderL, torso, 2.2f));
        if (elbowL != null) order.Add((elbowL, shoulderL, 1.6f));
        if (shoulderR != null) order.Add((shoulderR, torso, 2.2f));
        if (elbowR != null) order.Add((elbowR, shoulderR, 1.6f));
        if (hipL != null) order.Add((hipL, root, 3.4f));
        if (kneeL != null) order.Add((kneeL, hipL, 2.6f));
        if (hipR != null) order.Add((hipR, root, 3.4f));
        if (kneeR != null) order.Add((kneeR, hipR, 2.6f));

        _boneSet = new HashSet<Transform>();
        for (int i = 0; i < order.Count; i++)
            if (order[i].bone != null) _boneSet.Add(order[i].bone);

        var renderers = _modelRoot.GetComponentsInChildren<Renderer>(true);

        _bones.Clear();
        for (int i = 0; i < order.Count; i++)
        {
            var bone = order[i].bone;
            if (bone == null) continue;

            Vector3 size;
            Vector3 center;
            if (order[i].parent == null)
            {
                PelvisBox(bone, out center, out size);
            }
            else
            {
                Bounds local = BoneLocalBounds(bone, renderers, out bool anyMesh);
                center = local.center;
                size = local.size;
                if (!anyMesh)
                    size = new Vector3(0.2f, 0.2f, 0.2f);
            }

            // Neighbouring bones must OVERLAP rather than meet: a small intersection is invisible
            // and a small gap is a visible slit (the gable convention, rule 9).
            size += Vector3.one * 0.04f;

            var box = bone.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;

            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = order[i].mass;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            if (order[i].parent != null)
            {
                var parentRig = FindRig(order[i].parent);
                if (parentRig != null && parentRig.Body != null)
                {
                    var joint = bone.gameObject.AddComponent<CharacterJoint>();
                    joint.connectedBody = parentRig.Body;
                    joint.enableProjection = true;
                    joint.swing1Limit = new SoftJointLimit { limit = 75f };
                    joint.swing2Limit = new SoftJointLimit { limit = 75f };
                    joint.lowTwistLimit = new SoftJointLimit { limit = -40f };
                    joint.highTwistLimit = new SoftJointLimit { limit = 40f };
                }
            }

            _bones.Add(new BoneRig { Transform = bone, Body = body });
        }

        _rigBuilt = true;
    }

    /// <summary>Collider for the model root. It owns no mesh of its own (the Body sits under
    /// Torso), so its box is derived from where the two hip pivots actually are rather than
    /// authored — it stays right through a race scale change.</summary>
    private static void PelvisBox(Transform root, out Vector3 center, out Vector3 size)
    {
        var hipL = root.Find("HipL");
        var hipR = root.Find("HipR");
        float hipY = -0.25f;
        float hipSpan = 0.3f;
        if (hipL != null && hipR != null)
        {
            Vector3 a = hipL.localPosition;
            Vector3 b = hipR.localPosition;
            hipY = (a.y + b.y) * 0.5f;
            hipSpan = Mathf.Abs(a.x - b.x) + 0.22f;
        }
        center = new Vector3(0f, hipY, 0f);
        size = new Vector3(hipSpan, 0.3f, 0.3f);
    }

    /// <summary>Box enclosing only the meshes whose NEAREST bone ancestor is this one, expressed in
    /// the bone's own local space (which is the space a BoxCollider's centre and size are read in).
    /// Bone-local via InverseTransformPoint, so a scaled or yawed model still measures correctly.
    /// Instance (not static) because it claims meshes through NearestBone, which reads the rig's
    /// own _boneSet.</summary>
    private Bounds BoneLocalBounds(Transform bone, Renderer[] renderers, out bool anyMesh)
    {
        anyMesh = false;
        Bounds result = new Bounds(Vector3.zero, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (NearestBone(r.transform) != bone) continue;

            Bounds world = r.bounds;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = new Vector3(
                    (c & 1) == 0 ? world.min.x : world.max.x,
                    (c & 2) == 0 ? world.min.y : world.max.y,
                    (c & 4) == 0 ? world.min.z : world.max.z);
                Vector3 local = bone.InverseTransformPoint(corner);
                if (!anyMesh)
                {
                    result = new Bounds(local, Vector3.zero);
                    anyMesh = true;
                }
                else
                {
                    result.Encapsulate(local);
                }
            }
        }

        if (!anyMesh)
            result = new Bounds(Vector3.zero, new Vector3(0.05f, 0.05f, 0.05f));
        return result;
    }

    /// <summary>Walks up from a mesh to the first transform that is a bone, so each mesh is claimed
    /// by exactly one bone instead of being counted by every pivot above it.</summary>
    private Transform NearestBone(Transform t)
    {
        while (t != null)
        {
            if (_boneSet != null && _boneSet.Contains(t)) return t;
            t = t.parent;
        }
        return null;
    }

    private BoneRig FindRig(Transform bone)
    {
        for (int i = 0; i < _bones.Count; i++)
            if (_bones[i].Transform == bone) return _bones[i];
        return null;
    }

    private Transform FindUpperBody()
    {
        if (_modelRoot == null) return null;
        var torso = _modelRoot.Find("Torso");
        return torso != null ? torso : _modelRoot;
    }

    /// <summary>Kick the body in the direction it was hit from, biased upward so a knockdown reads
    /// as a launch rather than a crumple.</summary>
    private void ApplyImpulse(Vector3 hitDir, float force)
    {
        Vector3 dir = hitDir;
        dir.y = Mathf.Abs(dir.y);
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
        dir.Normalize();

        var upper = FindRig(_followBone);
        if (upper == null || upper.Body == null) return;

        float speed = KnockdownImpulse + Mathf.Max(0f, force);
        upper.Body.AddForce(dir * speed, ForceMode.VelocityChange);
        upper.Body.AddTorque(Vector3.Cross(dir, Vector3.up) * (speed * 0.4f), ForceMode.VelocityChange);
    }

    /// <summary>Where to put the player when the body stops flopping: the ground directly under the
    /// pelvis, with the player's OWN ragdoll colliders skipped so the ray does not land on the
    /// body it is standing up.</summary>
    private Vector3 ComputeLanding()
    {
        if (_modelRoot == null) return transform.position;

        Vector3 origin = _modelRoot.position + Vector3.up * 3f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 60f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        float bestDistance = Mathf.Infinity;
        Vector3 bestPoint = default;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            var hitTransform = hits[i].transform;
            if (hitTransform != null && hitTransform.IsChildOf(transform)) continue;
            if (hits[i].distance >= bestDistance) continue;
            bestDistance = hits[i].distance;
            bestPoint = hits[i].point;
            found = true;
        }

        if (!found) return transform.position;

        // The controller's centre is authored at half its height (EnsurePlayerPhysics), so its
        // bottom already sits at the transform origin — feet land on the point, plus a hair to
        // avoid starting inside the ground.
        return new Vector3(bestPoint.x, bestPoint.y + 0.05f, bestPoint.z);
    }
}
