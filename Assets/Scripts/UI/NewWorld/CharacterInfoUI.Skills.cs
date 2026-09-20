/// <summary>Character Info - Skills tab: general/class/race skill trees (build, pan/zoom, layout), node+line visuals, skill detail pane, key binding, sub-tab toggle, and the tree legend.</summary>
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

public sealed partial class CharacterInfoUI
{

    // ── Skill tree ────────────────────────────────────────────────────────
    private RectTransform BuildSkillTree(Transform parent)
    {
        var vp = new GameObject("TreeViewport");
        vp.transform.SetParent(parent, false);
        var vrt = vp.AddComponent<RectTransform>();
        vrt.anchorMin = new Vector2(0.5f, 0.5f);
        vrt.anchorMax = new Vector2(0.5f, 0.5f);
        vrt.pivot = new Vector2(0.5f, 0.5f);
        vrt.anchoredPosition = P(0f, -80f);
        vrt.sizeDelta = Sz(1000f, 560f);
        var vimg = vp.AddComponent<Image>();
        vimg.color = new Color(0.09f, 0.1f, 0.13f, 0.9f);
        vp.AddComponent<RectMask2D>();

        var content = new GameObject("TreeContent");
        content.transform.SetParent(vp.transform, false);
        _treeContent = content.AddComponent<RectTransform>();
        _treeContent.anchorMin = new Vector2(0.5f, 0.5f);
        _treeContent.anchorMax = new Vector2(0.5f, 0.5f);
        _treeContent.pivot = new Vector2(0.5f, 0.5f);
        _treeContent.anchoredPosition = Vector2.zero;
        _treeContent.sizeDelta = Sz(2200f, 2200f);

        var pan = vp.AddComponent<TreePan>();
        pan.Content = _treeContent;
        pan.Viewport = vrt;
        return vrt;
    }

    private void BuildSkillDetail(Transform parent)
    {
        var pane = new GameObject("SkillDetail");
        pane.transform.SetParent(parent, false);
        var pRt = pane.AddComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.5f, 0.5f);
        pRt.anchorMax = new Vector2(0.5f, 0.5f);
        pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.anchoredPosition = Vector2.zero;
        pRt.sizeDelta = Sz(380f, 300f);
        var pImg = pane.AddComponent<Image>();
        pImg.color = new Color(0.12f, 0.13f, 0.16f, 0.96f);
        _detailPane = pane;
        pane.SetActive(false);

        _detailTitle = MakeBodyText(pane.transform, "DetailTitle", P(-190f, 150f), Sz(360f, 32f));
        _detailTitle.fontSize = Mathf.Max(20f, Screen.height / 38f);
        _detailTitle.alignment = TextAlignmentOptions.Center;

        _detailDesc = MakeBodyText(pane.transform, "DetailDesc", P(-190f, 116f), Sz(360f, 60f));
        _detailDesc.enableWordWrapping = true;

        _detailMeta = MakeBodyText(pane.transform, "DetailMeta", P(-190f, 56f), Sz(360f, 100f));

        _detailLearnHint = MakeBodyText(pane.transform, "DetailHint", P(-190f, -46f), Sz(360f, 24f));
        _detailLearnHint.fontSize = Mathf.Max(14f, Screen.height / 64f);
        _detailLearnHint.enableWordWrapping = true;

        var learn = MakeButton(pane.transform, "LearnBtn", "Learn", P(-100f, -80f), LearnSelectedSkill);
        learn.GetComponent<RectTransform>().sizeDelta = Sz(160f, 38f);
        ApplyFullButtonSprite(learn.GetComponent<Image>());
        _learnBtn = learn;

        var assign = MakeButton(pane.transform, "AssignKeyBtn", "Assign Key", P(100f, -80f), AssignSelectedSkillKey);
        assign.GetComponent<RectTransform>().sizeDelta = Sz(160f, 38f);
        ApplyFullButtonSprite(assign.GetComponent<Image>());
        _assignKeyBtn = assign;

        // Red ✕ close: hides the detail pane (deselects) but keeps the tab menu open.
        MakeRedClose(pane.transform, "DetailCloseBtn",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-32f, -32f), new Vector2(34f, 34f), DismissSkillDetail);
    }

    private void DismissSkillDetail()
    {
        _selectedSkill = null;
        _selectedClassSkill = null;
        RefreshSkillTree();
    }

    private void RefreshSkillTree()
    {
        if (_skillSubTab == SkillSubTab.Class)
        {
            RefreshClassSkillTree();
            return;
        }
        if (_skillSubTab == SkillSubTab.Race)
        {
            RefreshRaceSkillTree();
            return;
        }

        var profile = SkillProfileOf();
        bool hasPoints = profile != null && profile.Points > 0;

        if (_detailPane != null)
            _detailPane.SetActive(_selectedSkill != null);

        // Node colors by state.
        foreach (var (skill, image, label) in _treeNodes)
        {
            if (image == null) continue;
            if (skill == _selectedSkill) image.color = NodeColor(skill, NodeSelected);
            else if (profile != null && profile.HasLearned(skill.id)) image.color = NodeColor(skill, NodeLearned);
            else if (profile != null && profile.CanLearn(skill)) image.color = NodeColor(skill, NodeAvailable);
            else image.color = NodeColor(skill, NodeLocked);

            if (label != null)
            {
                if (profile != null && profile.HasLearned(skill.id))
                    label.text = skill.displayName + "\nLv " + profile.LevelOf(skill.id);
                else
                    label.text = skill.displayName;
            }
        }

        // Lines: active when the target (child) or any prereq is learned.
        for (int i = 0; i < _treeLines.Count; i++)
        {
            var (line, target) = _treeLines[i];
            if (line == null) continue;
            bool active = profile != null && profile.HasLearned(target.id);
            if (!active && target.PrereqSkillIds != null && profile != null)
                foreach (var pid in target.PrereqSkillIds)
                    if (profile.HasLearned(pid)) { active = true; break; }
            line.color = active ? LineActive : LineInert;
        }

        // Selected-skill highlight: light up every link touching the clicked skill.
        if (_selectedSkill != null)
        {
            for (int i = 0; i < _treeLines.Count; i++)
            {
                var (line, target) = _treeLines[i];
                if (line == null) continue;
                bool touches = target.id == _selectedSkill.id;
                if (!touches && target.PrereqSkillIds != null)
                    foreach (var pid in target.PrereqSkillIds)
                        if (pid == _selectedSkill.id) { touches = true; break; }
                if (touches) line.color = LineHighlight;
            }
        }

        if (_skillPointsText != null)
            _skillPointsText.text = profile != null
                ? Localization.F("Skill Points: {0}", profile.Points)
                : "";

        var xp = SkillXpOf();
        if (_categoryLevelText != null)
        {
            int learned = 0;
            if (profile != null)
                foreach (var (skill, _, _) in _treeNodes)
                    if (profile.HasLearned(skill.id)) learned++;
            _categoryLevelText.text = Localization.F("Learned {0}/{1}", learned, _treeNodes.Count);
        }

        if (xp != null)
        {
            foreach (var (type, label) in _sectorLabels)
                if (label != null)
                    label.text = Localization.F("{0} Lv {1}", CategoryNames[(int)type], xp.GetLevel(type));
            foreach (var (type, _, label) in _legendChips)
                if (label != null)
                    label.text = Localization.F("{0} Lv {1}", CategoryNames[(int)type], xp.GetLevel(type));
        }

        RefreshSkillDetail();
        // Ensure Learn/Assign buttons are visible in General mode (Class mode hides them).
        if (_learnBtn != null)
        {
            _learnBtn.gameObject.SetActive(true);
            _learnBtn.interactable = hasPoints && _selectedSkill != null &&
                profile != null && profile.CanLearn(_selectedSkill);
        }
        if (_assignKeyBtn != null)
        {
            _assignKeyBtn.gameObject.SetActive(true);
            _assignKeyBtn.interactable = CanBindSelectedSkill();
        }
    }

    private void RefreshSkillDetail()
    {
        if (_detailTitle == null) return;
        var skill = _selectedSkill;
        if (skill == null)
        {
            _detailTitle.text = Localization.T("No skill selected");
            _detailDesc.text = "";
            _detailMeta.text = "";
            _detailLearnHint.text = "";
            return;
        }
        _detailTitle.text = skill.displayName;
        _detailDesc.text = skill.description;

        var meta = new StringBuilder();
        meta.Append(skill.IsPassive ? Localization.T("Type: Passive") : Localization.T("Type: Castable"));
        meta.Append('\n');

        if (!skill.IsPassive)
        {
            meta.Append("Cost: ");
            if (skill.SkillCost.Resource == ResourceKind.None) meta.Append("Free");
            else meta.Append(skill.SkillCost.Resource.ToString()).Append(' ').Append(skill.SkillCost.Amount.ToString("0.##"));
            if (skill.SkillCost.Cooldown > 0f)
                meta.Append('\n').Append("CD: ").Append(skill.SkillCost.Cooldown.ToString("0.#")).Append('s');
        }
        else
        {
            meta.Append(Localization.T("Cost: Free (always-on)"));
        }

        if (skill.PrereqSkillIds != null && skill.PrereqSkillIds.Length > 0)
        {
            meta.Append('\n').Append("Requires: ");
            for (int i = 0; i < skill.PrereqSkillIds.Length; i++)
            {
                var pre = SkillCatalog.Find(skill.PrereqSkillIds[i]);
                meta.Append(pre != null ? pre.displayName : skill.PrereqSkillIds[i]);
                if (i != skill.PrereqSkillIds.Length - 1) meta.Append(", ");
            }
        }

        // Currently-bound hotkey for the selected skill.
        if (!skill.IsPassive)
        {
            var bindings = BindingsOf();
            var key = bindings != null ? bindings.KeyOf(skill.id) : (Key?)null;
            if (key.HasValue)
                meta.Append('\n').Append("Key: ").Append(SkillBarHUD.KeyLabel(key.Value));
        }

        // Learned level + XP toward the next level.
        var levelProfile = SkillProfileOf();
        if (levelProfile != null && levelProfile.HasLearned(skill.id))
        {
            meta.Append('\n').Append("Lv ").Append(levelProfile.LevelOf(skill.id));
            if (!skill.IsPassive)
            {
                levelProfile.TryGetProgress(skill.id, out _, out float xp, out float toNext);
                meta.Append("  ·  XP ").Append(xp.ToString("0")).Append('/').Append(toNext.ToString("0"));
            }
        }
        _detailMeta.text = meta.ToString();

        var profile = SkillProfileOf();
        if (profile != null && profile.HasLearned(skill.id))
        {
            _detailLearnHint.text = Localization.T("Learned");   // font-safe (no ✔ glyph needed)
            _detailLearnHint.color = NodeLearned;
        }
        else if (profile != null && profile.CanLearn(skill))
        {
            _detailLearnHint.text = Localization.T("Learnable (spend 1 pt)");
            _detailLearnHint.color = NodeAvailable;
        }
        else if (profile != null)
        {
            _detailLearnHint.text = Localization.T("Locked — prerequisites or points missing");
            _detailLearnHint.color = NodeLocked;
        }
        else
        {
            _detailLearnHint.text = "";
        }
    }

    private bool CanBindSelectedSkill()
    {
        if (_selectedSkill == null || _selectedSkill.IsPassive) return false;
        var profile = SkillProfileOf();
        return profile != null && profile.HasLearned(_selectedSkill.id);
    }

    private void LearnSelectedSkill()
    {
        var profile = SkillProfileOf();
        if (_selectedSkill == null || profile == null) return;
        if (profile.Learn(_selectedSkill))
        {
            RefreshSkillTree();
        }
        else
        {
            _detailLearnHint.text = Localization.T("Cannot learn — prerequisites or points missing.");
        }
    }

    private void AssignSelectedSkillKey()
    {
        var bindings = BindingsOf();
        if (bindings == null) return;

        // Race tree: bind the selected racial active.
        if (_selectedRaceSkill != null)
        {
            if (_selectedRaceSkill.IsPassive)
            {
                if (_detailLearnHint != null)
                    _detailLearnHint.text = Localization.T("Passives are always-on — nothing to bind.");
                return;
            }
            bindings.BeginCapture(_selectedRaceSkill.id);
            if (_detailLearnHint != null)
                _detailLearnHint.text = Localization.F("Press a key to bind: {0}", _selectedRaceSkill.displayName);
            return;
        }

        if (_selectedSkill == null) return;
        if (!CanBindSelectedSkill())
        {
            if (_detailLearnHint != null)
                _detailLearnHint.text = Localization.T("Select a learned castable to bind.");
            return;
        }
        bindings.BeginCapture(_selectedSkill.id);
        if (_detailLearnHint != null)
            _detailLearnHint.text = Localization.F("Press a key to bind: {0}", _selectedSkill.displayName);
    }

    private RectTransform EnsureTreeRoot(ref RectTransform cached, string name)
    {
        if (cached == null)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(_treeContent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(2200f, 2200f);
            cached = rt;
        }
        return cached;
    }

    private void ShowTree(RectTransform root)
    {
        root.gameObject.SetActive(true);
        HideOtherTreeRoots(root);
        _treeContent.anchoredPosition = Vector2.zero;
        _treeContent.localScale = Vector3.one;
    }

    private void HideOtherTreeRoots(Transform keep)
    {
        if (_generalTreeRoot != null && _generalTreeRoot != keep)
            _generalTreeRoot.gameObject.SetActive(false);
        if (_classTreeRoot != null && _classTreeRoot != keep)
            _classTreeRoot.gameObject.SetActive(false);
        if (_raceTreeRoot != null && _raceTreeRoot != keep)
            _raceTreeRoot.gameObject.SetActive(false);
    }

    private void RebuildSkillTree()
    {
        if (_skillSubTab == SkillSubTab.Class)
        {
            RebuildClassSkillTree();
            return;
        }
        if (_skillSubTab == SkillSubTab.Race)
        {
            RebuildRaceSkillTree();
            return;
        }

        if (_treeContent == null) return;

        var treeRoot = EnsureTreeRoot(ref _generalTreeRoot, "GeneralTreeRoot");
        _treeBuildRoot = treeRoot;

        // Already built once: re-frame and repaint instead of destroying ~3k GameObjects.
        if (treeRoot.childCount > 0)
        {
            ShowTree(treeRoot);
            FitTreeToViewport();
            RefreshSkillTree();
            return;
        }

        for (int i = treeRoot.childCount - 1; i >= 0; i--)
            Destroy(treeRoot.GetChild(i).gameObject);
        _treeNodes.Clear();
        _treeLines.Clear();
        _treeSkills.Clear();
        _sectorLabels.Clear();
        _categoryNodes.Clear();
        _generalHeadings.Clear();
        _selectedSkill = null;
        _treeContent.anchoredPosition = Vector2.zero;
        _treeContent.localScale = Vector3.one;
        treeRoot.localScale = Vector3.one;

        SkillCatalog.EnsureBuilt();
        var list = new List<Skill>();
        foreach (var s in SkillCatalog.All)
            if (s != null) list.Add(s);
        if (list.Count == 0) return;

        // Depth is the effective layer per EffLayerOf: ExpandTree has already placed every skill
        // at its true prereq-chain depth (root = 0, branch = 1, deep chains = 2-3).
        var depth = new Dictionary<string, int>();
        foreach (var s in list)
            depth[s.id] = EffLayerOf(s);

        // Polar slot grid: each category fans out inside its own wedge as a cone from the central
        // category wheels, and every node claims a distinct ring/angle cell whose arc is sized to
        // the node pitch, so no nodes ever overlap. When a category outgrows its current rings the
        // layout creates new rings further out (no cap) instead of stacking/colliding.
        float[] layerPitch = { 22f, 16f, 10f }; // Per-layer node pitch: L0 base (18px node + 4px gap),
                                                // L1 branch (12px node + ~4px gap), L2 deep (8px node + 2px gap).

        var posOf = new Dictionary<string, Vector2>();
        var hubPosOf = new Dictionary<string, Vector2>();

        // Builds one independent tree wheel on the shared canvas. Compact wheels spread a single
        // category over a full circle with tighter rings and the hub bubble at the wheel center
        // (Magic, Crafting); standard wheels fan each category inside its own wedge around the hub,
        // Shield taking a small slice while the rest share the remaining arc (Physical combat). All
        // positions are offset by the wheel origin so several wheels share the board without
        // overlapping.
        void BuildWheel(IReadOnlyList<SkillType> wheelTypes, Vector2 origin, bool compact)
        {
            int wheelCount = wheelTypes.Count;
            // Category bubble sits at the wheel center (compact) or along the category angle (standard).
            float categoryHubR = compact ? 0f : 170f;
            // Root spokes end at each wedge's own hub: the school bubble on a compact wheel, the
            // category bubble on a standard wheel.
            float wedgeHubR = compact ? 100f : 170f;

            // Per-category wedge layout for the standard wheel: Shield gets a small fixed slice
            // (30°) and the other categories share the rest equally, so a small category no longer
            // spreads across a full-size sector. Centers accumulate from the top (-90° in radians);
            // compact wheels ignore this (they build per-school wedges below).
            float startAngle = !compact ? -90f * Mathf.Deg2Rad : 0f;

            for (int ci = 0; ci < wheelCount; ci++)
            {
                SkillType type = wheelTypes[ci];
                // Standard wheel: Shield = 30°, the remaining arc split equally among the rest.
                // Compact wheels keep the whole-circle layout (per-school wedges built below).
                float wedgeFull = !compact
                    ? (type == SkillType.Shield ? 30f : (360f - 30f) / (wheelCount - 1f))
                    : (360f / wheelCount);
                float wedgeRad = wedgeFull * Mathf.Deg2Rad;
                float categoryCenter = !compact
                    ? startAngle + wedgeRad * 0.5f
                    : (-90f + ci * (360f / wheelCount)) * Mathf.Deg2Rad;
                startAngle += wedgeRad;

                var catList = new List<Skill>();
                foreach (var s in list)
                    if (s.Type == type) catList.Add(s);
                if (catList.Count == 0) continue;

                // Group every skill by true prereq-chain depth (root = 0, branch = 1, deep = 2-3).
                var layerOf = new Dictionary<string, int>();
                int maxLayer = 0;
                foreach (var s in catList)
                {
                    int cd = depth.TryGetValue(s.id, out int v) ? v : 0;
                    layerOf[s.id] = cd;
                    maxLayer = Mathf.Max(maxLayer, cd);
                }

                // childIndex: parent -> direct children. Drives both layer ordering and wedge spread.
                var childIndex = new Dictionary<string, List<Skill>>();
                foreach (var s in catList)
                {
                    if (s.PrereqSkillIds == null) continue;
                    foreach (var pid in s.PrereqSkillIds)
                    {
                        if (!childIndex.TryGetValue(pid, out var kids))
                            childIndex[pid] = kids = new List<Skill>();
                        kids.Add(s);
                    }
                }

                // Union co-prereq root groups so parents of a shared child — skills that share a
                // common child — end up in the same wedge (adjacent siblings instead of scatter).
                // On a compact wheel each merged group becomes one school wedge (Magic: 9 roots ->
                // 9 wedges; Crafting: 5 roots -> 5 wedges); on a standard wheel the whole category
                // stays a single wedge.
                var groups = new Dictionary<int, List<Skill>>();
                var groupOf = new Dictionary<string, int>();
                int nextGroup = 0;
                foreach (var s in catList)
                {
                    if (layerOf[s.id] != 0) continue;
                    groupOf[s.id] = nextGroup;
                    groups[nextGroup] = new List<Skill> { s };
                    nextGroup++;
                }

                void MergeGroups(int into, int from)
                {
                    foreach (var m in groups[from])
                    {
                        groupOf[m.id] = into;
                        groups[into].Add(m);
                    }
                    groups.Remove(from);
                }

                foreach (var s in catList)
                {
                    if (s.PrereqSkillIds == null || s.PrereqSkillIds.Length < 2) continue;
                    int anchor = -1;
                    foreach (var pid in s.PrereqSkillIds)
                    {
                        if (!groupOf.TryGetValue(pid, out int g) || g == anchor) continue;
                        if (anchor < 0) anchor = g;
                        else MergeGroups(anchor, g);
                    }
                }

                // ---- Wedges ----------------------------------------------------------------.
                // Standard wheels fan one whole category inside its wedge: Shield takes a small
                // fixed slice (~30°) and the rest share the remaining arc equally (~82.5° each on
                // the 5-category Physical wheel), so a small category no longer spreads across a
                // full-size sector; compact wheels split the full circle into one wedge per school,
                // starting at the top (+90°), so each element spreads toward its own side of the
                // wheel instead of packing every ring's nodes into the bottom arc the way a single
                // full-circle wedge did.
                var wedges = new List<(List<Skill> wedgeSkills, float center)>();
                float sectorHalf;
                if (!compact)
                {
                    wedges.Add((catList, categoryCenter));
                    sectorHalf = (wedgeRad * 0.5f) - 0.004f;
                }
                else
                {
                    sectorHalf = (Mathf.PI / groups.Count) - 0.004f;
                    // Assign every skill to exactly one school wedge via BFS from that school's
                    // roots. After the merge pass no child is shared across groups, so each skill
                    // lands in exactly one wedge.
                    var home = new Dictionary<string, int>();
                    var queue = new Queue<Skill>();
                    for (int g = 0; g < groups.Count; g++)
                    {
                        foreach (var r in groups[g])
                        {
                            home[r.id] = g;
                            queue.Enqueue(r);
                        }
                        while (queue.Count > 0)
                        {
                            var p = queue.Dequeue();
                            if (!childIndex.TryGetValue(p.id, out var kids)) continue;
                            foreach (var k in kids)
                                if (!home.ContainsKey(k.id))
                                {
                                    home[k.id] = g;
                                    queue.Enqueue(k);
                                }
                        }
                    }
                    for (int g = 0; g < groups.Count; g++)
                        wedges.Add((new List<Skill>(), (90f + g * (360f / groups.Count)) * Mathf.Deg2Rad));
                    foreach (var s in catList)
                        if (home.TryGetValue(s.id, out int g))
                            wedges[g].wedgeSkills.Add(s);
                }

                // Layered tree layout inside each wedge. Order rule (rings kept, adjacency fixed):
                // each layer is ordered so every node's children are emitted right after their
                // parent, and co-prereq roots — skills that share a common child — are clustered
                // into adjacent siblings. Walking the wedge left→right then reads as prereq flow
                // (Backstab and Sly Fox sit side by side because Assassinate requires both) instead
                // of catalog scatter.

                // Tier-band radii (px). Each layer owns a fixed band of rings instead of drifting
                // outward, and each wedge is sized OUT from its hub so every ring's arc has enough
                // real estate. The 5-category Physical wheel gives Shield a small slice
                // (sectorHalf ≈ 0.258) and the other four a wide one (≈ 0.716) — the rings are
                // shorted accordingly and never need a pinhole arc:
                //   ring0      r=250  Layer 0 (base) — one ring, exactly sized to the category's ROOT
                //              count (5 Melee/Ranged/Stealth/Shield, 6 Fortitude) so no slots go to waste.
                //   ring1-2    r=380,400  Layer 1 (branch) — roots×5 branches (25 for Melee/Ranged/
                //              Stealth/Shield, 30 for Fortitude) at a 16px pitch: ring1 seats 37 ≥ 30.
                //   ring3      r=1150   Layer 2 (deep) — one ring for the FULL L2 catalog at a 10px
                //              pitch; arc capacity ~165 for the four big wedges, ~59 for Shield.
                //   ring4      r=1400   Layer 3 (deepest) — the 2-3 hop locks sit one ring further out.
                // Compact wheels reuse tighter bands for school-sized wedges:
                //   ring0 r=200, ring1-2 r=300/318, ring3 r=400, ring4 r=470 — Magic's 9 wedges seat
                //   ~25-30 depth-2 nodes each at a 10px pitch (capacity ~27 at ring3) and Crafting's 5
                //   wedges seat ~25 (capacity 49), so the tree stays small.
                float RingRadius(int ring)
                {
                    if (compact)
                    {
                        if (ring <= 0) return 200f;
                        if (ring == 1) return 300f;
                        if (ring == 2) return 318f;
                        if (ring == 3) return 400f;
                        return 470f + (ring - 4) * 70f;
                    }
                    if (ring <= 0) return 250f;
                    if (ring == 1) return 380f;
                    if (ring == 2) return 400f;
                    if (ring == 3) return 1150f;
                    return 1400f + (ring - 4) * 200f;
                }
                int RingCapacity(int ring, float pitch, float half) =>
                    Mathf.Max(1, Mathf.FloorToInt(RingRadius(ring) * (2f * half) / pitch));

                foreach (var (wedgeSkills, center) in wedges)
                {
                    if (wedgeSkills.Count == 0) continue;

                    var layers = new List<List<Skill>>();
                    var layer0 = new List<Skill>();
                    for (int g = 0; g < nextGroup; g++)
                        if (groups.TryGetValue(g, out var members))
                            foreach (var m in members)
                                if (wedgeSkills.Contains(m)) layer0.Add(m);
                    if (layer0.Count == 0) continue;
                    layers.Add(layer0);

                    for (int L = 1; L <= maxLayer; L++)
                    {
                        var layer = new List<Skill>();
                        var placed = new HashSet<string>();
                        foreach (var parent in layers[L - 1])
                        {
                            if (!childIndex.TryGetValue(parent.id, out var kids)) continue;
                            kids.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
                            foreach (var k in kids)
                                if (layerOf[k.id] == L && placed.Add(k.id))
                                    layer.Add(k);
                        }
                        foreach (var s in wedgeSkills)
                            if (layerOf[s.id] == L && placed.Add(s.id))
                                layer.Add(s);
                        layers.Add(layer);
                    }

                    // Ring-band allocation: the number of nodes on a layer decides how many rings
                    // that band takes (rings widen outward). Every link then points from an inner
                    // band ring to an outer band ring.
                    var ringTotal = new List<int>();
                    var ringFor = new Dictionary<string, int>();
                    int ringCursor = 0;
                    for (int li = 0; li < layers.Count; li++)
                    {
                        var layer = layers[li];
                        float pitch = li < layerPitch.Length ? layerPitch[li] : 12f;
                        if (layer.Count == 0) continue;
                        // Pin each layer to its fixed band slots (L0->0, L1->1-2, L2->3+) so a
                        // partially filled layer never shifts the next band inward onto a wrong
                        // radius.
                        int ringIdx = Mathf.Max(ringCursor, li == 0 ? 0 : li == 1 ? 1 : 3);

                        int band = 1;
                        while (band < 8)
                        {
                            int total = 0;
                            for (int b = 0; b < band; b++) total += RingCapacity(ringIdx + b, pitch, sectorHalf);
                            if (total >= layer.Count) break;
                            band++;
                        }
                        var load = new int[band];
                        foreach (var s in layer)
                        {
                            int pick = 0;
                            for (int b = 1; b < band; b++)
                            {
                                int pickCap = li == 0 ? layer.Count : RingCapacity(ringIdx + pick, pitch, sectorHalf);
                                int capB = li == 0 ? layer.Count : RingCapacity(ringIdx + b, pitch, sectorHalf);
                                bool pickFull = load[pick] >= pickCap;
                                if (!pickFull && load[b] >= capB) continue;
                                if (pickFull || load[b] < load[pick]) pick = b;
                            }
                            while (ringTotal.Count <= ringIdx + pick) ringTotal.Add(0);
                            ringFor[s.id] = ringIdx + pick;
                            ringTotal[ringIdx + pick]++;
                            load[pick]++;
                        }
                        ringCursor = li == 2 ? ringIdx + band : (li == 1 ? 3 : ringIdx + 1);
                    }

                    // Spread partially filled rings around the wedge center so isolated outer nodes
                    // sit mid-wedge, never hugging the low-angle (left) edge of the cone.
                    var used = new List<int>(ringTotal.Count);
                    for (int r = 0; r < ringTotal.Count; r++) used.Add(0);

                    foreach (var layer in layers)
                        foreach (var s in layer)
                        {
                            int ring = ringFor[s.id];
                            // Spread the ring's own nodes evenly across the wedge's FULL arc, like
                            // ring0's roots: a sparse layer (Fortitude's 3 branch nodes) fans across
                            // the whole category wedge instead of bunching at the center while the
                            // dense categories (Melee/Ranged/Stealth) span it. Single-node rings
                            // stay centered; count never exceeds the loader's balanced capacity so
                            // rings can't overlap.
                            float ang = center - sectorHalf +
                                (used[ring] + 0.5f) * (2f * sectorHalf) / ringTotal[ring];
                            used[ring]++;

                            float radial = RingRadius(ring);
                            posOf[s.id] = origin + new Vector2(Mathf.Cos(ang) * radial, Mathf.Sin(ang) * radial);
                            _treeSkills.Add(s);
                        }

                    // Hub position per skill: this wedge's hub point, so the spoke pass below
                    // connects every root to ITS wedge's hub (a school bubble on compact wheels)
                    // instead of one shared center angle.
                    Vector2 hubPos = origin + new Vector2(Mathf.Cos(center) * wedgeHubR, Mathf.Sin(center) * wedgeHubR);
                    foreach (var s in wedgeSkills)
                        hubPosOf[s.id] = hubPos;

                    // School hub bubble on a compact wheel: a small circle at the wedge's inner end
                    // labeled with the school's name, so each element reads as its own cluster on
                    // the wheel instead of every root sharing the category bubble.
                    if (compact)
                    {
                        Skill root = wedgeSkills[0];
                        for (int i = 1; i < wedgeSkills.Count; i++)
                            if (EffLayerOf(wedgeSkills[i]) == 0) { root = wedgeSkills[i]; break; }

                        var scol = new GameObject("SchoolNode_" + root.id);
                        scol.transform.SetParent(treeRoot, false);
                        var srt = scol.AddComponent<RectTransform>();
                        srt.anchorMin = new Vector2(0.5f, 0.5f);
                        srt.anchorMax = new Vector2(0.5f, 0.5f);
                        srt.pivot = new Vector2(0.5f, 0.5f);
                        srt.anchoredPosition = hubPos;
                        srt.sizeDelta = new Vector2(56f, 56f);
                        var simg = scol.AddComponent<Image>();
                        simg.sprite = CategoryNodeSprite();
                        simg.type = Image.Type.Simple;
                        simg.preserveAspect = true;
                        simg.color = NodeColor(root, CategoryColors[(int)type]);
                        simg.raycastTarget = false;

                        var slbl = MakeBodyText(treeRoot, "SchoolLabel_" + root.id,
                            new Vector2(hubPos.x, hubPos.y), Sz(80f, 20f));
                        slbl.alignment = TextAlignmentOptions.Center;
                        slbl.fontSize = Mathf.Max(11f, Screen.height / 80f);
                        slbl.color = Color.black;
                        slbl.text = root.displayName;
                    }
                }

                // Category hub node: a circle at the wheel center (compact) or wedge center
                // (standard) that shows the category name and level.
                Vector2 catHub = origin + new Vector2(Mathf.Cos(categoryCenter) * categoryHubR, Mathf.Sin(categoryCenter) * categoryHubR);
                var catGo = new GameObject("CategoryNode_" + CategoryNames[(int)type]);
                catGo.transform.SetParent(treeRoot, false);
                var crt = catGo.AddComponent<RectTransform>();
                crt.anchorMin = new Vector2(0.5f, 0.5f);
                crt.anchorMax = new Vector2(0.5f, 0.5f);
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = catHub;
                crt.sizeDelta = new Vector2(128f, 128f);
                var cimg = catGo.AddComponent<Image>();
                cimg.sprite = CategoryNodeSprite();
                cimg.type = Image.Type.Simple;
                cimg.preserveAspect = true;
                cimg.color = CategoryColors[(int)type];
                cimg.raycastTarget = false;
                _categoryNodes.Add((type, cimg));

                // Category name centered inside the hub bubble (drawn after -> on top of the node).
                var lbl = MakeBodyText(treeRoot, "Sector_" + CategoryNames[(int)type],
                    new Vector2(catHub.x - 64f, catHub.y), Sz(128f, 28f));
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.fontSize = Mathf.Max(16f, Screen.height / 66f);
                lbl.color = Color.black;
                _sectorLabels.Add((type, lbl));
            }
        }

    // Compose the three independent trees on one board: Magic (compact full-circle wheel) left,
    // Physical combat (standard 4-wedge wheel) center, Crafting (compact full-circle wheel) right.
    BuildWheel(new[] { SkillType.Magic }, new Vector2(-2200f, 0f), compact: true);
    BuildWheel(new[] { SkillType.Melee, SkillType.Ranged, SkillType.Stealth, SkillType.Fortitude, SkillType.Shield }, Vector2.zero, compact: false);
    BuildWheel(new[] { SkillType.Crafting }, new Vector2(2200f, 0f), compact: true);

    MakeGeneralHeading("MAGIC", new Vector2(-2200f, 560f));
    MakeGeneralHeading("PHYSICAL", new Vector2(0f, 1840f));
    MakeGeneralHeading("CRAFTING", new Vector2(2200f, 560f));

    if (posOf.Count == 0) return;

        // Connection lines (prereq -> child), plus a spoke from each root skill (no prereq) to its
        // wedge hub (school bubble on compact wheels, category bubble on standard wheels) so no node
        // ever floats unconnected.
        float thick = 1.5f * S;
        foreach (var s in list)
        {
            if (!posOf.TryGetValue(s.id, out Vector2 end)) continue;
            float lineThick = s.Layer == 0 ? thick * 1.3f : s.Layer == 1 ? thick : thick * 0.7f;
            if (s.PrereqSkillIds != null && s.PrereqSkillIds.Length > 0)
            {
                foreach (var pid in s.PrereqSkillIds)
                {
                    if (!posOf.TryGetValue(pid, out Vector2 start)) continue;
                    var line = MakeTreeLine(start, end, lineThick);
                    _treeLines.Add((line, s));
                }
            }
            else if (hubPosOf.TryGetValue(s.id, out Vector2 hub))
            {
                var spoke = MakeTreeLine(hub, end, lineThick * 0.7f);
                var tint = CategoryColors[(int)s.Type];
                spoke.color = new Color(tint.r, tint.g, tint.b, 0.4f);
                _treeLines.Add((spoke, s));
            }
        }

        // Nodes.
        foreach (var s in list)
        {
            if (!posOf.TryGetValue(s.id, out Vector2 pos)) continue;
            var node = MakeTreeNode(s, pos);
            _treeNodes.Add((s, node.image, node.label));
        }

        FitTreeToViewport();
        RefreshSkillTree();
    }

    /// <summary>Scales the tree content so the whole wheel fits inside the viewport.</summary>
    private void FitTreeToViewport()
    {
        if (_treeContent == null || _treeContent.parent == null) return;

        // Bounding-box fit (not max-radius): the General board hosts several off-center wheels, so a
        // centered maxR would clip the outer magic/crafting clusters. Re-center content on the box.
        bool any = false;
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
        void Include(Image image)
        {
            if (image == null) return;
            var a = ((RectTransform)image.transform).anchoredPosition;
            minX = Mathf.Min(minX, a.x);
            maxX = Mathf.Max(maxX, a.x);
            minY = Mathf.Min(minY, a.y);
            maxY = Mathf.Max(maxY, a.y);
            any = true;
        }
        switch (_skillSubTab)
        {
            case SkillSubTab.Class:
                foreach (var (_, image) in _classTreeNodes) Include(image);
                break;
            case SkillSubTab.Race:
                foreach (var (_, image) in _raceTreeNodes) Include(image);
                break;
            default:
                foreach (var (_, image, _) in _treeNodes) Include(image);
                break;
        }
        if (!any)
        {
            _treeContent.anchoredPosition = Vector2.zero;
            _treeContent.sizeDelta = new Vector2(2200f, 2200f);
            return;
        }

        float w = Mathf.Max(1f, maxX - minX) + 80f;
        float h = Mathf.Max(1f, maxY - minY) + 80f;
        _treeContent.anchoredPosition = -new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        _treeContent.sizeDelta = new Vector2(w, h);
        var vp = _treeContent.parent as RectTransform;
        if (vp == null) return;
        float scale = Mathf.Min(vp.rect.width / w, vp.rect.height / h);
        float fitFloor = Mathf.Min(TreePan.MinScale, scale);
        scale = Mathf.Clamp(scale, fitFloor, TreePan.MaxScale);
        _treeContent.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>Title label placed above one of the three General-tree wheels.</summary>
    private void MakeGeneralHeading(string text, Vector2 pos)
    {
        var h = MakeBodyText(_treeBuildRoot, "GeneralHeading_" + text, pos, Sz(420f, 30f));
        h.alignment = TextAlignmentOptions.Center;
        h.fontSize = Mathf.Max(16f, Screen.height / 44f);
        h.color = new Color(0.85f, 0.88f, 0.92f, 0.95f);
        _generalHeadings.Add(h);
    }

    /// <summary>
    /// Effective tree layer for a skill: ExpandTree has already resolved every skill to its true
    /// prereq-chain depth (root = 0, branch = 1, deep chains = 2-3), so the data layer is final.
    /// </summary>
    private static int EffLayerOf(Skill s)
    {
        if (s == null) return 0;
        return s.Layer;
    }

    /// <summary>
    /// Node body color for the General tree. Magic nodes blend their unlock-state color ~60%
    /// toward the skill's element (DamageKind) color, so fireball reads orange, frostbolt icy-blue,
    /// dark nodes violet, etc. — while learned/available/locked stays readable. Non-magic nodes
    /// keep their plain state color.
    /// </summary>
    private static Color NodeColor(Skill skill, Color stateColor)
    {
        if (skill == null || skill.Type != SkillType.Magic) return stateColor;
        Color element = skill.DamageKind != DamageType.Physical
            ? DamageNumber.ColorFor(skill.DamageKind)
            : CategoryColors[(int)SkillType.Magic];
        return Color.Lerp(stateColor, element, 0.6f);
    }

    private (Image image, TMP_Text label) MakeTreeNode(Skill skill, Vector2 pos)
    {
        var go = new GameObject("Node_" + skill.id);
        go.transform.SetParent(_treeBuildRoot, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        int le = EffLayerOf(skill);
        float nw = le == 0 ? 18f : le == 1 ? 12f : 8f;
        float nh = le == 0 ? 14f : le == 1 ? 10f : 6f;
        rt.sizeDelta = Sz(nw, nh);
        var img = go.AddComponent<Image>();
        img.color = NodeLocked;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        Skill captured = skill;
        btn.onClick.AddListener(() =>
        {
            _selectedSkill = captured;
            RefreshSkillTree();
        });

        var strip = new GameObject("CatStrip");
        strip.transform.SetParent(go.transform, false);
        var srt = strip.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 1f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.offsetMin = new Vector2(2f, -6f);
        srt.offsetMax = new Vector2(-2f, 0f);
        var stripImg = strip.AddComponent<Image>();
        stripImg.raycastTarget = false;
        stripImg.color = CategoryColors[(int)skill.Type];

        var label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        var lr = label.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(2f, 1f);
        lr.offsetMax = new Vector2(-2f, -1f);
        var tmp = label.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.text = skill.displayName;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 2f;
        tmp.fontSizeMax = Mathf.Max(2f, nh * 0.55f);
        tmp.fontSize = tmp.fontSizeMax;
        return (img, tmp);
    }

    private Image MakeTreeLine(Vector2 start, Vector2 end, float thick)
    {
        var go = new GameObject("Line");
        go.transform.SetParent(_treeBuildRoot, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        Vector2 mid = (start + end) * 0.5f;
        float len = Vector2.Distance(start, end);
        rt.anchoredPosition = mid;
        rt.sizeDelta = new Vector2(Mathf.Max(1f, len), thick);
        float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        var img = go.AddComponent<Image>();
        img.color = LineInert;
        img.raycastTarget = false;
        return img;
    }

    // ── General / Class sub-tab toggle ───────────────────────────────────

    private void OnGeneralTab() => SetSkillSubTab(SkillSubTab.General);
    private void OnClassTab() => SetSkillSubTab(SkillSubTab.Class);
    private void OnRaceTab() => SetSkillSubTab(SkillSubTab.Race);

    private void SetSkillSubTab(SkillSubTab tab)
    {
        if (_skillSubTab == tab) return;
        _skillSubTab = tab;
        _selectedSkill = null;
        _selectedClassSkill = null;
        _selectedRaceSkill = null;
        if (_detailPane != null) _detailPane.SetActive(false);
        // Legend only shown in General view (6-category colors irrelevant to class/race tree).
        if (_legendRoot != null)
        {
            foreach (var chip in _legendChips)
                if (chip.swatch != null) chip.swatch.transform.parent.gameObject.SetActive(tab == SkillSubTab.General);
            foreach (var node in _categoryNodes)
                if (node.image != null) node.image.gameObject.SetActive(tab == SkillSubTab.General);
            foreach (var sec in _sectorLabels)
                if (sec.label != null) sec.label.gameObject.SetActive(tab == SkillSubTab.General);
            foreach (var heading in _generalHeadings)
                if (heading != null) heading.gameObject.SetActive(tab == SkillSubTab.General);
        }
        UpdateSubTabButtons();
        RebuildSkillTree();
    }

    private void UpdateSubTabButtons()
    {
        bool gen = _skillSubTab == SkillSubTab.General;
        bool cls = _skillSubTab == SkillSubTab.Class;
        bool rac = _skillSubTab == SkillSubTab.Race;
        if (_generalTabBtn != null)
        {
            var img = _generalTabBtn.GetComponent<Image>();
            if (img != null) img.color = gen ? NodeLearned : NodeLocked;
        }
        if (_classTabBtn != null)
        {
            var img = _classTabBtn.GetComponent<Image>();
            if (img != null) img.color = cls ? NodeLearned : NodeLocked;
        }
        if (_raceTabBtn != null)
        {
            var img = _raceTabBtn.GetComponent<Image>();
            if (img != null) img.color = rac ? NodeLearned : NodeLocked;
        }
    }

    // ── Class skill tree builder ─────────────────────────────────────────

    private void RebuildClassSkillTree()
    {
        if (_treeContent == null) return;

        var unlocker = ClassUnlockerOf();
        string classId = unlocker != null ? unlocker.ActiveClassId : "wanderer";
        var root = EnsureTreeRoot(ref _classTreeRoot, "ClassTreeRoot");
        _treeBuildRoot = root;

        // Already built for this class: re-frame and repaint instead of destroying ~3k GameObjects.
        if (root.childCount > 0 && _classTreeBuildId == classId)
        {
            ShowTree(root);
            FitTreeToViewport();
            RefreshClassSkillTree();
            return;
        }
        _classTreeBuildId = classId;

        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
        _classTreeNodes.Clear();
        _classTreeLines.Clear();
        _classTreeSkills.Clear();
        _selectedClassSkill = null;
        _selectedSkill = null;
        _treeContent.anchoredPosition = Vector2.zero;
        _treeContent.localScale = Vector3.one;
        root.localScale = Vector3.one;

        ClassSkillCatalog.EnsureBuilt();
        var list = new List<ClassSkill>();
        foreach (var s in ClassSkillCatalog.ForClass(classId))
            if (s != null) list.Add(s);
        if (list.Count == 0) return;

        // Radial layout: hub at center, 3 paths fan out at 120° intervals.
        const float pathR = 200f;
        const float leafR = 420f;
        const float leafSpread = 50f;

        // Hub node.
        var hub = list.Find(s => s.Layer == 0);
        if (hub != null)
        {
            _classTreeSkills.Add(hub);
            _classTreeNodes.Add((hub, MakeClassTreeNode(hub, Vector2.zero, 24f)));
        }

        // Group Layer 1 nodes and their Layer 2 children by path order.
        var layer1 = new List<ClassSkill>();
        foreach (var s in list)
            if (s.Layer == 1) layer1.Add(s);
        layer1.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        int pathCount = Mathf.Max(1, layer1.Count);
        for (int pi = 0; pi < pathCount; pi++)
        {
            var pathNode = layer1[pi];
            float angle = (-90f + pi * (360f / pathCount)) * Mathf.Deg2Rad;
            Vector2 pos1 = new Vector2(Mathf.Cos(angle) * pathR, Mathf.Sin(angle) * pathR);

            _classTreeSkills.Add(pathNode);
            _classTreeNodes.Add((pathNode, MakeClassTreeNode(pathNode, pos1, 18f)));

            // Line from hub to path parent.
            var spoke = MakeTreeLine(Vector2.zero, pos1, 2.5f);
            spoke.color = LineActive;
            _classTreeLines.Add((spoke, pathNode));

            // Find Layer 2 children of this path node.
            var children = new List<ClassSkill>();
            foreach (var s in list)
            {
                if (s.Layer != 2 || s.PrereqSkillIds == null) continue;
                foreach (var pid in s.PrereqSkillIds)
                    if (pid == pathNode.id) { children.Add(s); break; }
            }
            children.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

            for (int ci = 0; ci < children.Count; ci++)
            {
                var child = children[ci];
                float childAngle = angle + (ci - (children.Count - 1) * 0.5f) * (leafSpread * Mathf.Deg2Rad);
                Vector2 pos2 = new Vector2(Mathf.Cos(childAngle) * leafR, Mathf.Sin(childAngle) * leafR);

                _classTreeSkills.Add(child);
                _classTreeNodes.Add((child, MakeClassTreeNode(child, pos2, 14f)));

                var line = MakeTreeLine(pos1, pos2, 1.5f);
                _classTreeLines.Add((line, child));
            }
        }

        FitTreeToViewport();
        RefreshClassSkillTree();
    }

    private Image MakeClassTreeNode(ClassSkill skill, Vector2 pos, float size)
    {
        var go = new GameObject("CNode_" + skill.id);
        go.transform.SetParent(_treeBuildRoot, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = Sz(size * 2f, size);
        var img = go.AddComponent<Image>();
        img.color = skill.IsPassive ? NodeAvailable : new Color(0.55f, 0.48f, 0.9f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        ClassSkill captured = skill;
        btn.onClick.AddListener(() =>
        {
            _selectedClassSkill = captured;
            RefreshClassSkillTree();
        });

        // Label.
        var lbl = new GameObject("Label");
        lbl.transform.SetParent(go.transform, false);
        var lr = lbl.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero;
        lr.offsetMax = Vector2.zero;
        var tmp = lbl.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.text = skill.displayName;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 2f;
        tmp.fontSizeMax = Mathf.Max(2f, size * 0.55f);
        tmp.fontSize = tmp.fontSizeMax;
        return img;
    }

    private void RefreshClassSkillTree()
    {
        if (_detailPane != null)
            _detailPane.SetActive(_selectedClassSkill != null);

        foreach (var (skill, image) in _classTreeNodes)
        {
            if (image == null) continue;
            if (skill == _selectedClassSkill) image.color = NodeSelected;
            else image.color = skill.IsPassive ? NodeAvailable : new Color(0.55f, 0.48f, 0.9f, 1f);
        }

        foreach (var (line, target) in _classTreeLines)
        {
            if (line == null) continue;
            line.color = LineActive;
        }

        if (_skillPointsText != null)
        {
            var unlocker = ClassUnlockerOf();
            string cls = unlocker != null ? unlocker.ActiveClassId : "wanderer";
            _skillPointsText.text = Localization.F("Class: {0}", cls);
        }

        if (_categoryLevelText != null)
            _categoryLevelText.text = Localization.F("Skills: {0}", _classTreeNodes.Count);

        RefreshClassSkillDetail();
    }

    private void RefreshClassSkillDetail()
    {
        if (_detailTitle == null) return;
        var skill = _selectedClassSkill;
        if (skill == null)
        {
            _detailTitle.text = Localization.T("No skill selected");
            _detailDesc.text = "";
            _detailMeta.text = "";
            _detailLearnHint.text = "";
            return;
        }
        _detailTitle.text = skill.displayName;
        _detailDesc.text = skill.description;

        var meta = new StringBuilder();
        meta.Append(skill.IsPassive ? Localization.T("Type: Passive") : Localization.T("Type: Castable"));
        meta.Append('\n');

        if (!skill.IsPassive)
        {
            meta.Append("Cost: ");
            if (skill.SkillCost.Resource == ResourceKind.None) meta.Append("Free");
            else meta.Append(skill.SkillCost.Resource.ToString()).Append(' ').Append(skill.SkillCost.Amount.ToString("0.##"));
            if (skill.SkillCost.Cooldown > 0f)
                meta.Append('\n').Append("CD: ").Append(skill.SkillCost.Cooldown.ToString("0.#")).Append('s');
        }
        else
        {
            meta.Append(Localization.T("Cost: Free (always-on)"));
        }

        if (skill.Mods != null && skill.Mods.Length > 0)
        {
            meta.Append('\n').Append(Localization.T("Passive Mods:"));
            foreach (var m in skill.Mods)
                meta.Append('\n').Append("  ").Append(m.kind.ToString()).Append(" +").Append((m.amount * 100f).ToString("0.#")).Append("%");
        }

        _detailMeta.text = meta.ToString();
        _detailLearnHint.text = Localization.T("Auto-granted when class is unlocked.");

        // Class castables run through ClassSkillCaster's own hotkey system — no manual binding.
        if (_learnBtn != null) _learnBtn.gameObject.SetActive(false);
        if (_assignKeyBtn != null) _assignKeyBtn.gameObject.SetActive(false);
    }

    // ── Race skill tree builder ────────────────────────────────────────────

    private void RebuildRaceSkillTree()
    {
        if (_treeContent == null) return;

        var stats = PlayerStatsOf();
        string raceId = stats != null && stats.Race != null ? stats.Race.raceId : "human";
        var root = EnsureTreeRoot(ref _raceTreeRoot, "RaceTreeRoot");
        _treeBuildRoot = root;

        // Already built for this race: re-frame and repaint instead of destroying ~3k GameObjects.
        if (root.childCount > 0 && _raceTreeBuildId == raceId)
        {
            ShowTree(root);
            FitTreeToViewport();
            RefreshRaceSkillTree();
            return;
        }
        _raceTreeBuildId = raceId;

        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
        _raceTreeNodes.Clear();
        _raceTreeLines.Clear();
        _raceTreeSkills.Clear();
        _selectedRaceSkill = null;
        _selectedSkill = null;
        _selectedClassSkill = null;
        _treeContent.anchoredPosition = Vector2.zero;
        _treeContent.localScale = Vector3.one;
        root.localScale = Vector3.one;

        RaceSkillCatalog.EnsureBuilt();
        var list = new List<RaceSkill>();
        foreach (var s in RaceSkillCatalog.ForRace(raceId))
            if (s != null) list.Add(s);
        if (list.Count == 0) return;

        // Radial layout: hub at center, 3 paths fan out at 120° intervals.
        const float pathR = 200f;
        const float leafR = 420f;
        const float leafSpread = 50f;

        // Hub node.
        var hub = list.Find(s => s.Layer == 0);
        if (hub != null)
        {
            _raceTreeSkills.Add(hub);
            _raceTreeNodes.Add((hub, MakeRaceSkillTreeNode(hub, Vector2.zero, 24f)));
        }

        // Group Layer 1 nodes and their Layer 2 children by path order.
        var layer1 = new List<RaceSkill>();
        foreach (var s in list)
            if (s.Layer == 1) layer1.Add(s);
        layer1.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        int pathCount = Mathf.Max(1, layer1.Count);
        for (int pi = 0; pi < pathCount; pi++)
        {
            var pathNode = layer1[pi];
            float angle = (-90f + pi * (360f / pathCount)) * Mathf.Deg2Rad;
            Vector2 pos1 = new Vector2(Mathf.Cos(angle) * pathR, Mathf.Sin(angle) * pathR);

            _raceTreeSkills.Add(pathNode);
            _raceTreeNodes.Add((pathNode, MakeRaceSkillTreeNode(pathNode, pos1, 18f)));

            // Line from hub to path parent.
            var spoke = MakeTreeLine(Vector2.zero, pos1, 2.5f);
            spoke.color = LineActive;
            _raceTreeLines.Add((spoke, pathNode));

            // Find Layer 2 children of this path node.
            var children = new List<RaceSkill>();
            foreach (var s in list)
            {
                if (s.Layer != 2 || s.PrereqSkillIds == null) continue;
                foreach (var pid in s.PrereqSkillIds)
                    if (pid == pathNode.id) { children.Add(s); break; }
            }
            children.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

            for (int ci = 0; ci < children.Count; ci++)
            {
                var child = children[ci];
                float childAngle = angle + (ci - (children.Count - 1) * 0.5f) * (leafSpread * Mathf.Deg2Rad);
                Vector2 pos2 = new Vector2(Mathf.Cos(childAngle) * leafR, Mathf.Sin(childAngle) * leafR);

                _raceTreeSkills.Add(child);
                _raceTreeNodes.Add((child, MakeRaceSkillTreeNode(child, pos2, 14f)));

                var line = MakeTreeLine(pos1, pos2, 1.5f);
                _raceTreeLines.Add((line, child));
            }
        }

        FitTreeToViewport();
        RefreshRaceSkillTree();
    }

    private Image MakeRaceSkillTreeNode(RaceSkill skill, Vector2 pos, float size)
    {
        var go = new GameObject("RNode_" + skill.id);
        go.transform.SetParent(_treeBuildRoot, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = Sz(size * 2f, size);
        var img = go.AddComponent<Image>();
        img.color = skill.IsPassive ? RaceNodePassive : RaceNodeActive;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        RaceSkill captured = skill;
        btn.onClick.AddListener(() =>
        {
            _selectedRaceSkill = captured;
            RefreshRaceSkillTree();
        });

        // Label.
        var lbl = new GameObject("Label");
        lbl.transform.SetParent(go.transform, false);
        var lr = lbl.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero;
        lr.offsetMax = Vector2.zero;
        var tmp = lbl.AddComponent<TextMeshProUGUI>();
        GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
        tmp.text = skill.displayName;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 2f;
        tmp.fontSizeMax = Mathf.Max(2f, size * 0.55f);
        tmp.fontSize = tmp.fontSizeMax;
        return img;
    }

    private void RefreshRaceSkillTree()
    {
        if (_detailPane != null)
            _detailPane.SetActive(_selectedRaceSkill != null);

        foreach (var (skill, image) in _raceTreeNodes)
        {
            if (image == null) continue;
            if (skill == _selectedRaceSkill) image.color = NodeSelected;
            else image.color = skill.IsPassive ? RaceNodePassive : RaceNodeActive;
        }

        foreach (var (line, target) in _raceTreeLines)
        {
            if (line == null) continue;
            line.color = LineActive;
        }

        if (_skillPointsText != null)
        {
            var stats = PlayerStatsOf();
            string race = stats != null && stats.Race != null ? stats.Race.displayName : "Human";
            _skillPointsText.text = Localization.F("Race: {0}", race);
        }

        if (_categoryLevelText != null)
            _categoryLevelText.text = Localization.F("Skills: {0}", _raceTreeNodes.Count);

        RefreshRaceSkillDetail();
    }

    private void RefreshRaceSkillDetail()
    {
        if (_detailTitle == null) return;
        var skill = _selectedRaceSkill;
        if (skill == null)
        {
            _detailTitle.text = Localization.T("No skill selected");
            _detailDesc.text = "";
            _detailMeta.text = "";
            _detailLearnHint.text = "";
            return;
        }
        _detailTitle.text = skill.displayName;
        _detailDesc.text = skill.description;

        var meta = new StringBuilder();
        meta.Append(skill.IsPassive ? Localization.T("Type: Passive") : Localization.T("Type: Castable"));
        meta.Append('\n');

        if (!skill.IsPassive)
        {
            meta.Append("Cost: ");
            if (skill.SkillCost.Resource == ResourceKind.None) meta.Append("Free");
            else meta.Append(skill.SkillCost.Resource.ToString()).Append(' ').Append(skill.SkillCost.Amount.ToString("0.##"));
            if (skill.SkillCost.Cooldown > 0f)
                meta.Append('\n').Append("CD: ").Append(skill.SkillCost.Cooldown.ToString("0.#")).Append('s');
        }
        else
        {
            meta.Append(Localization.T("Cost: Free (always-on)"));
        }

        if (skill.Mods != null && skill.Mods.Length > 0)
        {
            meta.Append('\n').Append(Localization.T("Passive Mods:"));
            foreach (var m in skill.Mods)
                meta.Append('\n').Append("  ").Append(m.kind.ToString()).Append(" +").Append((m.amount * 100f).ToString("0.#")).Append("%");
        }

        _detailMeta.text = meta.ToString();

        // Race castables route through RaceSkillCaster via the shared hotkey system.
        if (_learnBtn != null) _learnBtn.gameObject.SetActive(false);
        if (_assignKeyBtn != null)
        {
            _assignKeyBtn.gameObject.SetActive(true);
            _assignKeyBtn.interactable = !skill.IsPassive;
        }
        if (skill.IsPassive)
            _detailLearnHint.text = Localization.T("Passive — always active while this race is active.");
        else
            _detailLearnHint.text = Localization.T("Bind a key to use this racial active.");
    }

    private void BuildTreeLegend(RectTransform viewport)
    {
        const float gap = 122f * S;
        const float swatch = 14f * S;
        for (int i = 0; i < CategoryNames.Length; i++)
        {
            var go = new GameObject("Legend_" + CategoryNames[i]);
            go.transform.SetParent(viewport, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2((14f + i * gap) * S, -14f * S);
            rt.sizeDelta = Sz(114f, 18f);

            var swatchGo = new GameObject("Swatch");
            swatchGo.transform.SetParent(go.transform, false);
            var srt = swatchGo.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0.5f);
            srt.anchorMax = new Vector2(0f, 0.5f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.anchoredPosition = Vector2.zero;
            srt.sizeDelta = new Vector2(swatch, swatch);
            var simg = swatchGo.AddComponent<Image>();
            simg.raycastTarget = false;
            simg.color = CategoryColors[i];

            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            var lr = label.AddComponent<RectTransform>();
            lr.anchorMin = new Vector2(0f, 0.5f);
            lr.anchorMax = new Vector2(1f, 0.5f);
            lr.offsetMin = new Vector2(swatch + 5f, 0f);
            lr.offsetMax = Vector2.zero;
            var tmp = label.AddComponent<TextMeshProUGUI>();
            GameManager.Instance?.UIManager?.ApplyDefaultFont(tmp);
            tmp.raycastTarget = false;
            tmp.fontSize = Mathf.Max(11f, Screen.height / 100f);
            tmp.color = new Color(0.85f, 0.87f, 0.9f, 1f);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            _legendChips.Add(((SkillType)i, simg, tmp));
        }
    }
}