# Project Rules

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
   project's own existing usage is the reference to check it against.** 1jq wrote a runtime mesh update
   around `MeshUpdateFlags` + the `(array, start, count, flags)` overloads; the type does exist in Unity
   6000.5.1f1, the project's own 1jq verification pass (`StaticChecks.ps1`, grep, reread) all went green,
   and it still shipped CS0246 on the user's console - because **`TrailStrip.cs` was the only file in
   the repo that ever mentioned the symbol**, so nothing local could confirm the name and no check looks
   for it. Three habits, all mechanical:
     - **Before writing an API call, grep the tree for an existing use of it.** If there is one, copy
       that exact overload shape - `ChunkMeshGenerator`'s upload is the proven pattern here:
       `SetVertices(array)` / `SetColors(array)` / `SetTriangles(array, 0)` / `bounds` by hand, with **no
       flags**. If there is **no** existing use, treat the call as unproven and prefer the plainest
       overload the codebase already demonstrates over the one the docs describe as optimal.
     - **A count of zero hits is itself the finding.** "No other file uses this" is not a gap in the
       sweep, it is the answer: the risk is unbounded precisely because there is no local precedent.
     - **Swapping a bounded upload for a full-array one silently changes the contract.** Dropping the
       `(start, count)` overloads means leftover entries from a *longer previous frame* are still uploaded,
       and stale indices reference real vertex slots - so garbage geometry, not a harmless no-op. 1jq had
       to add `Array.Clear` over the index tail for exactly this. Whenever you trade a bounded write for
       an unbounded one, ask what the leftovers now point at.
     - **"A conversion exists" is not "this CONSTRUCTOR accepts these argument types."** 1jq's next
       error was CS1503 `cannot convert float to byte`, from `new Color32(look.Edge.r, look.Edge.g, ...)`.
       `SpellLook.Edge` is a `Color` (four normalised floats) and the `Color32` *constructor* takes four
       bytes; a `Color -> Color32` conversion does exist, but as an **implicit operator**, which is a
       different member from an overload of that constructor. So the fix that also deleted the whole
       float->byte ladder was to store the colour as a `Color` end to end - which then put
       `SetColors(List<Color>)` on the overload the rest of the project uses (`FarShell`, `VoxelMesher`,
       `ChunkMeshGenerator` all pass a `List<Color>`), where `List<Color32>` appears **nowhere** in 378
       files. **When a value has to cross a type boundary twice, remove one side of the boundary.**
     - **After the second console error in one file, sweep for a THIRD rather than patching again.**
       Two errors from the same new file is a signal about the file, not about the two lines. Grepping
       every remaining call against a precedent found `SetColors(List<Color32>)` - valid Unity API, zero
       local precedent, and the same family of assumption that had already failed twice - plus a check
       that `ObjectPooler.Get`/`Return` are **instance** methods on a `MonoBehaviour`, so a static call
       would have been CS0120 (it turned out the code never calls it at all).
   **Run `powershell -ExecutionPolicy Bypass -File tools\StaticChecks.ps1` before committing any
   change to `WorldBuilder*.cs` or `NewWorldTestGround.cs`** — it mechanises exactly those checks
   (balance, overload-aware arity, void-return, unassigned locals, cross-case locals, part-key parity).
   It reports *candidates*, not verdicts: 0 candidates still means "not compiled", so paste Unity's
   output into the handoff rather than fixing errors silently. If you add a file in those areas, add
   it to the script's `$files` list or checks 1–5 stop covering it. **Balance is not
   reachability.** 1in wrote `dishSpan = span;` above `float span = 0f;` (CS0103) and the script
   reported `braces 44/44 parens 263/263` on a file that could not compile: the script has no
   declaration-order check, so a clean run says nothing about use-before-declaration. Reread for that
   class specifically. Do not "fix" it by adding a naive use-before-declare scanner - one that fires
   on every legitimate field read trains the next reader to ignore it, which is the false-positive
   failure rule 7 already documents. 1in added `WorldStreamer.CraterAudit.cs` to `$files`; it is a
   `WorldStreamer` partial, so only checks 1, 4 and 7 apply to it. 1f6 added `ChunkDistanceCull.cs`
   and `NewWorldSystems.cs` — the cull is the class the streamer *skips dormant entries for*, so a
   wrong signature there is a silent visibility bug rather than an error, which is exactly the
   failure review cannot see.

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
      is `ChunkDistanceCull.EffectiveCullDistance()` and it is private; copying that expression into
      the lane would be rule 8 in reverse (a second spelling that rots when the cull side changes, and
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
- **A check must also be unable to PASS for the wrong reason, and an address inside a check is where
    that fails.** The bullet above is about silence; this is about a *false* green, which is worse
    because it carries a claim. 1jd's move comparator (`tools/Compare-MovedModel.ps1`, rule 17) reported
    **24/24 identical** on its green run, and one of its addresses was wrong the whole time: a start
    marker (`Vector3 mid = transform.position + Direction`) occurred **twice** in the pre-move file - once
    in the per-frame `Animate()`, once in `BuildVisual` - so the extractor compared the animation and
    reported its pulse maths as a difference. It was caught only because the two wrong blocks had
    different literals; had they matched, the run would have printed `==` for a block it never opened.
    Three habits, all mechanical:
  - **An address is part of the check, so make it verifiable rather than careful.** Require both ends of
    a range to exist in **both** files, and prefer a marker that occurs **once** in its file over one that
    merely reads distinctively. "I was careful" is not a property a re-reader can check.
  - **Two declarations of one name are two addresses.** `RingFlash` has an expression-bodied forwarder
    overload above the real one, and member-addressing took the forwarder - a one-line body with no
    braces. Overloads, partials and nested classes all make a bare name ambiguous; say which one.
  - **Prove the comparator can fail AND that its extraction is the block you meant** - one control is not
    enough. `-Mutate` proves the comparison is sensitive; only re-reading the *extracted* text against the
    file proves the extraction is aimed correctly. The first is cheap to add and the second is the one
    that catches a silent redefinition.
- **A balance count is only a signal if the file was BALANCED to begin with - otherwise read the DELTA,
    and measure the baseline before you trust the tool.** 1je edited `NewWorldTestGround.cs` and got
    `parens 1280/1279`, which reads as a broken file. It was not: the committed version is **1216/1215**,
    because one `Debug.LogWarning` in it contains an unbalanced `(` **inside a string literal**. So the
    absolute count on that file can never be a pass/fail verdict, and "the balance check is red" would
    have been a false positive that trains the next reader to ignore the tool on the file most likely to
    need it. Two habits, both cheap:
  - **Ask `git show HEAD:<path>` for the baseline before interpreting an absolute count.** Compare
    before -> after, not after -> perfect. 1je's own delta was **61/61**, i.e. clean, and that is the
    only claim the evidence supports. (Same shape as rule 7's "a retracted number's replacement is
    *unknown*, not zero": a count measured against no baseline is not a small number, it is no number.)
  - **A crutch that counts characters cannot see the difference between code and a string or comment,
    so treat any imbalance you did not cause as a *finding to locate*, not a defect to fix** - and do not
    "balance" the file to make the tool green, which would edit pre-existing content to satisfy an
    instrument that was never measuring it. Locate it (`git show HEAD:` first), record it, move on.
  - **Once the instrument strips strings and comments, the raw character count is not a baseline for it
    - it is a DIFFERENT measurement, and quoting one as the other is a wrong number wearing a real one's
    name.** 1jq measured its baseline the crude way (regex over the raw file: `201/201` braces,
    `1277/1276` parens) and then ran `StaticChecks.ps1`, which reported `178/178` and `1112/1112` for the
    *same file it had just edited*. Both were true and they disagreed, because check 1 runs the source
    through `StripNonCode` and the crutch does not. The crutch's baseline is therefore useless as the
    script's baseline, and the script's numbers are the only ones that can carry a pass/fail. The habit is
    cheap: **to baseline a stripping instrument, run its own stripper over both versions** - extract
    `StripNonCode` from the script, `Invoke-Expression` it, and count `HEAD` and `HEAD+edit` identically.
    That returned `173/173` and `1051/1051` for the true baseline and matched the script's own `NOW` line
    exactly, which is also the proof the extraction was aimed at the right function. So **when a crutch and
    the real instrument disagree, the crutch is not a second opinion - it is a second, uncalibrated
    instrument**, and the honest move is to throw away its number rather than average the two.
- **The mirror of a qualified-reference sweep has THREE false-positive classes, and only one of them is
      a real defect.** 1jk swept every `Owner.Member` reference in the tree against the members its owner
      declares, to check whether 1jj's CS0117 had any siblings - and got 9 "unresolved" hits, of which
      **0 were real**. Each class is a different way for the scan to be right about the text and wrong
      about the code, so all three have to be eliminated before a single candidate is filed:
  - **A reference inside a COMMENT is not a reference.** `WorldBuilder.SpawnRockDebris` appeared 3 times -
      twice in `//` prose and once in `<see cref=...>` - and read as three outside calls to a `private`
      method, i.e. CS0122. Stripping `//` and `/* */` and XML doc blocks is not optional in any of these
      sweeps, or a *comment naming a member* becomes an error report (rule 8's stale-comment rule, running
      the other way).
  - **An INHERITED member has no declaration in the owner's own files.** `WorldBuilder.Instance` is used 55
      times and is declared nowhere in the twelve partials, because `WorldBuilder : MonoSingleton<WorldBuilder>`
      gets it from the live `MonoSingleton<T>`. A per-file extractor cannot see a base class; ask "does the
      type have a base?" before calling a missing declaration a defect.
  - **A nested TYPE declared with its brace on the next line defeats a one-line declaration regex.**
      `public class FieldState` followed by a newline matches nothing that expects a trailing open paren,
      brace or equals sign, so five save-record types read as missing. Match declarations with a
      multiline-tolerant pattern or verify the extractor on a member you have *seen declared*.
  **And the instrument itself has to be able to report a presence AND a negative control before its zeros
  are quotable.** 1jk's first run printed "0 referenced, 0 unresolved" because `New-Object
  System.Collections.Generic.ArrayList` does not exist (the type is non-generic), so the ref table was
  never filled - a false zero produced by a broken instrument, indistinguishable in the output from a clean
  sweep. The fixed run printed 36 distinct / 204 refs, and a control file with none returned 0. Two habits
  fall out: **when a sweep reports a suspiciously round zero, re-run it with a value you know must be
  non-zero**, and check `New-Object` type names actually resolve before believing the table they populate.
- **A scan that reports ABSENCES must first be shown able to report PRESENCES, or its misses mean
      nothing.** The same class of error running the other way, and it is easy to mistake for a
      finding: verifying 1iu's two READMEs, `Select-String -Path 'Assets\Scripts\*.cs',
      'Assets\Scripts\**\*.cs'` reported **9 of the symbols MISSING** - including
      `MagicWeaponBehavior`, `FallRock` and `BuildProjectileBody`, all of which exist. `**` in a
      PowerShell `-Path` glob descends exactly **one** level, so a scan only as deep as the files it is
      looking for reports absences forever. The tell is internal: a scan that finds `Resolve(` 29 times
      and `HolsterPoint` 0 times is claiming to have read a tree that cannot contain the callers it just
      counted. **Build the file list once, explicitly and recursively**, and sanity-check it against a
      total (1iu: `scanned 365 .cs files`, equal to the `.cs.meta` count) before believing any zero.
      This is why `StaticChecks.ps1` uses its own `$files` list instead of a glob. Two more instances of
      the same mistake, both in 1iv: **`Get-ChildItem -Recurse -Include *.unity,*.prefab` does not
      filter** unless the path ends in `\*` or you pass `-Filter`, so a "which scenes reference this"
      scan returned 1131 files *including every `.meta`* - and a `.meta` always contains its own GUID, so
      the scan "found" 63 references that were self-matches; and a **greedy `-replace ".*guid:\s*",""`**
      silently returned the wrong field plus trailing text, printing a PASS for a check that had not run.
      Filter by extension after collecting, match `^guid:` not `guid:`, and treat any reference count you
      did not recompute as unmeasured.
    - **A move that changes a path's MEANING is not a move - a GUID only resolves inside `Assets/`.**
      1iv moved 90 source-art files (34 MB) to a repo-root `_ArtSource/`, and the `.meta` travelled with
      each one, which keeps the GUID stable - but a GUID means nothing to Unity unless the asset sits
      under `Assets/`, so the move turns a live reference into a **missing** one silently. Nothing
      errors, the scene still lists the object, and the texture is simply gone at runtime. So before
      moving any asset out of `Assets/`, enumerate **every serialized referrer** (`.unity`, `.prefab`,
      `.asset`, `.mat`, `.controller`) and grep it for the asset's GUID. Two habits fall out:
      - **Ask which referrers are actually live, not merely present.** The first scan found 10 referrers
        and 8 of them were `0 (N).unity` scenes inside the gitignored `Assets/_Recovery/` - already
        slated for deletion. The real constraint was **one** file, `Scenes/SampleScene.unity`, the only
        scene in `EditorBuildSettings`. Ten referrers would have meant "don't move anything"; one live
        one meant "move the other 9".
      - **A folder name is not evidence of what a file is for.** `Assets/xoanvnmexel/` is an
        unpronounceable pack name and looked exactly like junk to relocate; its `.ttf` is the source font
        of `Resources/VietPixel.asset`, which `UiAssetCache` loads as the shared default UI font and which
        TMP's own default font asset references. Three textures in `Assets/texture/` were referenced
        by that one scene as the named fields `FieldTexture`, `FertilizerTexture` and
        `PeashooterSeedTexture`. Junk-looking is not the same as unreferenced, and
        the reference count is the only thing that tells them apart.
      - **A count has a magnitude, and so does a string count: sanity-check it against the size of the
        file being counted.** 1iv's "how often is this texture referenced" check reported
        **7717 / 5292 / 7374 occurrences** for three GUIDs in a 29,003-character scene file. A 32-char
        GUID cannot occur 7717 times in 29 KB, and that arithmetic was available *before* the scan ran.
        The cause: `"$text".Split($guid)` splits on every **character** of the GUID, so it counts hex
        digits in the file, not the GUID. Those numbers reached `game-design.md`, `AGENTS.md`,
        `PROGRESS.md` and a commit message before anything caught them; the true counts are **1 each**,
        confirmed twice (regex `Matches`, then an `IndexOf` walk) against bogus-GUID controls. So:
        **count with `([regex]::Matches($text,[regex]::Escape($g))).Count`, never `String.Split` on a
        multi-char needle**, and **print a known-zero control beside every count** — a check that has
        never been shown able to report 0 for a string it does not contain is not a count.
      - Corollary for the *inverse* edit: relocating a live asset "properly" (into `Resources/`, say) means
        hand-editing a serialized GUID inside a scene file, which no review here can verify
        without opening the editor (rule 3). **Leave a duplicated-but-live pair alone and say so**, rather
        than dedupe it on paper.
  - **A safe idiom in one caller is not evidence it is safe in another.** 1i4 read `_loadedChunks` from
     `BuildChunkMeshData`, which runs on a **ThreadPool thread** via `BackgroundGenerateChunk`, while
     the main thread builds/unloads/demotes — and `Dictionary<TKey,TValue>` is not safe to read during
     a write. The F3 audit reads the same dictionary constantly and never races, because it is
     main-thread-only, so the pattern looked safe. **Check the thread the code runs on before reusing
     an idiom, and never argue that a data source is "strictly better" without naming the thread.**
- **"Not used" is a claim about a CALL GRAPH, and a folder that dominates a directory is exactly
      where that claim is most tempting and least true.** 1iw was asked to move `MapBuilder` out of
      `Assets/Scripts/Models/` because it "takes up too much space and isn't used in the game" - it was
      10 of that folder's 17 files and 5,153 lines, so both halves of the premise felt checkable. The
      move itself is cosmetic and harmless; the *reason* was false, and had it been acted on (`rm`
      instead of `git mv`) it would have deleted the player model (`RaceRig`,
      `PlayerController.Animation`, `PlayerAnimator` -> `BuildPlayerModel`), sitting
      (`PlayerSitController`), the **tornado spell** (`SpellTornado`/`SpellBeam`/`SpellCaster.Cast`),
      pets, random events and world sign text - and ~40 files would have failed to compile. Three habits:
      - **Do the cosmetic move, but report the premise failure in the same pass.** A refactor that is
        safe on its own is still the right moment to say "these are load-bearing", because the next
        reader inherits the tidy folder as evidence for the deletion that did not happen.
      - **The surprising caller is the one worth grepping for, and it is usually a layering inversion.**
        `World/Chunks/ChunkObject.cs` calls `MapBuilder.BuildTree` and `BuildStone`, so **the streamed
        terrain draws through a class that lives under `Models/`**. A class's folder describes where it
        was written, not who depends on it; "under Models" reads as cosmetic-only, and it is not.
      - **Separate "unused by the game" from "unused by the legacy layer" - they have different
        partials.** Nine of the ten partials are village buildings and NPCs used only by `WorldBuilder`
        and the nine `CutsceneManager.Ending*` files, so retiring the legacy content really would orphan
        them, but `PlayerModels.cs` and `Nature.cs` are in the new world's path and would have to be kept
        or promoted first. Ask *which member* is unused, never *whether the class* is.
      - Corollary for the check itself: a path-string grep cannot see a move, so a file moved into a
        subfolder stays verifiable only if you also confirm nothing addresses it **by path**
        (`AssetDatabase.LoadAssetAtPath`, an `.asmdef`, an editor tool). Here that grep read 0 and the
        move was free; had it read non-zero, the move would have needed a rewrite, not a `git mv`.
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
   - **A procedural cycle's RATE is a DIVISION by a stride, which makes its input and its seed
     load-bearing in a way a clamped blend was not.** 1jn's walk/run change is the general case: the
     gait rate went from `cadence = 1.8f + norm * 2.0f` to `cadence = speedH / StrideLength`, and the
     two requests in one report ("too fast to see" + "scale with movement speed") turned out to be the
     *same* lever rather than two features, because the complaint was the constant **floor** and the fix
     is a relationship. Three habits, each from something that would have shipped broken:
     - **A cycle rate that tracks movement speed is `speed / stride`, and that is the no-foot-slide
       invariant.** One cycle advances the body by exactly one stride, so the planted foot travels with
       the body. Any other formula - `1.8 + norm * k` included - has to be tuned until it happens to hold
       at one speed and drifts at the rest, and the drift is invisible in a still frame. The installed
       `scenario-unity-animation` skill has the measured version of the same rule ("script speed that
       disagrees with the clip slides the feet, p50 0.19 vs 1.14 m/s"); it targets Animator Controllers,
       which this project does not use, so it supplied the constraint and not the verification.
     - **A rate that is a division is a far more sensitive consumer of its input than a `Clamp01` was,
       so re-audit the input's own hygiene.** 1jn's speed was a raw per-frame transform delta, and the
       old clamp made a garbage frame cost one frame of pose; under the divisor the same frame lands in
       the **`_phase` integrator**, where an error is *permanent* rather than transient. That is the
       general shape: **a clamp absorbs bad input, an integrator remembers it.** Low-pass the input, and
       discard values no locomotion could produce (a teleport, a respawn) rather than smoothing them in.
     - **A tracker with no seed has a fictional first sample, and grep cannot see it** - the symbol is
       declared and assigned, so an existence sweep passes. 1jn's `_lastRootPos` was read-then-written
       and never initialised, so frame 1 measured the player against `default(Vector3)` and reported its
       distance from the world **origin** as speed. Seed a tracker in `OnEnable`, and remember that the
       seed only matters once something downstream is sensitive enough to care.
     - **A clamp that exists to protect legibility breaks the invariant it protects - say so where the
       knob is.** `MaxCadence` stops a fast build from becoming a blur, and above it the feet *do* slide,
       because the animation can no longer express the real speed. Compute where the clamp first binds
(1jn: `4.3 x 3.2 = 13.8 m/s`; after 1jp: `5.6 x 3.2 = 17.9 m/s`, vs 10 m/s sprint, 12.5 m/s for a
        +25% perk build) and record the
        consequence in the field's own tooltip, not only in the design doc - the tooltip is what the next
        person reads while dragging the slider.
      - Corollary for "too fast" reports: **ask whether the art parameter or the character speed is the
        lever before touching either.** 1jn's base speeds were already `MoveSpeed 5f` x
        `SprintMultiplier 2f` = a 5 m/s walk and a 10 m/s sprint, so at 10 m/s *no* honest cadence is
        slow. Slowing the character is a gameplay change and not the one that was asked for; lengthening
        the stride buys fewer, bigger steps and leaves the feel alone. Two halves of one report being one
        lever is worth establishing before asking the user to choose.
      - **The knob a user calls "the animation speed" is the one whose side effects they have NOT seen,
        so say what the second knob does before you turn it again.** 1jp asked for the same slowdown 1jn
        had just shipped, and the naive answer was to raise `StrideLength` again - which *is* the rate knob,
        and which quietly trades against foot skate, because a slower cycle must cover *more* ground to keep
        up with the same speed. The two levers are: **slow the rate** (`StrideLength` up, more skate) or
        **cap it** (`MaxCadence` up, less skate, but then it stops reading as slow). 1jp shipped the rate
        change the user asked for and recorded the trade in both the tooltip and the design doc rather than
        silently pairing it with an amplitude bump. Three habits: **a tuning knob usually has a partner
        knob, and the pair moves in opposite directions** - find it by asking what invariant the first one
        is protecting (`StrideLength` protects foot planting; `MaxCadence` protects legibility); **quantify
        where a clamp first binds** so you can tell whether it is even live
        (`MaxCadence x StrideLength` vs the character's top speed - 17.9 vs 10 m/s, so it is not); and
        **never answer a second "also slow it" with the same edit and no new number**, because the user's
        second ask is evidence the first did not land far enough - re-read what the first number actually
        produced rather than incrementing blind.
   - **A one-frame lag is a WRITE ORDER, and the file that reads the value is not the file that writes
     it.** 1jo's "endlag of the path predict ray" was a phase mismatch, not a smoothing curve: the aim
     preview is built in `PlayerController.Update`, while `CameraModeSwitch` writes the camera transform in
     `LateUpdate`, so the preview read **last frame's** camera. Unity runs all `Update`s before all
     `LateUpdate`s, and the report's own qualifier ("when moving the camera") was the tell - a stale read is
     only visible while the value is changing fast. Four habits:
     - **Name the phase of every writer before trusting a reader.** Grep *where* a value is assigned, not
       just that it exists: `CameraModeSwitch` writes the camera only in `LateUpdate` and never in `Update`,
       which is what makes the read stale. "It might write in both" is a different bug and a grep settles it.
     - **Prefer the look SOURCE over the thing that renders it, when they hold the same value.** The pivot
       and the camera hold the same direction (first person copies the pivot's rotation outright; third
       person's lateral offset cancels between eye and target), but the pivot is written in the same
       `Update` the reader runs in. So this was a latency fix, not an aim change - and it cost nothing.
       Aimed-at-something reads should prefer the look source; only *position* needs the camera, and a
       raycast from the eye genuinely does.
     - **Fix the shared function, never one call site, when two consumers must agree.** 1jm made the
       preview **call** `StraightFlightDirection` precisely so the readout cannot drift from the flight
       path. Patching the preview's argument alone would have turned "the ray is one frame stale" into
       "the ray points somewhere the bullet never goes" - worse, and much harder to see. The seam to fix
       is wherever the consumers are already agreed to meet.
     - **A report about one instance of a class is not a report about the class, and the fix for the
       instance is not the fix for the class.** Grepping `cam.transform.forward` found **~35** aim sites
       carrying the identical one-frame offset (`ToolManager`, interaction rays, `FishingController`, the
       farming managers). They were *reported*, not fixed: most want the camera's **position**, so the
         tempting systemic fix (move the camera write into `Update`) would change the camera's own
         smoothing feel. A defect with one instance named and 35 unnamed is a design decision about the
         camera, and saying so is cheaper than silently rewriting 35 call sites.
     - Corollary for art-side numbers: **changing a value invalidates every doc that quotes it, in the same
       pass.** 1jo changed `ThirdPersonSideOffset` and both `SpellCaster`'s XML doc and `game-design.md`
       quoted the old figure - grep the *docs* for the value being changed, not just the code for the symbol.
   - **A secant is a claim about a line, so the derivative's sign and units are part of the mechanism.**
     (this is a placeholder, see above)
   - Also: a check that flags a false positive on the first file you add it to is a check whose
      silence has stopped meaning anything. 1hy's `StaticChecks.ps1` check 4 reported every `out`
      parameter as an unassigned local; a reader trained by 4 false candidates waves through the
     next real CS0165. Fix the check rather than filing the candidates as "expected".
   - **An absent measurement and a measurement of zero are different, and the UNIT decides which is
     which.** 1ik's frame-budget probe reads a `ProfilerRecorder` per source, and the trap is that a
     *valid* recorder which has never been filled still returns 0 — so printing that 0 into a
     millisecond column reads "this side costs nothing" about the one side the Editor cannot see,
     which sends the next reader to the wrong subsystem. The rule generalises: **in a time-valued
     column, 0 means "no sample" and must print `n/a`; in a count-valued column, 0 is a real
     observation** (0 draw calls is the frame that proves nothing is being drawn). Decide this per
     source from that source's own declared unit, never from which field is being read — hard-coding
     "divide by 1e6" at each call site is rule 8's rotting second spelling in numeric form. Print the
     sample count beside every averaged source, and when nothing could be measured, say the
     measurement failed — never let an absent column read as a fast frame.
   - **A verdict's DENOMINATOR must mean the same thing as its numerator, and a clamp breaks
     that.** 1ik's first version divided measured work by the *frame* time to decide which side
     owned the frame. Under vsync that is not a share of anything: a frame can only be a whole number
     of present intervals, so 20 ms of work behind a 33.3 ms frame is a full lost present yet reads
     as 0.60, and a majority threshold on it would refuse to name the side that did it. Three habits:
     (a) prefer a comparison between the **sides** (CPU vs GPU), which needs no reference frame and
     is clamp-proof; (b) when the denominator is quantised, report the **bracket** ("work in (1
     interval, 2.0 intervals]"), not a point estimate; (c) label the residual per regime — under a
     clamp up to one interval of residual is arithmetic, and calling it "unexplained" files a finding
     against nothing.
    - **Two counters with different windows must not sit on adjacent lines.** `peaks` in the QA HUD
      resets every refresh while `cull sweep`'s peak (the 1ea rolling cull's, `PeakCullMs`) is a
      monotonic max since scene start that nothing
      resets — so `peaks 0.00` directly above `cull sweep 0.62 / 60.56` compares a window against a
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
   - **A navigation map is a copy of the codebase, and it is the one file nothing in this repo checks.**
      1iu added `Magic/README.md` and `Animation/README.md`, and every symbol in them was wrong on the
      first pass: the rest-pose pair (`SyncRestFromIdle` / `RestoreAuthoredRest`, `AGENTS.md` rule 16) was
      filed under `PlayerAnimator` when it belongs to `WeaponAnimator`; `HolsterPoint` was named as
      `WeaponStowAnimator`'s sheathe anchor and **does not exist** (the real one is the `AnchorParent`
      field); and `PlayerAnimator` was described as owning idle/attack/dodge blends when it is a
      *procedural walk/run* component for the `MapBuilder` model. Nothing errors, and a reader who trusts
      it is sent to the wrong file with full confidence. Three habits, all mechanical:
      - **Generate a map; never hand-maintain one.** 1iy's answer was a `tools/Write-Tree.ps1` that
        writes `TREE.md` from `git ls-files`, and the file's first line says *generated - do not
        hand-edit* and names the commit it saw. A hand-typed tree of a 1169-file repo rots on the next
        structural commit; a generated one is at worst out of date and self-reporting. **Prefer a file
        that can be re-derived over a file that must be remembered.**
      - **Grep every identifier the doc names, after writing it.** A prose claim about a symbol's
        *ownership* is exactly as rot-prone as a stale symbol name (rule 8's third bullet) — the name can
        exist and still be filed under the wrong owner, which is the more expensive error because it
        sends the reader somewhere plausible.
      - **Never write a line number.** It is a copy of a fact that dies on the next edit above it, and
        there is no `cs:line` convention to update. Name the symbol and let the reader jump.
      - **Do not re-derive a private formula or a dispatch table into a doc.** Same failure as the
        visible-radius rule 7 records: a second spelling rots silently when the original moves.
      - Corollary from shipping the generator: **a generated artifact still needs a check that can fail.**
        1iy's completeness verifier first "passed" with 0 == 0 because its regex swallowed the tree
        glyphs into the directory names, so both sides of the comparison were empty. Rule 7's "a green
        check nobody has seen fail is not a check" applies to the verification of the fix, not just the
        fix. Re-run it wrong on purpose and watch it go red.
   - **"I can't find X" is a claim about NAMES, not about existence — and grep only ever proves
     existence.** 1iz was asked to "group models and animations together, I don't see magic models." The
     magic models existed, worked, and had been shipping the whole time: `BuildStaff`, `BuildHolyBook`,
     `BoneWand` and `BuildControlOrb` are four **contiguous methods at L199-275 of the 379-line
     `Models/WeaponModelBuilder.cs`**, numbered 11–14 among fourteen melee/ranged/shield weapons, with no
     file, folder, or symbol anywhere in the repo that says "magic model". So the report read as a
     missing feature and was actually a **discoverability** failure, and the two demand opposite fixes
     (build it vs. name it). Three habits:
     - **A feature with no name is invisible, and no search will find it.** Before concluding a feature
       is absent, ask *what a reader would have typed*. Here every plausible query — "magic", "staff",
       "wand", "orb", "book" — returns a folder or a method that is about something else. `grep` proves
       the code is there; nothing in the toolchain can prove it is *findable*, so the fix is a name and a
       home, not an implementation.
     - **A contiguous block of methods is a latent file, and the tell is that it is contiguous.** Four
       adjacent sections with their own numbering (`// 11.` … `// 14.`) inside an 18-weapon-builder file is a
       category that has not been split yet. Scrolling a file top to bottom is how you find these; a
       directory listing never shows them, which is why the user noticed and the code review did not.
     - **"Group A and B together" is usually a report that A and B are not separable.** Both this and
       1iw's request had the shape *move these files, the premise is wrong*: here `Models/` is a
       **procedural geometry factory** (`BuildCloud`, `BuildTornado`, `BuildCafe`, `BuildPoliceCar` sit
       beside `BuildPlayerModel` in one 40-dependent `MapBuilder` class), so "group the models" has no
       single target. **Measure what each file actually is before naming a destination** — and when the
       answer is "this folder is three categories wearing one name", write that down instead of moving
       files into a differently-shaped pile.
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
   rule of that copy IS the seam contract. Do not infer correctness from how pure the source is — pure
   noise sampled per corner is exact, the same noise *copied through a wrong owner slot* is off by a
   whole metre, permanently, in an unedited world. Two habits follow: check the copy's index/owner
   arithmetic with a concrete worked example at a boundary node (the 4-chunk corner catches what
   mid-edge checks miss), and never let a validator stand in for a layer it does not read —
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
     real dig ever clamps; and the `game-design.md` pointer said `§5.7` for a lane that lives under
     `§2.2`. None of these can fail a compile or a static check - they are the *narrative* around a
     change, which is precisely what no tool in this repo reads. So when an edit introduces or renames
     a symbol, **grep the new comment for the symbol it names and confirm the declaration exists**, and
when it cites a doc section, confirm the heading is still where it was. A comment that documents
      a path that is not taken is worse than no comment: the next reader sizes a decision on it.
    - **A "guarantee" comment that asserts a MECHANISM must be checked against the guard's actual exit
      conditions, and code that exists only to satisfy a wrong mechanism is worse than no code.**
      1jq's `TrailStrip.Spawn` pushed the spawn position **twice**, with a comment claiming this
      "guarantees a legal segment on the first `Update`" because "letting it take the too-few-points
      early-out would destroy the trail 0.3 m after it was born". Both halves were false: `Update`
      destroys only on `_count == 0`, and returns harmlessly on `_count < 2`, so a one-point strip
      survives on its own. The duplicate therefore bought nothing - and it actively *corrupted a
      measurement*, because a strip with a zero-length first segment reported `1` segment / `2` triangles
      while having no visible geometry. That is rule 8's stale-comment failure with extra confidence
      (the comment asserted a control-flow fact, which is exactly the kind of claim no compiler or grep
      checks) plus 1ik's "an absent measurement and a measurement of zero are different": `VertexCount`
      was returning `_count * 2` for a mesh nothing had been written into. Three habits:
      - **Re-read the guard, not the comment, before trusting what an early return does.** The claim was
        about one line (`_count == 0` vs `_count < 2`) and one read settled it.
      - **A defensive duplicate is a cost, so it has to be justified in the present tense.** "The first
        frame would break without it" is a claim about the code as it now stands, re-verifiable on demand.
      - **A count of geometry must count only geometry that exists.** Any `*Count`/`*Count` readback
        should ask what a degenerate input makes it report, and the answer should be 0.
   - **A task id is a copy of a fact about the repo's history, and the copy is written LAST — so
     "grep before you edit" cannot cover it.** 1ip wrote `1io` into four places (the code comment, the
     `game-design.md` bullet, the `PROGRESS.md` and `THINKING.md` headings) and `1io` is a **real
     shipped task** (the crater lane's F1 → F13 move); `1im` is taken too. Only a grep of
     `PROGRESS.md`'s `^## 1..` headings *after* writing them caught it. Three reasons this is worse than
     a stale symbol name: (a) **nothing in review catches it** — a stale `CraterCapRadius` looks wrong,
     whereas a stale `1io` looks like an ordinary task reference *precisely because some task called
     `1io` genuinely exists*; (b) the id is chosen at commit time, i.e. **after every file is edited**,
     so the pre-edit grep habit structurally cannot see it; (c) the answer lives in the one file a task
     *adds* to. So: **assign the id by enumerating `PROGRESS.md`'s headings first**, and when a task id
     has already been pushed out of order (1ip's predecessor shipped as `1il`, between `1ik` and `1im`),
     leave it and say so in `PROGRESS.md` — rule 1 forbids amending, so renumbering is not on the table.
   - **State which inputs are on different ladders, or the derived number is silently a different
     number at every call site.** 1f3's cap depth is `min(CraterStep, reach)`, and `reach` is
     `radius + feather`, so the clamp that looked necessary (a sphere deeper than its rim radius) is
     unreachable in practice. The habit is not "check the arithmetic" - the arithmetic was right - but
**name where each input's ladder starts before concluding a guard is live**, and if it cannot be
      reached, say so in the comment instead of leaving it to look load-bearing.
    - **A navigation map is a copy of the codebase, and it is the one file nothing in this repo checks.**
      1iu added `Magic/README.md` and `Animation/README.md`, and every symbol in them was wrong on the
      first pass: the rest-pose pair (`SyncRestFromIdle` / `RestoreAuthoredRest`, `AGENTS.md` rule 16) was
      filed under `PlayerAnimator` when it belongs to `WeaponAnimator`; `HolsterPoint` was named as
      `WeaponStowAnimator`'s sheathe anchor and **does not exist** (the real one is the `AnchorParent`
      field); and `PlayerAnimator` was described as owning idle/attack/dodge blends when it is a
      *procedural walk/run* component for the `MapBuilder` model. Nothing errors, and a reader who trusts
      it is sent to the wrong file with full confidence. Three habits, all mechanical:
      - **Grep every identifier the doc names, after writing it.** A prose claim about a symbol's
        *ownership* is exactly as rot-prone as a stale symbol name (rule 8's third bullet) — the name can
        exist and still be filed under the wrong owner, which is the more expensive error because it
        sends the reader somewhere plausible.
      - **Never write a line number.** It is a copy of a fact that dies on the next edit above it, and
        there is no `cs:line` convention to update. Name the symbol and let the reader jump.
      - **Do not re-derive a private formula or a dispatch table into a doc.** Same failure as the
        visible-radius rule 7 records: a second spelling rots silently when the original moves.

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
- **A second surface for the same ground is a resolution bug before it is a performance win
      (1f5, resolved by deletion in 1f6).** This used to be written as "a LOD band is a THIRD owner of
      the surface": `ChunkLodManager` switched detail at **30 m and 60 m** and
      `ChunkObject.BuildLodChild` decimated the 31x31 corner lattice to every 2nd/3rd corner — the same
      `step/√2` resample as the facet step, one dimension down. So the `step/√2` test above applied to
      the LOD stride too, and at a distance that needs no commitment:
      a 1.9 m-reach crater cleared Lod1's 1.41 m worst case by 0.5 m and fell **inside** Lod2's 2.12 m,
      so backing up while playing silently swapped the surface you were looking at for a resampled one.
      **1f6 deleted the whole thing** — the band children, `RefreshLodMeshes`/`BuildLodChild`/
      `BuildVoxelLodChild`, the `LodDirty` staleness flag, the `NeedsLodDetail`/`LodDetailCurvature`
      gate, and the `ChunkCornerGrid.Normals` copy only those builders read. A chunk's root mesh is now
      its only render output at every distance (~+400k resident triangles for ~336 chunks; draw calls
      unchanged). The habits survive the deletion, because the same shape of bug is still available
      anywhere a *stride* is introduced:
      - **A coarse surface is not a stale one, and the two fixes are opposites.** Rule 11's remedy for a
        stale mesh is *rebuild*; a decimated mesh is perfectly fresh and rebuilding changes nothing,
        because the information was never in it. Check whether the value is *wrong* or *absent* before
        reaching for a refresh. Conversely the carve's staleness guard (`_lodDirty`, `LodDirty`) worked
        fine and was never the bug — a passing staleness test is not evidence that a surface carries
        the feature you are looking at.
      - **A feature change can expose a dormant defect, and then the feature gets blamed.** 1f3 did
        not create this: pre-1f3 the crater was a smooth cone, and a smooth cone resampled at 2 m
        still looks like itself. Terraces are the highest-frequency content in the shape and decimation
        deletes high frequencies first, so 1f3 turned an invisible LOD defect into a reported one. When
        a shape change produces a "it used to be fine" report, suspect the coupling it made legible
        rather than the shape — and say so in the handoff, or the next reader hunts in the wrong file.
      - **Promoting a rarely-used path to the only path promotes its unexercised assumptions — enumerate
        them BEFORE the flip, and make the play-test a verdict.** 1jf was one boolean
        (`CameraModeSwitch.StartInFirstPerson` → `false`) to open the game in third person, and the edit
        itself had **zero** interesting failure modes: the field had one reader, `CurrentMode`/`IsFirstPerson`
        had **0** external readers, and `CameraModeSwitch` is `AddComponent`ed at runtime so no scene
        override existed. The whole risk lived in the path becoming *unavoidable*. Four checks, all cheap,
        all mechanical, and the first two are the ones that decide whether the edit does anything at all:
        - **Is the value serialized?** A `[Tooltip]` field initializer is a **default**, and a default
          loses to whatever the scene stored. 1jf grepped the one live scene for the component *and* the
          field (0 each) and confirmed the `AddComponent` path, because "I flipped the default" and "the
          game now starts that way" are different claims and only the second one is the task.
        - **Read the path for assumptions that only pay off when it is optional.** The third-person camera
          clamp `SphereCast`s **from the pivot**, which sits 1.5 m up — inside the player's own
          `CharacterController` — with `CollisionMask = ~0`. If Unity reports that overlap the camera
          clamps to 0.1 m from the pivot and third person renders as first person: the commit looks inert
          and nobody can tell a no-op from a success by looking. So state the numbers, then write the
          read **both ways** ("camera at the head = trap fired; camera 6.5 m back = clean") so the read is a
          verdict rather than an impression.
        - **Do not encode the hypothesis in a comment.** 1jf found this trap and deliberately wrote
          **no** comment about it, because asserting an unmeasured mechanism is rule 8's stale-comment
          failure with extra confidence: it reads as settled and stops the next reader from measuring.
        - **Grep for a second owner of the concept before assuming there is one path.** The folder held a
          second, **dead** `ThirdPersonCamera` (0 code refs, 0 asset-GUID refs) - the 1iw shape. Reported,
          not deleted: that is a rule 14 task with its own sweep, and riding it onto a camera flip is how
          a one-field commit becomes an unreviewable diff.
        - Corollary: a **framing** request and a **view** request are different tasks. "Change the camera
          angle to 3rd view" carried both, so 1jf asked rather than picking, and shipped the framing
          untouched — a guessed framing is a play-test cycle no static check can verify (rule 3).
        - **A framing offset must move the LOOK-AT too, or it moves nothing on screen.** 1jl added a
          lateral shoulder offset to `CameraModeSwitch` and the camera appeared *identical*, because
          `UpdateThirdPerson` ends in `LookRotation(pivotPos - camera.position)`: offset the position alone
          and the camera simply rotates to keep the pivot re-centred, trading a view angle for zero
          framing. The fix is to add the offset to a single `lookTarget` and use it for **both** the
          desired position and the rotation, which also makes the view direction provably unchanged
          (`lookTarget - desired` is the same vector with the lateral term cancelling out of both) rather
          than merely plausible. Two habits: state which of the pair the request was about - "move the
          camera right" is a framing claim about *where the character sits*, and a re-centring camera
          answers a different question; and **an offset that also bends a collision cast is a new
          interaction, not a no-op.** The `SphereCast` here starts at the pivot, so a lateral term enters
          the direction it sweeps and a wall beside the player now pulls the camera in. That is a
          play-test item, and the honest move was to record it in the play-test list rather than leave it
          as an unremarked side effect of "a cosmetic offset".
        - **A cached VALUE is only valid along the AXIS it was measured on, and a rotation is what
          makes that false.** 1jr: the user reported "the camera sometime bug and zoomin if player turn
          while moving". `UpdateThirdPerson` runs its `SphereCast` at ~10 Hz and reused the clamped
          distance in between - but a distance is only meaningful *along the ray that produced it*, and
          `toCam` changes on every turn (1ju: **not** on a strafe - `toCam` is
          `(desired - pivotPos).normalized` and `pivotPos` cancels exactly, so it depends only on the
          pivot's orientation and three constants; this bullet previously said "and strafe" and was wrong).
          So a clamp measured "straight back" was applied to
          "back and to the left", and the error was largest exactly when the direction changed fastest:
          **turning**. Three habits:
          - **When a cache exists to save a *query*, its validity is tied to the frame of reference the
            query was made in, so cache the query's INPUTS too - or invalidate on the delta.** Here that
            is one extra `Vector3` (`_cachedDir`) plus an `Angle` comparison. The cheap-looking version
            ("just cache the number") silently assumes the world is stationary, and a 3D boom is not.
          - **A time-based cache that is only correct when nothing moves is a rate-vs-correctness
            trade dressed as a perf win.** Widening `CollisionCheckInterval` would have made the symptom
            rarer and the bug larger. If a report says "sometimes, when I turn", suspect the axis the
            cache dropped, not the interval - and **write down the trigger word**, because "while moving"
            and "while turning" name different mechanisms and the user used both.
          - **Gate the extra query on the cache being non-trivial, so the fix costs nothing when it
            cannot matter.** `_cachedFinalDist < 0` (no obstruction) skips the `Angle` test entirely, so
            a clean frame is still one query per 0.1 s. A correctness fix that runs a physics query every
            frame has usually moved the bug rather than fixed it.
        - **A SMOOTHED position must never feed a look-at, or the smoother is a steering input.** This is
          1jr's "name the phase of every writer" turned inside out: 1jr's bug was a *distance* cached
          across a change of *direction*, and 1ju's is the same shape one level down — the camera's
          rotation was `LookRotation(lookTarget - _camera.transform.position)` where that position is the
          output of `Vector3.SmoothDamp`. A first-order smoother leaves the camera **trailing** the
          player, so the lag vector was being subtracted from the look-at, i.e. **position lag steered
          the aim**. Three habits, each from something that would have shipped:
          - **The bug names the motion that has a component along the thing you are not smoothing.**
            Forward running trails *along* the view axis (a small pitch shift), so it reads as "fine";
            **strafing** trails *sideways*, which is yaw. That is why the report said "moving to the side"
            and a test that only walks forward passes. Before fixing a follow-camera report, ask **which
            translation direction makes the lag perpendicular to the view** — that is the one to measure.
          - **Steady-state lag is `v * SmoothTime`, so always quote the number, not "a bit".** Here
            0.15 s × 5 m/s = 0.75 m over a 6.5 m boom = **6.6°**, and 13° sprinting — comparable to
            `ThirdPersonSideOffset` (0.9 m), which is why the over-the-shoulder framing wobbled too. A
            report phrased as "a bit" hides a 13° swing; the magnitude is what makes the fix reviewable.
          - **Derive the aim from the UNSMOOTHED target when the unlagged value is already in hand, and
            then prove it is a no-op at rest.** `CameraModeSwitch` already computed `desired` (the boom
            position) one line above the SmoothDamp, so the fix was **one token** — aim from `desired`.
            The claim that makes it safe is that `lookTarget - desired` is *byte-identical* to the old
            expression whenever the camera has caught up, and identical under a collision pull-in because
            it is the same `finalDist`. Say that out loud in the diff; "I subtracted a different position"
            reads as a behaviour change and is not one.
          - **Corollary — the same anti-pattern, a DIFFERENT trigger, is a separate task.** Under a
            collision pull-in, `lookTarget` reappears as a lateral term in the new expression and the
            camera still yaws toward it. Fixing that too is a *visible* change, so it was reported rather
            than bundled onto a fix whose whole value is that it changes nothing at rest.
          - **A derivation that disproves an existing comment is part of the fix, not a footnote.** 1jr's
            comment (and `game-design.md`) both claimed `toCam` changes "on every turn **and strafe**".
            It does not: `toCam` is `(desired - pivotPos).normalized` and `pivotPos` **cancels exactly**,
            so `toCam` depends only on pivot orientation and three constants — strafing is pure
            translation and leaves it bit-for-bit invariant. The wrong claim had survived a shipped task
            because grep finds the sentence and not its algebra, so it was corrected in code, in the
            design doc and here in the same pass.
        - **An aim derived from a POINT in front of the camera is silently coupled to where the camera
          IS, so any camera edit re-aims it.** 1jm's projectile aim was
          `normalize((camera.position + camera.forward * Range) - castOrigin)`. Read it as a direction and
          the error term is `camera - castOrigin`, scaled by `1/Range` — so the aim depends on the camera's
          *position*, not just its rotation. 1jl moved that camera 0.6 m sideways, which would have skewed
          every projectile in the game for a commit whose entire diff was three lines in a camera file that
          mentions no spell. The magnitude scales with how far the camera sits from the cast origin, which
          is why it read as "fine" in first person (camera at the pivot, near the hand) and as a bug in
          third person (6.5 m back), so **no amount of first-person testing clears this class of report.**
          Two habits: when an aim, a reach or a spawn point is computed from a camera *position*, write down
          what it now depends on; and prefer a camera **direction** for anything that flies, since a
          direction is invariant to where the camera sits.
        - **A "does X still happen" check must name the OTHER thing that could have caused it.** 1jm
          changed the spell aim, and the natural verification was "cast a projectile, look at the path". But
          the player had *also* just been given a sideways camera in 1jl, which is a second candidate for a
          changed-looking shot, and only one of the two was under test. When a fix lands into a session
          that already has an adjacent change in the same subsystem, name both in the play-test list and
          say which one each check is meant to implicate - otherwise a pass attributes the fix and a fail
          sends the next reader to the wrong file.
      - **A derived gate is a mitigation, never a licence: ask whether the second surface is worth
        its own identity.** 1f5's `NeedsLodDetail` gate was genuinely derived, not chosen — a
        **discrete Laplacian is exactly zero for any planar surface at any stride**, so it measured
        relief, not scale (natural floor ≈0.016 m over one 1 m cell; terraced crater ≈0.24–0.95 m;
        gate 0.20 m). That derivation is what made it safe to ship *unmeasured*, and it was still the
        wrong answer: it made the terrain passable at two different shapes instead of one. If a gate
        exists only to keep a second surface honest, delete the surface. Keep the derivation skill for
        the cases where the second surface is genuinely a different owner (the far shell, the
        decimated collider).
      - **Adaptive detail is where a resolution bug turns into a GEOMETRY bug, and the cheap version is
        the wrong one.** Subdividing only the distorting cells leaves each refined cell's shared edge as
        a polyline against its neighbour's straight chord — the T-junction row rule 12 records from the
        pre-1ej far shell, now with a visible crack instead of thin lines. With no compiler (rule 3)
        that is not a gamble worth taking, so 1f5 refined **whole-chunk**: a uniform stride has no
        transitions and therefore cannot crack. Ask which failure you would rather ship before
        optimising the *scope* of a refinement rather than its *cost*.
      - The 1f5 remark "a refined chunk keeps the root's 900 quads out to the last band" describes a
        world that no longer exists. If you find yourself reasoning about which stride a chunk is on
        because of its DISTANCE, you are looking at the deleted design; the only distance-driven
        question left is the cull (§2.5, `ChunkDistanceCull`).
    - **The COLLIDER is the ground, and it is a separate decision from the render step (1ex).** The
      player has no ground raycast — `CharacterController.Move` sweeps the chunk `MeshCollider`
      directly (`PlayerController.Movement.cs`) — so `ChunkColliderDecimation` *is* the surface
      underfoot. It is not the facet step: `ColliderStep` is `_meshStep > 0 ? _meshStep :
      ChunkColliderDecimation`, so the low-poly look borrows the facet step while the default uses its
      own value (1 m since 1ex). Three habits:
      - **Never refine the render surface while leaving the collider coarse** — that *widens* the
        mismatch, it does not close it. 1ex raised the collider 2 → 1 after the
        walk-through-a-visible-crater bug: at 2 m a 1 m crater centred on an odd x or z had **no
        sampled collider node inside its footprint**, so the player crossed a pit they could see. The
        pairing (render step : collider step) is the invariant; the render step alone is not. A
        "finer mesh" change that does not touch the collider is a visual-only change and must be
        stated as one.
      - **A collider-step change owes a number on F2.** The recipe is ring bodies × tris-per-collider:
        `ColliderRingRadius = 7` Chebyshev = 225 bodies, each `(30/step + 1)²` verts, so 1 m ⇒
        961 verts / 1800 tris and **~405k** ring triangles (2 m ⇒ 256 / 450 and ~101k). Do not carry
        a stale figure forward — three comments claimed "~7k-tri" and were wrong at *every* step.
      - **The lattice is horizontal quads only — there is no vertical strip pass** (the render mesh
        has one for cliffs). A vertical step is therefore sampled as a ramp whose slope is
        step-dependent: 1 m ⇒ 45°, exactly the default `CharacterController.slopeLimit`, which this
        project never assigns. Cliff traversal is a play-test item on any collider-step change.

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
    - **Rule 15's other exception: a skill may shape the approach but still lose to a project rule.**
      "Derived in one place" is a source-level invariant; a skill that says "call the factory" does not
      get to decide where the factory lives. State which side won.
    - **A held HUE is not a held PERCEIVED hue, so "same colour, brighter" is not a one-line change -
      and a new colour member must be checkable BY HAND.** 1js asked for the projectile trail to read
      as its element. The trail already received the per-spell resolved look, so this was a
      **wrong-member** finding, not missing data: it read `SpellLook.Edge`, and `EdgeFor` drops
      saturation to 55% - correct for a one-quad rim highlight, grey for a wide soft ribbon at partial
      alpha. The first fix held hue in HSV and raised saturation and value, which is the textbook way
      to write "same hue, brighter" and was **still wrong**: chroma is `s * v`, so raising `v` on a
      low-saturation school increases apparent chroma and the perceived hue drifts while `h` sits
      untouched. Hand-checked, that formula turned `Earth (0.78,0.62,0.42)` into `(1.00,0.73,0.38)` -
      r-g = 0.27, g-b = 0.35, a saturated **orange**, i.e. the Earth trail read as *Fire*: the exact
      confusion the task existed to remove, introduced by the fix. Three habits, all from that:
      - **Prefer a colour operation whose result you can redo in your head.** `Color.Lerp(c, white, t)`
        is monotone and preserves each channel's ORDER, so "does Dark stay violet" is answerable by
        comparing two subtractions. `RGBToHSV -> clamp -> HSVToRGB` is not, and its failure is silent
        because the output is still a plausible colour.
      - **Before writing a colour formula, ask which schools are LOW-chroma**, because they are the
        only ones a brightness boost can re-hue. Fully saturated primaries survive almost anything;
        `Earth` and `Holy` are where a "brighter" pass does its damage. Then check the extreme cases
        explicitly, not the average one.
      - **A request about one drawn surface is not permission to restyle the palette it comes from.**
        "Make the physical trail white" was satisfiable by editing `SchoolColor(Physical)` - the
        smallest diff, and it would have recoloured every physical projectile body and impact as a side
        effect. The right shape was a NEW member on the look struct with the override inside its own
        resolver, which is rule 13's drawn-vs-gameplay split one level down. **Then prove which side
        landed**: physical trail white *and* physical body still gold is the read that shows the
        override went into the trail resolver.
    - Corollary for the instrument: **1js's PowerShell HSV helper printed a complete, plausible
      ten-school table and was wrong** - its own round-trip self-test (`RGB->HSV->RGB`) reported a
      worst-case error of **0.70**, and a hand calculation of one school disagreed with its row. The
      table was discarded and recomputed with scalars plus two passing controls. Per rule 7 a number
      that has never been shown able to fail is not a number, and per 1i2 its replacement is
      **unknown**, not the second instrument. **A palette is exactly where a plausible wrong answer is
      most expensive, because every cell looks like a colour.**
    - **A colour a look layer derives must be derived FROM the palette, never spelled beside it.**
      `TrailColor` reads `SchoolColor` and lerps; a hand-written ten-row trail table would be the
      third independently spelled copy of the school identity, and `SpellLook`'s own header records
      that the first two already drifted once (Dark was (0.70,0.55,1) in one and (0.85,0.45,1) in the
      other). Deriving from the table makes drift structurally impossible; a second table makes it
      merely unlikely.
    - **A "does it still work" check must name what ELSE the edit could have broken.** The obvious
      verification for "the trail is now the element colour" is casting a fire spell. The thing that
      could also have changed is the projectile *body* - same `SpellLook`, adjacent member - so the
      play-test reads **both** ends ("fire trail orange-red, fire body unchanged") or a pass is
      ambiguous between the fix landing and the fix overreaching.
    - **"A new visual axis needs the resolution pattern AND a way to SEE it."** 1f7 added `SkyRockStyle` because the falling rock was the last spell
      visual with no per-spell hook. Two habits, both from shipping it:
      - **Ask whether the axis is a CHOICE BETWEEN EQUALLY-VALID LOOKS or a STATEMENT ABOUT THE
        SPELL.** Impact and cast families jitter between looks that are all fine, so they get a school
        array and a deterministic `Pick()`. `SkyRockStyle` and `DisplayShape` are structural - a swarm
        instead of a boulder, or homing - so only an authored profile may grant them. 1f7 nearly put
        `Swarm` in the Fire school array because "the picker already exists"; that would have given
        **Meteor** a swarm on half its casts. `Inherit` resolving to one named default is the correct
        shape for an authored-only axis, not an oversight.
      - **Before adding a value to a look layer, check that every existing readout can render it.** The
        magic-model bench drew `CreateProjectileDisplay`, so the new Swarm body - belonging to a *Zone*
        spell, which has no projectile display - would have fallen through to the generic orb and the
        bench would have kept reporting the OLD model for the exact spell that changed. The fix was
        structural, not a workaround: `BuildRockBody` was split out of `FallRock` so a non-falling
        caller can mount the real body. **A visual that exists only inside a live cast has no
        acceptance readout**, and rule 7's "the audit must ask the question the proposal is about" is
        the same failure one layer out.
      - Corollary for the collision audit: adding an axis to a packed identity key **cannot** lower the
        distinct count (a new axis can only split a group, never merge one), so if every spell
        previously resolved the axis to the *same* default, no verdict can move. Say that reasoning
        out loud when you change the key - "the numbers should not have moved" is otherwise
        indistinguishable from "the numbers were never re-run".

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
    - **Ask what CREATES the thing you are keeping, not only what reads it - a dead type's only
      constructor can be the dead type you are deleting.** 1it removed `HousePlotPlacer` (planning
      Task 6.5 scaffolding, unreferenced) and in doing so discovered that `AddComponent<HousePlot>()`
      existed **only** inside it, while `HomeBuilder.TryBuild(HousePlot, ...)` already had zero
      callers: `World\Housing` was a dead *sub-tree*, not one orphan file. The direction that bites is
      the opposite of "producers outlive their consumer" - the **creator** is the orphan, so a
      reference count of 3 (`HomeBuilder` + `HousePlot` + the placer) reads as healthy while the
      whole chain is unreachable from any root. So for each deletion, walk **down** the graph too:
      for every type that *declares* an `AddComponent`/`new`/factory of a surviving type, ask whether
      that factory is itself reachable. A surviving type whose only creator is being deleted is a
      cascade decision, not a cleanup - and each hop opens new ones (`HousePlot` references
      `CraftingStation`, `HomeChest`, `FarmPlot`), which is exactly where an unaudited removal stops
      being a sweep and starts being a rewrite. 1it therefore deleted only what it had audited and
      **reported** the `World\Housing` sub-tree as the next candidate instead of following it.
- **A container type carries an ADDRESSING CONTRACT, and swapping it to match a signature transfers
      that contract silently.** 1jq's runtime trail got its third console error in a row -
      `ArgumentOutOfRangeException` on the first frame a trail drew - and it was **caused by the fix for
      the second one**. CS1503 said `Color32` was wrong, so the colour was retyped to `Color` end to end,
      which put `SetColors(List<Color>)` on the proven overload... by making the buffer a `List<Color>`
      instead of an array. But `new List<Color>(58)` takes a **Capacity**: the list is born with
      `Count == 0`, and `List<T>`'s indexer **setter** rejects any index `>= Count`. An array's `Length`
      is simultaneously its capacity and its last valid index + 1; a `List`'s is not. So `_cols[i * 2] = c`
      compiled cleanly - right overload, satisfied compiler, right type - and threw on the first write.
      Three habits:
      - **A fix can be correct as a COMPILE fix and still be wrong at RUNTIME.** CS0246 and CS1503 were
        compile-time; this one is not, so the same edit has to be judged twice, on two axes. "The
        compiler stopped complaining" is evidence about exactly one of them.
      - **Sweep the NEW axis, not the one you just swept.** The sweep after CS1503 ran on *type and
        overload existence* and found two more real problems. This error is not on that axis at all - it
        is **index validity**, invisible to a type-focused sweep and to `StaticChecks.ps1`, which
        reported 0 candidates on a file that throws on its first frame. When an error arrives from a
        direction the previous sweep did not look in, that is the axis to grep next.
      - **"Reserved" is not "present", and a slot nobody reads still has to exist.** The fill loop adds
        58 placeholder `Color.clear` entries whose value is deliberately irrelevant - the entries past
        the live segment count are referenced by no drawn triangle, because the index tail is zeroed to
        degenerate triples. But they must be *there*. That is rule 7's "an absent measurement and a
        measurement of zero are different" in a new dress: a cell that must exist, whose contents are
        never read, and whose absence is not a no-op.
- **An enum or field with no reader can still be a doc's promise - deleting it orphans the
      DOCUMENT, and that is a removal failure no compile reports.** 1it deleted the `HandUsage` enum
      (3 cases, no field on `WeaponData` used it) and found `game-design.md` listing "hand usage
      (single / dual / two-hand)" as a `WeaponData` shared field, with the doc's own §2208-2216
      saying wielding is governed by `Weight` + `StrengthRequirement`. The doc described a field that
      did not exist, and deleting the enum would have left it describing one that still does not. The
      enum's own header also cited §5.4, which is *Crafting* - the wielding section is §5.5. So when
      a deletion removes a name that appears in a design doc as an implemented field, **fix the doc
      in the same pass** (rule 2), and check the section number the comment cites still holds that
      heading. Rule 8's stale-comment rule, applied to prose instead of C#.
    - **A public event with zero subscribers is not a defect, and removing its invocations IS a
      behavioural edit.** `CombatAnimation` was the only subscriber of
      `CombatController.OnStateChanged`, which `CombatController` invokes at 7 sites. Deleting the
      subscriber leaves 7 `?.Invoke` calls on a null delegate - free, and a public extension point
      someone may subscribe from outside the tree. Stripping the event and its 7 invocations would be
      a second, unrelated change to a live combat file riding along in a "delete dead code" diff, so
      1it left both in place and recorded the zero-subscriber state in `PROGRESS.md` instead. Say
      which of the two you chose; a silent choice reads as an oversight.

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
   Rule 7 says measure before fixing a report you cannot see, and rule 8 says a *copy* of a fact rots
   where the original does not. 1jb was a pure code move - 18 static methods out of one file, 8 shape
   builders out of another - and it produced two failures that no existing rule covered:
   - **The two halves of a move are never compared to each other, and a line can be textually
     IDENTICAL in both places and still have to change.** `Destroy(col)` was correct inside
     `SpellCaster : MonoBehaviour` and stops resolving inside a `static class`, so the moved copy needs
     `Object.Destroy(col)`. A `git diff` cannot show this: the line is unchanged text, and the deletion
     hunk and the new-file hunk live in different files, so a reader diffing either one sees nothing.
     The same blind spot drops or duplicates a whole method with `braces` and `parens` still balanced.
     So **diff the moved body against the pre-move source**, comment- and whitespace-normalised, and
     **prove the comparator can fail** by mutating one line and watching it report the change. 1jb did
     both: 379 normalised lines, 0 differences, and a `0.38f` -> `0.39f` mutation that fired. Rule 3 is
     why this matters twice over - with no compiler, the comparator IS the compiler.
   - **A doc that records OWNERSHIP goes stale invisibly, because grep still finds the symbol.** This is
     rule 8's stale-comment bullet at documentation scale, and 1jb found the rot in a file 1ja had
     already shipped: `Magic/README.md` still sent the reader to `Models/WeaponModelBuilder.cs` for the
     four magic weapon models a whole task after 1ja moved them out of it, and still claimed "all twenty
     weapons" when the dispatch has **18**. Neither is a missing symbol, so no existence grep can catch
     either - the symbol is present and the reader is sent to the wrong file with full confidence. So
     after any move, grep for the **qualified** owner string (`SpellCaster.CreateProjectileDisplay`),
     never only the bare name: a bare-name grep passes on stale ownership *by construction*. This is the
     same reason no doc in this repo writes a line number.
   - Corollary on the same seam: **the moment a helper's output has to cross a class boundary, look for
the handoff.** `MagicImpactModelBuilder` returns its pieces, and the shape it used to hand back was
      three *parallel lists* (`_materials`/`_parts`/`_spins`) that a fade loop in another class indexed in
      lockstep - an invariant no compiler and no check enforced. That is rule 8's copy-rot in miniature:
      when an extraction forces an interface, return one record per piece (here `Part`, carrying transform +
      material + spin flag together) so the alignment cannot be violated at all.
    - **A `partial` -> standalone-class split orphans every unqualified sibling reference, and an R100
      rename diff is structurally blind to it.** 1ji is the instance: `75fd44d` moved
      `MapBuilder.PlayerModels.cs` to `Models/Player/PlayerModelBuilder.cs` and rewrote
      `public static partial class MapBuilder` to `public static class PlayerModelBuilder` - and git
      recorded the file as **R100, byte-identical**, because the body genuinely did not change. But a
      `partial` resolves bare names against its *siblings*, so all 86 unqualified `MakePart(...)` calls
      (and 3 `ActiveGender` reads) silently became CS0103 in a file that looked untouched. Rule 17's own
      comparator - diff the moved body against the pre-move source - is **satisfied perfectly by a file
      that no longer compiles**, so the one instrument built for moves cannot see this class of move at
      all. Three habits, all from fixing it:
      - **A rename that changes the class declaration is not a rename, it is a new class.** So sweep
        for **all** bare identifiers the file uses that resolve to the former host before fixing the one
        name on the console: 1ji's report was `MakePart` only, and `ActiveGender` was sitting behind it.
        Unity prints what you look at, not what is broken.
      - **The mirrored mistake is on the CALL SIDE, and a partial sweep is the same bug wearing the other
        hat.** 1ji fixed the moved *file*; 1jj found the call *sites* were never fixed. `75fd44d` rewrote
        `MapBuilder.BuildPlayerModel` -> `PlayerModelBuilder.BuildPlayerModel` at 10 sites in 10 files -
        and **touched** `CutsceneManager.Driving.cs` and `CutsceneManager.Helpers.cs` while changing **no
        `Build` line** in either, because those files hold the *seated* variants (`BuildSeatedPlayerModel`,
        `BuildSitPlayerModel`) and the sweep searched for the one name that had moved. Every
        `MapBuilder.Build*Model` reference is then CS0117, so **the sweep's unit is the moved class's whole
        public surface, not the symbol the console quoted** - and its denominator is *every* caller, not the
        files you happened to open. Two habits: enumerate the class's `public` members once and account for
        each one (here exactly 3, each with its own call-site count), and count the files the sweep rewrote
        against the files it *touched* - a file in the diff with no `Build` line changed is a gap someone
        should have caught in that same commit.
      - **Qualify with the former host (`MapBuilder.MakePart`), and never add a forwarder to the host
        when the host is `Legacy/`** - rule 18 makes that a read-only fence. Live -> legacy calls are
        the sanctioned direction, and the repo already had the convention (`SaveManager` and
        `UIManager.MainMenu` both write `MapBuilder.ActiveGender`).
      - **Two things fake a finding in that sweep, and both cost a wrong edit.** PowerShell's `-contains`
        is **case-insensitive**, so the file's own local `headScale` "matched" MapBuilder's NPC field
        `HeadScale`; and a **top-level type in the global namespace** - `public enum PlayerGender` sitting
        one line *outside* the class - resolves unprefixed from anywhere, so "declared in that file" is not
        "member of that class" (`PlayerGender` was flagged and needed no change). Finally, **accessibility
        decides whether qualification can work at all**: a `partial` can see its host's `private` members
        and a standalone class cannot, so qualifying one would trade CS0103 for CS0122. Check the
        modifier before editing, not after.

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
