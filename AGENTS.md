# Project Rules

1. **Always commit and push after every task.** Commit on Git `main` and push to
   `https://github.com/supergigachadsupreme300/new-world` (PowerShell; e.g.
   `git add -A; git commit -m "..." ; git push origin main`). Make a new commit for follow-up
   fixes rather than amending. Do not commit unless a task is complete.

2. **Before finishing any implementation, read AND update all "read/update" docs below in the same
   pass as the code change** (never a follow-up commit by itself):
   - `game-design.md` â€” keep it in sync with implemented behavior (section references like Â§3.3,
     Â§3.7, Â§3.8, Â§5.7). Update stats/counts, status tables, and signature mechanisms on any feature
     change, and remove stale text (e.g. a removed currency/UI leftover) in the same pass.
   - `AGENTS.md` â€” this file; update it whenever a recurring convention changes (see rules 4-6).
   - `PROGRESS.md` â€” record every completed task as a new `## 1xx` entry at the top with a `### 1xx-status`
     block; note pending play-test items and any follow-up fixes. Refresh the intro ("Last updated")
     when older entries drift.
   - `THINKING.md` â€” for any non-trivial investigation, keep the raw reasoning trail (hypotheses,
     evidence for/against, dead ends, verdicts) in a `## 1xx` section; mark each hypothesis
     confirmed/rejected/open and keep the section marked OPEN until the task ships. This is the
     "how I reasoned it out" log â€” never cite it as implemented behavior.

3. **No CLI/Unity build is run in this project.** Compile and behavior are verified by code review;
   the user play-tests in Unity afterwards. Note that verification status in each task's status block.
   **Review is not compilation:** grep confirms a symbol exists, not that two signatures agree, and a
   local declared inside a `switch` case is in scope for the *whole* switch, so a read from a sibling
   case looks legal and only fails definite assignment. When the user pastes Unity console errors,
   fix them in a **new commit** (never an amend) and sweep the whole class â€” every
   `return <void helper>(...)`, and every name declared in one `case` and read in another.
   **Run `powershell -ExecutionPolicy Bypass -File tools\StaticChecks.ps1` before committing any
   change to `WorldBuilder*.cs` or `NewWorldTestGround.cs`** â€” it mechanises exactly those checks
   (balance, overload-aware arity, void-return, unassigned locals, cross-case locals, part-key parity).
   It reports *candidates*, not verdicts: 0 candidates still means "not compiled", so paste Unity's
   output into the handoff rather than fixing errors silently. If you add a file in those areas, add
   it to the script's `$files` list or checks 1â€“5 stop covering it. **Balance is not
   reachability.** 1in wrote `dishSpan = span;` above `float span = 0f;` (CS0103) and the script
   reported `braces 44/44 parens 263/263` on a file that could not compile: the script has no
   declaration-order check, so a clean run says nothing about use-before-declaration. Reread for that
   class specifically. Do not "fix" it by adding a naive use-before-declare scanner - one that fires
   on every legitimate field read trains the next reader to ignore it, which is the false-positive
   failure rule 7 already documents. 1in added `WorldStreamer.CraterAudit.cs` to `$files`; it is a
   `WorldStreamer` partial, so only checks 1, 4 and 7 apply to it.

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
   then state the verification performed (grep/reread â€” no build) and list the pending play-test items
   the user should check in Unity. Do not add this summary until every requested task is actually
   committed and pushed.

7. **Measure before fixing a visual/geometry report.** When the user reports something you cannot see
   from the code (gaps, holes, invisible meshes, z-fighting, popping), add a read-only on-demand
   measurement lane to `NewWorldTestGround.cs` FIRST â€” one key, one number, cached on the HUD and
   logged â€” and let the readout name the mechanism before changing behaviour. Ship the measurement and
   the fix as SEPARATE tasks so the readout that justified the fix stays in history. A fix chosen
   without a measurement is a guess: it can be correct and still change nothing, and then the real
   cause is still unmeasured for the next attempt. Read-only means read-only: no rebuild, no patch,
   no re-stamp, no forced poll â€” the report must describe the frame the key was pressed on.
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
     real chunks that are dormant-and-hidden past that ring with no far-cell owner â€” ~84 correct,
     expected, invisible "voids" at 630 m that bury the one real finding. A measurement that reports
     known-absent things is not conservative, it is unreadable.
   - **Never re-derive another component's private formula to define that scope.** The visible radius
     is `ChunkLodManager.EffectiveCullDistance()` and it is private; copying that expression into the
     lane would be rule 8 in reverse (a second spelling that rots when the LOD side changes, and
     silently mis-scopes the audit when it does). Find the invariant both sides already agree on â€”
     here, the ownership band â€” and scope to that instead.
   - **A scope is a claim about the mechanism, so write it down as one â€” and a correct scope for the
     wrong *family of owner* still reports "clean" confidently.** 1hy scoped section B to the band
     where a void is possible and printed the band, which is why B's zero was trustworthy. It then
     scoped section C to `_loadedChunks`, comparing real chunks against each other â€” and never stated
     that this *presupposed the defect is between two loaded chunks*. The player then reported the
     gap **only at the outer x/z corner tile**, which is the near/far boundary: one loaded quadrant
     and three far ones. C could not see it at any depth, and it printed real numbers the whole time,
     which made a wrong premise look like evidence. So when scoping, name **which owner(s) the walk
     admits** as well as the radius, and if the report is about a seam, check that the walk contains
     both sides of it before concluding anything. "Drawn" and "flush" are separate properties: a
     surface that is live but one step off is exactly the failure a "is anything drawn here" check
     cannot see.
   - **A verdict line that conflated "broken" with "smaller than promised" is the same bug as a
     coverage walk that admits one owner â€” and the conflation is what makes a number unusable.**
     1i1's section B reported 2965 undrawn footprints because it demanded `!FarShadowedByCoarse` on a
     footprint's owner, but that helper is *true* when a live coarser cell already covers the same
     ground â€” it names a reserved shadow, not an absence. So all 2965 were reserved shadows being
     called holes. Two habits: ask coverage questions directly (enumerate every owner that can draw,
     take the first live one) instead of inferring them from the ownership predicate, and split the
     remainder into **claimed-dead** (an owner exists and nothing is drawing: a hole) and **unowned**
     (nothing claims it: a shorter horizon) with separate verdicts. When a number is retracted, say
     the replacement is *unknown*, not zero â€” 1i2 retracted 2965 and had no number to put in its
     place.
   - **Gate a classifier on the WIDTH of its own test, or it will name a mechanism for arithmetic
     noise.** 1i1's section D used `RenderedCornerTolerance` both as the "is this stepped" threshold
     and as the "is this side on pristine noise" test, so two values each within tol of the same
     pristine value could differ by up to `2*tol` â€” stepped *and* both-pristine at once. It filed 19
     nodes at 1â€“2 cm under `not an edit - look at the lattice/seed`, which reads like a finding and
     is noise from two facets rounded to one lattice. Any threshold comparison used to *enter* a
     classification must be at least the sum of the tolerances used *inside* it.
   - **A check is only evidence if you have seen it fail on the thing it is for.** A check that flags a
     false positive on the first file you add it to is a check whose silence has stopped meaning
     anything. 1hy's `StaticChecks.ps1` check 4 reported every `out` parameter as an unassigned local;
     a reader trained by 4 false candidates waves through the next real CS0165. **A balance check
     cannot see a construct in the wrong place** â€” 1i4 declared a method between the `using` block and
     `partial class WorldStreamer` and reported braces 25/25, because the method body and the class
     body were two separate well-formed constructs. Grep found the symbol, balance passed, review
     passed, and only Unity's parser objected (CS0106, which does not even name the class). Check 7
     now flags any member at brace depth 0, and was verified by **reintroducing that exact bug and
     watching it fire** â€” a green check nobody has seen fail is not a check.
   - **A safe idiom in one caller is not evidence it is safe in another.** 1i4 read `_loadedChunks` from
     `BuildChunkMeshData`, which runs on a **ThreadPool thread** via `BackgroundGenerateChunk`, while
     the main thread builds/unloads/demotes â€” and `Dictionary<TKey,TValue>` is not safe to read during
     a write. The F3 audit reads the same dictionary constantly and never races, because it is
     main-thread-only, so the pattern looked safe. **Check the thread the code runs on before reusing
     an idiom, and never argue that a data source is "strictly better" without naming the thread.**
- **"No references found" is a claim about the PATTERNS YOU TYPED, and a key has three
      spellings.** 1in shipped the crater audit on **F1** after grepping `Key.F1` and `KeyCode.F1`,
      finding nothing, and documenting the key as free. It was bound: the combat-mode toggle uses
      `Keyboard.current.f1Key` (`Player\PlayerController.Interactions.cs:521`), the **property-name**
      spelling. The lane and the toggle both fired on one press, so the measurement was taken with
      weapons drawing underneath it. Three habits:
     - **Search for the CONCEPT across every spelling its API offers, not the one you remember.**
       The Input System (used exclusively here — zero legacy `Input.*` calls) binds a key as
       `Keyboard.current.f1Key`, as `Keyboard.current[Key.F1]`, or indirectly as `kb[SomeLaneKey]`.
       Grepping the enum literal alone only ever matches the second of those three.
     - **A key choice recorded as a reason is a claim that outlives the task that made it.** The
       false "F1 is a skill hotkey" note propagated from 1hy through four tooltips and two older
       `PROGRESS.md` entries, where it was still doing duty as a *justification* long after the
       lane it belonged to was gone. When a key is free because "F1-F4 are already taken", record
       *who* takes each one, or the next reader repeats the claim without re-deriving it.
     - **Check 8 now mechanises this**: it reads the QA lane keys out of their declarations and
       fails on any second binding in `Assets\Scripts` across all three spellings. Verified by
       reverting the lane to F1 and watching it name `Interactions.cs:521`.
   - Also: a check that flags a false positive on the first file you add it to is a check whose
      silence has stopped meaning anything. 1hy's `StaticChecks.ps1` check 4 reported every `out`
      parameter as an unassigned local; a reader trained by 4 false candidates waves through the
     next real CS0165. Fix the check rather than filing the candidates as "expected".
   - **An absent measurement and a measurement of zero are different, and the UNIT decides which is
     which.** 1ik's frame-budget probe reads a `ProfilerRecorder` per source, and the trap is that a
     *valid* recorder which has never been filled still returns 0 â€” so printing that 0 into a
     millisecond column reads "this side costs nothing" about the one side the Editor cannot see,
     which sends the next reader to the wrong subsystem. The rule generalises: **in a time-valued
     column, 0 means "no sample" and must print `n/a`; in a count-valued column, 0 is a real
     observation** (0 draw calls is the frame that proves nothing is being drawn). Decide this per
     source from that source's own declared unit, never from which field is being read â€” hard-coding
     "divide by 1e6" at each call site is rule 8's rotting second spelling in numeric form. Print the
     sample count beside every averaged source, and when nothing could be measured, say the
     measurement failed â€” never let an absent column read as a fast frame.
   - **A verdict's DENOMINATOR must mean the same thing as its numerator, and a clamp breaks
     that.** 1ik's first version divided measured work by the *frame* time to decide which side
     owned the frame. Under vsync that is not a share of anything: a frame can only be a whole number
     of present intervals, so 20 ms of work behind a 33.3 ms frame is a full lost present yet reads
     as 0.60, and a majority threshold on it would refuse to name the side that did it. Three habits:
     (a) prefer a comparison between the **sides** (CPU vs GPU), which needs no reference frame and
     is clamp-proof; (b) when the denominator is quantised, report the **bracket** ("work in (1
     interval, 2.0 intervals]"), not a point estimate; (c) label the residual per regime â€” under a
     clamp up to one interval of residual is arithmetic, and calling it "unexplained" files a finding
     against nothing.
   - **Two counters with different windows must not sit on adjacent lines.** `peaks` in the QA HUD
     resets every refresh while `lod sweep`'s peak is a monotonic max since scene start that nothing
     resets â€” so `peaks 0.00` directly above `lod sweep 0.62 / 60.56` compares a window against a
     since-boot figure and reads as one quantity. 1ik is how that 60 ms was traced to the initial
     fill sweep instead of a recurring cost. State each counter's window in its own label, and reset
     per-window peaks on the same cadence as the counters beside them.
   - **An audit must ask the question the PROPOSAL is about, or the fix ships with no acceptance
     readout.** Sections A-D of the F13 crater lane measure depth, radial profile, resolution and
     expressibility; every one of them can read clean on a perfectly **smooth** cone, which is exactly
     the current shape, so a proposal whose entire point is "now make it *stepped*" had no section
     that could see it. 1hy already stated the general form ("drawn" and "flush" are separate
     properties); 1f2 is the second instance, on a different pair - **deep** and **stepped**. So when a
     request changes one property of a shape, first name every property the existing lane already
     reports, and ask which of them the request is *not* about. That gap is the new section.
   - **The same audit must check its own premise before classifying, and report a contradiction as
     `UNKNOWN`.** 1f2's first draft walked the footprint regardless of whether a crater was there, and
     would have reported untouched terrain's micro-relief as a `CONTINUOUS` verdict - a
     positive-looking number about nothing. Two follow-ons from that: (a) **membership and the value
     measured may legitimately use different references** - the crater lane picks membership by
     deviation from pristine and measures the raw height, because a carve writes `refY + offset`
     against ONE reference so the slope is overwritten not added, and measuring dig-below-pristine
     folds the untouched slope back in and reports a quantised carve as a continuum; (b) when two
     sections that read the same data through the same gate disagree, the verdict is a contradiction
     about **where** they disagree, never a number - per 1i2, a retracted number's replacement is
     *unknown*, not zero.
   - **A data model that cannot express a shape cannot be coaxed into it by tuning tolerances.** 1f2
     wanted vertical risers between flat terraces, and the mesh path seemed to offer them
     (`EdgeIsRaised` / `SideBandCount`). They do not exist: `ChunkData` stores one height per
     **shared** corner, so adjacent tiles' edge vertices are the same two floats - for tile A to be
     flat at `h0` and tile B flat at `h1`, those shared corners would have to equal both, so **adjacent
     flat tiles at different levels are inexpressible** and `EdgeIsRaised` never fires in-chunk. Before
     sizing a tolerance to make a shape appear, check whether the data can *hold* it; if not, the
     answer is a data-model change (per-tile tops, the voxel path) or a **separate draw owner** (1f4's
     cube-sphere facet shell) - not a smaller epsilon. Shared corners are also why the lattice yields
     a *ramp* and never a *wall*, which is rule 12's "no vertical strip pass" seen from the data side.

8. **A seam invariant is only as good as the arithmetic of the COPY, not the source.** When a value is
   copied out of another structure instead of re-derived (the corner lattice copies a tile's stored
   vertex; a cache copies a source array; a lookup indexes by another system's key), the addressing
   rule of that copy IS the seam contract. Do not infer correctness from how pure the source is â€” pure
   noise sampled per corner is exact, the same noise *copied through a wrong owner slot* is off by a
   whole metre, permanently, in an unedited world. Two habits follow: check the copy's index/owner
   arithmetic with a concrete worked example at a boundary node (the 4-chunk corner catches what
   mid-edge checks miss), and never let a validator stand in for a layer it does not read â€”
   `ChunkValidator` compares tile heights tile-vs-tile and is structurally blind to a lattice bug, so
   a green validator is not evidence about the lattice.
   **Two spellings of "a corner's height" exist here, and they disagree by half a metre.** Corners sit
   *at* integer world coords (`ChunkData.Size == 1`, and `WorldStreamer.ChunkBuild.cs:293-295` seeds
   corner `(gx,gz)` from `GetHeight(seed, tc.X*cs+gx, tc.Z*cs+gz)`), so pristine-at-a-corner is
   `GetHeight(Seed, cx, cz)`. But `CurrentHeightOf` samples `(cx + 0.5f, cz + 0.5f)`, and
   `GetDigDepth` mixes both - it references pristine at `(cx,cz)` against a *fallback* height at
   `(cx+0.5, cz+0.5)`. Both spellings are correct **for their own use**, and 1in shipped a profile
   ring biased half a metre outward by copying the reader's convention instead of checking the
   writer's. So: **read the code that WRITES the value before reusing the one that READS it**, and
   when a measurement compares a current height against a pristine reference, assert the two are
   sampled at the *same* point - a mismatch reads as a phantom dig that no threshold will ever clear.
   - **A comment that NAMES an identifier is a copy of that identifier, and it rots like any other
     copy.** 1f3 introduced three stale claims in a single edit and caught all three only by rereading:
     the block comment documented `CraterCapRadius` and `CraterTerraceStep` when the code declares
     `craterCapR` (a local) and `CraterTerraceFraction`/`Min`/`Max`; it described the
     `min(CraterStep, reach)` clamp as a live tool-dig path when `reach` is `radius + feather`, so no
     real dig ever clamps; and the `game-design.md` pointer said `Â§5.7` for a lane that lives under
     `Â§2.2`. None of these can fail a compile or a static check - they are the *narrative* around a
     change, which is precisely what no tool in this repo reads. So when an edit introduces or renames
     a symbol, **grep the new comment for the symbol it names and confirm the declaration exists**, and
     when it cites a doc section, confirm the heading is still where it was. A comment that documents
     a path that is not taken is worse than no comment: the next reader sizes a decision on it.
   - **State which inputs are on different ladders, or the derived number is silently a different
     number at every call site.** 1f3's cap depth is `min(CraterStep, reach)`, and `reach` is
     `radius + feather`, so the clamp that looked necessary (a sphere deeper than its rim radius) is
     unreachable in practice. The habit is not "check the arithmetic" - the arithmetic was right - but
     **name where each input's ladder starts before concluding a guard is live**, and if it cannot be
     reached, say so in the comment instead of leaving it to look load-bearing.

9. **Hand-authored block geometry is stated by its support, not by its centre.** The block-built
   structures (holy places, NPC rigs, `CreatePartCube` call sites) are positioned by a hand-computed
      **centre** Y, so the bottom face has to be re-derived by every reader and misses its support by a
      fraction of a metre without anything looking wrong. When adding or editing geometry there, use the
      bottom-referenced helpers in `WorldBuilder.Blueprints.cs` and keep these conventions:
   - `CreatePartBoxOn(root, x, bottomY, z, size, colour)` â€” never hand-compute a centre from a bottom;
     write the support's top and the block that rests on it on adjacent lines.
   - `CreatePartPanelBetween(root, a, b, halfWidth, thickness, across, colour)` â€” roofs, ramps and
     stairs are stated as the two ends of their **underside**, never as a tilt sign. A tilt sign hides
     an inverted pitch: the pagoda's four roofs were authored `Euler(+14)` for the +Z panel, which in
     Unity pitches the outer eave *up*, so all four read as a butterfly roof with the centre cap
     floating 0.57â€“1.44 m above its own panels â€” invisible in the source, obvious in the world.
    - `CreatePartGableSteps(...)` â€” the stepped profile that closes a gable triangle; each step's top
      overshoots 6 cm into the roof underside, because **a 6 cm intersection is invisible and a 6 cm
      gap is a slit** â€” never place two solids flush. Its `gableHalfSpan` (the wall) and
      `roofHalfSpan` (the eave) are separate arguments for a reason: they were one number only because
      the church's wall and eave happened to agree, and one number traces the roof's line on a narrower
      wall.
   - **A derived dimension whose inputs sit on different ladders is clamped in the helper, never at the
      call site.** A gable band's height is `roofUnderside(innerEdge) âˆ’ wallTop`: the first datum belongs
      to the roof, the second to the wall, and they are only guaranteed ordered while the eave stays
      *above* the wall's top line. The church cleared that by 6 cm, the shrine's 64 cm eave drop does not,
      and the un-clamped helper answered with a **negative height** â€” a mirrored cube whose BoxCollider
      is inside out. Any helper that subtracts one structure's datum from another's must decide inside
      itself what happens when they cross, and every caller must then be re-checked, because "the fix
      changed the church's band width" means the church was passing the clamp by luck.
    Related invariants for the same structures: all parts of one structure share the site origin
    (author in site coordinates, y = 0 at the platform top) so the assembly is auditable in one frame;
    and **never rename a structure part type** (`Church_*` / `Shrine_*` / `Pagoda_*`) â€” those strings are
    the save/load keys, and a renamed part falls through `SpawnStructurePart`'s dispatch to the generic
    `else switch` and builds *nothing*, silently. Put new content inside an existing part instead.

10. **A menu's own chrome is not in its panels' coordinate system, and it is drawn LAST.** The
    panels under `MenuPanelBase` share the canvas origin (`Body` is inset 60/60, so the origin is
    the canvas centre) and are built *before* the subclass's own chrome â€” so a tab bar or band is
    the **later sibling** and Unity draws it **over** the content, with nothing in the content's
    coordinates saying so. A band that no panel mentions is a band whose height is set by nothing:
    the 84-unit Character Info band silently covered the Skills sub-tabs, both Skills readouts, the
    Faith title/status and the top of the Info/Inventory headings. Three habits follow:
    - **Size a band from the rows under it, not from taste.** Read every widget whose extent
      reaches it (`grep` the `P(â€¦, yyy)` literals in the partials), then place the band's *bottom*
      edge above the tallest one and keep the band in a named constant. The band is a *later
      sibling* of the content, so an overlap hides the content; it is never the other way round.
    - **A row's real height is its INK, not its box.** `MakeBodyText` is TopLeft in a fixed box
      (`MakeButton` is pivot-top), so glyphs grow *downward* from the declared y â€” a title at 238 in
      a 34 box reaches ~200. Two rows that "don't overlap" as boxes can overlap as text; the Faith
      title/status pair did, invisibly, for as long as the band covered it.
    - **Anything that must not be clipped is a mask, and a mask built later wins.**
      `TreeViewport` is a `RectMask2D` added after the Skills header row, so a row that overlaps it
      loses its bottom border â€” check the mask's edge as if it were solid.
    - A metric that **two code paths must agree on** (a build pass and an aspect-fit pass) is one
      named constant. The band carried `36` in one site and `34` in the other, so it used to change
      position the first time the window was resized.
    - **A field's border is drawn INSIDE the field, and the field's text is inset to clear it.** A
      border hung *outside* the rect is a collision waiting to happen: the class/race rows are 4 units
      apart and the last one is 2 units from the buttons under it, so an outside border lands on the
      neighbour's glyphs. The inset costs the row height it is paid for â€” a 30-tall row minus 2Ã—4
      leaves 22 for a 22.5pt line, whose descent then crosses the border it just paid for â€” so the
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
    - `_loadedChunks` â€” every `ChunkObject` owns an uploaded `RootMesh`, and nothing re-runs the
      generator for a chunk that is already loaded.
    - `_dormantChunks` â€” the wake pass re-shows a demoted chunk "in place â€” the same GameObject,
      same pooled mesh, same tile data", and `EnqueueChunkIfNeeded` *wakes* a dormant chunk rather
      than re-dispatching it. So returning to a region re-activates the mesh that region was built
      with, however old.
    - the far shell â€” its cells are sampled from the real chunks' surfaces, so they keep the old
      heights as well.
    A mid-session render edit therefore leaves a world where the resident chunks are OLD-algorithm and
    the chunks that stream in later are NEW-algorithm, and the two part along their shared edges.
    Four habits follow:
    - **The drop covers every loaded AND every dormant chunk, plus the far shell.** Both halves
      matter: a dormant chunk you never wake is a landmine that pops the stale mesh back the moment
      the player walks into that region again.
    - **`WorldStreamer.ResetTerrainSaves()` is the right sequence and the wrong tool.** It does
      exactly the right thing in order (`ClearFarShell()` â†’ unload + requeue every loaded and
      dormant chunk) but first calls `ChunkSaveManager.ResetWorldSaves(Seed)`, which **permanently
      deletes the player's terrain edits** â€” the save files hold *heights*, which are data, not
      render output. Use it only when a pristine world is the actual goal.
    - **Since 1hx the only remedy is a play-session restart** â€” the non-destructive wrapper
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
      **1hy restored F3** as a read-only rendered-corner + void audit â€” see the last bullet of rule 7
      â€” so this is measurable again, but only for the near/far resident set, and F2's cross-chunk seam
      measurement is still gone. F4 was not restored.)
    - **1ia is itself an instance of this rule, and the reason its verification cannot be claimed in
      the same session.** Reverting the render algorithm to pre-1hi is exactly the edit this rule
      describes, so the facet world stays on screen until the next launch no matter how obviously the
      code says otherwise. Do not report the revert as seen working from a session that was already
      running â€” the *only* honest statement is "restarted, then observed".

12. **The facet size is ONE decision living in two files, and the step must divide three numbers.**
    The near chunks and the far shell render the same facet language, so `WorldStreamer.LowPolyStep`
    and `FarSectorStep` (`WorldStreamer.FarShell.cs`) are the same constant wearing two hats â€” change
    one and the world reads *inverted*: chunky underfoot, finer at the horizon, with a density break
    at the rim. Two invariants ride on the value:
    - **It must divide 30, 90 AND 180** â€” the near chunk side and the far cells' span-3 and span-6
      boxes. That set is {1, 2, 3, 5, 6, 10, 15, 30}; anything else leaves the last grid row short
      of a chunk boundary, which is a visible crack along every chunk edge. (A *non*-uniform ladder
      is worse still: the pre-1ej 3/6/9/12/15-by-radius version produced T-junction rows on every
      shared cell edge, read as permanent "thin lines" â€” which is why `FarSectorStep` ignores its
      `span`/`maxRing` arguments on purpose.)
    - **A coarser step is not free.** The collider rides the same step, so footing gets lumpier, and
      prop heights still sample the 1 m lattice, so props float/sink by up to the facet error. The 1 m
      grid stays canonical for saves and edits either way, but the *edit granularity* is the step:
      a 1 m dig only moves a facet vertex when the edited corner happens to land on the grid.
    1hx moved the default 3 â†’ 6 because 3 m facets sampled this 5-octave field (base octave amplitude
    55 m at frequency 0.0012) came out near-coplanar, and facet shading contrast scales with
    `curvature Ã— span` â€” so at 3 m the normals barely differed and the world still read as smooth
    haze. The lesson generalises: **"low-poly" is a normal-contrast problem, not a triangle-count
    problem**, so reach for the span before reaching for the shading.
    - **1ia: the facet language is DORMANT (`LowPolyFacets = false`), and `FarSectorStep` is NOT one
      of the gated values.** The flag governs the *flat-facet render path* (flat per-quad far normals,
      the 1ew split at threshold 0, and the 1hi.1 coarse roots) â€” but `FarSectorStep` returns a
      constant with no reference to the flag, so the far shell samples every 3 m whether or not the
      look is on. That means a "revert the look" change is **two edits, not one**: flipping the flag
      alone leaves the far shell at whatever step it was left on, and the shell's sampling density is
      then describing a surface nobody is drawing. 1ia moved both to 3 together. When you next move
      this step, grep for `FarSectorStep` as well as the field â€” and remember `NewWorldTestGround`
      mirrors **both** (`EnableLowPolyTerrain`, `LowPolyStep`) and pushes them onto the streamer in
      `Awake` *before* the first stream poll, so a mirror left at the old value silently re-applies
      the look on the test platform every session. The 1i9 `CraterFacetSkirt` is likewise dormant
      (it is gated on `EffectiveLowPolyStep`, which is `0` while the flag is off), which is why the
      1 m-resolution world needs no skirt at all.
    - **Raising the step SILENTLY DELETES every carve narrower than half a facet â€” check this
      before changing the step, and again after.** The rendered surface holds only every
      `step`-th lattice node, so a deform whose *reach* is smaller than the worst-case distance to
      a sampled node (`step/âˆš2`, and 3 m at step 3 vs 4.24 m at step 6) writes its entire shape
      into nodes no triangle is built from. It does not look wrong, does not error, and leaves the
      data layer perfectly correct â€” it simply stops existing on screen. 1hx (3 â†’ 6) deleted the
      universal projectile impact dent this way: its 1.9 m reach fell inside the new 4.24 m
      worst case, and the user reported "the dent function is gone now" with the impact sphere
      still playing, which is the tell â€” **the FX is outside the carve's gate, so a crater that
      vanishes while its effect plays is a resolution problem, not a hit-detection problem.**
      (This bullet only bites while the facet look is ON; at the 1ia default the surface holds every
      1 m node, so a carve under 4.24 m renders normally. Check the flag before diagnosing a
      "missing" carve.)
      Three habits:
      - **Compare a deform's reach to `step/âˆš2`, not to `step` or to the step's value.** The
        bound is the *diagonal* of the sampled cell, because the nearest sampled node can be half
        a step away in BOTH axes.
      - **A carve guarantee must be a `Max` with the authored influence, never a replacement for
        it.** `DeformAt`'s `CraterFacetSkirt` returns 0 unless the carve would move *no* rendered
        node, so it can only ever turn an invisible carve into a visible one â€” the authored radius,
        depth and per-cast ratchet stay untouched, and a carve that already reaches a sampled node
        keeps exactly its shape. Widening the reach instead would have multiplied the excavation
        rate with it (a Crater ratchets `CraterStep` per cast), turning a "small dent" into a pit.
      - **Widen the loop's WRITE bounds to cover the guarantee, or the skirt targets nodes the loop
        never visits** â€” a guarantee computed outside the region being written is a guarantee that
        writes nothing.
      - Unfixed consequence of the same coupling: the RAISED shapes (`Wall`/`Ring`/`Pillar`/
        `Spikes`) use the same radii and are subject to the same invisibility, but 1i9 scoped
        itself to `Crater` deliberately. If a raised shape is reported invisible, that is this
        same bug, not a new one â€” and the fix must keep the shapes' `Max(current, target)`
        idempotency intact.
    - **A LOD band is a THIRD owner of the surface, and it resamples the same lattice the facet step
      does â€” one dimension down (1f5).** `ChunkLodManager` switches detail at **30 m and 60 m**, and
      `ChunkObject.BuildLodChild` decimates the 31x31 corner grid to every 2nd/3rd corner. So the
      `step/âˆš2` test above applies to the LOD stride too, and at a distance that needs no commitment:
      a 1.9 m-reach crater clears Lod1's 1.41 m worst case by 0.5 m and falls **inside** Lod2's 2.12 m,
      so backing up while playing silently swaps the surface you are looking at for a resampled one.
      Four habits, all from 1f5:
      - **A coarse surface is not a stale one, and the two fixes are opposites.** Rule 11's remedy for a
        stale mesh is *rebuild*; a decimated mesh is perfectly fresh and rebuilding changes nothing,
        because the information was never in it. Check whether the value is *wrong* or *absent* before
        reaching for a refresh. Conversely a carve's staleness guard (`_lodDirty`, `LodDirty`) already
        existed and worked fine here â€” it was never the bug, and a passing staleness test is not
        evidence that a surface carries the feature you are looking at.
      - **A feature change can expose a dormant defect, and then the feature gets blamed.** 1f3 did
        not create this: pre-1f3 the crater was a smooth cone, and a smooth cone resampled at 2 m
        still looks like itself. Terraces are the highest-frequency content in the shape and decimation
        deletes high frequencies first, so 1f3 turned an invisible LOD defect into a reported one. When
        a shape change produces a "it used to be fine" report, suspect the coupling it made legible
        rather than the shape â€” and say so in the handoff, or the next reader hunts in the wrong file.
      - **Fixing this class of thing needs a threshold DERIVED from the generator, not chosen, because
        there is often no readout.** Rule 7 wants a measurement first; 1f5 had none (the user has no F13
        key). The gate had to come from `TerrainNoiseGenerator`'s octave table, which is possible
        because a **discrete Laplacian is exactly zero for any planar surface at any stride** â€” so
        `NeedsLodDetail` measures relief, not scale. Natural floor â‰ˆ0.016 m over one 1 m cell; the
        terraced crater â‰ˆ0.24â€“0.95 m; gate 0.20 m. That property is what makes such a gate safe to
        ship unmeasured, so look for it before reaching for a hand-picked epsilon.
      - **Adaptive detail is where a resolution bug turns into a GEOMETRY bug, and the cheap version is
        the wrong one.** Subdividing only the distorting cells leaves each refined cell's shared edge as
        a polyline against its neighbour's straight chord â€” the T-junction row rule 12 records from the
        pre-1ej far shell, now with a visible crack instead of thin lines. With no compiler (rule 3)
        that is not a gamble worth taking, so 1f5 refined **whole-chunk**: a uniform stride has no
        transitions and therefore cannot crack. Ask which failure you would rather ship before
        optimising the *scope* of a refinement rather than its *cost*.
      - `BuildVoxelLodChild` still decimates to 2/3 unconditionally (`VoxelTerrainEnabled` is false by
        default). Same coupling, still open â€” if a carved voxel chunk looks wrong at distance, that is
        this bug, not a new one.
    - **The COLLIDER is the ground, and it is a separate decision from the render step (1ex).** The
      player has no ground raycast â€” `CharacterController.Move` sweeps the chunk `MeshCollider`
      directly (`PlayerController.Movement.cs`) â€” so `ChunkColliderDecimation` *is* the surface
      underfoot. It is not the facet step: `ColliderStep` is `_meshStep > 0 ? _meshStep :
      ChunkColliderDecimation`, so the low-poly look borrows the facet step while the default uses its
      own value (1 m since 1ex). Three habits:
      - **Never refine the render surface while leaving the collider coarse** â€” that *widens* the
        mismatch, it does not close it. 1ex raised the collider 2 â†’ 1 after the
        walk-through-a-visible-crater bug: at 2 m a 1 m crater centred on an odd x or z had **no
        sampled collider node inside its footprint**, so the player crossed a pit they could see. The
        pairing (render step : collider step) is the invariant; the render step alone is not. A
        "finer mesh" change that does not touch the collider is a visual-only change and must be
        stated as one.
      - **A collider-step change owes a number on F2.** The recipe is ring bodies Ã— tris-per-collider:
        `ColliderRingRadius = 7` Chebyshev = 225 bodies, each `(30/step + 1)Â²` verts, so 1 m â‡’
        961 verts / 1800 tris and **~405k** ring triangles (2 m â‡’ 256 / 450 and ~101k). Do not carry
        a stale figure forward â€” three comments claimed "~7k-tri" and were wrong at *every* step.
      - **The lattice is horizontal quads only â€” there is no vertical strip pass** (the render mesh
        has one for cliffs). A vertical step is therefore sampled as a ramp whose slope is
        step-dependent: 1 m â‡’ 45Â°, exactly the default `CharacterController.slopeLimit`, which this
        project never assigns. Cliff traversal is a play-test item on any collider-step change.

13. **A spell's colour, shape and halo are derived in exactly one place â€” and "one place" is not the
    same as "one colour everywhere".** 1ib added `SpellLook.Resolve` so no consumer re-derives a
    spell's identity, which is rule 8's "second spelling that rots" applied to look rather than
    geometry. This codebase had already shipped **two** drifting `DamageType` palettes, so the
    convention is not theoretical. Three habits:
    - **Resolution has exactly three steps, and a named fallback is not a fourth.** authored
      `SpellLookProfile` on the `SpellData` â†’ deterministic pick from the school's family â†’ the
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
      silently recoloured four schools â€” and the merge destroyed the QA signal, because a header
      tinted the same colour as the thing it labels cannot show you that the thing is mis-coloured.
      **Before deduplicating a colour, ask what would catch the bug if that value were wrong.** If the
      answer is "the same table, because it is tinted to match", the two tables must stay separate and
      the exception gets a comment. Debug-only and save-invisible surfaces need no parity check; a
      building save key does (rule 13's other half).
    - **Asymmetry between two adjacent code paths can be correct, and the comment must say so.**
      1if made the spell-backed release burst look-derived and the *no-spell-armed* burst Arcane-pink
      on the grounds that the two look inconsistent. They are not the same event: pink there asserts
      "an Arcane spell was cast", which is false. When you fix a colour regression, check whether the
      two neighbouring lines actually share an identity before restoring the symmetry â€” and if they
      do not, say so in a comment or the next reader will "fix" it again.
    - **Rule 15's other exception: a skill may shape the approach but still lose to a project rule.**
      "Derived in one place" is a source-level invariant; a skill that says "call the factory" does not
      get to decide where the factory lives. State which side won.

14. **A removal has three failure modes, and only one of them is a compile error.** Deleting a feature
    leaves behind more than references to the deleted *name*, and the three residue classes fail in
    three different ways â€” 1hz removed the multiplayer layer, the night club, fast travel and horse
    riding in one pass and hit all three:
    - **Producers outlive their consumer.** Grepping the deleted type's name finds the *call sites*;
      it cannot find code that existed only to **feed** the deleted system, because that code is
      perfectly correct on its own. `FastTravelSign` went away with `FastTravelMenu` and four POI
      files went on building sign GameObjects, colliders and `SignPost` cubes for a list nothing
      would ever read. The grep "passed" â€” it found exactly the four files that needed deleting and
      reported nothing left over. So for each hit ask **what is this line for**, not *does it name the
      deleted type*; and when a type disappears, list what it was *fed by*, not just what read it.
    - **A save key outlives its builder, and a missing `case` never fails to compile.** A `switch`
      dispatching on a save key is legal with any subset of its cases, so deleting a building's
      builder leaves the key live and silent. `WorldBuilder.Persistence` still listed `"NightClub"`
      in the essential-restore branch, whose body is `RebuildEssentialBuilding(...)` followed by
      `_buildings[_buildings.Count - 1]` â€” no case, nothing appended, and the club's health, part
      healths and **door state** were written onto the *previous* building. A compile error stops the
      game; this loads a village that looks fine and has one ruined building. This is rule 9's part-key
      concern one level out, and unlike part keys it has **no** parity check in `StaticChecks.ps1` Â§6.
      When a builder is deleted, grep for its *save key* as well as its type â€” and verify the fall-through
      is actually inert before relying on it (read `CreateBuildingEntity`/`SpawnBuildingDirect`, don't
      assume a missing definition is a safe skip).
    - **A behavioural orphan is invisible to every grep.** `RichManNPC` kept an entire state machine
      (`ClubHangState`, `ClubIdleState`, pace spots, `HandleClubHangout`, a 19:00â€“21:00 window) pacing
      the player around a building 1hz deleted. Every symbol resolved, the file compiled, the code
      ran â€” it was just an NPC walking an empty lot; the 1hz follow-up commit removed it. Removing a
      *place* means re-reading whatever *story* pointed at it, and that is a human read, not a search.
      Two things a removal like this also takes with it that no compile error names: **the breadcrumb
      the behaviour was the player's only lead for** (that hangout's "he is at the bar" toast was the
      only in-game cue for the 21:00 deal, so the deletion silently made a quest step
      undiscoverable â€” replace it or say so), and **any constant the deleted block shared with
      surviving code** (its `CLUB_WINDOW_END` was a second, independent spelling of the deal's 21:00,
      so removing it left `TryStartDeal`'s bare `TimeOfDay < 21f` as the only remaining hour; both now
      read one `DEAL_HOUR`).
    Two corollaries for the mechanics of the sweep itself:
    - **Deleting a block deletes its locals, and a local is in scope for the whole method.** Removing
      `DungeonSystem`'s sign block took `Vector3 doorDir` with it, but `doorDir` is read 20 lines
      later for the enemy spawn offset. Grep the removed block's *identifiers* for other readers
      instead of trusting that the block looked self-contained â€” this caught the error in my own edit,
      and it is the same reflex that finds the first bullet's leftovers.
    - **A localized string is a runtime key, so "no reference" needs the dynamic path checked too.**
      14 dead `Localization` keys looked removable from the comment above them, but `Localization.T`
      resolves by string at runtime, so a key is live if *any* code passes that Vietnamese text â€”
      including the ~60 non-literal `T()` call sites and any `.asset`/`.json`. Use exact
      (`-SimpleMatch`) matching, not a regex: a `.`-wildcarded Vietnamese pattern returned 50 008
      "matches" against a mangled console, which is worse than no search because it looks like a
      result. Confirm a reference-counting grep can find something real before trusting a zero.

15. **The globally installed skill set is a tool, and reaching for it is not optional.** Every session
    runs with skills available outside this repo (Blender, Maya, ZBrush, Unreal, Unity, asset and
    pipeline skills, plus `skill-creator` for authoring new ones). Rule 2 lists the docs you must
    update; it does not list this. **Before starting a task, check whether a skill covers it, and load
    it if one does** â€” the Skill tool costs one call, and re-deriving a domain workflow from scratch is
    both slower and more likely to be wrong than following the shipped one.
    - **Silence is not a verdict.** 1ibâ€“1ij shipped in three commits without a single skill loaded, and
      the omission only surfaced when the user asked afterwards. Not loading a skill is a *decision*;
      it has to be made deliberately and **stated in the handoff**, the same way rule 3's "no build" is.
      A future reader cannot tell "I checked and none applied" from "I forgot" â€” the two are identical
      in the transcript. Write one line: `skills: none applied â€” <reason>`, or name what was loaded and
      what it changed about the approach.
    - **A skill that contradicts this project's rules loses to this project's rules, and the conflict
      gets stated.** The Unity skills are the obvious case: `scenario-unity-expert` and its siblings
      target driving a running editor over MCP or in `-batchmode`, while rule 3 says **no Unity build or
      CLI run happens in this project**. Loading one is still allowed â€” it may carry real API and
      architecture knowledge â€” but it cannot *verify* anything here, and the difference between
      "informative" and "authoritative" is exactly what rule 11 is about for render changes. Say which
      of the two it was.
    - **Match the skill to the artifact, not to the topic.** Most of the installed set is DCC-side (Maya,
      Blender, ZBrush, Unreal, image/video/audio generation). A task that merely *mentions* Unity, or
      the phrase "a skill", is not a match. Ask what artifact the task produces â€” a `.blend`, a `.ma`
      scene, an Unreal `.uasset`, a C# edit reviewed by a human â€” and load only when the skill governs
      that artifact. The failure this prevents is loading a heavyweight 3D-DCC workflow to edit 12 lines
      of `PlayerController.Combat.cs`: a near-miss skill burns context and contributes nothing.
    - **The repo's own tooling is not a substitute, and does not excuse skipping the set.**
      `tools/StaticChecks.ps1` (rule 3) is mandatory on its own terms, but it checks *this codebase's*
      invariants â€” brace/paren balance, overload arity, CS0165 candidates, part-key parity,
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
