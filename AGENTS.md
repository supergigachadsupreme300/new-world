# Project Rules

1. **Always commit and push after every task.** Commit on Git `main` and push to
   `https://github.com/supergigachadsupreme300/new-world` (PowerShell; e.g.
   `git add -A; git commit -m "..." ; git push origin main`). Make a new commit for follow-up
   fixes rather than amending. Do not commit unless a task is complete.

2. **Before finishing any implementation, read AND update all "read/update" docs below in the same
   pass as the code change** (never a follow-up commit by itself):
   - `game-design.md` — keep it in sync with implemented behavior (section references like §3.3,
     §3.7, §3.8, §5.7). Update stats/counts, status tables, and signature mechanisms on any feature
     change, and remove stale text (e.g. a removed currency/UI leftover) in the same pass.
   - `AGENTS.md` — this file; update it whenever a recurring convention changes (see rules 4-6).
   - `PROGRESS.md` — record every completed task as a new `## 1xx` entry at the top with a `### 1xx-status`
     block; note pending play-test items and any follow-up fixes. Refresh the intro ("Last updated")
     when older entries drift.
   - `THINKING.md` — for any non-trivial investigation, keep the raw reasoning trail (hypotheses,
     evidence for/against, dead ends, verdicts) in a `## 1xx` section; mark each hypothesis
     confirmed/rejected/open and keep the section marked OPEN until the task ships. This is the
     "how I reasoned it out" log — never cite it as implemented behavior.

3. **No CLI/Unity build is run in this project.** Compile and behavior are verified by code review;
   the user play-tests in Unity afterwards. Note that verification status in each task's status block.
   **Review is not compilation:** grep confirms a symbol exists, not that two signatures agree, and a
   local declared inside a `switch` case is in scope for the *whole* switch, so a read from a sibling
   case looks legal and only fails definite assignment. When the user pastes Unity console errors,
   fix them in a **new commit** (never an amend) and sweep the whole class — every
   `return <void helper>(...)`, and every name declared in one `case` and read in another.
   **Run `powershell -ExecutionPolicy Bypass -File tools\StaticChecks.ps1` before committing any
   change to `WorldBuilder*.cs` or `NewWorldTestGround.cs`** — it mechanises exactly those checks
   (balance, overload-aware arity, void-return, unassigned locals, cross-case locals, part-key parity).
   It reports *candidates*, not verdicts: 0 candidates still means "not compiled", so paste Unity's
   output into the handoff rather than fixing errors silently. If you add a file in those areas, add
   it to the script's `$files` list or checks 1–5 stop covering it.

4. **QA/test features go on the independent test platform**, never in the legacy world: add an opt-in
   lane + serialized toggle in `Assets\Scripts\Opt\NewWorldTestGround.cs` (`RunBenchSpawn`,
   `RunSafely("lane", SpawnLane)`), mirroring the existing `Spawn*` lane methods, and key placement
   off `PlatformTopY`/`PlatformCenter`. Do not touch the legacy `WorldBuilder` village or streamed
   terrain.

5. **Verify by grep + reread, not by compiling.** When removing/changing public members (e.g. dropping
   a currency, renaming a method), grep `Assets\Scripts` for every removed symbol and its call sites
   (UI builders like `CharacterInfoUI`, save/restore paths, consumers) and confirm signatures match
   before finishing the commit.

6. **When done with all requested tasks, wrap up with a short closing summary** in your final message:
   one line per task naming the task id (`1xx`), the commit hash it shipped in, and the files touched;
   then state the verification performed (grep/reread — no build) and list the pending play-test items
   the user should check in Unity. Do not add this summary until every requested task is actually
   committed and pushed.

7. **Measure before fixing a visual/geometry report.** When the user reports something you cannot see
   from the code (gaps, holes, invisible meshes, z-fighting, popping), add a read-only on-demand
   measurement lane to `NewWorldTestGround.cs` FIRST — one key, one number, cached on the HUD and
   logged — and let the readout name the mechanism before changing behaviour. Ship the measurement and
   the fix as SEPARATE tasks so the readout that justified the fix stays in history. A fix chosen
   without a measurement is a guess: it can be correct and still change nothing, and then the real
   cause is still unmeasured for the next attempt. Read-only means read-only: no rebuild, no patch,
   no re-stamp, no forced poll — the report must describe the frame the key was pressed on.
   Three conventions 1hy added, all of them about making a *positive* result mean something:
   - **Order the sections by whether their PREMISE holds, not by how much they look like a test.**
     The F3 corner/void audit runs its build fingerprint *first*, ahead of the two sections that
     look more like tests, because if the resident set was built by two versions of the generator
     (rule 11) then those two sections are evidence about a world that is not on screen. A static
     proof about a generator is a claim about the premise, not about the screen: 1hy could prove the
     4-chunk node emits four coincident vertices, and that proof is void under a mixed resident set.
     Cheapest-check-first and premise-check-first coincide here; do not let the aesthetic ordering win.
   - **Scope the walk to the band where a positive result is *possible*, and print the band.** The
     void walk covers rings `0 .. view + FarOuterKeep` because that is the union of what the two
     owners promise, so a hole in it means something. Scanning everything visible instead pulls in
     real chunks that are dormant-and-hidden past that ring with no far-cell owner — ~84 correct,
     expected, invisible "voids" at 630 m that bury the one real finding. A measurement that reports
     known-absent things is not conservative, it is unreadable.
   - **Never re-derive another component's private formula to define that scope.** The visible radius
     is `ChunkLodManager.EffectiveCullDistance()` and it is private; copying that expression into the
     lane would be rule 8 in reverse (a second spelling that rots when the LOD side changes, and
     silently mis-scopes the audit when it does). Find the invariant both sides already agree on —
     here, the ownership band — and scope to that instead.
   - **A scope is a claim about the mechanism, so write it down as one — and a correct scope for the
     wrong *family of owner* still reports "clean" confidently.** 1hy scoped section B to the band
     where a void is possible and printed the band, which is why B's zero was trustworthy. It then
     scoped section C to `_loadedChunks`, comparing real chunks against each other — and never stated
     that this *presupposed the defect is between two loaded chunks*. The player then reported the
     gap **only at the outer x/z corner tile**, which is the near/far boundary: one loaded quadrant
     and three far ones. C could not see it at any depth, and it printed real numbers the whole time,
     which made a wrong premise look like evidence. So when scoping, name **which owner(s) the walk
     admits** as well as the radius, and if the report is about a seam, check that the walk contains
     both sides of it before concluding anything. "Drawn" and "flush" are separate properties: a
     surface that is live but one step off is exactly the failure a "is anything drawn here" check
     cannot see.
   - **A verdict line that conflated "broken" with "smaller than promised" is the same bug as a
     coverage walk that admits one owner — and the conflation is what makes a number unusable.**
     1i1's section B reported 2965 undrawn footprints because it demanded `!FarShadowedByCoarse` on a
     footprint's owner, but that helper is *true* when a live coarser cell already covers the same
     ground — it names a reserved shadow, not an absence. So all 2965 were reserved shadows being
     called holes. Two habits: ask coverage questions directly (enumerate every owner that can draw,
     take the first live one) instead of inferring them from the ownership predicate, and split the
     remainder into **claimed-dead** (an owner exists and nothing is drawing: a hole) and **unowned**
     (nothing claims it: a shorter horizon) with separate verdicts. When a number is retracted, say
     the replacement is *unknown*, not zero — 1i2 retracted 2965 and had no number to put in its
     place.
   - **Gate a classifier on the WIDTH of its own test, or it will name a mechanism for arithmetic
     noise.** 1i1's section D used `RenderedCornerTolerance` both as the "is this stepped" threshold
     and as the "is this side on pristine noise" test, so two values each within tol of the same
     pristine value could differ by up to `2*tol` — stepped *and* both-pristine at once. It filed 19
     nodes at 1–2 cm under `not an edit - look at the lattice/seed`, which reads like a finding and
     is noise from two facets rounded to one lattice. Any threshold comparison used to *enter* a
     classification must be at least the sum of the tolerances used *inside* it.
   - **A check is only evidence if you have seen it fail on the thing it is for.** A check that flags a
     false positive on the first file you add it to is a check whose silence has stopped meaning
     anything. 1hy's `StaticChecks.ps1` check 4 reported every `out` parameter as an unassigned local;
     a reader trained by 4 false candidates waves through the next real CS0165. **A balance check
     cannot see a construct in the wrong place** — 1i4 declared a method between the `using` block and
     `partial class WorldStreamer` and reported braces 25/25, because the method body and the class
     body were two separate well-formed constructs. Grep found the symbol, balance passed, review
     passed, and only Unity's parser objected (CS0106, which does not even name the class). Check 7
     now flags any member at brace depth 0, and was verified by **reintroducing that exact bug and
     watching it fire** — a green check nobody has seen fail is not a check.
   - **A safe idiom in one caller is not evidence it is safe in another.** 1i4 read `_loadedChunks` from
     `BuildChunkMeshData`, which runs on a **ThreadPool thread** via `BackgroundGenerateChunk`, while
     the main thread builds/unloads/demotes — and `Dictionary<TKey,TValue>` is not safe to read during
     a write. The F3 audit reads the same dictionary constantly and never races, because it is
     main-thread-only, so the pattern looked safe. **Check the thread the code runs on before reusing
     an idiom, and never argue that a data source is "strictly better" without naming the thread.**
   - Also: a check that flags a false positive on the first file you add it to is a check whose
     silence has stopped meaning anything. 1hy's `StaticChecks.ps1` check 4 reported every `out`
     parameter as an unassigned local; a reader trained by 4 false candidates waves through the
     next real CS0165. Fix the check rather than filing the candidates as "expected".

8. **A seam invariant is only as good as the arithmetic of the COPY, not the source.** When a value is
   copied out of another structure instead of re-derived (the corner lattice copies a tile's stored
   vertex; a cache copies a source array; a lookup indexes by another system's key), the addressing
   rule of that copy IS the seam contract. Do not infer correctness from how pure the source is — pure
   noise sampled per corner is exact, the same noise *copied through a wrong owner slot* is off by a
   whole metre, permanently, in an unedited world. Two habits follow: check the copy's index/owner
   arithmetic with a concrete worked example at a boundary node (the 4-chunk corner catches what
   mid-edge checks miss), and never let a validator stand in for a layer it does not read —
   `ChunkValidator` compares tile heights tile-vs-tile and is structurally blind to a lattice bug, so
   a green validator is not evidence about the lattice.

9. **Hand-authored block geometry is stated by its support, not by its centre.** The block-built
   structures (holy places, NPC rigs, `CreatePartCube` call sites) are positioned by a hand-computed
      **centre** Y, so the bottom face has to be re-derived by every reader and misses its support by a
      fraction of a metre without anything looking wrong. When adding or editing geometry there, use the
      bottom-referenced helpers in `WorldBuilder.Blueprints.cs` and keep these conventions:
   - `CreatePartBoxOn(root, x, bottomY, z, size, colour)` — never hand-compute a centre from a bottom;
     write the support's top and the block that rests on it on adjacent lines.
   - `CreatePartPanelBetween(root, a, b, halfWidth, thickness, across, colour)` — roofs, ramps and
     stairs are stated as the two ends of their **underside**, never as a tilt sign. A tilt sign hides
     an inverted pitch: the pagoda's four roofs were authored `Euler(+14)` for the +Z panel, which in
     Unity pitches the outer eave *up*, so all four read as a butterfly roof with the centre cap
     floating 0.57–1.44 m above its own panels — invisible in the source, obvious in the world.
    - `CreatePartGableSteps(...)` — the stepped profile that closes a gable triangle; each step's top
      overshoots 6 cm into the roof underside, because **a 6 cm intersection is invisible and a 6 cm
      gap is a slit** — never place two solids flush. Its `gableHalfSpan` (the wall) and
      `roofHalfSpan` (the eave) are separate arguments for a reason: they were one number only because
      the church's wall and eave happened to agree, and one number traces the roof's line on a narrower
      wall.
   - **A derived dimension whose inputs sit on different ladders is clamped in the helper, never at the
      call site.** A gable band's height is `roofUnderside(innerEdge) − wallTop`: the first datum belongs
      to the roof, the second to the wall, and they are only guaranteed ordered while the eave stays
      *above* the wall's top line. The church cleared that by 6 cm, the shrine's 64 cm eave drop does not,
      and the un-clamped helper answered with a **negative height** — a mirrored cube whose BoxCollider
      is inside out. Any helper that subtracts one structure's datum from another's must decide inside
      itself what happens when they cross, and every caller must then be re-checked, because "the fix
      changed the church's band width" means the church was passing the clamp by luck.
    Related invariants for the same structures: all parts of one structure share the site origin
    (author in site coordinates, y = 0 at the platform top) so the assembly is auditable in one frame;
    and **never rename a structure part type** (`Church_*` / `Shrine_*` / `Pagoda_*`) — those strings are
    the save/load keys, and a renamed part falls through `SpawnStructurePart`'s dispatch to the generic
    `else switch` and builds *nothing*, silently. Put new content inside an existing part instead.

10. **A menu's own chrome is not in its panels' coordinate system, and it is drawn LAST.** The
    panels under `MenuPanelBase` share the canvas origin (`Body` is inset 60/60, so the origin is
    the canvas centre) and are built *before* the subclass's own chrome — so a tab bar or band is
    the **later sibling** and Unity draws it **over** the content, with nothing in the content's
    coordinates saying so. A band that no panel mentions is a band whose height is set by nothing:
    the 84-unit Character Info band silently covered the Skills sub-tabs, both Skills readouts, the
    Faith title/status and the top of the Info/Inventory headings. Three habits follow:
    - **Size a band from the rows under it, not from taste.** Read every widget whose extent
      reaches it (`grep` the `P(…, yyy)` literals in the partials), then place the band's *bottom*
      edge above the tallest one and keep the band in a named constant. The band is a *later
      sibling* of the content, so an overlap hides the content; it is never the other way round.
    - **A row's real height is its INK, not its box.** `MakeBodyText` is TopLeft in a fixed box
      (`MakeButton` is pivot-top), so glyphs grow *downward* from the declared y — a title at 238 in
      a 34 box reaches ~200. Two rows that "don't overlap" as boxes can overlap as text; the Faith
      title/status pair did, invisibly, for as long as the band covered it.
    - **Anything that must not be clipped is a mask, and a mask built later wins.**
      `TreeViewport` is a `RectMask2D` added after the Skills header row, so a row that overlaps it
      loses its bottom border — check the mask's edge as if it were solid.
    - A metric that **two code paths must agree on** (a build pass and an aspect-fit pass) is one
      named constant. The band carried `36` in one site and `34` in the other, so it used to change
      position the first time the window was resized.
    - **A field's border is drawn INSIDE the field, and the field's text is inset to clear it.** A
      border hung *outside* the rect is a collision waiting to happen: the class/race rows are 4 units
      apart and the last one is 2 units from the buttons under it, so an outside border lands on the
      neighbour's glyphs. The inset costs the row height it is paid for — a 30-tall row minus 2×4
      leaves 22 for a 22.5pt line, whose descent then crosses the border it just paid for — so the
      row grows to 34 and the font cap is stated against the *inset* box, never the outer one.
    - **Never 9-slice a border that has to serve two different rect shapes.** Unity scales a slice by
      the drawn rect's *own* dimension, so a 2-texel ring is ~2 units on a 64-wide stat field and ~58
      on the 700-wide class row. Four flat `Image` strips of a constant thickness cost five
      GameObjects and are correct at every size; a sliced sprite is correct at exactly one shape.
    - A border strip is a child of the rect it outlines, so **draw order is the caller's problem**:
      create the strips *before* the text child, and give every strip `raycastTarget = false` or it
      silently steals clicks from the input field it is decorating.

11. **Editing the terrain's RENDER algorithm changes nothing that is already on screen, and half the
    world keeps the old geometry until the resident terrain is dropped.** Three holders of render
    output all outlive a code edit:
    - `_loadedChunks` — every `ChunkObject` owns an uploaded `RootMesh`, and nothing re-runs the
      generator for a chunk that is already loaded.
    - `_dormantChunks` — the wake pass re-shows a demoted chunk "in place — the same GameObject,
      same pooled mesh, same tile data", and `EnqueueChunkIfNeeded` *wakes* a dormant chunk rather
      than re-dispatching it. So returning to a region re-activates the mesh that region was built
      with, however old.
    - the far shell — its cells are sampled from the real chunks' surfaces, so they keep the old
      heights as well.
    A mid-session render edit therefore leaves a world where the resident chunks are OLD-algorithm and
    the chunks that stream in later are NEW-algorithm, and the two part along their shared edges.
    Four habits follow:
    - **The drop covers every loaded AND every dormant chunk, plus the far shell.** Both halves
      matter: a dormant chunk you never wake is a landmine that pops the stale mesh back the moment
      the player walks into that region again.
    - **`WorldStreamer.ResetTerrainSaves()` is the right sequence and the wrong tool.** It does
      exactly the right thing in order (`ClearFarShell()` → unload + requeue every loaded and
      dormant chunk) but first calls `ChunkSaveManager.ResetWorldSaves(Seed)`, which **permanently
      deletes the player's terrain edits** — the save files hold *heights*, which are data, not
      render output. Use it only when a pristine world is the actual goal.
    - **Since 1hx the only remedy is a play-session restart** — the non-destructive wrapper
      (`DropResidentTerrainKeepSaves`) and its bench key were removed at the user's request, so a
      renderer change is not observable until every chunk has been rebuilt from scratch. Two things
      that makes worse, both worth saying out loud before editing render code: an inspector tweak to
      `LowPolyStep` / `VoxelTerrainEnabled` mid-session **silently does nothing** (the value is only
      read at dispatch), and a chunk **mid-build** captured its mesh mode at dispatch time, so it
      lands after any unload with the old settings. Treat "restart, then look" as the verification
      procedure, and do not report a render change as verified from a session that was already
      running when the edit landed.
    - **Do not bother clearing `ChunkMeshGenerator`'s mesh pool.** `UploadMerged` re-specifies
      vertices and indices on every upload and `Mesh.Clear()`s whenever the vertex count changed, so
      a pooled `Mesh` is a *buffer*, never a stale cache. Emptying it costs a little and shows
      nothing.
    - **An algorithm edit is not verified by watching chunks stream in.** That only ever exercises
      the new code. Stream one chunk, then walk back and forth across a boundary so a resident chunk
      and a freshly built one are on screen together. (1hx removed the F2/F3/F4 measurement lanes;
      **1hy restored F3** as a read-only rendered-corner + void audit — see the last bullet of rule 7
      — so this is measurable again, but only for the near/far resident set, and F2's cross-chunk seam
      measurement is still gone. F4 was not restored.)
    - **1ia is itself an instance of this rule, and the reason its verification cannot be claimed in
      the same session.** Reverting the render algorithm to pre-1hi is exactly the edit this rule
      describes, so the facet world stays on screen until the next launch no matter how obviously the
      code says otherwise. Do not report the revert as seen working from a session that was already
      running — the *only* honest statement is "restarted, then observed".

12. **The facet size is ONE decision living in two files, and the step must divide three numbers.**
    The near chunks and the far shell render the same facet language, so `WorldStreamer.LowPolyStep`
    and `FarSectorStep` (`WorldStreamer.FarShell.cs`) are the same constant wearing two hats — change
    one and the world reads *inverted*: chunky underfoot, finer at the horizon, with a density break
    at the rim. Two invariants ride on the value:
    - **It must divide 30, 90 AND 180** — the near chunk side and the far cells' span-3 and span-6
      boxes. That set is {1, 2, 3, 5, 6, 10, 15, 30}; anything else leaves the last grid row short
      of a chunk boundary, which is a visible crack along every chunk edge. (A *non*-uniform ladder
      is worse still: the pre-1ej 3/6/9/12/15-by-radius version produced T-junction rows on every
      shared cell edge, read as permanent "thin lines" — which is why `FarSectorStep` ignores its
      `span`/`maxRing` arguments on purpose.)
    - **A coarser step is not free.** The collider rides the same step, so footing gets lumpier, and
      prop heights still sample the 1 m lattice, so props float/sink by up to the facet error. The 1 m
      grid stays canonical for saves and edits either way, but the *edit granularity* is the step:
      a 1 m dig only moves a facet vertex when the edited corner happens to land on the grid.
    1hx moved the default 3 → 6 because 3 m facets sampled this 5-octave field (base octave amplitude
    55 m at frequency 0.0012) came out near-coplanar, and facet shading contrast scales with
    `curvature × span` — so at 3 m the normals barely differed and the world still read as smooth
    haze. The lesson generalises: **"low-poly" is a normal-contrast problem, not a triangle-count
    problem**, so reach for the span before reaching for the shading.
    - **1ia: the facet language is DORMANT (`LowPolyFacets = false`), and `FarSectorStep` is NOT one
      of the gated values.** The flag governs the *flat-facet render path* (flat per-quad far normals,
      the 1ew split at threshold 0, and the 1hi.1 coarse roots) — but `FarSectorStep` returns a
      constant with no reference to the flag, so the far shell samples every 3 m whether or not the
      look is on. That means a "revert the look" change is **two edits, not one**: flipping the flag
      alone leaves the far shell at whatever step it was left on, and the shell's sampling density is
      then describing a surface nobody is drawing. 1ia moved both to 3 together. When you next move
      this step, grep for `FarSectorStep` as well as the field — and remember `NewWorldTestGround`
      mirrors **both** (`EnableLowPolyTerrain`, `LowPolyStep`) and pushes them onto the streamer in
      `Awake` *before* the first stream poll, so a mirror left at the old value silently re-applies
      the look on the test platform every session. The 1i9 `CraterFacetSkirt` is likewise dormant
      (it is gated on `EffectiveLowPolyStep`, which is `0` while the flag is off), which is why the
      1 m-resolution world needs no skirt at all.
    - **Raising the step SILENTLY DELETES every carve narrower than half a facet — check this
      before changing the step, and again after.** The rendered surface holds only every
      `step`-th lattice node, so a deform whose *reach* is smaller than the worst-case distance to
      a sampled node (`step/√2`, and 3 m at step 3 vs 4.24 m at step 6) writes its entire shape
      into nodes no triangle is built from. It does not look wrong, does not error, and leaves the
      data layer perfectly correct — it simply stops existing on screen. 1hx (3 → 6) deleted the
      universal projectile impact dent this way: its 1.9 m reach fell inside the new 4.24 m
      worst case, and the user reported "the dent function is gone now" with the impact sphere
      still playing, which is the tell — **the FX is outside the carve's gate, so a crater that
      vanishes while its effect plays is a resolution problem, not a hit-detection problem.**
      (This bullet only bites while the facet look is ON; at the 1ia default the surface holds every
      1 m node, so a carve under 4.24 m renders normally. Check the flag before diagnosing a
      "missing" carve.)
      Three habits:
      - **Compare a deform's reach to `step/√2`, not to `step` or to the step's value.** The
        bound is the *diagonal* of the sampled cell, because the nearest sampled node can be half
        a step away in BOTH axes.
      - **A carve guarantee must be a `Max` with the authored influence, never a replacement for
        it.** `DeformAt`'s `CraterFacetSkirt` returns 0 unless the carve would move *no* rendered
        node, so it can only ever turn an invisible carve into a visible one — the authored radius,
        depth and per-cast ratchet stay untouched, and a carve that already reaches a sampled node
        keeps exactly its shape. Widening the reach instead would have multiplied the excavation
        rate with it (a Crater ratchets `CraterStep` per cast), turning a "small dent" into a pit.
      - **Widen the loop's WRITE bounds to cover the guarantee, or the skirt targets nodes the loop
        never visits** — a guarantee computed outside the region being written is a guarantee that
        writes nothing.
      - Unfixed consequence of the same coupling: the RAISED shapes (`Wall`/`Ring`/`Pillar`/
        `Spikes`) use the same radii and are subject to the same invisibility, but 1i9 scoped
        itself to `Crater` deliberately. If a raised shape is reported invisible, that is this
        same bug, not a new one — and the fix must keep the shapes' `Max(current, target)`
        idempotency intact.

13. **A spell's colour, shape and halo are derived in exactly one place — and "one place" is not the
    same as "one colour everywhere".** 1ib added `SpellLook.Resolve` so no consumer re-derives a
    spell's identity, which is rule 8's "second spelling that rots" applied to look rather than
    geometry. This codebase had already shipped **two** drifting `DamageType` palettes, so the
    convention is not theoretical. Three habits:
    - **Resolution has exactly three steps, and a named fallback is not a fourth.** authored
      `SpellLookProfile` on the `SpellData` → deterministic pick from the school's family → the
      identity-less `Resolve(DamageType, ProjectileShape)`. That last one exists for callers that
      genuinely have no spell. Anything else needs its own resolution step *stated*, because "just
      resolve it again, differently" is how the second spelling appears.
    - **A field that means gameplay and a field that means drawn must not be merged.**
      `spell.Shape` is homing/large-projectile behaviour; `SpellLook.DisplayShape` is the body the
      player sees. A spell can be a homing missile and still want its school's family body, so
      nothing writes `spell.Shape` from the look. When one value needs to be both, it stops being one
      value.
    - **A swatch is not a readout, and that is the sanctioned exception.** `MagicTestMatrix`'s school
      header keeps its own palette rather than calling `SpellLook.SchoolColor`. 1ib merged them and
      silently recoloured four schools — and the merge destroyed the QA signal, because a header
      tinted the same colour as the thing it labels cannot show you that the thing is mis-coloured.
      **Before deduplicating a colour, ask what would catch the bug if that value were wrong.** If the
      answer is "the same table, because it is tinted to match", the two tables must stay separate and
      the exception gets a comment. Debug-only and save-invisible surfaces need no parity check; a
      building save key does (rule 13's other half).
    - **Asymmetry between two adjacent code paths can be correct, and the comment must say so.**
      1if made the spell-backed release burst look-derived and the *no-spell-armed* burst Arcane-pink
      on the grounds that the two look inconsistent. They are not the same event: pink there asserts
      "an Arcane spell was cast", which is false. When you fix a colour regression, check whether the
      two neighbouring lines actually share an identity before restoring the symmetry — and if they
      do not, say so in a comment or the next reader will "fix" it again.

14. **A removal has three failure modes, and only one of them is a compile error.** Deleting a feature
    leaves behind more than references to the deleted *name*, and the three residue classes fail in
    three different ways — 1hz removed the multiplayer layer, the night club, fast travel and horse
    riding in one pass and hit all three:
    - **Producers outlive their consumer.** Grepping the deleted type's name finds the *call sites*;
      it cannot find code that existed only to **feed** the deleted system, because that code is
      perfectly correct on its own. `FastTravelSign` went away with `FastTravelMenu` and four POI
      files went on building sign GameObjects, colliders and `SignPost` cubes for a list nothing
      would ever read. The grep "passed" — it found exactly the four files that needed deleting and
      reported nothing left over. So for each hit ask **what is this line for**, not *does it name the
      deleted type*; and when a type disappears, list what it was *fed by*, not just what read it.
    - **A save key outlives its builder, and a missing `case` never fails to compile.** A `switch`
      dispatching on a save key is legal with any subset of its cases, so deleting a building's
      builder leaves the key live and silent. `WorldBuilder.Persistence` still listed `"NightClub"`
      in the essential-restore branch, whose body is `RebuildEssentialBuilding(...)` followed by
      `_buildings[_buildings.Count - 1]` — no case, nothing appended, and the club's health, part
      healths and **door state** were written onto the *previous* building. A compile error stops the
      game; this loads a village that looks fine and has one ruined building. This is rule 9's part-key
      concern one level out, and unlike part keys it has **no** parity check in `StaticChecks.ps1` §6.
      When a builder is deleted, grep for its *save key* as well as its type — and verify the fall-through
      is actually inert before relying on it (read `CreateBuildingEntity`/`SpawnBuildingDirect`, don't
      assume a missing definition is a safe skip).
    - **A behavioural orphan is invisible to every grep.** `RichManNPC` kept an entire state machine
      (`ClubHangState`, `ClubIdleState`, pace spots, `HandleClubHangout`, a 19:00–21:00 window) pacing
      the player around a building 1hz deleted. Every symbol resolved, the file compiled, the code
      ran — it was just an NPC walking an empty lot; the 1hz follow-up commit removed it. Removing a
      *place* means re-reading whatever *story* pointed at it, and that is a human read, not a search.
      Two things a removal like this also takes with it that no compile error names: **the breadcrumb
      the behaviour was the player's only lead for** (that hangout's "he is at the bar" toast was the
      only in-game cue for the 21:00 deal, so the deletion silently made a quest step
      undiscoverable — replace it or say so), and **any constant the deleted block shared with
      surviving code** (its `CLUB_WINDOW_END` was a second, independent spelling of the deal's 21:00,
      so removing it left `TryStartDeal`'s bare `TimeOfDay < 21f` as the only remaining hour; both now
      read one `DEAL_HOUR`).
    Two corollaries for the mechanics of the sweep itself:
    - **Deleting a block deletes its locals, and a local is in scope for the whole method.** Removing
      `DungeonSystem`'s sign block took `Vector3 doorDir` with it, but `doorDir` is read 20 lines
      later for the enemy spawn offset. Grep the removed block's *identifiers* for other readers
      instead of trusting that the block looked self-contained — this caught the error in my own edit,
      and it is the same reflex that finds the first bullet's leftovers.
    - **A localized string is a runtime key, so "no reference" needs the dynamic path checked too.**
      14 dead `Localization` keys looked removable from the comment above them, but `Localization.T`
      resolves by string at runtime, so a key is live if *any* code passes that Vietnamese text —
      including the ~60 non-literal `T()` call sites and any `.asset`/`.json`. Use exact
      (`-SimpleMatch`) matching, not a regex: a `.`-wildcarded Vietnamese pattern returned 50 008
      "matches" against a mangled console, which is worse than no search because it looks like a
      result. Confirm a reference-counting grep can find something real before trusting a zero.


