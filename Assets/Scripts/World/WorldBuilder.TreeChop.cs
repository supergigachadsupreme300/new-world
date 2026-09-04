using System.Collections.Generic;
using UnityEngine;
using TMPro;
using static CountryLife.Helpers.PickupVisualHelper;

public partial class WorldBuilder
{
    public bool RemoveTree(GameObject tree)
    {
        if (tree == null)
            return false;

        if (_trees.Contains(tree))
        {
            if (_treeChopStates.TryGetValue(tree, out var state))
            {
                if (state.ChopMark != null) Destroy(state.ChopMark);
                _treeChopStates.Remove(tree);
            }
            Destroy(tree);
            _trees.Remove(tree);
            return true;
        }
        return false;
    }

    public bool ChopTree(GameObject treeRoot, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (treeRoot == null || !_trees.Contains(treeRoot))
            return false;

        if (_treeChopStates.TryGetValue(treeRoot, out var state))
        {
            if (state.IsChopped) return false;

            state.ChopProgress = Mathf.Min(1f, state.ChopProgress + 0.25f);
            UpdateChopMarkVisual(state);

            if (state.ChopProgress >= 1f)
            {
                CutTree(state);
                return true;
            }
            return false;
        }
        else
        {
            var trunk = FindTrunk(treeRoot);
            if (trunk == null) return false;

            float trunkH = trunk.transform.localScale.y;
            float trunkW = trunk.transform.localScale.x;

            state = new TreeChopState
            {
                TreeRoot = treeRoot,
                TrunkObject = trunk,
                TrunkHeight = trunkH,
                TrunkWidth = trunkW,
                ChopProgress = 0.25f,
                HitWorldPoint = hitPoint,
                HitNormal = hitNormal,
                HitLocalY = trunk.transform.InverseTransformPoint(hitPoint).y,
                ChopMark = null,
                IsChopped = false
            };

            Vector3 trunkLocal = trunk.transform.InverseTransformPoint(hitPoint);
            Vector3 localNormal = trunk.transform.InverseTransformDirection(hitNormal);
            state.IsHitOnX = Mathf.Abs(localNormal.x) > Mathf.Abs(localNormal.z);
            if (state.IsHitOnX)
                trunkLocal.z = 0f;
            else
                trunkLocal.x = 0f;
            state.CenterWorld = trunk.transform.TransformPoint(trunkLocal);
            state.InitialDepth = Mathf.Lerp(0.05f, trunkW * 1.2f, 0.25f);

            _treeChopStates[treeRoot] = state;
            CreateChopMark(state);
            return false;
        }
    }

    private GameObject FindTrunk(GameObject treeRoot)
    {
        foreach (Transform child in treeRoot.transform)
        {
            if (child.name == "Trunk")
                return child.gameObject;
        }
        return null;
    }

    private void CreateChopMark(TreeChopState state)
    {
        var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mark.name = "ChopMark";
        Destroy(mark.GetComponent<Collider>());

        mark.transform.SetParent(state.TreeRoot.transform, true);
        mark.transform.position = state.CenterWorld;
        mark.transform.rotation = state.TrunkObject.transform.rotation * Quaternion.Euler(0f, 90f, 0f);

        var r = mark.GetComponent<Renderer>();
        if (r != null) r.material.color = Color.black;
        state.ChopMark = mark;
        UpdateChopMarkVisual(state);
    }

    private void UpdateChopMarkVisual(TreeChopState state)
    {
        if (state.ChopMark == null) return;
        float tw = state.TrunkWidth;
        Transform mt = state.ChopMark.transform;

        Vector3 inward = -state.HitNormal.normalized;
        float depth = Mathf.Lerp(0.05f, tw * 1.2f, state.ChopProgress);
        float depthExtra = depth - state.InitialDepth;

        if (state.IsHitOnX)
            mt.localScale = new Vector3(tw * 1.2f, 0.08f, depth);
        else
            mt.localScale = new Vector3(depth, 0.08f, tw * 1.2f);

        mt.position = state.CenterWorld + inward * depthExtra * 0.5f;
    }

    private void CutTree(TreeChopState state)
    {
        state.IsChopped = true;

        Transform trunk = state.TrunkObject.transform;
        Vector3 trunkPos = trunk.localPosition;
        Quaternion trunkRot = trunk.localRotation;
        float fullH = state.TrunkHeight;
        float fullW = state.TrunkWidth;
        Vector3 trunkUp = trunkRot * Vector3.up;

        Vector3 chopLocal = state.TreeRoot.transform.InverseTransformPoint(state.CenterWorld);
        Vector3 bottomLocal = trunkPos + trunkRot * new Vector3(0, -fullH / 2f, 0);
        float cutHeight = Mathf.Max(0.1f, Vector3.Dot(chopLocal - bottomLocal, trunkUp));

        trunk.localPosition = Vector3.Lerp(bottomLocal, chopLocal, 0.5f);
        trunk.localScale = new Vector3(fullW, cutHeight, fullW);

        float topH = fullH - cutHeight;

        var toMove = new List<Transform>();
        foreach (Transform child in state.TreeRoot.transform)
        {
            if (child.name == "Trunk") continue;
            if (child.name == "Leaf")
            {
                Object.Destroy(child.gameObject);
                continue;
            }
            if (Vector3.Dot(child.localPosition, trunkUp) > Vector3.Dot(chopLocal, trunkUp) + 0.3f)
            {
                if (child.GetComponent<Collider>() == null)
                    child.gameObject.AddComponent<BoxCollider>();
                toMove.Add(child);
            }
        }

        if (topH > 0.3f)
        {
            var topRoot = new GameObject("TreeFelled");
            topRoot.transform.position = state.TreeRoot.transform.position;
            topRoot.transform.rotation = state.TreeRoot.transform.rotation;
            var rb = topRoot.AddComponent<Rigidbody>();
            rb.mass = 10f;

            var topTrunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topTrunk.name = "Trunk";
            topTrunk.transform.SetParent(topRoot.transform);
            topTrunk.transform.localScale = new Vector3(fullW, topH, fullW);
            topTrunk.transform.localPosition = chopLocal + trunkUp * (topH / 2f);
            topTrunk.transform.localRotation = trunkRot;
            var topTrunkR = topTrunk.GetComponent<Renderer>();
            if (topTrunkR != null)
            {
                var origR = state.TrunkObject.GetComponent<Renderer>();
                topTrunkR.material = origR != null ? new Material(origR.material) : CreateFallbackWoodMaterial();
            }

            foreach (var child in toMove)
                child.SetParent(topRoot.transform, true);

            _trees.Add(topRoot);
        }

        if (state.ChopMark != null)
        {
            Destroy(state.ChopMark);
            state.ChopMark = null;
        }

        _treeChopStates.Remove(state.TreeRoot);
    }

    public bool ChopBranch(GameObject treeRoot, GameObject branch, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (branch == null) return false;

        if (_branchChopStates.TryGetValue(branch, out var state))
        {
            state.ChopProgress = Mathf.Min(1f, state.ChopProgress + 0.25f);
            UpdateBranchChopMarkVisual(state);

            if (state.ChopProgress >= 1f)
            {
                CutBranch(state);
                _branchChopStates.Remove(branch);
                return true;
            }
            return false;
        }

        var bt = branch.transform;
        float branchH = bt.localScale.y;
        float branchW = bt.localScale.x;

        Vector3 trunkLocal = bt.InverseTransformPoint(hitPoint);
        Vector3 localNormal = bt.InverseTransformDirection(hitNormal);
        bool isHitOnX = Mathf.Abs(localNormal.x) > Mathf.Abs(localNormal.z);

        if (isHitOnX) trunkLocal.z = 0f;
        else trunkLocal.x = 0f;

        state = new BranchChopState
        {
            BranchObject = branch,
            TreeRoot = branch.name == "TrunkSeg" ? treeRoot : null,
            ChopProgress = 0.25f,
            HitWorldPoint = hitPoint,
            HitNormal = hitNormal,
            HitLocalY = trunkLocal.y,
            IsHitOnX = isHitOnX,
            CenterWorld = bt.TransformPoint(trunkLocal),
            InitialDepth = Mathf.Lerp(0.05f, branchW * 1.2f, 0.25f),
        };

        CreateBranchChopMark(state);
        _branchChopStates[branch] = state;
        return false;
    }

    private void CreateBranchChopMark(BranchChopState state)
    {
        var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mark.name = "ChopMark";
        Destroy(mark.GetComponent<Collider>());

        mark.transform.SetParent(state.BranchObject.transform.parent, true);
        mark.transform.position = state.CenterWorld;
        mark.transform.rotation = state.BranchObject.transform.rotation * Quaternion.Euler(0f, 90f, 0f);

        var r = mark.GetComponent<Renderer>();
        if (r != null) r.material.color = Color.black;
        state.ChopMark = mark;
        UpdateBranchChopMarkVisual(state);
    }

    private void UpdateBranchChopMarkVisual(BranchChopState state)
    {
        if (state.ChopMark == null) return;
        float tw = state.BranchObject.transform.localScale.x;
        Transform mt = state.ChopMark.transform;

        Vector3 inward = -state.HitNormal.normalized;
        float depth = Mathf.Lerp(0.05f, tw * 1.2f, state.ChopProgress);
        float depthExtra = depth - state.InitialDepth;

        if (state.IsHitOnX)
            mt.localScale = new Vector3(tw * 1.2f, 0.08f, depth);
        else
            mt.localScale = new Vector3(depth, 0.08f, tw * 1.2f);

        mt.position = state.CenterWorld + inward * depthExtra * 0.5f;
    }

    private void CutBranch(BranchChopState state)
    {
        GameObject branchObj = state.BranchObject;
        if (branchObj == null) return;

        Transform branch = branchObj.transform;
        Transform parent = branch.parent;
        Vector3 branchPos = branch.localPosition;
        Quaternion branchRot = branch.localRotation;
        float fullH = branch.localScale.y;
        float fullW = branch.localScale.x;
        Vector3 branchUp = branchRot * Vector3.up;

        Vector3 chopLocal = parent.InverseTransformPoint(state.CenterWorld);
        Vector3 bottomLocal = branchPos + branchRot * new Vector3(0, -fullH / 2f, 0);
        float cutHeight = Mathf.Max(0.1f, Vector3.Dot(chopLocal - bottomLocal, branchUp));
        float topH = fullH - cutHeight;

        branch.localPosition = Vector3.Lerp(bottomLocal, chopLocal, 0.5f);
        branch.localScale = new Vector3(fullW, cutHeight, fullW);

        if (topH > 0.2f)
        {
            var topRoot = new GameObject("BranchTop");
            Vector3 topCenter = chopLocal + branchUp * (topH / 2f);
            topRoot.transform.position = parent.TransformPoint(topCenter);
            topRoot.transform.rotation = parent.rotation;
            var rb = topRoot.AddComponent<Rigidbody>();
            rb.mass = 3f;

            var topBranch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topBranch.name = "BranchTopPart";
            topBranch.transform.SetParent(topRoot.transform);
            topBranch.transform.localScale = new Vector3(fullW, topH, fullW);
            topBranch.transform.localPosition = Vector3.zero;
            topBranch.transform.localRotation = branchRot;
            var r = topBranch.GetComponent<Renderer>();
            if (r != null)
            {
                var origR = branchObj.GetComponent<Renderer>();
                r.material = origR != null ? new Material(origR.material) : CreateFallbackWoodMaterial();
            }

            Vector3 origTipLocal = branchPos + branchRot * new Vector3(0, fullH / 2f, 0);
            MoveSubBranches(parent, topRoot.transform, origTipLocal, fullW);

            if (state.TreeRoot != null)
            {
                topRoot.name = "TreeFelled";
                rb.mass = 10f;

                foreach (Transform child in state.TreeRoot.transform)
                {
                    if (child == branchObj.transform || child.name == "Trunk" || child.name == "Leaf") continue;
                    if (Vector3.Dot(child.localPosition, branchUp) > Vector3.Dot(chopLocal, branchUp) + 0.3f)
                    {
                        if (child.GetComponent<Collider>() == null)
                            child.gameObject.AddComponent<BoxCollider>();
                        child.SetParent(topRoot.transform, true);
                    }
                }

                _trees.Add(topRoot);
            }
        }

        if (state.ChopMark != null)
        {
            Destroy(state.ChopMark);
            state.ChopMark = null;
        }
    }

    private void MoveSubBranches(Transform parent, Transform target, Vector3 tipLocal, float tipWidth)
    {
        var matches = new List<(Transform t, Vector3 childTip, float childWidth)>();
        foreach (Transform child in parent)
        {
            if (child.name != "Branch") continue;
            Vector3 childBottom = child.localPosition + child.localRotation * new Vector3(0, -child.localScale.y / 2f, 0);
            if (Vector3.Distance(childBottom, tipLocal) < (tipWidth + child.localScale.x) * 0.5f)
            {
                Vector3 childTip = child.localPosition + child.localRotation * new Vector3(0, child.localScale.y / 2f, 0);
                matches.Add((child, childTip, child.localScale.x));
            }
        }
        foreach (var (t, childTip, childWidth) in matches)
        {
            t.SetParent(target, true);
            MoveSubBranches(parent, target, childTip, childWidth);
        }
    }
}

