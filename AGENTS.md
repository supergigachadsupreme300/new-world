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
