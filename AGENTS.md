# Project Rules

> **Split (task 1kd):** the long case studies behind rules 3, 7, 8, 12, 13, 14 and 17 now live
> in `LESSONS.md` under `## §N` headings - open one on demand. This file keeps every rule and
> its habits and is authoritative; where the two disagree, this file wins.

1. **Always commit and push after every task.** Commit on Git `main` and push to
   `https://github.com/supergigachadsupreme300/new-world` (PowerShell; e.g.
   `git add -A; git commit -m "..." ; git push origin main`). Make a new commit for follow-up
   fixes rather than amending. Do not commit unless a task is complete.

## Token cost minimization

- Read small: use `Read` with `offset`/`limit` and read only what's needed (not whole files). Prefer `glob` + targeted `grep` over dumping files.
- Summarize tool outputs: don't echo full stdout in your answer; report only hits, counts, or a short one-line summary (e.g. "3 hits in X.cs").
- Batch: combine related commands in one bash call; avoid repeated scans of the same tree.
- Don't print raw diffs verbatim in text unless necessary to show a concrete failure; state the diff size/summary instead.
- Prefer targeted searches: include `path`/`include` and search for specific symbols, not broad regex across all files repeatedly.
- Avoid long explanations in messages: keep responses < 4 lines unless user explicitly asks for detail.
- Don't dump generated artifacts (e.g. TREE.md full text) - summarize what changed.
- Skip printing byte dumps, hex walks, or full context for non-failing checks.
- The long case studies behind rules 3/7/8/12/13/14/17 live in `LESSONS.md` - read a `§N` section on demand.

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
   **An API that you are using for the FIRST time in this codebase is an unverified assumption, and the
   project's own existing usage is the reference to check it against.** Grep the tree for an existing
   use and copy that exact overload shape (`ChunkMeshGenerator`'s mesh upload — `SetVertices(array)` /
   `SetColors` / `SetTriangles(array, 0)` / `bounds` by hand, no flags — is the proven pattern here);
   if there is **no** existing use, that zero is itself the finding, because the risk is unbounded
   where no local precedent exists, so prefer the plainest overload the codebase demonstrates. When
   you trade a bounded write for an unbounded one, ask what the leftovers now point at; a conversion
   existing is not this CONSTRUCTOR accepting those argument types (keep colours as `Color` end to
   end, which keeps every uploader on the `List<Color>` overload the rest of the project uses).
   **After the second console error in one file, sweep for a THIRD rather than patching again.**
   **Run `powershell -ExecutionPolicy Bypass -File tools\StaticChecks.ps1` before committing any
   change to `WorldBuilder*.cs` or `NewWorldTestGround.cs`** — it mechanises balance, overload-aware
   arity, void-return, unassigned locals, cross-case locals, part-key parity. It reports *candidates*,
   not verdicts: 0 candidates still means "not compiled", so paste Unity's output into the handoff
   rather than fixing errors silently. If you add a file in those areas, add it to the script's
   `$files` list or checks 1–5 stop covering it. **Balance is not reachability** — the script has no
   declaration-order check, so a clean run says nothing about use-before-declaration; reread for that
   class yourself, and do not "fix" it with a naive use-before-declare scanner (one that fires on every
   legitimate field read trains the next reader to ignore it, which is the false-positive failure the
   case studies record).
   **Case studies:** LESSONS.md §3.

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
   Conventions that make a *positive* result mean something:
   - **Order the sections by whether their PREMISE holds**, not by how much they look like a test —
     the build fingerprint runs first, because a static proof about a generator is void under rule
     11's mixed resident set.
   - **Scope the walk to the band where a positive result is *possible*, print the band, and name
     which OWNER(S) the walk admits**; if the report is about a seam, check the walk contains both
     sides of it. Never re-derive another component's private formula to define that scope — find the
     invariant both sides already agree on. A correct scope for the wrong *family of owner* still
     reports "clean" confidently, and "drawn" and "flush" are separate properties.
   - **Ask coverage questions directly** (enumerate every owner that can draw, take the first live
     one) instead of inferring them from an ownership predicate; split the remainder into
     *claimed-dead* (an owner exists, nothing draws: a hole) and *unowned* (a shorter horizon) with
     separate verdicts; when a number is retracted, its replacement is *unknown*, not zero.
   - **Gate a classifier on the WIDTH of its own test:** any threshold comparison used to *enter* a
     classification must be at least the sum of the tolerances used *inside* it, or arithmetic noise
     gets a mechanism's name.
   - **A check is only evidence if you have seen it fail on the thing it is for — and it must also be
     unable to PASS for the wrong reason.** Verify a comparator's addresses (both range ends exist in
     *both* files; prefer a marker that occurs *once*; two declarations of one name are two
     addresses), prove it can fail (mutate a line and watch it fire), and re-read the *extracted*
     text against the source — a green check nobody has seen fail is not a check, and a false green
     is worse than silence because it carries a claim.
   - **Baseline before interpreting an absolute count:** `git show HEAD:<path>` first and read the
     DELTA, not the absolute. For an instrument that strips strings and comments, baseline *both*
     versions with its own stripper — when a crutch and the real instrument disagree, the crutch is a
     second, uncalibrated instrument, not a second opinion. Never edit pre-existing content to appease
     a counter that was never measuring it; locate the imbalance, record it, move on.
   - **A scan that reports ABSENCES must first be shown able to report PRESENCES.** Build the file
     list once, explicitly and recursively, and sanity-check it against a total (a PowerShell `-Path`
     glob descends exactly one level; `-Recurse -Include` does not filter unless the path ends in
     `\*`); strip `//`, `/* */` and XML doc blocks before member sweeps; print a known-zero control
     beside every count; and count strings with `([regex]::Matches($text,[regex]::Escape($g))).Count`,
     never `String.Split` on a multi-char needle (which counts hex digits and can exceed the file's
     own size).
   - **Before filing an "unresolved member" candidate, eliminate three false positives:** a reference
     inside a comment, an inherited member with no declaration in the owner's own files, and a nested
     type declared with its brace on the next line. And a sweep reporting a suspiciously round zero
     must be re-run with a value that must be non-zero — a broken instrument's false zero is
     indistinguishable in the output from a clean sweep.
   - **The thread decides whether an idiom is safe:** a main-thread read does not transfer to a
     ThreadPool path, and "strictly better" is an argument that must name its thread.
   - **"No references found" is a claim about the PATTERNS YOU TYPED, and a key has three
     spellings:** search the concept across every spelling its API offers (`Keyboard.current.f1Key`,
     `Keyboard.current[Key.F1]`, or a wrapper field), and record *who* takes each key when you
     document one as free, or the claim outlives the task that made it. (Check 8 mechanises this for
     QA lane keys.)
   - **"Not used" is a claim about a CALL GRAPH:** grep the surprising callers (a folder describes
     where a class was written, not who depends on it), separate "unused by the game" from "unused by
     the legacy layer", ask *which member* is unused rather than whether the class is, and confirm
     nothing addresses a moved file **by path** (`AssetDatabase.LoadAssetAtPath`, an `.asmdef`, an
     editor tool) before calling a move free.
   - **"I can't find X" is a claim about NAMES, not about existence:** grep proves the code is there,
     nothing proves it is *findable* — ask what a reader would have typed before concluding a feature
     is absent, treat a contiguous block of methods as a latent file, and measure what each file
     actually is before naming a destination.
   - **Units and windows decide what 0 means:** in a time-valued column, 0 means "no sample" and must
     print `n/a`; in a count-valued column, 0 is a real observation. A verdict's denominator must mean
     the same thing as its numerator — under a quantised (vsync) frame, compare the two *sides* or
     report a bracket, label the residual per regime, and never let a per-window counter sit next to
     a since-boot one without saying which window each uses.
   - **An audit must ask the question the PROPOSAL is about**, check its own premise before
     classifying (membership and the value measured may legitimately use different references), and
     report a contradiction between two sections as `UNKNOWN` — never as a number.
   - **A one-frame lag is a WRITE ORDER:** name the phase of every writer before trusting a reader
     (`Update` vs `LateUpdate` — a stale read only shows while the value is changing fast), prefer
     the look SOURCE over the thing that renders it when they hold the same value, fix the shared
     function when two consumers must agree, treat one reported instance as one instance (~35 aim
     sites share the same offset — report them, do not silently rewrite all 35), and grep the *docs*
     for every value you change.
   - **A procedural cycle's rate is `speed / stride`** (the no-foot-slide invariant). A clamp absorbs
     bad input but an integrator *remembers* it, so low-pass the input, discard impossible samples,
     and seed trackers in `OnEnable`. Quantify where a clamp first binds and record it in the field's
     tooltip; a tuning knob has a partner knob that moves the opposite way (stride vs max cadence =
     skate vs legibility); and never answer a second "also slow it" with the same edit and no new
     number.
   - **Navigation maps are generated, never hand-maintained** (`tools/Write-Tree.ps1` → `TREE.md`,
     re-derivable and self-reporting); never write a line number in a doc, and never re-derive a
     private formula or dispatch table into one.
   **Case studies:** LESSONS.md §7.

8. **A seam invariant is only as good as the arithmetic of the COPY, not the source.** When a value is
   copied out of another structure instead of re-derived (a corner lattice copying a tile's stored
   vertex, a cache copying a source array, a lookup indexing by another system's key), the addressing
   rule of that copy IS the seam contract. Check its index/owner arithmetic with a concrete worked
   example at a boundary node (the 4-chunk corner catches what mid-edge checks miss), and never let a
   validator stand in for a layer it does not read — `ChunkValidator` compares tile heights tile-vs-tile
   and is structurally blind to a lattice bug, so a green validator is not evidence about the lattice.
   - **Two spellings of "a corner's height" exist here, and they disagree by half a metre:** corners sit
     *at* integer world coords, so pristine-at-a-corner is `GetHeight(Seed, cx, cz)`, while
     `CurrentHeightOf` samples `(cx + 0.5f, cz + 0.5f)`. Both are correct **for their own use** —
     **read the code that WRITES the value before reusing the one that READS it**, and when a
     measurement compares a current height against a pristine reference, assert the two are sampled at
     the *same* point, or the mismatch reads as a phantom dig no threshold will ever clear.
   - **A comment that NAMES an identifier is a copy of that identifier, and it rots like any other
     copy.** When an edit introduces or renames a symbol, grep the new comment for the symbol it names
     and confirm the declaration exists; when it cites a doc section, confirm the heading is still
     where it was. **Re-read the guard, not the comment,** before trusting what an early return does;
     a defensive duplicate is a cost and must be justified in the present tense; and a count of
     geometry must report 0 for degenerate input. A comment documenting a path that is not taken is
     worse than no comment: the next reader sizes a decision on it.
   - **State which inputs are on different ladders,** or the derived number is silently a different
     number at every call site: name where each input's ladder starts before concluding a guard is
     live, and if it cannot be reached, say so in the comment instead of leaving it to look
     load-bearing.
   - **A task id is a copy of a fact about the repo's history, and the copy is written LAST — so
     "grep before you edit" cannot cover it.** Assign the id by enumerating `PROGRESS.md`'s `^## 1..`
     headings first; if an id has already been pushed out of order, leave it and say so in
     `PROGRESS.md` — rule 1 forbids amending, so renumbering is not on the table.
   - **A navigation map is a copy of the codebase, and it is the one file nothing in this repo
     checks:** grep every identifier the doc names *after* writing it — a name can exist and still be
     filed under the wrong owner, which is the more expensive error because it sends the reader
     somewhere plausible. Never write a line number (it dies on the next edit above it), and never
     re-derive a private formula or dispatch table into a doc.
   **Case studies:** LESSONS.md §8.

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
      boxes. The valid set is **{1, 2, 3, 5, 6, 10, 15, 30}**; anything else leaves the last grid row
      short of a chunk boundary, a visible crack along every chunk edge. A *non-uniform* ladder is
      worse still (T-junction rows on every shared cell edge, read as permanent "thin lines" — which
      is why `FarSectorStep` ignores its `span`/`maxRing` arguments on purpose).
    - **A coarser step is not free:** the collider rides the same step, so footing gets lumpier; prop
      heights still sample the 1 m lattice, so props float/sink by up to the facet error; and the
      *edit granularity* becomes the step (a 1 m dig only moves a facet vertex when the edited corner
      lands on the grid). The 1 m grid stays canonical for saves and edits either way.
    - **"Low-poly" is a normal-contrast problem, not a triangle-count problem:** facet shading contrast
      scales with `curvature × span`, so reach for the span before reaching for the shading.
    - **The facet language is DORMANT (`LowPolyFacets = false`), and `FarSectorStep` is NOT one of
      the gated values** — so a "revert the look" change is **two edits, not one**: the flag governs
      the flat-facet render path while the shell keeps sampling at whatever step it was left on, and
      `NewWorldTestGround` mirrors BOTH (`EnableLowPolyTerrain`, `LowPolyStep`) and pushes them onto
      the streamer in `Awake` *before* the first stream poll, so a stale mirror silently re-applies
      the look on the test platform every session. `CraterFacetSkirt` is likewise dormant (gated on
      `EffectiveLowPolyStep`, which is 0 while the flag is off). When you next move the step, grep
      `FarSectorStep` as well as the field.
    - **Raising the step SILENTLY DELETES every carve narrower than `step/√2`** — the bound is the
      *diagonal* of the sampled cell (worst case `step/√2`: 4.24 m at step 6 vs 3 m at step 3), the
      data layer stays perfectly correct, and it leaves no error: the tell is a crater that vanishes
      while its impact FX still plays, because the FX sits outside the carve's gate — a resolution
      problem, not a hit-detection problem. The fix is a `Max` with the authored influence (never a
      replacement — a Crater ratchets `CraterStep` per cast, so widening the reach would multiply the
      excavation rate), and the loop's WRITE bounds must cover the guarantee or the skirt targets
      nodes the loop never visits. The RAISED shapes (`Wall`/`Ring`/`Pillar`/`Spikes`) share the same
      radii and the same invisibility — that is this same bug, not a new one.
    - **A second surface for the same ground is a resolution bug before it is a performance win** (the
      LOD band was deleted outright). Wherever a *stride* is introduced: **coarse is not stale** —
      decide whether a value is wrong or absent before reaching for a refresh; **a feature change can
      expose a dormant defect** — suspect the coupling it made legible rather than the shape, and say
      so in the handoff; **a derived gate is a mitigation, never a licence** — if a gate exists only
      to keep a second surface honest, delete the surface; **adaptive refinement must be uniform**
      (whole-chunk), because partial refinement is a T-junction crack machine with no compiler to
      catch it (rule 3); and if you are reasoning about a chunk's stride from its DISTANCE you are
      looking at the deleted design — the only distance-driven question left is the cull (§2.5,
      `ChunkDistanceCull`).
    - **Promoting a rarely-used path to the only path promotes its unexercised assumptions.** Check
      whether the value is serialized (a field initializer is a *default* that loses to whatever the
      scene stored — grep the live scene and confirm the runtime path); read the optional branch for
      traps that only pay off when it is on; do not encode an unmeasured hypothesis in a comment;
      grep for a second owner of the concept before assuming one path; write the acceptance read
      both ways so it is a verdict rather than an impression; make the play-test say which claim it
      implicates; and treat a **framing** request and a **view** request as different tasks — ask.
    - **Camera lifetime rules from the same cluster:** a framing offset must move the LOOK-AT too (one
      `lookTarget` for both the desired position and the rotation — offset the position alone and a
      re-centring camera shows zero change; an offset that also bends a collision cast is a new
      interaction, so record it as a play-test item); a cached value is only valid along the AXIS it
      was measured on (cache the query's *inputs* or invalidate on the delta — a time-based cache is
      a rate-vs-correctness trade dressed as a perf win); a SMOOTHED position must never feed a
      look-at (the smoother is a steering input — derive from the unsmoothed target and prove it is a
      no-op at rest; steady-state lag is `v * SmoothTime`, so quote the number, not "a bit"); an aim
      derived from a POINT in front of the camera is coupled to where the camera IS (prefer a camera
      *direction* for anything that flies — no amount of first-person testing clears this class); a
      "does X still happen" check must name the OTHER change that could have caused it; and a
      derivation that disproves an existing comment is part of the fix, not a footnote.
    - **The COLLIDER is the ground, and it is a separate decision from the render step.** `ColliderStep`
      is `_meshStep > 0 ? _meshStep : ChunkColliderDecimation` (1 m since 1ex). **Never refine the
      render surface while leaving the collider coarse** — that widens the mismatch; the pairing
      (render step : collider step) is the invariant, so a "finer mesh" change that does not touch the
      collider is a visual-only change and must be stated as one. **A collider-step change owes a
      number on F2:** `ColliderRingRadius = 7` Chebyshev = 225 bodies × `(30/step + 1)²` verts ⇒ 1 m
      = 961 verts / 1800 tris ≈ **405k** ring triangles (2 m = 256 / 450 ≈ 101k) — never carry a
      stale figure forward; three comments claiming "~7k-tri" were wrong at *every* step. **The
      collider lattice is horizontal quads only — there is no vertical strip pass,** so a vertical
      step is sampled as a ramp whose slope is step-dependent: 1 m ⇒ 45°, exactly the default
      `CharacterController.slopeLimit` this project never assigns — cliff traversal is a play-test
      item on any collider-step change.
    **Case studies:** LESSONS.md §12.

13. **A spell's colour, shape and halo are derived in exactly one place — and "one place" is not the
    same as "one colour everywhere".** `SpellLook.Resolve` exists so no consumer re-derives a spell's
    identity, and the codebase has already shipped two drifting `DamageType` palettes. Three habits:
    - **Resolution has exactly three steps, and a named fallback is not a fourth:** the authored
      `SpellLookProfile` on the `SpellData` → a deterministic pick from the school's family → the
      identity-less `Resolve(DamageType, ProjectileShape)`. Anything else needs its own resolution
      step *stated*, because "just resolve it again, differently" is how the second spelling appears.
    - **A field that means gameplay and a field that means drawn must not be merged.** `spell.Shape`
      is homing/large-projectile behaviour; `SpellLook.DisplayShape` is the body the player sees. When
      one value needs to be both, it stops being one value — and nothing writes one from the other.
    - **A swatch is not a readout, and that is the sanctioned exception** (`MagicTestMatrix`'s school
      header keeps its own palette). **Before deduplicating a colour, ask what would catch the bug if
      that value were wrong**; if the answer is "the same table, because it is tinted to match", the
      two tables must stay separate, debug-only and save-invisible surfaces need no parity check, but
      a building save key does.
    - **Asymmetry between two adjacent code paths can be correct** — check whether the two lines
      actually share an identity before restoring symmetry; if they do not, say so in a comment or the
      next reader will "fix" it again. And when a skill (rule 15) conflicts with "derived in one
      place", state which side won.
    - **A held HUE is not a held PERCEIVED hue.** `Color.Lerp(c, white, t)` is monotone and preserves
      each channel's ORDER, so "does Dark stay violet" is answerable by comparing two subtractions;
      `RGBToHSV -> clamp -> HSVToRGB` is not, because chroma is `s * v` — raising `v` on a
      low-saturation school raises apparent chroma and the perceived hue drifts while `h` sits
      untouched (a hand-checked Earth boost turned it into a saturated orange, i.e. *Fire*). **Before
      writing a colour formula, ask which schools are LOW-chroma** (`Earth`, `Holy` — saturated
      primaries survive almost anything), check the extremes rather than the average, and prefer an
      operation you can redo in your head. **A request about one drawn surface is not permission to
      restyle the palette it comes from** — add a NEW member on the look struct with the override in
      its own resolver — **then prove which side landed** (trail changed, body unchanged). A colour a
      look layer derives must be derived FROM the palette (`TrailColor` reads `SchoolColor`), never
      spelled beside it as a second independently drifting table.
    - **A "does it still work" check must name what ELSE the edit could have broken** — here the
      projectile *body*, the adjacent `SpellLook` member, so the play-test reads both ends.
    - **A new visual axis needs the resolution pattern AND a way to SEE it:** ask whether the axis is
      a CHOICE BETWEEN EQUALLY-VALID LOOKS (a school array + deterministic `Pick()`) or a STATEMENT
      ABOUT THE SPELL (structural axes like `SkyRockStyle`/`DisplayShape` — only an authored profile
      may grant them, `Inherit` resolving to one named default); before adding a value, check that
      every existing readout can render it (a visual that exists only inside a live cast has no
      acceptance readout); and an axis added to a packed identity key cannot *lower* the distinct
      count (it can only split groups), so say out loud when the numbers should not have moved.
    **Case studies:** LESSONS.md §13.

14. **A removal has three failure modes, and only one of them is a compile error.** Deleting a feature
    leaves behind more than references to the deleted *name*:
    - **Producers outlive their consumer.** Grep cannot find code that existed only to **feed** the
      deleted system — it is perfectly correct on its own. For each hit ask **what is this line for**,
      not *does it name the deleted type*, and when a type disappears, list what it was *fed by*,
      not just what read it.
    - **A save key outlives its builder, and a missing `case` never fails to compile.** A `switch`
      on a save key is legal with any subset of its cases, so deleting a builder leaves the key live
      and silent — and a fall-through body can write the data onto the *previous* record, loading a
      world that looks fine with one entity corrupted. Grep the *save key* as well as the type, and
      verify the fall-through is actually inert before relying on it. (Rule 9's part-key concern one
      level out — and unlike part keys it has **no** parity check in `StaticChecks.ps1` §6.)
    - **A behavioural orphan is invisible to every grep.** Every symbol resolves, the code compiles,
      it is just behaviour pointing at something that no longer exists. Removing a *place* means
      re-reading whatever *story* pointed at it — a human read — and two things go with it that no
      compile error names: the breadcrumb the behaviour was the player's only lead for (replace it or
      say so), and any constant the deleted block shared with surviving code (a second, independent
      spelling of the same deadline, so removing it leaves one bare literal as the only remaining one).
    Two corollaries for the sweep itself:
    - **Deleting a block deletes its locals, and a local is in scope for the whole method** — grep the
      removed block's *identifiers* for other readers instead of trusting that it looked
      self-contained.
    - **A localized string is a runtime key, so "no reference" needs the dynamic path checked too:**
      `Localization.T` resolves by string at runtime, so a key is live if *any* code passes that
      text, including non-literal call sites and `.asset`/`.json`. Use exact (`-SimpleMatch`)
      matching, never a regex (`.`-wildcards Vietnamese into tens of thousands of false "matches",
      which is worse than no search), and confirm your counter can find something real first.
    - **Ask what CREATES the thing you are keeping, not only what reads it.** A surviving type whose
      only `new`/`AddComponent`/factory is being deleted is a cascade decision, not a cleanup — walk
      *down* the graph too, audit what you deleted, and **report** the unaudited sub-tree instead of
      following it.
    - **A container type carries an ADDRESSING CONTRACT, and swapping it to match a signature
      transfers that contract silently.** A fix can be correct as a COMPILE fix and still be wrong at
      RUNTIME (`List<T>`'s Capacity is not its Count; the indexer rejects `i >= Count`, so an array's
      "capacity = last valid index + 1" habit throws on first write). Sweep the NEW axis when an
      error arrives from a direction the previous sweep did not look in, and remember **"reserved" is
      not "present"** — slots nobody reads still have to exist.
    - **An enum or field with no reader can still be a doc's promise.** Deleting it orphans the
      DOCUMENT — when a removal removes a name that appears in a design doc as an implemented field,
      fix the doc in the same pass (rule 2), and check the section number a comment cites still holds
      that heading.
    - **A public event with zero subscribers is not a defect.** Stripping the event and its
      invocations IS a behavioural edit to a live file riding along in a "delete dead code" diff —
      leave it and record the state, or say explicitly which of the two you chose.
    **Case studies:** LESSONS.md §14.

15. **The globally installed skill set is a tool, and reaching for it is not optional.** Every session
    runs with skills available outside this repo (Blender, Maya, ZBrush, Unreal, Unity, asset and
    pipeline skills, plus `skill-creator` for authoring new ones). Rule 2 lists the docs you must
    update; it does not list this. **Before starting a task, check whether a skill covers it, and load
    it if one does** — the Skill tool costs one call, and re-deriving a domain workflow from scratch is
    both slower and more likely to be wrong than following the shipped one.
    - **Silence is not a verdict.** 1ib–1ij shipped in three commits without a single skill loaded, and
      the omission only surfaced when the user asked afterwards. Not loading a skill is a *decision*;
      it has to be made deliberately and **stated in the handoff**, the same way rule 3's "no build" is.
      A future reader cannot tell "I checked and none applied" from "I forgot" — the two are identical
      in the transcript. Write one line: `skills: none applied — <reason>`, or name what was loaded and
      what it changed about the approach.
    - **A skill that contradicts this project's rules loses to this project's rules, and the conflict
      gets stated.** The Unity skills are the obvious case: `scenario-unity-expert` and its siblings
      target driving a running editor over MCP or in `-batchmode`, while rule 3 says **no Unity build or
      CLI run happens in this project**. Loading one is still allowed — it may carry real API and
      architecture knowledge — but it cannot *verify* anything here, and the difference between
      "informative" and "authoritative" is exactly what rule 11 is about for render changes. Say which
      of the two it was.
    - **Match the skill to the artifact, not to the topic.** Most of the installed set is DCC-side (Maya,
      Blender, ZBrush, Unreal, image/video/audio generation). A task that merely *mentions* Unity, or
      the phrase "a skill", is not a match. Ask what artifact the task produces — a `.blend`, a `.ma`
      scene, an Unreal `.uasset`, a C# edit reviewed by a human — and load only when the skill governs
      that artifact. The failure this prevents is loading a heavyweight 3D-DCC workflow to edit 12 lines
      of `PlayerController.Combat.cs`: a near-miss skill burns context and contributes nothing.
    - **The repo's own tooling is not a substitute, and does not excuse skipping the set.**
      `tools/StaticChecks.ps1` (rule 3) is mandatory on its own terms, but it checks *this codebase's*
      invariants — brace/paren balance, overload arity, CS0165 candidates, part-key parity,
      member-at-depth-0. It is not a general engineering skill, and "the project has a script" is not a
      reason to skip the skill set.
    - **When a skill's guidance is what you actually followed, name it in `PROGRESS.md`** so the next
      session can reproduce the reasoning instead of re-deriving whether it was followed at all.

16. **An animation's rest pose is a fact about the rig, so it is AUTHORED - never sampled back out of
    a frame the animation itself is writing.** `WeaponAnimator` writes every animated frame as
    `_baseEuler + accent` and re-derived `_baseEuler` from the live transform at the start of each
    phase. Those two facts compose into a silent integrator: a phase that begins while the previous
    phase's pose is still applied captures that offset as its new rest, and because `End`/`StopSway`/
    `AbandonSway` all restore **to** the base, nothing unwinds it. 1im shipped this as a magic weapon
    that slowly rolled a few degrees further with every cast until it was permanently sideways. Three
    habits:
    - **A capture that reads live output is an accumulator wearing a hat.** Ask what the frame looks
      like at the instant of the capture, not at the start of the phase. If the answer is "mid-pose",
      the capture is wrong for every phase after the first.
    - **The tell is an ASYMMETRY in the data, not a magnitude.** Nothing about "the animation changes
      the rotation" is alarming; a 14° accent on a 30° base reads fine. The finding was that *only
      magic* drifted, and the reason turned out to be structural (the only defs with a **rotation**
      accent — staff 14°, book 16°, wand 10°, orb 30°; lute is scale-only, and every melee/ranged/shield
      def is `K_None` or a no-op). When a per-instance bug shows up on one category only, diff that
      category's data against the others before theorising about timing — here it named the mechanism
      in one step. This is rule 13's "ask what would catch the bug if that value were wrong" applied to
      animation.
    - **Restoring *to* the base cannot unwind a polluted base — so state the rest once, separately.**
      `SyncRestFromIdle` (write, only while no phase owns the transform) and `RestoreAuthoredRest`
      (read) are the pair. Keeping them as two named methods is what stops the next phase entry from
      re-introducing the sample, and it makes an already-drifted session self-heal on the next cast
      instead of needing a restart. Note the guard's condition — "no phase owns the transform" — is
      also what keeps re-parenting correct: a re-parent rewrites the local pose, so the rest must be
      re-authorable, and an idle rig is exactly when that is safe.

17. **A move is the one edit whose diff proves nothing, and it rots the docs that name what moved.**
    Three habits, each from a shipped failure:
    - **Diff the moved body against the pre-move source** (comment- and whitespace-normalised) and
      **prove the comparator can fail** by mutating one line and watching it report the change —
      with no compiler (rule 3), the comparator IS the compiler. A line can be textually IDENTICAL in
      both places and still have to change (`Destroy(col)` is correct inside a `MonoBehaviour` class
      and stops resolving inside a `static class`, where it needs `Object.Destroy(col)`), and a
      brace-balanced extraction can silently drop or duplicate a whole method.
    - **A doc that records OWNERSHIP goes stale invisibly, because grep still finds the symbol** —
      after any move, grep the **qualified** owner string (`SpellCaster.CreateProjectileDisplay`),
      never only the bare name: a bare-name grep passes on stale ownership *by construction*. (The
      same failure shipped a `Magic/README.md` pointing at the pre-move file for a whole task.) And
      when a helper's output crosses a class boundary, **return one record per piece** (transform +
      material + spin flag together), never parallel lists a fade loop in another class must index in
      lockstep.
    - **A `partial` -> standalone-class split orphans every unqualified sibling reference, and an
      R100 rename diff is structurally blind to it** — git records the file byte-identical while 86
      bare `MakePart(...)` calls silently become CS0103, because a `partial` resolves bare names
      against its siblings. So: a rename that changes the class declaration is a new class — sweep
      **all** bare identifiers resolving to the former host (the console prints what you look at, not
      what is broken); the sweep's unit is the moved class's whole public surface with *every* caller
      as denominator (count files *touched* in the diff vs files whose `Build` lines changed — a
      touched file with no changed line is a gap); qualify with the former host and never add a
      forwarder when the host is `Legacy/` (rule 18); and check the modifier before editing — a
      `partial` can see its host's `private` members and a standalone class cannot, so qualifying
      would trade CS0103 for CS0122. Three things fake a finding in that sweep: PowerShell's
      `-contains` is **case-insensitive** (a local `headScale` "matched" `HeadScale`), a top-level
      type in the **global namespace** resolves unprefixed from anywhere (not a member of that
      class), and a type with a base class gets its inherited members from outside the owner's files.
    **Case studies:** LESSONS.md §17.

18. **The old game's code is READ-ONLY and quarantined in `Assets\Scripts\Legacy\`, because this is a
    different game that happens to share a repository.** 1jc quarantined 32 files - the eleven
    `CutsceneManager*` files (the base plus **8** `Ending*` partials, `Driving`, `Helpers`), the twelve
    `WorldBuilder.*` partials, the **nine** `MapBuilder.*` partials -
    under `Assets/Scripts/Legacy/{Cutscenes,WorldBuilder,MapBuilder}/`, with a `README.md` in the folder.
    (Counted per folder by 1jk, because the wrong version of this sentence was *stable*: it said "the ten
    `CutsceneManager.*` endings" and "the ten `MapBuilder.*` partials", and **the two errors cancelled -
    10+12+10 and 11+12+9 are both 32**, so anyone who checked the total saw it agree. It was also wrong at
    its own origin commit `aae400b`, so this was never rot, it was a bad count. Only 8 of the 11 cutscene
    files are endings.)
    Rule 4 already said the legacy world is not the test surface; this says it is not the *subject* either.
    Four habits, each from a way the quarantine is easy to defeat by accident:
    - **Read-only means read-only, in both directions.** No edit, rename, move, delete, or drive-by comment
      fix inside `Legacy/` - including "just a typo" and "just a comment that names a moved file". Live code
      calling INTO legacy is normal and fine (`GameManager`, `ToolManager`, `GoblinPet`, `PetController` and
      `ChunkObject` all do); the forbidden direction is legacy gaining a NEW dependency on live code, which
      is how old-game behaviour starts failing because new code changed. If a task needs a legacy behaviour,
      implement it as a NEW class outside `Legacy/` and leave the old one exactly as it is - rule 17's
      comparator discipline applies to anything you copy out of there.
    - **Deleting legacy is its own task, and it needs rule 14's save-key sweep, not a grep for the type
      name.** `WorldBuilder.Persistence` writes `"NightClub"`-shaped keys and part keys
      (`Church_*` / `Shrine_*` / `Pagoda_*`) that `SpawnStructurePart` dispatches on; a missing `case`
      builds nothing and loads a village that looks fine. Nothing here is going to delete anything, so this
      is a guard for the day someone is asked to.
    - **A quarantine MOVES the paths, and every tool and doc that names a path moves with it.** The move
      left `tools\StaticChecks.ps1` - rule 3's only instrument - pointing at four pre-move
      `Assets\Scripts\World\WorldBuilder*.cs` paths, so the whole script died on `Resolve-Path` before
      check 1 printed anything. It was not reported by any check; the only way it surfaced was running the
      script. So **after moving anything, grep for the OLD PATH string across `Assets`, `tools` and the root
      `*.md` files**, and re-run rule 3's script - a green instrument from before the move is not a green
      instrument after it.
    - **New code never goes in `Legacy/`, and a legacy symbol never gets resurrected outside it.** The
      folder is a one-way door: `MapBuilder.BuildTree` is still called by `ChunkObject`, so the streamed
      terrain draws through quarantined code, and that is exactly why the temptation to "just fix it there"
      is strong. It stays a fence, not a shared workbench.
    - **There is exactly ONE authorised exception on record, and it is recorded here so a reader does not
      file it as a violation.** 1jj changed 3 lines in `Legacy/Cutscenes/CutsceneManager.Driving.cs`
      (`MapBuilder.BuildSeatedPlayerModel` -> `PlayerModelBuilder.BuildSeatedPlayerModel`), on the
      owner's explicit authorisation, because those lines were CS0117 and the project could not compile.
      Why it is not a new dependency: `75fd44d` - the commit that moved the class out of `MapBuilder` in the
      first place - had already rewritten **8 of the 9** `CutsceneManager` files in that same folder to
      call `PlayerModelBuilder.BuildPlayerModel` and missed only `Driving.cs`. The fence was crossed nine
      times by the same commit family; this completes a sweep that was already half-done rather than
      choosing a direction. Three rules the exception does **not** grant: it is not a precedent for editing
      Legacy again, it does not license the `MapBuilder` forwarder that rule 17's 1ji bullet forbids, and a
      Legacy edit that is *not* a compile fix still needs the same authorisation.
    Note what the quarantine is NOT: a claim that legacy is unreferenced. 1jc measured it - the legacy types
    are the *hub* the new systems hang off (23 live files name `WorldBuilder`, 15 name `MapBuilder`, 11 name
    `CutsceneManager`), and `Assets/Scenes/SampleScene.unity` - the only scene in `EditorBuildSettings` -
    still carries a `WorldBuilder` and a `CutsceneManager` component, so the old game still boots. Untouched
    has to keep meaning *still runs*; a play-test item, not a claim.
    What the quarantine is verified by: grep + reread + `tools\StaticChecks.ps1` -> 0 candidates. One more
    instrument detail worth keeping: checks 2/3/6 still READ `Legacy\WorldBuilder\WorldBuilder*.cs`, because
    the part-key parity check (rule 9) is the only guard over a contract that silently builds nothing when
    broken - and **a finding inside a `Legacy` file is a report of a rule-17 violation, not a fix queue.**

19. **A per-frame effect belongs in the loop that runs while it MOVES, not in the shape factory - and
    the reason is the factory's other callers, which are all static.** 1jg added an in-flight voxel trail
    (**1jq replaced it** with `TrailStrip`, still in `Assets\Scripts\Magic\Fx\`, a single camera-facing
    quad strip; the placement rule below is unchanged and is what made the rewrite a drop-in) and the obvious home was
    `MagicProjectileModelBuilder.AttachDefaultProjectileVisual`, where every projectile body is born. It has **three** callers and only one is a projectile in flight: the other
    two are `NewWorldTestGround`'s spell band (every castable spell drawn as a **motionless pedestal**)
    and `SpellCaster.DecorateProjectile` (a summoned turret's bolt, which never flies). A trail emitted
    there is a row of cubes hanging in mid-air on a pedestal that never moves - the exact artifact 1gb
    was filed for, rebuilt somewhere new. Two habits:
    - **Before putting behaviour in a builder, count the builder's callers and ask how many of them
      actually MOVE.** A factory that draws an object owns that object's *shape* and not anything
      time-dependent, and "it is where projectiles are made" is the fact that makes it look right.
    - **Prefer the consumer whose loop is already gated on the condition you need.** `SpellEffect.Update`
      opens with `if (!_launched) return;`, and `Launch` destroys a Zone *before* setting `_launched`, so
      "only flying casts trail" is free rather than a condition someone has to remember to add. When a
      gate already exists one layer down, moving the emission there is strictly smaller than writing the
      gate yourself - and it cannot drift, because it is not a separate statement.
    Two smaller points from the same task, both about a pooled FX with no `Update` of its own:
    - **`Object.Destroy` is deferred, and the first emit usually happens in the frame that built the
      template.** Destroying a template's Collider the ordinary way leaves it alive long enough for that
      frame's `Instantiate` to **clone a collider onto live geometry** - intermittent by construction,
      and invisible to every check in this repo. `DestroyImmediate` is the right call for a
      runtime-built, never-rendered template. 1jg fixed it in the new trail template and **1jh found the
      same hazard already live** in `WorldStreamer.SharedDebrisCube`, where `Destroy` had been deferred
      since 1du; it had been survivable for rare tool digs and stopped being a good bet the moment an
      emitter went behind every projectile impact. A convention that only ever finds new code is not
      yet a convention.
    - **A taper can be an AGE rather than a state.** Recycling each voxel a fixed life after it was left
      makes the tail the part about to disappear, which is a gradient for free: no fader component to
      re-initialise on reuse (which then has to not fight the pooler's delayed recycle), no alpha ramp,
      no material state, no per-voxel `Update`. `ObjectPooler.Get` already carries a special case for
      stale `ParticleSystem`s, so the pooler knows this class of problem is real.
    And the reuse check: **gate on DISTANCE, never on frames, for anything drawn along a path.** A
    frame-gated emitter makes density a function of speed *and* framerate; accumulating distance and
    emitting every N metres makes the look a property of the spell instead of of the machine.

20. **A request about where something is DRAWN is a request about the look, not the delivery - and the
    cheapest way to lose a feature is to answer it by moving an enum.** 1jt was "summon circles should be
    under the player, everything else in front", and the two Meteors were the awkward case: they summon
    from the sky but are `SpellDelivery.Zone` spells. The obvious implementation - switch them to
    `SpellDelivery.Summon` so they classify as summons - **silently deletes both of their rocks and
    moves the Earth crater behind the player**, because `SpellCaster.ResolveZone` is the *only* resolver
    that has a `SummonFallingRock` branch and the only one that applies a Zone `TerrainShape`. Nothing
    errors; the spell just stops being the spell. Three habits, all from that:
    - **Before you move a gameplay field to achieve a visual result, grep every reader of the field and
      ask which of them you are about to take a branch away from.** This is rule 13's drawn-vs-gameplay
      split one level up, and the enumeration is the same work: `ResolveZone` / `ResolveSummon` /
      `ResolveStorm` were each read, not assumed. The answer was a new **drawn-only** axis
      (`SpellCastAnchor`) on the look profile, which is strictly smaller than the fix it avoided.
    - **Prefer a total predicate over an enumerated list, because only the predicate has a coverage
      claim.** The user's rule was "Summon draws at the feet, everything else in front" - a predicate
      over a field that already exists, so all 172 spells classify with zero unclassified cases. Had 1jt
      shipped a hand-listed set of "summon-like" spells, the set would have been *right* and still
      carried no statement about the spell nobody remembered to add. Ask which of the two you were given;
      when in doubt the total predicate is the one to implement, and the exceptions become authored
      overrides **where the exceptions are visible** (both Meteors are named in one comment each).
    - **An authored-only profile on a spell that had none flips a QA counter even when it changes no
      pixel.** Fire Meteor had to gain a profile to carry `CastAnchor`, and `SpellLook.Authored` is
      `p != null`, not "a profile overrides something" - so the F4 lane's `authored profiles` count moves
      21 -> 22 while Fire Meteor looks exactly as it did (`Impact`/`Cast` left `Inherit`). **A
      data-only catalog edit can move a readout that a reader will compare against a remembered value.**
      State the predicted delta and let the lane confirm it; do not "fix" the readout to match a guess.
    - **A second same-named factory in the same partial class is reachable only through a named argument,
      and that is a latent divergence.** `Look(...)` exists twice - `SkillCatalog.cs` (with `skyrock`) and
      `ClassSkillCatalog.cs` (without) - and C#'s better-function-member rule makes the *skyrock* one
      unreachable unless the call names `skyrock`. So 1jt had to add `castAnchor` to **both** or the two
      would silently express different subsets of a profile. When you find two overloads that differ only
      by optional parameters, the honest question is "can a reader tell which one any given call reached?"
      - and the answer here was no. Record it at both declarations, and prefer a named argument when the
      distinction matters.
- **A component that re-derives its own transform every frame DISCARDS a position handed to it.** That
      is why `CastingCircle` needs a `_flat` *mode flag* holding `_flatPos`, not just a new `Show`
      overload taking a point: `Update` rewrites `transform.position` from `_anchor` before the next
      repaint, so a parameter would live exactly one frame. **If a placement has to survive a frame, it
      has to be state, and the flag is the thing that says which placement is authoritative.**
      (1jt's flag was `_grounded`/`_groundPos`; 1ka renamed it to `_flat`/`_flatPos` when the armed
      halo moved from the ground to a flat point in front of the magic weapon.)
- **A per-frame placement and the one-shot event that ends it must derive their point through ONE
      function**, or the halo and the ring that announces it can disagree. `WeaponCastPoint` (1jt's
      `GroundCastPoint`) is called from both the per-frame `UpdateCastingCircle` and the release
      `BurstCastingCircle`; because the halo is also re-placed per frame, a fresh point at release is
      *the same* point - had the halo been placed once at cast start, the "same" derivation would have
      drifted by the player's whole walk-up and still read as two derivations of one point. (1ka: the
      point moved from the ground to a flat position at the weapon, but the shared-derivation
      invariant is unchanged.)
