# THINKING â€” Working Reasoning Log

This file stores the **raw reasoning trail** behind tough investigations: every hypothesis
considered, the evidence for/against it, dead ends, and why the surviving candidates survived. It is
deliberately more verbose and tentative than the other docs.

Authority order (do not confuse these):
- `game-design.md` â€” the durable design of record (what the game IS).
- `PROGRESS.md` â€” the per-task handoff summary (what was done + status + play-test checklist).
- `THINKING.md` â€” **this file**: the messy "how I reasoned it out", including guesses that turned out
  wrong. Never cite a hypothesis here as if it were implemented behavior.

When an investigation closes, keep its section but mark the verdict (confirmed / rejected / fixed by
`1xx`), so the same wrong paths aren't walked twice.

---

---

---

---

---

## 1iy. A file tree: the deliverable is the generator, not the tree

**OPEN until shipped; closed on commit.**

### H1 - "Write down a project file tree into a file."
**CONFIRMED as the goal, but the naive form of it is the trap.** The literal reading is "type the tree
into a markdown file". 1iu is the precedent against that: `Magic/README.md` and `Animation/README.md`
were hand-written navigation maps and **every symbol in both was wrong on the first pass** — the rest-pose
pair filed under `PlayerAnimator` instead of `WeaponAnimator`, a `HolsterPoint` that does not exist, and
`PlayerAnimator` described as owning idle/attack/dodge blends when it is a procedural walk/run component.
The failure is not carelessness; it is structural. A doc claims things about code, and no compiler,
analyzer or test in this repo reads a doc. So the doc is the only artifact here with **no feedback loop
at all**, and hand-maintaining one guarantees it drifts.

### H2 - "So add a test that keeps the tree honest."
**REJECTED as the primary move - it treats the symptom.** A checker over a hand-typed tree tells you when
it is stale, which means you now maintain both the tree and the checker, and the checker is a re-derivation
of the same filesystem. The stronger move is to remove the copy: `tools/Write-Tree.ps1` reads
`git ls-files` and writes `TREE.md`, so the file is *derived*, not *maintained*. It carries the commit it
saw and the command to rebuild it. A stale derived file is harmless and self-announcing; a stale
maintained file is indistinguishable from a correct one.

### H3 - "Include everything, it is only 1169 files."
**REJECTED - 632 of those are `.meta`, i.e. 54% of the repo is Unity importer bookkeeping.** A tree that
lists them is 2x longer, unreadable, and rots faster (every add/remove changes it twice). Collapsing them
and stating the count is strictly better. Then the same question recurses: `_ArtSource` (38),
`TextMesh Pro` (33), `ProjectSettings` (27) and `Resources` (26) are large and uninteresting, so they
collapse to `[N files]` - but then **`Assets/Scripts` (367) must still be readable**, which is what
`-AlwaysExpand` is for.

### H4 - "`-ExpandBelow 25` is a reasonable default."
**REJECTED once measured - and this is the interesting one.** First render collapsed `Assets/` (445
files) on the threshold, and because `Assets/` was collapsed, `Assets/Scripts` was never reached. The
force-list silently did nothing. A size heuristic that can hide a subtree is not a heuristic, it is a
filter on the thing you cared about, and it produced a *plausible-looking* 62-line file with no error and
no warning. Fix: expand any **ancestor** of a forced directory, so reaching a protected subtree is
unconditional. Worth noting the failure was invisible in the output - it read as a short, tidy document.

### H5 - "The output looks right, so it is right."
**REJECTED - and this is where the real lesson is.** My completeness check rebuilt paths from the
rendered indentation and diffed against `git ls-files`. It reported **success: 0 == 0**. It was
comparing two empty sets, because the regex had swallowed the `├`/`└` glyphs into the directory names.
A second version then reported two *false failures* by dropping the `Assets/` ancestor prefix. Only the
third version - which I deliberately fed a wrong count to confirm it printed `BAD` - produced a verdict
worth having. This is the same shape as rule 7's "a check that flags a false positive on the first file
you add it to is a check whose silence has stopped meaning anything", pointed at myself: **I had written
a green check for a verification, which is the one place a green check costs nothing to fake.** A check
has to be shown failing before its passing means anything.

### Verdict

`tools/Write-Tree.ps1` + `TREE.md` at commit `6e25792`: 537 non-`.meta` files described, `.meta` collapsed
by count, `Assets/Scripts` always expanded, four directories collapsed with counts that are verified
against git. Verified: 413 rendered leaves + 124 collapsed = 537, zero invented paths. No game code
touched, no build, no play-test. The tree is a convenience; the generator is the durable part, because it
is the version that cannot quietly become a lie.

---

## 1iz. "I don't see magic models" — the feature was never missing, only unnamed

**OPEN until the plan ships. Closed on the commit that adds `ARCHITECTURE.md`.**

### H1 - "Magic models are missing; build them."
**REJECTED on the first grep.** `BuildStaff`, `BuildHolyBook`, `BuildBoneWand` and `BuildControlOrb` all
exist, they have been shipping, and the magic weapons visibly render. They were never absent.

### H2 - "Then they're just hard to find; point at them."
**CONFIRMED, and it is the actual finding.** The four are **contiguous at L198–273** of the 379-line
`Models/WeaponModelBuilder.cs`, numbered `// 11.`–`// 14.` between a greatsword and a bard's lute. So
the report is not a missing feature, it is a **missing name**: nothing in the repo — no file, no folder,
no symbol — contains the word "magic" in connection with a model. `grep` can prove the code exists;
nothing in this toolchain can prove it is *findable*. Those need opposite fixes, and the request as
phrased ("I don't see them") points at the one that would have shipped nothing.

Worth stating plainly: the give-away is a **contiguous block with its own numbering** inside a
19-method file. A directory listing never shows that. A scroll does.

### H3 - "Move `Animation/` into `Models/` so models and animations sit together."
**REJECTED as a move, KEPT as a diagnosis.** Before naming a destination, measure what each file *is*.
`Models/MapBuilder/` is 10 partials of one class referenced by **40 files** — the most depended-on symbol
in the repo — and it builds `BuildCloud`, `BuildTornado`, `BuildCafe`, `BuildPoliceCar` and
`BuildPlayerHouse` next to `BuildPlayerModel`. So `Models/` is not "models", it is a procedural geometry
factory, and moving `Animation/` into it would make the ambiguity worse rather than fix it. Same shape as
1iw's request: *move these files, the premise is wrong.* The correct output is not a tidier folder, it is
the observation that this folder is three categories wearing one name.

Two smaller misplacements fell out of the same measurement: weapon **visuals** live under `Combat/`
(`WeaponRigBuilder`, `WeaponRigHost` decide where a sword sits in a hand), and the controller → model →
animation bridge is a **single 92-line partial**, `PlayerController.Animation.cs`, doing three separable
jobs (build the model, wire the animator, re-seat and pose weapon rigs).

### H4 - "Extract the magic models; the shared palette moves with them."
**REJECTED — and this is where a plausible plan was wrong.** My first draft said the 14-colour palette and
the `MakeBlock` helper "must move or become accessible". Reading `WeaponModelBuilder.cs` says otherwise:
the magic four use **11** of the 14 fields, and **the other fifteen weapons use the same fields**. The
palette cannot move. The only genuine blocker is `MakeBlock` being `private static` — and even that is
narrower than it looks, because it also reaches into `MapBuilder.CreateSolidMaterial`, so it was never a
self-contained helper to begin with. Stage 1 collapses from "untangle shared state" to "widen one
modifier, move 76 lines".

The generalisable part: **I wrote the dependency claim from the four methods I was looking at, and the
other fifteen methods falsified it.** A claim about what a helper *depends on* is only as good as the
number of callers you checked.

### H5 - "Do it all in one pass, it's only folders."
**REJECTED.** Stage 3 (`PlayerController.Animation.cs` → an `Avatar/PlayerAvatar` component) changes
signatures rather than paths, and with no compiler (rule 3) a signature error is invisible to review. It
also invalidates the paragraph in `Animation/README.md` that currently *justifies* keeping the partial
where it is — which has to be rewritten in the same commit, not left to rot. So the stages are ordered by
blast radius, each is one commit, and the path-only stages come first where review is actually evidence.

### Verdict

`ARCHITECTURE.md`: three measured category errors, a target layout split on *what decides the value*
(read input / write a transform / construct a mesh), and four migration stages. No code touched —
365 `.cs` and 365 `.cs.meta`, unchanged. The user asked to be able to edit the structure by hand; the
honouring of that is a document they can argue with, not a set of moves made on their behalf. The first
stage is the one that fixes the thing they actually reported.

## 1iw. "MapBuilder isn't used in the game" — a premise, measured before it was acted on

**OPEN until shipped; closed on commit.**

### H1 - "It takes up too much space in `Models`, so put it in a folder."
**CONFIRMED, and this half was checkable by counting.** 10 of `Models/`'s 17 `.cs` files, 5,153 lines,
59% of the folder, for one static partial class. Grouping is the right fix for that, and it is free:
C# is folder-agnostic, the class name is unchanged, and the one thing that *could* break a move - a
path-string reference (`AssetDatabase.LoadAssetAtPath`, an `.asmdef`, an editor tool) - grepped **0**.

### H2 - "And it isn't used in the game, so while we're here it could just go."
**REJECTED — and this is the half worth pushing back on.** Enumerating `MapBuilder.<symbol>` across
`Assets/Scripts` returned **~40 calling files**, including things that have nothing to do with the
legacy village:

- `RaceRig` / `PlayerController.Animation` / `PlayerAnimator` -> `BuildPlayerModel`. **The player model
  is this class.** Deleting it breaks the thing you are looking at.
- `SpellTornado` / `SpellBeam` / `SpellCaster.Cast` -> `BuildTornado`. A live spell body, and one of the
  display shapes 1ib gave a `SpellLook` axis to.
- `ChunkObject` -> `BuildTree`, `BuildStone`. This is the one that reframed the task: **the streamed
  terrain draws its foliage through a class filed under `Models/`.** I had been treating "under
  Models" as a proxy for "cosmetic, models-only", and that proxy was wrong by exactly one dependency
  edge, pointing at the most important caller in the codebase.
- `PlayerSitController`, `PetController`, `RandomEventManager`, `UIManager.RefreshWorldSignTexts`.

So the honest decomposition is not "used / unused" but **"used by the legacy layer" vs "used by the
new world"**, and those do not split along file boundaries: nine of the ten partials really are
village-only (`.Houses`, `.Mansion`, `.Restaurants`, `.Stores`, `.Police`, `.Vehicles`, `.NPCs`), but
`PlayerModels.cs` and `Nature.cs` are in the new world's path. Retiring the legacy layer would orphan
most of this class and still require keeping two partials. **Ask which member is unused, never whether
the class is** - the unit that turned out to be indivisible is the *class*, and the unit that would
actually be individually dead is the *partial*.

### H3 - "Since the premise was wrong, refuse the move."
**REJECTED — a wrong reason does not make a safe edit unsafe.** The request had two parts, one true
(files dominate the folder) and one false (unused). The true part is a pure win with zero call-site
edits, and refusing it would have cost the user the tidying they asked for while protecting nothing.
The move went ahead; the measurement went into `game-design.md` §9.4b, `AGENTS.md` rule 7 and `PROGRESS.md`
in the same pass. That ordering is the whole point: **the next reader of a tidy folder inherits it as
evidence for the deletion that didn't happen**, so the correction has to land while the move is being
explained, not in a follow-up commit.

### Verdict

10 partials grouped into `Models/MapBuilder/` with metas and GUIDs intact, 7 builders left in `Models/`,
zero code edits, `.cs` parity 365/365, 633 unique metas, 0 orphan metas, StaticChecks 0 candidates. The
stated reason for the request was false and is now documented in three places, so a later reader
proposing `rm -r Models/MapBuilder` meets the counter-evidence before the tidy folder.

## 1iv. Moving source art out of `Assets/`: the GUID survives, the reference does not

**OPEN until shipped; closed on commit.** Written while the moves were being scoped.

### H1 - "These four folders are unreferenced junk; move them to `_ArtSource/`."
**REJECTED as stated - and it is the finding, not a correction.** A GUID scan of the 4 folders
(59 GUIDs) against every `.unity/.prefab/.asset/.mat/.controller` returned **10 live referrers**. So
the premise was false for two of the four folders. This is rule 7's whole shape: a folder's *name* is
not evidence of what is inside it, and "34 MB of art" reads as dead weight until something counts the
references. The `.meta` travelling with each file keeps the GUID **stable**, which is exactly what
makes this dangerous - a stable GUID outside `Assets/` is a reference Unity cannot resolve, so nothing
errors and the texture is simply absent at runtime.

### H2 - "10 referrers means don't move any of it."
**REJECTED - it asked whether a referrer exists, not whether it is live.** 8 of the 10 were
`0 (N).unity` scenes inside `Assets/_Recovery/`, a gitignored folder 1hz had already abandoned and
which was on this task's own delete list. The real constraint was **one** file,
`Scenes/SampleScene.unity`, the only scene in `EditorBuildSettings`. Ten referrers would have meant
"don't move anything"; one live referrer meant "move the other 9 and report the constraint". So the
question is never "is this referenced" but "by something that can still be opened".

### H3 - "`Assets/xoanvnmexel/` is junk by name."
**REJECTED - it is the live UI font.** `Resources/VietPixel.asset` carries
`m_SourceFontFileGUID: 2632940b8efd5fc4087c070078a1a871`, which is that folder's `.ttf`;
`UiAssetCache` does `Resources.Load<TMP_FontAsset>("VietPixel")` and TMP's own default font asset
references it too. Moving the folder would have removed every glyph in the game. Confirmed by
re-resolving the GUID after the move rather than by trusting the earlier scan. The general rule, now
in `AGENTS.md`: **a folder name is not evidence of what a file is for** - an unpronounceable pack
name is not a synonym for unreferenced, and the reference count is the only thing that tells them
apart. The same scan also caught 3 textures referenced by `SampleScene.unity` as the named fields
`FieldTexture`, `FertilizerTexture` and `PeashooterSeedTexture` - so `Assets/texture/` going from 12
files to 3 was the finding, not a rounding error.

### H3b - "The three textures are referenced 7717 / 5292 / 7374 times, so they are load-bearing."
**REJECTED - the count was fabricated by the measuring command, and it nearly became a finding.**
`"$text".Split($guid)` splits on every **character** of the GUID, so it counted hex digits in the
file rather than the GUID, and returned a different huge number per texture (which is exactly why it
looked plausible - three textures, three different numbers). The tell was arithmetic available before
the scan ever ran: `SampleScene.unity` is 29,003 characters, and a 32-char GUID cannot occur 7717 times
in 29 KB. Recounted with `[regex]::Matches(..., [regex]::Escape($g))` and again with an `IndexOf`
walk: **1 occurrence each**, with mixed-char bogus controls reading 0. Those inflated numbers had
already reached `game-design.md`, `AGENTS.md`, `PROGRESS.md` and the drafted commit message.
**The verdict survived; the number did not.** That is the good case and the bad case at once - the
decision was right (those textures are live) on evidence that was wrong, which is the situation where a
reader is most likely to keep the conclusion and discard the correction. Note the shape of the fix: the
control has to be a GUID that is *absent*, and it has to be printed, because "0" is the only output
that proves a counter can fail.

### H4 - "`dirt_texture.png` duplicates `Resources/texture/dirt_texture.png`, so dedupe it."
**REJECTED - identical content is not one owner.** Both copies are **live**: the scene holds one GUID
as a named field, `Resources.Load` resolves the other. Merging them means hand-editing a serialized
GUID in a scene file, which under rule 3 is exactly the edit that cannot be verified without opening the
editor. So the pair stayed and the doc says it is a deliberate duplicate. Contrast with `grass_blade` /
`leaves_texture` / `wood_texture`: byte-identical, *and* zero referrers, so the twin in `Resources` is
unambiguously the live copy and the `Assets/texture` one was deleted rather than archived. **"Identical"
is not a verdict; "identical AND unreferenced" is.**

### H5 - "My first reference scan is fine - it reported 63 hits."
**REJECTED - the scan was broken twice, and both failures pointed at a *finding*.**
`Get-ChildItem -Recurse -Include *.unity,*.prefab` **does not filter** unless the path ends in `\*` or
you pass `-Filter`, so it returned 1131 files including every `.meta` - and a `.meta` contains its own
GUID, so 63 of the "hits" were the files matching themselves. The corrected scan (filter by extension
*after* collecting) found 10. Second bug, same session: `.Split("guid:")[1]` on a line already extracts
an empty string for the same char-split reason as H3b, which is how an empty "GUID" came to be counted
29004 times against a 29,003-character file - the arithmetic again, again available in advance. Both are
rule 7's "a check nobody has seen fail is not a check" wearing a pass as a disguise: **an unmeasured
reference count is not a small number, it is an unknown.** Now written into rule 7 with the two
mechanical fixes.

### H6 - "A folder leaving `Assets/` takes its folder `.meta` with it."
**REJECTED on the target side.** Each moved *asset*'s meta travelled - that is what keeps the GUID
stable if the folder is ever dropped back into `Assets/`. But the moved *folders'* own metas
(`Assets/model.meta`, `Assets/UI component.meta`) were deleted, because a folder meta is the importer
record for a folder Unity can no longer see; carrying it to `_ArtSource/` would leave a Unity asset
record for a non-Unity directory. Deleting the `_Recovery` *folder* created a new orphan
(`Assets/_Recovery.meta`), which the post-move orphan sweep caught - the class of residue a deletion
leaves behind when the deleted thing had a sibling, and another instance of rule 14's "a save key
outlives its builder". Verified afterwards in both directions: every tracked non-`.cs` meta under
`Assets/` has a folder behind it, every folder under `Assets/Scripts` has a meta, and all 639 tracked
metas hold 639 distinct GUIDs.

### Verdict

90 files / 34 MB out of `Assets/`, 3 dead duplicates + 3 root clutter files deleted, 52 untracked
recovery files removed from disk, and **four files left in place because measurement said they are
load-bearing**. `.cs`/`.cs.meta` 365/365 untouched, StaticChecks 0 candidates. The plan I started from
("move all four folders out") was wrong in a way that would have shipped a game with no fonts and three
missing terrain textures, and the only reason it did not is that the reference scan ran before the
move. Rule 7's first line - measure before you fix, and let the readout name the mechanism - is what
caught it. The second-order lesson is H3b: the measurement that saved the textures was itself broken,
and it was the *magnitude check* rather than the tool that caught it, so "the scan said 63" and "the
scan said 7717" were both the scan's opinion, not a fact about the repo.

## 1iu. Moving 34 files for "findability": what could go wrong that a grep for the class name would not

**OPEN until shipped; closed on commit.** Written while the moves and READMEs were in flight.

### H1 - "A folder move cannot break this project, so a grep for the class name is the whole check."
**CONFIRMED, and it is why this task was cheap.** Searched `Assets\Scripts` for `namespace `,
`.asmdef`, `SerializeReference`, `Assembly-CSharp` and string-keyed `GetComponent`/`AddComponent`
lookups: zero hits. No namespaces, one implicit assembly, no reflection. So the mechanical risk of a
pure move is **zero**, and 34 files moved with no signature edits. Worth writing down: the check that
*matters* is not "does it still compile" but "did every `.meta` travel", because a lost `.meta` is a
silent GUID re-issue rather than an error.

### H2 - "New folders just need `git mv`; Unity will generate the folder metas."
**REJECTED, and the project's own tree says so.** `git ls-files` over `Assets/Scripts` shows 63
non-`.cs` metas that are *all* folder metas - every folder is tracked. Leaving 8 new ones untracked
means the next person to open the project gets 8 Unity-assigned GUIDs nobody else has. Authored 8
folder metas + 2 README metas with fresh GUIDs, then checked **both** directions: every one of the 71
on-disk folders has a meta, and every tracked non-`.cs` meta has a folder behind it. That second
direction is the one that found the real bug (H4).

### H3 - "Every file is accounted for, so `Combat/Effects` is fully redistributed."
**CONFIRMED, but only after counting *files* and not folders.** 19 pre-1it, minus 2 deleted by 1it =
17, and the move list accounts for exactly 17 (1 + 3 + 1 + 4 + 6 + 2). The check that would have lied:
`-Recurse -File | Measure` on the *parent* (`Combat\`) is non-zero while `Effects\` is empty, so "is
there anything left under Combat" answers *no* when the answer is *yes*.

### H4 - "The orphan `Assets/Scripts/Audio.meta` was already handled."
**REJECTED - this is the one real find.** After 1it I listed every tracked non-`.cs` meta and asked
whether a folder of that name exists. `Audio.meta` had none. This is a residue of the 1is isolation
dance: `Audio.meta` was *already* deleted on disk before this session began, and I **restored** it to
keep an unrelated WIP out of the 1is commit - right for that commit, and it left a tracked meta
pointing at a folder that does not exist. Generalisable, and it is the mirror image of 1ii's rule:
*"is this folder empty" cannot ask about a folder that is absent.* An orphan meta is invisible to every
emptiness test; only the reverse question finds it. Removed in 1iu, and 1it's folder count corrected
17 -> 18, because `Player/Creation` became empty in that same pass and the entry had omitted it.

### H5 - "`WeaponModelBuilder` builds the magic models, so it belongs in `Magic/`."
**REJECTED - the premise is false.** It builds **all twenty** weapons; four are magic
(`BuildStaff`/`BuildHolyBook`/`BuildBoneWand`/`BuildControlOrb`). A path move cannot separate them,
because the magic bodies are methods inside one dispatcher; moving the file would cost `Models\` its
single source of truth for every weapon shape. Same question killed moving `MagicWeaponBehavior` to
`Magic\`: it is a `WeaponCategory` driven by `WeaponData` - a *weapon* fact, not a spell-pipeline fact.
The rule that decided all three: **group by what decides the behaviour, not by which folder a reader
would guess.** A near-miss in the other direction: `MagicTestMatrix` + `MagicWheelUI` *did* move to
`Magic\Ui\` even though they only *read* spells, because they are the pipeline's own readouts - it is
the writer relationship that counts.

### H6 - "`PlayerController.Animation.cs` is animation code, so it goes in `Animation/`."
**REJECTED.** It is a `partial class PlayerController` (model build/reload, race-change, draw/stow,
arm-chain checks). Splitting a class's partials away from the class costs the reader more than it
saves, because every other partial must stay adjacent for the same reason. Test recorded: group
**independent components** (all three animators stand alone); leave a class's partials with their
class. The animators group for exactly that reason.

### H7 - "The READMEs are prose, so they will be right."
**REJECTED, four times over.** Grepping every symbol *after* writing found: `SyncRestFromIdle` /
`RestoreAuthoredRest` filed under `PlayerAnimator` when rule 16 is about `WeaponAnimator`;
`HolsterPoint` named as `WeaponStowAnimator`'s anchor when **no such symbol exists** (the field is
`AnchorParent`); `PlayerAnimator` described as owning attack/dodge/swing state when it is a
*procedural* walk/run component; and two invented section refs (`§3.13` does not exist; the bench is
`§3.8.3`, not `§2.2`). Each is the more expensive kind of doc bug: the name either does not exist, or
exists and is filed under the **wrong owner**, so the reader is sent somewhere plausible with full
confidence. Prose about ownership is a copy of the codebase, and rule 8's stale-comment rule applies to
prose. Hence the three mechanical habits now in rule 8: grep each identifier after writing, **never
write a line number**, and never re-derive a private formula or dispatch table into a doc.

### H8 - "I verified the READMEs' symbols with a recursive grep."
**REJECTED - the verification was itself the bug, and it nearly shipped.** The first pass used
`Select-String -Path 'Assets\Scripts\*.cs','Assets\Scripts\**\*.cs'` and reported **9 of 22 symbols
MISSING**, including `MagicWeaponBehavior`, `FallRock` and `BuildProjectileBody`. `**` in a PowerShell
`-Path` glob descends exactly **one** level, so files 2-3 levels deep (`Combat\Weapons\`, `Magic\Fx\`)
were invisible while call sites 1 level deep were found. That combination yields a scan reporting 29
hits for `Resolve(` and 0 for `HolsterPoint` on the same tree - internally impossible, and the only
reason to notice. This is rule 7's "a green check nobody has seen fail is not a check" running in the
direction that *looks like a finding*: absences are evidence only if the same scan has demonstrably
found presences. Fixed by building the list once with `Get-ChildItem -Recurse` (`scanned 365 .cs
files`, sanity-checked against the `.cs.meta` count) - which is also why `StaticChecks.ps1` keeps its
own `$files` list instead of a glob. New rule 7 bullet records it.

### Verdict

34 files moved, no behaviour change, `.cs`/`.cs.meta` 365/365, 0 static-check candidates, zero stale
path strings outside deliberately-preserved history. The only behaviour-adjacent change is removing
the orphan `Audio.meta`. Both deliverables needed four factual and two section-reference corrections
before they were honest - **all** caught by grepping after writing, none by rereading. They are now
symbol-named and line-number-free by construction.

## 1it - what is actually dead, and in which direction (SHIPPED)

**Verdict: confirmed.** Seven whole files and two declarations were unreachable and are deleted; two
further "dead-looking" symbols were audited and **deliberately kept**; and one deletion was left
unfinished on purpose because its blast radius had not been measured.

### The scan, and the two ways it lied

Hypothesis: reference counting over `Assets\Scripts` identifies dead code. Confirmed - with two
traps found first, both of which had produced *false positives* before I trusted a number:

1. **A one-level glob.** `Select-String -Path "Assets\Scripts\**\*.cs"` does **not** recurse in
   PowerShell - `**` behaves like a single `*` there. My first scan reported `PlayerController`,
   `NewWorldSystems`, `CharacterCreationUI`, `WorldBuilder`, `UIManager` and `MagicWheelUI` as
   *unreferenced*, which is obviously wrong for a project's entry points. Had I trusted it I would
   have deleted the game's wiring. `Get-ChildItem -Recurse` fixed it.
2. **Substring matching.** `SimpleMatch` for `CharacterCreation` hits `CharacterCreationUI` (7 hits in
   4 files, all false). Word-bounded `\bName\b` drops it to 1 hit - the declaration. This is the same
   trap rule 14 records about wild-carded Vietnamese text in `Localization` keys.

A third one I nearly shipped: the O(n^2) reference scan timed out at 120 s. I did not raise the
timeout and re-run the slow version; I cached each file's text in a hashtable first, which turned a
multi-minute scan into a few seconds. Worth remembering that "the tool timed out" and "the answer is
unknown" are different states.

### Which number actually decides deadness

The first corrected pass counted *all* occurrences per type and reported `Total <= 2`, which read as
"nine dead". But `Total = 2` is ambiguous: it is both "declaration + one real use" (LIVE) and
"declaration + a mention in a doc comment" (DEAD). Re-running with the declaring file's own count
subtracted left a clean discriminator: **`Total = 1`**, i.e. the declaration is the only occurrence
anywhere. That is the only figure that means dead. Every `Total = 2` row I had worried about
(`CombatFeedback`, `ElementSignatureStatus`, all 20 enemy scripts, the `RaceTauntEffect` family) was
live.

### The direction I had not planned to check

The brief was "delete the dead files", and the obvious audit is *who reads this*. Grepping that way
finds the call sites and cannot find **producers** - code that existed only to feed the deleted
system, which is perfectly correct on its own. Two checks in the other direction, neither of which a
reference count would have flagged:

- **`HousePlot`.** Three types mention it (`HousePlot`, `HomeBuilder`, `HousePlotPlacer`) - a healthy
  count. But `AddComponent<HousePlot>()` existed *only inside the placer I was deleting*, and
  `HomeBuilder.TryBuild(HousePlot, ...)` already had zero callers. So `World\Housing` is a dead
  sub-tree, not one orphan file, and the reference count of 3 was measuring a closed loop.
  **Rejected** the cascade: `HousePlot` references `CraftingStation`, `HomeChest` and `FarmPlot`, and
  I have not audited who creates *those*. Rule 14's own warning is that an unaudited removal stops
  being a sweep and starts being a rewrite. Reported as the next candidate instead.
- **`CombatController.OnStateChanged`.** `CombatAnimation` was the only subscriber, so deleting it
  leaves a public event with 7 `?.Invoke` sites and nobody listening. Tempting to strip both
  together. **Rejected**: it is a public extension point, the invoke is free, and removing it is a
  behavioural edit to a live combat file that has nothing to do with a dead-code sweep. Recorded the
  zero-subscriber state in `PROGRESS.md` instead, because a code comment saying "nobody subscribes"
  rots the moment someone does.

### Two "dead" symbols that survived the audit

- **`RaceSpellEffect`** (in `RaceEffect.cs`): unreferenced, but one of ten `IRaceEffect`
  implementations. It is the only natural slot for "a racial ability casts a spell" - the mechanism
  a future author would look for. **Kept.** An unused member of a live polymorphic family is a
  different object from an orphaned file, and "delete all unused" would have quietly removed the
  extension point.
- **`HandUsage`** (in `WeaponData.cs`): also unreferenced, and *this* one is a genuine leftover - no
  field on `WeaponData` uses it, and wielding is really governed by `Weight` +
  `StrengthRequirement`. But deleting it exposed the failure that decided the task's shape:
  `game-design.md:1487` listed "hand usage (single / dual / two-hand)" as a `WeaponData` field. The
  doc described a field that did not exist, and after the deletion it would have described one that
  still does not. A missing `case` never fails to compile and an over-documented field never fails to
  compile either; both just quietly make the next reader size a decision on something false.

### The `_Archived/` README as a worked example of a rotting doc

Four claims, all false, each checkable in one grep: `CutsceneManager` is live in `Scripts\Cutscenes\`
(11 files); `WorldBuilder` is live in `Scripts\World\`; `QuestManager`/`RandomEventManager` are live
in `Scripts\Quests\`; and `EnemyController` was said to live in `Scripts/Combat/AI/` - **the folder
that had been empty all along**, which is how I found it in the empty-folder sweep in the first
place. The archive's one true claim (a `RemoveEndings` shim) describes a flag on a live class with
no relationship to the archive. A README is the one document nobody re-reads *because* it describes
something nobody touches any more.

### What I could not establish here

No build and no play-test (rule 3), so "dead" means *unreachable by every mechanism this project
actually uses* - direct reference, `AddComponent`, serialized field, scene GUID, `Resources.Load`,
reflection, string-keyed lookup. I verified the last three are absent project-wide (zero
`[SerializeReference]`, zero `Type.GetType`, zero string `AddComponent`/`GetComponent`, zero
`Shader.Find`/`Resources.Load` by type name), and that only 11 project scripts are scene-referenced.
What I cannot rule out is an external tool or a future asset authored in the Editor that names one of
these types; the honest statement is "unreachable from any root in this repository", not "dead".

---

## 1ir - the meteor line grows a channel and a familiar (OPEN: awaiting play-test)

**The request was "remove comet and asteroid, add a flamethrower and a continuous fireball", which is
four words of content and three whole mechanics. Most of the work was not the content - it was noticing
that two visual axes had exactly one user each, and that the agreed plan and the code disagreed about
where a familiar's orb sits. Both are recorded here because neither is visible in the diff.**

### H1 - the cone can be built by fanning the existing single capsule. PARTLY CONFIRMED, then rejected.

The obvious implementation is "reuse `OverlapCapsule`, loop 7 angles". Confirmed for collision - that
is what shipped - but it hid two things I would not have found by reading only the loop.

1. **A fanned capsule overlaps itself at the mouth.** Seven rays at 44 degrees across an 11 m beam put
   neighbouring capsules ~1.5 m apart at the tip while each has radius 2.4, so an enemy standing between
   two rays is hit by both. Not a visual artefact - it is up to **double damage** for standing in a
   gap, and it is invisible because nothing reports "hit twice". Hence the per-tick `HashSet` on
   `Transform.root`. Rule 8: dedupe on the OUTCOME (after a real hit), not on first contact - marking on
   contact would let a decoration collider consume its own enemy, which the line path never did because
   it tested every collider independently.
2. **The heal branch was the hole in that dedupe.** I first wrote `TickCollider` returning `bool
   damaged`, and returned `false` from the heal branch - so a healing root never entered the seen set
   and got healed once per capsule. Found by rereading my own contract, not by any check. The fix is a
   three-state enum: the walk must stop re-touching a root after the first effect of ANY kind, while
   only damage may spawn the once-per-tick flash. No shipped cone heals, so the bug was free to ship -
   which is exactly the shape of bug rule 12's "a check that flags nothing taught me nothing" warns about
   in the check domain.

### H2 - the cone visual should be positioned like the line visual (position/rotation). REJECTED.

The line builds its body with `position`/`rotation` (world) and I copied that. Then I checked whether
the same builder could be mounted on the QA bench's model root - and it could not: world positioning
ignores the parent, so every cone would have appeared at the **world origin**, not on its pedestal. The
bench was going to be the thing that caught it, which is the 1f7 argument in reverse: the fix is that the
builder works in the parent's **local** frame so one builder serves both. `_coneHalfRad` fans around
`Vector3.forward` (local) at build time and around `Direction` (world, then
`InverseTransformDirection`d) per frame, and the two agree for any parent because the beam transform's
rotation is `LookRotation(fwd)`. The general lesson: *"reuse the sibling's code"* is not a reason, it is
a place where the sibling's coordinate convention gets inherited unexamined.*

### H3 - Continuous Fireball wants a new summon lifetime that does NOT scale with charge. REJECTED by the user.

I proposed a separate `ChargeDurationBonus` and a fixed `Duration = 12s`, on the reasoning that a
duration multiplier is one more knob and the size ladder already exists. The user's answer was to reuse
the size ladder for **both**: `DurationScale(charge) => SizeScale(charge)`. I implemented that and did
not add the field I had proposed. Worth recording *why* it is defensible rather than merely accepted:
the familiar's body and its lifetime are one thing to the player ("how long is this thing going to be
*mine*"), and splitting them across two ladders produces a charge that grows the body but not the
duration - which reads as a bug. 17.6s at full charge, ~44 bolts.

**Follow-up (post-`824952f`): I made this ladder `static`, which does not compile.** `ChargeSizeBonus`
is a public *instance* field, so `public static float SizeScale(float charge) => 1f + charge *
ChargeSizeBonus;` is CS0120. Both ladders are now instance methods.

What I got wrong was not the static keyword - it was the sentence I wrote next to it: *"It is static
because it never read instance state."* I asserted a fact about the code while writing the code that
contradicted it, so the comment was born stale rather than becoming stale. Review could not see it
either, and here is the mechanism worth generalising:

- Every caller **inside** `SpellCaster` sits in an instance method (`Execute` reads
  `transform.position`), so unqualified `SizeScale(charge)` resolves and looks correct.
- `SizeScale` and its only input live on the *same class*, so the declaration reads as self-consistent.
- `StaticChecks.ps1` reported 0 candidates. Check 4 is CS0165 (unassigned locals), check 5 is cross-case
  locals, check 7 is depth-0 members. **None of them asks whether a static member reads instance state**,
  because that is a type question, not a brace/paren/flow question. Rule 3's own warning applies with
  full force: a green check I have never seen *fail* on this failure mode is not a check.

So the whole class of error is only visible at **class-qualified call sites** - `Type.Member(...)` -
because that is the one spelling where static-ness is load-bearing, and because the preview was the
only caller outside the class. Hypothesis I did not run and should have: after adding any `static`
member, grep for the qualified spelling of that member across the project. One command, and it is the
only thing that would have caught this.

The fix itself is uninteresting; the tell was not. Second-order note: I also left a play-test item
saying the charged familiar's "bolt size" should grow, which contradicts H4 three sections below -
the bolts are deliberately fixed at 1.6 m. The play-test list is prose nobody diffs, and it had
already inherited the symmetry assumption that H4 rejected on evidence.

### H4 - the summon's burst radius should scale with the charge. REJECTED after tracing the call sites.

My first instinct was symmetry: the familiar grows 2.2x, so its 1.6 m bolt burst should too. Then I
traced the only two `SpellEffect.Initialize` call sites: `SpellCaster.Projectiles.cs:44` passes the
player's `sizeScale`, and `SpellSummon.cs:166` passes a hard-coded `1f`. So *every* summon bolt in the
game - Ember Effigy included - has a fixed burst, and had that forever. Changing it would have been a
**balance change to an existing spell** disguised as a consistency fix. So the multiplier stays in
`SpellEffect.SplashRadius` (where it is correct and inert for this spell) and the summon keeps `1f`,
with both sides stating why. Rule 8's "name where each input's ladder starts": `BoltSplashRadius` and
`Radius` are authored metres on the same ladder, but the thing that decides whether the ladder applies
at all is the `radiusMult` argument, and no grep of the field name would have revealed it - I had to
follow the constructor.

### H5 - a feature that removes a look axis is a removal task (rule 14). CONFIRMED, and it had a third residue class.

Removing Swarm and Ember Streak felt like two deletions. Rule 14 names three residue classes and this
was all three at once:

- **Producers:** `SkillFx.BuildRockBody`'s `style` parameter became **inert** (both surviving values draw
  the same boulder). Kept deliberately - it is the seam a future sky-rock style grows through - but that
  is only defensible if the parameter says so, so the doc comment now states that it is inert and why.
  A silently-unused parameter is the residue that greps clean and reads as a forgotten branch.
- **Save keys:** the look axis is packed into the F4 identity key, so deleting the enum value shifts
  bits. I first wrote that the `Swarm` slot should be **kept** as a migration guard, then asked what a
  save file can actually hold — and the answer is nothing: looks are authored in `SkillCatalog` and are
  never serialized, while `SaveManager` persists only `learnedSkills` and `skillLevelsJson`. There was
  no migration to guard, so reserving a value nothing can reference would have been a decision made
  from a half-checked premise. The enum value, the body and the branch are all deleted, and because the
  key packs the **raw** value at bits 24+, dropping a value no spell used cannot split or merge a
  group — so `172 / 172 / 0` is untouched, which is the same "adding an axis cannot lower the distinct
  count" argument 1f7 made, run in reverse.
- **Narrative:** four doc lines and two stale bench comments still described a swarm that no longer
  exists, and one of them ("Asteroid's Swarm since 1f7") sat inside the paragraph explaining how the
  bench works. None of that can fail a compile. This is the class rule 8's comment rule covers and no
  tool in the repo reads.

### H6 - both new deliveries would show the generic orb on the bench. CONFIRMED before shipping, which is the point.

`NewWorldTestGround` mounted `CreateProjectileDisplay` for anything that was not a falling rock - and
`CreateProjectileDisplay` has no answer for a Beam or a familiar. Both new spells would have been
invisible to the one surface built to let you look at them, and the F4 readout would have gone on
reporting "unchanged" for exactly the two deliveries 1ir changed. This is 1f7's finding repeated on a
different pair, and the confirmation is that I caught it **while writing the bench branch, not after a
user report** - which is the only version of this check that is worth anything. Both branches mount the
**live** builders (`SpellBeam.BuildConeVisual`; the summon circle), and `ConeMouthFraction` is public so
the bench cannot restate the ratio and rot when the cone geometry changes.

### H7 - a task id is a copy written last. CHECKED (rule 8's third bullet).

Enumerated `PROGRESS.md`'s `## 1xx` headings before assigning `1ir`, since `1io` and `1im` are both real
shipped tasks and a duplicate would read as an ordinary reference. Recorded here because the check is
the *only* part of this task whose failure mode is invisible to grep, review and every static check.

### Left open

- Whether the familiar's orb belongs at the circle's **centre** (shipped, with a reason) or its rim
  (the agreed plan). Deliberate, stated in `PROGRESS.md`, and the one place code and plan differ.
- Whether `BoltSplashRadius` should ever scale for summons - a balance question about Ember Effigy as
  much as about Continuous Fireball, so it belongs in a play-test, not in this task.
- Every number in this section is review-derived. Rule 3 bars a build, so the cone's overlap arithmetic,
  the 44-bolt count and the 17.6 s lifetime are all arithmetic on the code, not observations.

---

## 1iq - the aiming cone belongs to the bow alone (OPEN: awaiting play-test)

**The request was small ("the projectile cone should only apply to bow") and carried a stated reason
("its the only one need charging to increase accuracy"). That reason turned out to be false, and
checking it is most of the work. Had I taken the reason as given, I would have shipped a bow-only cone
that still lies.**

### H1 - the cone and the ray are one shape, so hiding the cone hides the aim aid. REJECTED.

First instinct: dropping the cone from magic/throwing-hammer would leave projectile spells with no aim
feedback at all, so I nearly gated `HidePathPreview()` for them instead of passing a zero angle. Read
`ProjectilePathPreview.Apply` before editing: everything derives from
`narrow = Clamp01(1 - halfAngle / 45°)`, so `halfAngle = 0` is not a special case - it is the existing
collapse path taken to its end. Rings: alpha `Lerp(0.55,0,1)=0`, width `Lerp(0.06,0,1)=0`. Ray: alpha
`Lerp(0.12,0.95,1)=0.95`. So `spreadDeg = 0` gives exactly ray-only, and cone/ray become two claims a
caller can make independently. Confirmed by arithmetic on the code, not by running it (rule 3).

### H2 - "projectile magic" here means spells whose accuracy varies with charge. REJECTED.

The reason I was handed says the cone expresses charge-improved accuracy, so the natural reading was
"whoever has charge-dependent accuracy". Grepped `insideUnitSphere` under `Spell*.cs`: **zero matches**.
Nothing in the spell path ever offsets the fire direction. Magic has no spread whatsoever, so the old
`8° × (1-charge)` fan was drawing outcomes the game does not have. Magic loses the cone because the
cone is *false* there, not because magic is imprecise.

### H3 - charge improves the shot's accuracy for the bow, as stated. **REJECTED - the premise is wrong.**

Read `RangedWeaponBehavior.BeginAttack` rather than trusting the doc comment on
`ShowRangedPathPreview`:

```
float accuracy = 1f + Stats.GetStat(Dexterity) * Data.AccuracyFromDex;
Vector3 aimed = ApplySpread(dir, Mathf.Clamp01(1f / Mathf.Max(accuracy, 0.01f)));
```

`charge` is **not in that expression**. `charge` feeds damage (x2.5), speed (x1.5), lifetime (x2) and
reach (x2) - four scalars - and not the spread. So `spread * (1f - c)` in the preview claims a
tightening the fire path never performs.

### H4 - so at least the base spread is meaningful and Dexterity tightens it. **REJECTED, and worse.**

`AccuracyFromDex` defaults to `0` (`WeaponData.cs:74`) and grep found only three references total: the
declaration and two *readers*. `WeaponCatalog.Make()` - the sole builder for both `longbow` and
`throwing_hammer` - has no parameter for it. So `accuracy = 1 + Dex*0 = 1` and
`spread = Clamp01(1/1) = 1.0`, i.e. `ApplySpread` adds `Random.insideUnitSphere * 0.15` at **every**
charge level. The preview's cone starts at `atan(0.15) = 8.53°` (maximum) and narrows to 0° at full
charge, while the actual shot stays at 8.53°-equivalent scatter forever. Dexterity's documented "ranged
accuracy" role (`game-design.md` §3.4, `RangedAccuracy = 1 + Dexterity × k_racc`) is **inert**.

Consequence: scoping the cone to the bow does not make the bow's cone true. It is the *same* lie, now
confined to the one weapon that was supposed to justify it.

### H5 - then fix it here, since I am already in the file. REJECTED - deliberately not done.

Tempting and cheap: pass `charge` into `ApplySpread`, and set a non-zero `AccuracyFromDex` on the
longbow. Both change real hit outcomes - that is a gameplay change (damage feel, encounter
difficulty, ranged viability) wearing a visual task's clothes, and the user's request was about *which
weapon draws a cone*. Doing it silently would also destroy the distinction between "I scoped a readout"
and "I changed ranged combat", and the second claim would ride along in a commit titled the first.
Shipped as a **reported finding** instead, with the numbers, in `PROGRESS.md` and `game-design.md`.

This is AGENTS rule 7's "drawn and flush are separate properties" one layer out: the cone was the
*drawn* claim, and no audit had ever compared it against the *shot*. Also rule 13's "ask what would
catch the bug if this value were wrong" - the answer here was "nothing", which is why it survived.

### H6 - gate on the weapon id (`id == "longbow"`). REJECTED as a second spelling.

Rule 8: a hardcoded id is a copy of identity that rots on a rename, and this repo has already been
burned by that (`craterCapR` vs the documented `CraterCapRadius` in 1f3). Instead gated on
`AmmoItemId != null`, which is *semantic* - "consumes a drawn projectile" - and hands a future crossbow
the cone with no second edit. **Stated its cost in the comment and in `game-design.md`:** that gate
means "consumes ammo", so a future ammunition firearm inherits a charge-narrowing cone it has no
mechanic for. A copy whose failure mode is nameable is fine; an unnameable one is not.

### H7 - the magic branch should also gain charge-scaled reach, since the cone used to shrink. REJECTED.

Mid-edit I "improved" the magic ray's reach by multiplying by `Lerp(1, 2, charge)`, reasoning that the
old `8° × (1-c)` cone implied charge lengthened flight. Nothing in `SpellEffect` says charge extends
spell flight, and I had not verified it - so this was rule 8's exact failure (re-deriving another
component's private formula as a second spelling, then scaling it by a guess). **Caught it on reread of
my own diff and reverted it before committing.** The preview's job is to mirror what the spell actually
flies; if charge ever does extend spell flight, that belongs in `SpellEffect` and the preview should
follow it there.

### Where this landed

`spreadDeg = 0` for magic and for non-ammo ranged; the cone survives for the longbow only, narrowing
with the draw. Gate in one place (`ShowRangedPathPreview`), so both callers - the aim-frame path and
the per-hand dual draw at `PlayerController.Combat.cs:249` - inherit it. Also fixed the two class docs
that named "bow pull, throwing-hammer wind-up" and "projectile magic" as cone consumers (rule 8: a
comment naming a behaviour you just changed is the most reliable way to ship a stale claim), and the
adjacent `game-design.md` zone-spell bullet that referred to "the cone above".

Still OPEN: the H3/H4 defect is unfixed by design, and the only way to confirm the visual split is the
five-item play-test checklist in `PROGRESS.md`.

---

## 1ip - the player's base weapon: iron sword -> magic staff (OPEN: awaiting play-test)

**A one-line data request, so this section is short. It is here because two of the three things I had
to check are the kind that are invisible in the diff.**

### H1 - "the player's base weapon" is one place in the code. CONFIRMED.

Grepped `iron_sword` across `Assets\Scripts`: 8 hits in 4 files, but only **one** of them decides what
the player starts holding:

- `WeaponCatalog.cs:42` - `public const string StarterWeaponId = "iron_sword"` <- the decision
- `WeaponCatalog.cs:85-86` - the `iron_sword` roster entry itself (keep it; the sword stays equippable)
- `WeaponModelBuilder.cs:42,358` - `BuildIronSword` + the model dispatch `case "iron_sword"`
- `WeaponAnimator.cs:149,165` - the sword's pose key and 4-clip combo def
- `RecipeData.cs:13` - an unrelated doc-comment example string (`craft_iron_sword`), not a reference

So the change is `StarterWeaponId`, not the roster entry. Changing the sword's *stats* would have been
a different (wrong) task; changing the id changes only what gets equipped at boot.

### H2 - three readers, and I should not miss one. CONFIRMED, and the "miss one" risk was real.

`StarterWeaponId` is read in exactly two places, both found:

1. `NewWorldTestGround.cs:910` - `SpawnAllWeapons` finds it and `WeaponRigBuilder.EquipInto`s it, then
   sheaths it (`pc.ReApplyWeaponPose(instant: true)`).
2. `CharacterInfoUI.Equipment.cs:365-366` - `CycleWeapon`'s fallback: if the owned list is empty or does
   not contain the starter, it **inserts the starter at index 0** so the cycle has something to land on.

Both resolve through `WeaponCatalog.Find(id)`, so both are data-driven and neither hardcodes a category.
The doc comment on the old constant also named the weapon in prose ("Wanderer's Iron Sword"), so it was
a third place in the same sense rule 8 is about - a comment that names an identifier and rots when the
identifier changes. Rewrote it to say what the id *is* rather than repeat its current value.

### H3 - the staff is fully wired, so this is a data swap and not a feature. CONFIRMED.

Before assuming `"staff"` is a drop-in, checked the four things a Magic-category weapon needs that a
Melee one does not:

- **Model**: `WeaponModelBuilder.cs:368` - `case "staff": return BuildStaff(parent)`. Exists.
- **Hold pose**: `WeaponRigBuilder.cs:293-300` - the `WeaponCategory.Magic` branch, with a
  **staff-specific** sub-case (`weapon.id == "staff"`) that applies the sword-like yaw + roll. Exists.
- **Animation**: `WeaponAnimator.cs:274` - `"staff"` has a `WeaponAnimDef` with `K_Staff`. Exists.
- **Behavior**: `WeaponDatabase.cs:27` - `WeaponCategory.Magic -> MagicWeaponBehavior`, which routes
  to the spell pipeline and applies the weapon's magic mods. Exists.

All four present, so no new code was needed and the change cannot regress the draw.

### H4 - does anything assume the starter is MELEE? REJECTED - it does not, and this was the real risk.

A starter-weapon swap from a Melee to a Magic category changes which branch of several switches the
player lands in, so I looked for "melee-assuming" code rather than for the id:

- `CombatController.cs:197-200` - `BothHandsMagic` requires **both** hands to hold magic weapons.
  With one staff equipped the loadout is single-wield, so this is false - but that is the *same* loading
  state the sword was in (one hand), so nothing changed. Confirmed rather than assumed.
- `CombatController.cs:211` - `PerHandScheme` is `HasLoadedDual && !BothHandsMagic`, also gated on both
  hands being loaded. Unchanged.
- `PlayerController.Combat.cs:165,489` - `IsMelee`/`IsMagic` are per-weapon queries on the **equipped**
  rig, not on the starter id. They will now answer differently, which is the intended consequence.
- `MagicWheelUI.cs:255` - gates on the equipped category, so the magic wheel becomes available on a
  fresh character. That is a *feature unlock from the swap*, and it is the main thing to play-test.

Nothing keys off "the starter is melee". Verdict: the swap is safe, and the interesting consequences
are gameplay ones (spell scaling, wheel availability), not crashes.

### What this task is NOT

- Not a nerf/buff to the sword - `iron_sword`'s own stats, model, anim and combo def are untouched and
  it stays in the roster.
- Not a respec of `WeaponCategory` defaults - `WeaponData.Category` still defaults to `Melee`
  (`WeaponData.cs:47`), which is correct for hand-authored assets.
- Not verified in play. Rule 3: no build, no CLI, no Unity run. Everything above is grep + reread.

### H5 - my own task id collided with a shipped one. CONFIRMED (by the check that came too late).

I wrote `1io` into four places — the code comment, the `game-design.md` bullet, and the `PROGRESS.md` /
`THINKING.md` headings — and **`1io` is a real shipped task**: "Crater/deform audit lane moved F1 -> F13
+ StaticChecks check 8". `1im` is also taken (the WeaponAnimator drift task). I only found it by
grepping `PROGRESS.md`'s `^## 1..` headings **after** writing all four, not before.

The habit this is an instance of: **a task id is a copy of a fact about the repo's history, and the copy
rots exactly like a symbol name does** (rule 8's third bullet). Two things make it worse than a symbol:

- **Nothing in code review catches it.** A stale `CraterCapRadius` in a comment looks wrong; a stale
  `1io` looks like any other task reference, because *some* task called `1io` genuinely exists. The
  error is invisible precisely when the id is a real one.
- **The copy is written after the work, so "grep before you edit" does not cover it.** The id is
  chosen at commit time, which is *after* every file has been edited. So the check has to be
  "enumerate the ids in use, then assign" — and the enumeration is over `PROGRESS.md`'s headings, which
  is the one file a task adds to and therefore the one that can answer the question.

Renumbered to `1ip` everywhere. The Deep Freeze commit before it went out as `1il`, which is free but
out of order; rule 1 forbids amending a pushed commit, so that one stays and is noted in its place.

### Open

- Does the staff's forward-lean hold read correctly both stowed on the back and drawn? It was authored
  for the bench rack, where it was drawn, but the *sheathed* staff pose is a different question.
- Is the magic wheel's availability at boot intended? It follows from the swap; it is a consequence
  the user should confirm rather than something I decided.

---

## 1f7 - the falling rock was the last spell visual with no per-spell hook (OPEN: awaiting play-test)

**This one started from a design request, not a bug report**, so there is no measurement lane and
nothing to attribute. What follows is the reasoning that picked the design, recorded because two of
the conclusions are reusable and one of them was wrong on the first pass.

### The gap, stated precisely

Mapped every visual axis a spell carries: `SpellImpactStyle`, `SpellCastStyle`, `ProjectileShape`,
`Core`/`Edge`, `Scale`, `Tempo`. All resolve through `SpellLook.Resolve` and all have a
`SpellLookProfile` field. Then mapped what `SkillFx.FallRock` draws: **one ragged boulder**, decided
by nothing on the spell.

- **Hypothesis A (confirmed):** the falling body is the one spell visual with no per-spell hook.
  Evidence: `FallRock`'s body came from `scale` alone, and `scale` is the *blast radius* - so the
  spell's gameplay number was silently doubling as its art budget, and every sky spell drew the same
  silhouette. Confirmed by reading the call sites: 6 spells set `summonFallingRock`.
- **Hypothesis B (confirmed):** the flag was doing more than "spawn a rock". Comet's
  `projectileShape: ProjectileShape.Comet` plus `summonFallingRock: true` meant Comet's *in-flight*
  body was the `Comet(rockBody: true)` boulder core. So a single flag was selecting between two
  different bodies in two different places, and "keep Meteor as-is" had to mean fixing both sites.
- **Hypothesis C (rejected):** `magic_earth_meteor` was listed in the docs but missing from the
  catalog. I carried a stale memory that Earth Meteor did not exist. It does, at
  `SkillCatalog.cs:346-353`, with its own authored profile. I was reading the docs, not the code -
  the exact failure rule 8 warns about, caught only because I grepped before writing prose about the
  roster.

### Why a new axis rather than a spell-side boolean

A `bool rockSwarm` on `SpellData` would have been three lines. Rejected: it would be a fourth
spelling of "what does this spell look like" living *outside* the resolution rule, which is the
rule-13 failure this codebase already shipped twice (two drifting `DamageType` palettes, then
`AutoShapeFor` next to the school shape families).

- **Hypothesis D (confirmed):** this axis must be `Inherit`-resolvable and authored-only. Adding it
  to a school `Families` array would be wrong for a specific reason, not a stylistic one: a
  sky-rock style is a *structural* statement about the spell, not a look that is equally valid
  either way. Jittering it would have given Meteor a swarm on roughly half its casts. Same argument
  that keeps `DisplayShape` from handing out `Missile`'s homing.
- **Hypothesis E (rejected, and it nearly shipped):** put `Swarm` in the Fire school's family array
  so it "just falls out of the existing picker". Rejected by D. Worth writing down because it is the
  tempting version - it needs no new authored field and the picker already exists.

### The shape-vs-flag trap

- **Hypothesis F (rejected):** give Comet a `ProjectileShape.CometEmber` and leave
  `projectileShape:` on the spell. That would have made Comet *homing-free but shape-bearing* - and
  the real risk was elsewhere: `spell.Shape` is read at exactly **one** behavioural site
  (`SpellEffect.cs:83`, for `Missile`), so a new enum value is safe there. But the habit is the
  point: a display value written into a behaviour field is how rule 13's "a field that means
  gameplay and a field that means drawn must not be merged" gets violated by accident. Routed
  through `SpellLookProfile.DisplayShape` so `spell.Shape` stays `Auto` and no behaviour path
  changes. Verified by grepping every `.Shape` reader rather than trusting the argument.
- **Hypothesis G (confirmed):** the flat fan is not a taste call. `RockDrop` snaps the whole
  formation to one ground height on landing, so any per-rock Y offset means the outer rocks float or
  sink on a slope - seven chances to see it against the boulder's one core. X/Z spread only. This is
  why the swarm needed no per-rock fall state, which in turn is why `BuildRockBody` could stay a
  pure builder with no `RockDrop` coupling.

### The bench had no way to see the new body

- **Hypothesis H (confirmed, and this is the one that would have shipped a silent hole):** the
  magic-model bench draws `SpellCaster.CreateProjectileDisplay`. A **Zone** spell has no projectile
  display, so Asteroid would have fallen through to the generic orb - the bench would have kept
  reporting the *old* model for the exact spell 1f7 changed. Extracting `BuildRockBody` out of
  `FallRock` was not a tidiness refactor; it was the only way to give the new visual an acceptance
  readout. Rule 7's "an audit must ask the question the PROPOSAL is about" - the proposal was about
  a *falling* body, and the audit could only draw a *flying* one.

### Where I got numbers wrong

- The plan estimated the comet tail at ~2.3 m. Shipped, it measures ~2.0 m off the literals. The
  comment now carries the measured figure and says which literals produced it.
- The first draft of the `SkyRockStyle` doc claimed "all five `SummonFallingRock` spells fell as the
  same boulder at two different sizes". Grepping the actual roster: it was **six** spells, and the
  ladders are **3-4** (Zone spends the full blast radius) and **1.6-1.8** (Storm spends half). Both
  halves of that sentence were wrong, and neither could have failed a compile or a static check -
  the narrative around a change is precisely what no tool in this repo reads (rule 8).

---

## 1f6 - "Lod1/Lod2 visually cover some chunks" - VERDICT: the whole detail-LOD is deleted, and the report that named it is UNMEASURED (OPEN: awaiting play-test)

**Note on discipline, because this one skipped a rule.** Rule 7 wants a measurement lane FIRST on a
geometry report you cannot see from the code. The user was offered it and chose not to: the report
was treated as a design defect with a known mechanism rather than as an unknown. So **there is no
readout behind this change**, and the "covered" claim below is a mechanism I can *derive*, not a
measurement I took. Rule 7's last habit applies with full force: a check you have never seen fail has
stopped meaning anything, and here I have not even got a check.

**Hypotheses, in the order I considered them:**

1. **CONFIRMED (by reading, not by looking).** The band children are a *different surface* from the
   root. `BuildLodChild` strides the 31x31 lattice to every 2nd/3rd corner while the root mesh keeps
   every 1 m corner, so the two agree only where the heightfield is smooth, and the band is switched on
   by *disabling the root renderer* (1e6's fix). That is a mechanism for both symptoms in the user's
   report - a shape change at 30 m/60 m (1f5, already reported and half-fixed) and a coarse triangle
   **covering** the real surface wherever it spans convex ground, because the coarse surface is a
   different surface, not a coarser view of the same one.
2. **CONFIRMED, and the reason this is a deletion.** No threshold can fix hypothesis 1. 1f5's
   `NeedsLodDetail` gate already took the best available shot and it was genuinely *derived* (a
   discrete Laplacian is exactly zero for any planar surface at any stride - it measured relief, not
   scale), and it still made the terrain passable at two different shapes. Worse, the one place the
   band child could sit in FRONT of the real surface has nothing to do with sub-cell relief at all:
   a 3 m lattice triangle over convex ground is in front of the 1 m surface by construction, at any
   stride and any gate. And `BuildVoxelLodChild` decayed unconditionally with no gate at all. So the
   second surface is only ever honest by luck, and a second surface that needs a gate to be honest is
   the bug.
3. **OPEN / not investigated, and worth naming.** Why the user saw it as *covering* rather than as
   *reshaped*. A band switch is a hard, discrete event at 30 m and 60 m, so a chunk crossing either
   boundary should visibly *pop*, not quietly cover. Covering implies the coarse surface sits above
   the fine one over some region - which is exactly the convex-ground case - and would have to have
   been seen while crossing a band, or after walking back. No readback exists to confirm which.
4. **REJECTED, recorded so it is not walked again.** "Just push the bands further out" / "raise the
   cull distance". The bands are at fixed distances, not at the cull; moving the cull changes nothing
   about what a band draws, and the cull has its own 1gh invariant that must not be loosened for this.
5. **REJECTED.** "Keep the bands but make them render *alongside* the root instead of instead of it."
   That is strictly more geometry for the same wrong picture, and it keeps the resampling seam.
6. **REJECTED, and it is the tempting one.** "Replace the fixed stride with an adaptive one, and put
   a uniform whole-chunk decision behind it." 1f5 already built that (`NeedsLodDetail`, whole-chunk so
   no T-junction row). Rejected now for the reason in hypothesis 2, plus a cost: it is a mesh
   rewrite with no compiler behind it (rule 3) whose failure mode is a crack.

**What the deletion costs, so it is a decision and not a shrug:** ~+400k resident triangles
(~336 chunks x the ~1350-triangle difference between a decimated child and the full root), draw calls
unchanged. The honest alternative - a genuinely *distant* owner that is allowed to be coarse because
it is far enough that nobody is standing on it - already exists: the far shell. If the frame budget
cannot take the trade, the fix is there, not a near band.

**Also folded into the same pass, and both are rule 14 residue rather than new design:**
`ChunkCornerGrid.Normals` was written in two places and read by nothing once the builders went (the
array had no owning corner under the 1ew rule either - a corner's *normal* is not a property of a
corner), and the QA HUD's `lod1`/`lod2`/`band` fields described children that no longer exist, which
would have made the F3 line quietly useless for the next "why is a chunk invisible" report.

**Verdict:** shipped as a deletion, mechanism derived from the code, **report unmeasured**. Open
until the user restarts the session and confirms both that the covering is gone and that the frame
budget survived the triangle count.

---

## 1f5 - "the crater was good on first load then it got altered" (VERDICT: fixed by 1f5 as the LOD band stride, NOT the carve — **superseded by 1f6, which deleted the stride, the gate and the whole band**)

The report has no distance, no direction and no timing. That is the whole difficulty: "altered" is
compatible with at least four unrelated mechanisms, and three of them are one-line greps away.

**H1 - the far shell re-owns the surface.** *Initially my leading theory, and it was WRONG.* The far
cell's corner grid comes from disk save mods with a pristine-noise fallback
(`WorldStreamer.FarShell.cs:1333`), and 1i3 deliberately prefers the LIVE grid on **boundary corners
only** - interior nodes stay on disk/noise. A crater is almost all interior, so this looked like a
strong candidate. **Rejected on arithmetic**: the far shell starts at ring 10 (~300 m) while
`ChunkLodManager.CullDistance` auto-scales to the render radius (~120 m). Streamed chunks are culled
before the shell is ever the owner of anything. I had not checked the *distances* when I wrote this
theory down - the data-source asymmetry is real but it is unreachable at any distance a player can
stand. (Dead end worth keeping: the asymmetry itself is still true, it just cannot be the report.)

**H2 - a repeated cast re-carved the same spot.** Both QA crater lanes exist and both re-cast: `SpawnTerrainSlabDemo`
casts one Crater, `SpawnDigLayersDemo` casts 2 at one spot and 4 at another, so a single press of that
lane digs ~6 CraterSteps into the same two pits, and every cast re-snaps the ladder so the treads MOVE
between casts. **Rejected as the default**: both `EnableTerrainSlabDemo` and `EnableDigLayersDemo` are
`false` (`NewWorldTestGround.cs:66,68`), and `RunSafely` is a try/catch, not a re-entry guard - a lane
runs once per press. Retained as a conditional: if the user has that toggle ticked on, it is a
sufficient explanation on its own and would need no code change.

**H3 - the near chunk's own mesh resolves out-of-chunk corners from pristine noise.** *True, and still
true.* `BuildBorderCorners` only fills the border map for tiles present in `_loadedData`
(`CornerIfLoaded`, `WorldStreamer.Deform.cs:498-510`); when the owner is absent,
`ChunkMeshGenerator.CornerHeight` substitutes `TerrainNoiseGenerator.GetHeight`. So a rebuild can draw
a 1 m ring of pristine ground along a chunk seam while the data is correct. **Rejected as the report**:
it needs a rebuild to land while the neighbour is unloaded, it only affects the seam ring, and the user
described the crater's shape changing, not its edge. Kept as a separate open item - it is a real
data/drawn split and F13 is structurally blind to it.

**H4 - saves lose the carve on reload.** **Rejected**: the NaN-stamped corner grid
(`WorldStreamer.ChunkBuild.cs:202-226`) preserves every saved corner and only regenerates unstamped
ones; `ApplyHeightEdits`'s `CornerOrBase` falls back to `CurrentHeightOf`, not to pristine, so it cannot
erase an untouched corner; `ReconcileModifiedBorders` only *requests* neighbour mesh rebuilds and never
writes a height; and `ChunkValidator` is a read-only logging MonoBehaviour the streamer never calls.

**H5 - the LOD band children.** *Confirmed.* `ChunkLodManager.cs:26-28` switches detail at **30 m and
60 m**, and `BuildLodChild` decimates the 31x31 corner lattice to every 2nd and every 3rd tile. The
comparison that matters is rule 12's own - a deform's reach against `step/sqrt(2)`, not `step`:

| surface | lattice | worst node distance | crater (reach 1.9 m) |
|---|---|---|---|
| root (<30 m) | 1 m | 0.71 m | terraces intact |
| Lod1 (30-60 m) | 2 m | 1.41 m | reach barely clears it, treads erased |
| Lod2 (>60 m) | 3 m | 2.12 m | **reach 1.9 m < 2.12 m** - can vanish |

Back up 30 m while playing, which needs no commitment, and the surface you are looking at silently
switches to a resampled one. This is the 1hx/1ia coupling one dimension down, and it is the only
mechanism in the render path that resamples a carve below its own reach.

**The part that made it a *1f3* story rather than an old bug: 1f3 did not cause it, it made it
legible.** Pre-1f3 the crater was a smooth cone; a smooth cone resampled at 2 m still looks like
itself, so the defect was invisible. A terraced cone obviously does not - the treads are the highest
frequency content in the shape, and decimation deletes the highest frequencies first. So 1f3 turned a
dormant LOD defect into a reported one. Anyone reading 1f3's symptom as "1f3 broke the crater" would be
wrong about the cause and would go looking in the deform path.

**Sizing the gate, without a measurement.** Rule 7 wants a readout first, and there is none available:
the user has no F13 key, so F13 section E cannot be pressed or read. A tolerance guessed here would be
the exact failure 1i1 hit (a gate on the width of its own test, naming a mechanism for noise). So the
threshold had to come from the generator instead: `TerrainNoiseGenerator`'s five octaves
(55/22/3.5/0.6/1.5 m at 833/250/83/33/125 m) give a worst-case 1 m discrete Laplacian of about
**0.016 m**, because a discrete Laplacian is exactly zero for any planar surface at any stride. The
crater's staircase puts 0.475 m into one cell, so `LodDetailCurvature = 0.20 m` has ~12x clearance over
noise and ~2.4x under the authored step. A smooth un-terraced dish (~0.15 m) falls below it **on
purpose**: that is what LOD is for, and chasing it would make every shallow dent refine.

**The trap I walked into and backed out of.** The elegant fix is per-cell: test each LOD cell against
the bilinear surface of its four corners and subdivide only the ones that distort. It is local, cheap,
and it is wrong here - a refined cell meets its decimated neighbour along a polyline against a straight
chord, which is the T-junction row rule 12 records from the pre-1ej far shell ("permanent thin lines").
With no compiler (rule 3) I was not willing to ship a mesh rewrite whose failure mode is a visible
crack. Hence whole-chunk refinement: uniform stride, no transition, cannot crack. That is a real cost
(900 quads instead of 225/100, per carved chunk, only out to the last band) paid for a guarantee.

**Still open.** Nothing here was observed. The user never confirmed a distance, so H5 is the mechanism
that fits every detail of the report that I could check, not a measured cause. `BuildVoxelLodChild`
has the identical coupling and is still hard-coded to 2/3.

---

## 1f3 - the blanket is a CONTINUITY complaint, and continuity has exactly two owners on a 1 m lattice (OPEN: awaiting 1f3 play-test output)

The user's read on the 1f2 world: the crater is a "smoothed out blanket". Before writing code I
re-derived what could possibly be making it smooth, because 1f2's own section E had already told me
the shape was *expressible* (deep enough, rimmed, drawn) — so "make it bigger" and "make it faceted"
were both guesses.

### Hypothesis 1 - the normals are interpolated, so the facets need flat shading. REJECTED before any edit.

`ChunkMeshGenerator.cs:174-179` assigns a quad's four corner normals from the same face, i.e. the mesh
is **already flat-shaded per quad**. 1hy said so too ("far normals are flat per-quad"), and I had
half-remembered it as a far-shell-only property. So the "low-poly look" was never missing: the mesh
has been flat all along. Rejected on the code, not on taste — and this is why reaching for a material
or normal change would have been a second task with nothing to fix in it.

### Hypothesis 2 - the blanket is the profile's shape: a cone has no level breaks. CONFIRMED, shipped.

The cone was `1 - dist/reach`: a straight line from the impact point to the footprint edge. Every
sample along it holds a *different* height, so on a 1 m lattice there is nothing for the renderer to
draw as a step — it is one continuous ramp, bilinearly interpolated. That is precisely "smoothed out".
A sphere's lower cap is a **curve**, and the interesting part is that the curve is steepest at the rim
and flattest at the floor, so quantising it produces treads in the middle and risers on the wall.

Deriving the cap honestly cost one wrong turn worth recording. I first wrote
`depth = sqrt(capR^2 - d^2) + (capR - capDepth)`, i.e. I put the sphere's centre *below* grade when it
is above it, and got influence ≈ 0 at the impact point — the exact inverse of the intent. The
identity that fixes it and is worth keeping in the code comment: `sqrt(capR^2 - reach^2) = capR -
capDepth`, i.e. `capR = (reach^2 + capDepth^2) / (2*capDepth)`. That single equation is what makes the
cap meet grade at exactly `reach`, which is why the feather to untouched ground is unchanged and only
the interior moved.

I also nearly wrote a comment claiming tool digs clamp to a hemisphere. Wrong, and only a reread of
`reach = radius + feather` caught it: `reach` is radius **+ 0.5 m**, so the smallest real dig (pickaxe,
radius 1.0) reaches 1.5 m against a 1.1 m `CraterStep` and the `min()` is defensive only. A comment
that states a live path which is not live is the same class of error as the wrong `§5.7` pointer I
also had to fix in `game-design.md` this pass.

### Hypothesis 3 - quantise the ABSOLUTE height, so treads are flat even on a slope. REJECTED, and the reason is a perf cliff, not taste.

Snapping the height to a world-wide Y ladder does give genuinely flat treads on sloped ground (two
adjacent corners land on one rung). It is the stronger visual. I rejected it on two counts:
- it makes the crater's **depth a function of the ground's absolute elevation** (± half a terrace,
  varying across the map), which is a strange thing for an impact crater to have; and
- it floods `ChunkMeshGenerator.IsFlatTile`, whose consumer `ChunkContainsFlatTile` escalates to
  `FullRebuildChunk` — a 900-tile rebuild plus a collider re-cook, sized for **slab side walls**. A
  quantised crater emits no side walls, so that escalation would be pure cost, on every cast, in
  every chunk the crater touches.

This is 1i4's lesson wearing different clothes: I had read `IsFlatTile`/`ChunkContainsFlatTile` in
1f2 while reasoning about `EdgeIsRaised`, and reusing that reading felt safe. The extra step was
asking *who consumes a flat tile* rather than only what produces one. So offset snap it is — flat
treads where the ground is genuinely flat (including the test platform), hard breaks with ramps
between them where it is not. **If the user finds the terrace read too weak on a slope, that is the
trade to revisit — and the revisit is to `IsFlatTile`'s predicate, not to a smaller epsilon.**

### Hypothesis 4 - the terrace can round the deep core away and silently kill the ratchet. REJECTED, arithmetically.

If the snap could take the centre's offset to 0, the floor would stop dropping per cast and 1cv's
"unbounded downward ratchet" would become "unbounded in intent only". It cannot: the centre's offset
is `-CraterStep` and `CraterStep / terrace >= 1.1 / 0.8 = 1.375`, so it always rounds to at least one
whole terrace. Worth stating in the code because it is the invariant a future edit to the terrace
clamp could quietly break (raise `CraterTerraceMax` past `2 * CraterStep` and it breaks).

### The interaction I found but did NOT fix

1i9's `CraterFacetSkirt` only contributes when `EffectiveLowPolyStep > 0`, and 1ia holds
`LowPolyFacets = false`, so today the skirt adds nothing to `offset` and the question is moot. If the
facet path is re-enabled (1ia revert / 1f4), a skirt whose entire influence is under half a terrace
snaps back to **0** and its guarantee — "the carve reaches a rendered node" — quietly fails, with the
crater becoming invisible for exactly the reason 1hx documented. I left it as a comment beside the snap
rather than guarding it: guarding a dormant path is speculative work, and rule 7 says measure first.

### Still open

- Whether the snap's `flatTreads`/`riserEdges` readout lands as derived. **Every number in the 1f3
  handoff is hand-derived**, including the rim crossover (t ≈ 0.72), the snapped net rim (+0.475 m) and
  the centre dig (−0.95 m). F13 on a freshly cast crater is the only thing that confirms them.
- Whether the 60°/73° rim wall traps the player in a way that reads as a bug rather than as a crater.
- Whether 1f4 (the literal sub-metre facet shell) is still wanted once 1f3's readout lands, or
  whether the terraced cap already answers "faceted" well enough.

---

## 1f2 - "make the crater a low-poly ball-cap": the fix is a cube-sphere facet shell, NOT per-tile verticals; and the lane needed a question none of A-D asked (OPEN: awaiting 1f2 play-test output)

The request: every scale-derived crater should read as a low-poly spherical cap at its **actual
collision point**, with face count and size scaling from the magic, stepped/terraced, re-centring and
deepening on later hits, keeping the 1ez rim. Chosen representation, in the user's words:
**"facet shell over a low-poly ball-cap, no dish."**

### Hypothesis 1 — the terrace can be made by writing the low-poly cap's heights onto the lattice and letting the existing mesh draw the risers. REJECTED, and the reason is structural.

The plan was to quantise each carved corner to `refY + step*floor(offset/step)`, giving flat treads
and step risers, and let `EdgeIsRaised` / `SideBandCount` build the vertical faces. That reads like it
should work, and it is wrong for a reason that took a second pass to see: **flat treads and vertical
risers are mutually exclusive on this data model.**

`ChunkData` holds **one height per shared corner** (4 slots, `[0]=NW [1]=NE [2]=SE [3]=SW`, 1 m
pitch). Two adjacent tiles share an entire edge of two corners, so both tiles' edge vertices are the
*same two floats*. For tile A's tread to be flat at height `h0` and tile B's to be flat at `h1`, those
two shared corners would have to equal `h0` for A and `h1` for B — so `h0 == h1`, and if the four
corners of A are all `h0` and B's are all `h1`, A's shared edge is at `h0` and B's is at `h1` while
being literally the same two numbers. **Therefore adjacent flat tiles at different levels are not
expressible, at all**, and `EdgeIsRaised` never fires in-chunk: it is reached only for a seam or
legacy flat case.

I initially wrote this up as "the side-wall path automatically produces vertical risers" and that was
flatly incorrect. The correct statement is the opposite: the lattice gives you a *ramp*, never a
*wall*.

### Hypothesis 2 — give each tile its own top, so the render mesh can draw per-tile flats and verticals. REJECTED as a cost/risk verdict, not a correctness one.

This is the voxel model. It works, and it is the reason the global voxel path exists. But the same
`ChunkCornerGrid` feeds the render mesh, `BuildDecimatedCollider`, LOD, and the far shell — so per-tile
tops means a vertical strip pass in the collider, a save-format bump, a new `ChunkValidator`, and the
per-vertex vertex-count cost that got the global voxel path deprecated in 1ev (~2M tris against
~405k). Rule 12's "a coarser step is not free" and rule 7's "one owner per claim" both point the same
way. Rejected: it changes the whole data layer to draw one shape.

### Hypothesis 3 — a separate cube-sphere facet shell owns the surface, the lattice owns the data. CONFIRMED as the design (not yet implemented; 1f4).

Lower-hemisphere cube-sphere, `N = round(radius / step)` per cube edge so facet edge ≈ `step` and
facet *count* scales with the spell (fireball N=4, Meteor N=5, pickaxe N=2), flat tangent-plane
facets, and vertical skirts dropped to the exact lattice corner heights so the shell meets the
terrain it was carved from. The lattice underneath holds the quantised low-poly cap — hence "no
dish": there is no smooth surface under the shell, the surface *is* the stepped cap.

Two sub-questions this answered cheaply:
- *Does the shell need to be in the raycast path?* No. `ResolveGroundTarget` already raycasts `~0`
  with `QueryTriggerInteraction.Ignore`, so a **solid, non-trigger, normal-layer** `MeshCollider` is
  already hittable. It must NOT carry a `ChunkObject`, or `ResolveGroundTarget` will report it as
  raised terrain and apply a raised-shape step offset.
- *Does the shell need a save format?* No, if it is rebuilt from saved heights by flood-filling the
  connected riser nodes. That keeps rule 8's "one lattice, one owner" and avoids a v2 migration. OPEN:
  the flood-fill detection is the fragile part and is the first thing 1f4 must prove.

### The measurement question — why the lane needed a new section at all

Sections A–D measure depth, profile, resolution and expressibility. Every one of them can read clean
on a **perfectly smooth cone**, which is exactly the current shape. None of them asks whether the
carve is *stepped*, so a proposal whose entire point is "stepped" would have had **no acceptance
readout** — the 1hy/1i1 class of mistake, where a measurement cannot see the thing it was built to
judge. Hence section E, and hence the rule-7 sub-decisions inside it:

- **E's own premise is checked first.** With no dig past the threshold it prints
  `<no crater in band>` and classifies nothing. The first version walked the footprint regardless and
  would have reported untouched terrain's micro-relief as a `CONTINUOUS` verdict — a
  positive-looking number about nothing.
- **Membership and value use different references.** This is the subtle one. Membership = deviation
  from pristine (either direction: the 1ez rim is a *raise*, and a rim terrace is a step too); value
  = raw height. A carve writes `refY + offset` against one reference height, so the pristine slope is
  *overwritten*, not added to, and carved heights are exactly the authored offsets plus a constant —
  the ladder directly. The obvious shortcut, measuring dig-below-pristine, folds the untouched slope
  back in and reports CONTINUOUS for a perfectly quantised carve. I hit this bug in the first draft.
- **A contradiction is `UNKNOWN`, never `0`.** If B clears the threshold and E finds no deviating
  corner, both read the same grid through the same gate, so they disagree about *where* — a
  contradiction. 1i2 retracted 2965 and had nothing to put in its place; the rule here is that a
  retracted number's replacement is *unknown*, not zero.
- **Thresholds are read, not written.** `stepOffset = 0.5f` is set in exactly one place and
  `slopeLimit` is never assigned at all, so hardcoding either would be rule 8's second spelling.

### Scope still open

- The plan scopes 1f3 to `TerrainShape.Crater`. `TerrainShape.Projectile` dents, `FlattenAt` and the
  raised shapes (`Wall`/`Ring`/`Pillar`/`Spikes`) are untouched, and **it is not confirmed** that the
  user means the projectile dent path should take the new cap too.
- Quantising to a step **shrinks the effective radius**: the outermost sub-step band rounds to zero.
  That is inherent to quantisation and must be stated in 1f3 rather than discovered as "the crater
  got smaller".
- Terraced tiles are flat, so `ChunkContainsFlatTile` (`WorldStreamer.Deform.cs:443`) can route
  craters through `FullRebuildChunk`. A cost to measure in 1f3, not a blocker.
- Props and any lattice query can now disagree with the shell by up to one step (rule 12's
  "a carve guarantee must be a `Max`" reasoning, applied to what sits on top).

---

## 1ez - the raised crater rim - VERDICT: shaped by a signed (lip - dish) profile, not two Max'd curves; shipped without any lattice or save change (OPEN: not play-tested)

1in proved the crater reads as a flat cone because `DeformAt`'s crater branch is a monotone
subtraction. 1ez is the shape fix. The whole question was **how to add the positive term without
introducing a step**, and the answer is a signed radial profile.

**Hypothesis 1 - author the rim as an independent raised bump and `Mathf.Max` it against the dish.
REJECTED.** The dish target near the inner edge of the rim band is still ~0.47 m below pristine, while
a bump starting at zero there is ~0. A `Max` would therefore snap from dish to bump the instant the
bump's own smoothstep clears the dish — a visible ledge, not a blend. The two curves are far apart
where the band begins, so any pointwise combine of *separate* dish and lip curves jumps somewhere.

**Hypothesis 2 - one signed radial profile, `offset = lipBump*CraterRimLift - depression`, written as
`current + offset` when negative and `Max(current, pristine + offset)` when positive. CONFIRMED.** The
two terms share one `t = dist/reach`, so `offset` passes continuously through zero (measured crossover
~0.68 x reach at radius 1.4): the ground neither pops nor double-counts. The sign also picks the right
idempotency rule per side automatically — negative = excavation (ratchets down, the deliberate inverse
of raised shapes), positive = raise (idempotent, `Max` against pristine-based target). `lipBump` is a
smoothstep that is zero at BOTH ends of the band (`0.55*reach` and `reach`), so the footprint boundary
is untouched and the dish still feathers to grade exactly as before.

**Hypothesis 3 - the rim height should scale with `CraterStep`. REJECTED.** The code comment initially
claimed `CraterRimLift` was "a fraction of `CraterStep`", and it is not — it is an absolute lift. That
turned out to be the *better* behaviour: the lip height is `lift` minus a fraction-only depression, so
it is scale-independent (bigger crater ⇒ wider rim, not taller), and an absolute ~0.44 m net rim stays
under the player's `stepOffset = 0.5 m` at every crater size, so the lip is a bump you walk over
rather than a wall. Keeping it a fraction would let a `CraterStep` bump turn the rim into an obstacle.
The comment was corrected to say absolute; the code is unchanged.

**Hypothesis 4 - the rim needs the stored fine lattice (1ey) to render. REJECTED.** The rim is written
as ordinary 1 m corner heights through the existing `ApplyHeightEdits` path, and 1ex already re-cooks
the collider from that lattice, so the rim is both rendered and physical today. The lattice's payoff is
sub-tile sharpness for a crater landing *between* nodes — real, but marginal, and unmeasured; it stays
deferred until a measurement justifies the v2 save migration.

**Hypothesis 5 - the rim must be excluded from the caster keep-out ring. CONFIRMED (defensively).** The
band starts at `0.55 * reach`; for the smallest authored crater (`radius >= 1.2`) that is `>= 0.935 m`,
already outside the `0.9 m` ring, so the guard never fires in practice. It is kept because the constant
could move, and a rim rearing under the capsule is exactly the depenetration "teleport" the ring exists
to prevent.

**Dead end - `reach`/`feather` arithmetic for the lip's own bounds.** First pass quoted the inner bowl
as 0.39 m; recomputing `s = smootherstep(1 - t)` gives 0.47 m. The interior figures in the code comment
were stale and were corrected against the live formula. This is rule 3's "grep is not compilation"
one level down: the numbers in a comment are a claim, and a claim about arithmetic can be checked.

**Still open.** 1ez is emitted but play-test is Unity-only (rule 3), and the verified-by-hand geometry
here is about `DeformAt`, not about the frame the user will see. The F13 section D readout (corners
above pristine) is the instrument that will confirm the lip exists; it should be non-zero now.

---

## 1ex - "a 0.5 m lattice fixes the crater" - VERDICT: the render resolution was never the blocker; the COLLIDER resolution was (shipped 1 m collider; rim shipped as 1ez; lattice deferred to 1ey; caves split out — NOT 1ew, which is already the adaptive stretch-split)

The task began as "the projectile dent is a monotone cone; give it a rim and enough resolution to hold
one". The plan was going to be "add a 0.5 m stored fine lattice, then author the rim into it". Halfway
through reading the mesh path that premise broke, and the break is the whole finding.

**Hypothesis 1 - the crater is invisible/wrong because the render lattice is 1 m. REJECTED as the cause.**
1 m renders the dish fine (the user can see it); the reason it *reads* as a flat cone is that the
profile has no positive term at all - `DeformAt`'s crater branch is `target = current - s * CraterStep`,
a monotone subtraction, so no amount of resolution produces a rim. Resolution and *shape* were being
conflated. The rim is a 1ez change, not a lattice change.

**Hypothesis 2 - reach vs. sampled node is the reason a crater can be invisible. CONFIRMED, but on the
COLLIDER, not the mesh.** Reading `ChunkObject`/`PlayerController` together: the player has **no ground
raycast** - `CharacterController.Move` sweeps the chunk `MeshCollider` (`Movement.cs:143`). That collider
is built by `BuildDecimatedCollider` at `ChunkColliderDecimation = 2` - **every 2nd node of the 31×31
corner grid**. A 1 m crater has reach `radius + feather = 1.4 + 0.5 = 1.9 m`, which *does* cover the 1 m
mesh node spacing, so the mesh always shows the dish. But the 2 m collider's nearest node can be 1 m
away in the worst case (√2 m diagonally), and a footprint centred on an odd x or z has **no sampled
collider node inside it at all**. So the player walks over a visible pit. This is a live bug today and it
is what actually stood between the player and the crater - not the render resolution.

**Hypothesis 3 - "just add the 0.5 m lattice and the crater becomes physical." REJECTED - it would have
made the mismatch WORSE.** A finer *render* lattice while the *collider* stays coarse moves the two
surfaces further apart, not closer. The render/collider pairing is the invariant; the render step alone
is not. This redirected the task: fix the collider first (1ex), independently.

**Hypothesis 4 - the collider should be 1 m. CONFIRMED by the user's choice.** Three options were put
up (stay 2 m; global 1 m; 1 m only in the budget-exempt `MustCollideRadius = 2` zone). The hybrid looked
attractive - the exempt zone already cooks unconditionally - but the collider is cooked **once per
chunk** and cached, so a radius-dependent step needs a live re-cook/swap on an already-cooked
`MeshCollider`: new machinery, for a rim (0.33 m) that is below `stepOffset = 0.5 m` and steps over
regardless. Global 1 m is one constant; the user picked it and accepted the F2 cost (~405k collider tris
at full ring vs ~101k).

**Dead end - the "~7k-tri broadphase" figure.** Three separate comments claimed the collider ring is
"~7k-tri". Recomputed: ring 7 Chebyshev = (2·7+1)² = 225 bodies; at step 2 that is 225·450 = **101k**
tris, at step 1 it is **405k**. The "~7k" was wrong even before 1ex and is now replaced with the real
number. Lesson repeated from rule 7: a stale number in a comment is a claim that outlives its task.

**Hypothesis 5 - the fine lattice needs `RefineSubdiv` 2 → 3 (0.25 m). REJECTED for now - this is the
important one.** A research pass flagged a landmine: `IsRefined` and `ChunkObject.IsTileRefined` are
**boolean** tests, and `RebuildChunkRegion` compares vertex counts, so a 16 → 25 vert change is invisible
to both and the merged block table is silently mis-indexed. The intended fix was "make the predicates
count-based". The better fix is to **not change the vertex count**: `BuildRefinedMeshData` already builds
a 3×3 grid whose centre entry sits at local (0.5, 0.5). Storing *just that one node* yields a
half-offset lattice (2× the angular resolution for a rim ring) at 16 verts / 24 tris - so `IsRefined`,
`IsTileRefined`, `PatchRegion`'s count logic and `BuildCornerGrid`'s border-slot coincidence are all left
exactly as they are. **Resolution is not the same quantity as DOF count.** Waiting for a measured reason
to go to 0.25 m.

**Hypothesis 6 - caves are part of the same "finer terrain" job. REJECTED.** `ChunkData` holds **one
height per column** (`Size = 1f`, `VertexCount = 4`); every mesh and collider derived from it is a
heightfield `y = f(x,z)`, which **cannot have a roof**. `TerrainShape` has no `Cave`. The only cave that
ever existed, `SculptVoxelCave`, is a **sealed bubble** (its remove range opens no column's overburden)
and the voxel mesher admits it renders **no cavity side walls** (`VoxelMesher.cs:71-73`). So "caves" is
a *different data model* (a second surface, or the voxel run-list path re-litigated), not a resolution
setting. Split to 1ew; the user agreed.

**Why 1ex shipped alone.** It fixes a bug that is live *now*, needs no lattice, and is one constant.
Sequencing it ahead of 1ey keeps the F2 before/after attributable to the collider alone. If 1ex and the
lattice had shipped together and F2 regressed, the collider cost and the lattice cost would have been
one number.

---

## 1io - "F1 is free, grep says so" - VERDICT: REJECTED (the lane was on a bound key; check 8 now mechanises it)

The user said F1 is their fighting-mode switch. They were right, and the finding is worth more than
the rebind: **"grep found no references" was a claim about the patterns I typed, not about the code.**

### H59 - "no `Key.F1` and no `KeyCode.F1` means F1 is unbound" - REJECTED

The project uses the Input System exclusively (zero legacy `Input.*` calls anywhere in
`Assets\Scripts`), and that API binds a key three ways:

```csharp
Keyboard.current.f1Key          // property name  <-- the one 1in's grep did not match
Keyboard.current[Key.F1]        // indexer + enum literal  <-- the one it DID match
someKeyboard[SomeLaneKey]       // indirection through a serialized field
```

`Player\PlayerController.Interactions.cs:521` uses the first: `Keyboard.current.f1Key` ->
`ToggleCombatMode()`. So 1in grepped the enum literal, found nothing, wrote "F1 has NO binding
anywhere in `Assets\Scripts`" into both the tooltip and `HANDOFF-1in.md`, and shipped the lane.
Pressing F1 ran the audit **and** toggled fighting mode, which draws weapons and resets the
`ToolManager` selection — so the measurement was taken under a moving scene. This is rule 7's
"measure before fixing" being satisfied in form and void in substance: the number existed, and it
described a frame in which the player had just been disarmed.

### H60 - "F1 is a skill hotkey" - REJECTED; it is the combat-mode toggle

This claim had been sitting in the codebase since 1hy as the *reason* F2/F3/F4 were considered
available, appearing in four tooltips and three older task entries. 1ik noticed it was unsourced
and deliberately declined to repeat it — correct call, wrong conclusion, because the right move was
to go find the binding rather than to stop quoting it. The answer was one grep away the whole time
and nobody asked the question in the right vocabulary. **A recorded justification outlives its task
and keeps doing duty long after the thing it justified is gone** (rule 7's "producers outlive their
consumer", one level out).

### H61 - "F13 is free" - SUPPORTED, and now enforced rather than asserted

Grep of both spellings (`Key.F13`, `f13Key`) across `Assets\Scripts` returns zero, verified against
the Input System 1.19 `Key` enum in `Library\PackageCache` (which lists `F13`-`F24`). `F13` is the
user's choice over `Numpad1`: it keeps the QA keys together on the F-row, at the cost of needing an
external keyboard or `Fn` on a laptop.

### DEAD END - the check that fired 54 false positives on its first run

`StaticChecks.ps1` check 8 exists because of the above. The first version searched each lane key
as a bare substring and reported **54 candidates**: `Leaf4` contained `f4`, `#44FF44` contained
`ff4` -> `f4`, `Pagoda_Roof4` contained `f4`, and four tooltip strings named the key in prose. That
is precisely the failure rule 7 documents — a check that cries wolf on the first file it is added
to has a silence nobody can read any more — and I had walked straight into it by treating a key name
as if it were a token. Anchoring the pattern on a `Keyboard`-typed expression (`\b\w*[Kk]eyboard\w*\s*[.\[]`)
cut it to zero.

The second dead end is the more interesting one: the anchored pattern still reported **clean**,
because `Keyboard.current.f1Key` is two member hops and the capture stopped at `current`. A check
that is green on the exact bug it was written for is worse than no check, because it buys false
confidence rather than an absence of it. Fixed by making `current.` an optional middle segment and
normalising `f1Key` -> `F1`.

Only then was it verified in the direction rule 7 asks for: revert the lane to `Key.F1`, watch it
name `Interactions.cs:521`, restore `Key.F13`, watch it go quiet.

### What this cost, concretely

Nothing shipped *broken* — the crater audit itself is read-only and its arithmetic is unchanged.
But the readout it was built to produce cannot be trusted if it was taken on F1, so the measurement
has to be re-taken on F13 before 1ex can be scoped from it. A wrong key is cheap; a wrong key that
silently contaminates the measurement it was introduced to protect is not.

---

## 1in - "the dent looks like the ground was pulled down" - VERDICT: OPEN (measurement shipped, awaiting readout)

The user asked for a raised-rim crater instead of stretching the surrounding tiles, and mentioned
caves. **No behaviour has been changed.** This is the reasoning trail behind the crater measurement.
Note the lane shipped on **F1**, which was already bound to the combat-mode toggle — see **1io** for
that; the lane is now F13 and any F1 readout is void.

### H54 - "the dish is written to nodes the render path never samples, so it is invisible" - REJECTED as the default

Tempting, because rule 12 makes it a real failure mode and the seam work (1i9) had just been bitten
by it. But it does not fit the default configuration: `EffectiveLowPolyStep` is **0** when
`LowPolyFacets` is false, and at step 0 the surface holds **every** 1 m lattice node, so every corner
a carve writes *is* drawn. The rule-12 trap needs `step > 0`.

The residual worry is that `LowPolyFacets` is set **at runtime** (the test ground pushes its own
mirrored value in `Awake` before the first poll), and I found no serialized override in any
`.unity`/`.prefab` in the repo. So the value in the session the user is looking at is UNKNOWN to me.
That is a premise question, not a hole in the theory - which is why it is a fingerprint line, not an
assumption baked into the audit.

### H55 - "1ew's adaptive refinement should already be subdividing the crater" - REJECTED

`ChunkMeshGenerator` emits a refined 2x2 block when a tile's corner SPREAD exceeds
`DefaultRefineThreshold = 2.5f`. A 1.9 m-reach, 1.1 m-deep crater over a 1 m lattice produces a corner
spread on the order of the depth, around 1 m - comfortably under 2.5. So no refined block is emitted.

Worse for the purpose even if it were: the refined block's 3x3 fine heights are **bilinear** samples of
the *same four coarse corners*. It adds normal variation; it cannot add shape. So "the crater needs
subdivision" was wrong twice - it would not trigger, and it would not help if it did.

### H56 - "the shape is missing because a cone has no rim" - CONFIRMED as the mechanism, and it is the one that matters

The crater profile in `WorldStreamer.Deform.cs` is a smoothstep **monotone** dish:
`target = current - s * CraterStep`, with `s` the smoothstep falloff and no term anywhere that RAISES.
A rim is a positive raise. A function that can only lower cannot produce one, at any resolution, by
any amount of subdivision. Section D of the audit therefore counts corners **above** pristine rather
than inferring a missing rim from the shape's appearance.

This is the finding that made me stop and measure rather than fix: the obvious "make it sub-tile so it
looks better" fix does not touch the thing the user actually named.

### H57 - "the crater reaches far enough to be worth sub-tile geometry" - SUPPORTED

A 1.9 m reach is 3-4 coarse nodes across. That is genuinely below the resolution where a floor, a wall
and a rim are separately expressible - which is also exactly why the user expects caves to be easier
afterwards. It is the reason the user chose the fine-lattice architecture over a crater-only overlay.

### H58 - "1ex can store sub-tile heights without breaking the seam proof" - CONFIRMED, with one carve-out

The no-crack proof rests on every tile edge being **linear** and continuous across a shared edge. A
naive per-tile 3x3 fine grid breaks it: each tile interpolates its own edge nodes, and two tiles
sharing an edge would disagree about the vertices along it. So 1ex stores **interior** fine nodes only
and leaves every edge node bilinear. Corner-lattice saves, cross-chunk seams and the existing
`ChunkMeshGenerator` edge pass are all untouched. Cost: geometry stops exactly at the tile boundary,
which is why this is a resolution change and not a seam fix.

### DEAD END - a half-metre bias I nearly shipped

The first profile ring used `FloorToInt(cx + 0.5 + cos*r)`. `ChunkData.Size` is 1 and the chunk
builder seeds corner `(gx,gz)` from `GetHeight(seed, tc.X*cs+gx, tc.Z*cs+gz)` - corners sit AT integer
world coords, so every ring sat half a metre out. I had been reading `CurrentHeightOf`, which uses
`(cx + 0.5, cz + 0.5)`, and carried its convention over. Two spellings of "a corner's height" in one
codebase; I picked the wrong one and the numbers would have been quietly plausible.

Lesson recorded in AGENTS rule 8's family: when a copy's arithmetic is the contract, read the
*writer*, not the reader that looks like it.

### DEAD END - assuming StaticChecks would catch the next mistake

It did not. `dishSpan = span;` written above `float span = 0f;` is CS0103, and the script reported
`braces 44/44 parens 263/263` on a file that could not compile - balance is not reachability. Rule 7
already warns that a check nobody has seen fail is not a check; the converse also holds. I did not add
a naive declaration-order scanner, because a detector that fires on every legitimate field read would
train the next reader to ignore it - which is the exact failure mode rule 7 documents. Found by
rereading; recorded as a known blind spot.

---

## 1ik â€” the frame-budget lane, and a report that named the wrong denominator first â€” VERDICT: OPEN (shipped, awaiting readout)

### H53 â€” "30 FPS with every streamer counter near zero means the streamer is the cost" â€” REJECTED

**The premise I brought in.** The user screenshotted the HUD: `FPS 30 (33.9 ms)`, `chunks 380`, `far
cells 1224`, `last poll 3.39 ms`, `lod sweep 0.62 / 60.56 ms`, and then told me FPS "mostly falls under
20". Every counter on that overlay is a *streamer* counter, and the streamer total was 3.39 ms of a
33.9 ms frame. The obvious reading is "the streamer is fine, the frame cost is elsewhere".

**Why the obvious reading is not yet evidence.** `peaks 0.00 ms` and `rebuilds 0` mean the player was
**standing still** and no poll had run inside the window â€” so 3.39 ms is the last poll from some earlier
moment, not a per-frame cost. Under that premise, "the streamer costs 3.39 ms" is a claim about a frame
that is not on screen. Rule 7 again: cheapest-check-first and premise-check-first coincide; a number
read out of an idle window describes the idle window.

**What is actually unknown.** The ~30 ms that is not streaming is unattributable, because the project
has **no CPU/GPU instrumentation at all** â€” grep for `FrameTimingManager`, `ProfilerRecorder`,
`Unity.Profiling` returns zero hits. So there is no fork to even ask the question with. 1ik builds the
fork before anything else touches it, which is the plan agreed with the user: *measure first, name the
mechanism, then fix in a separate commit.*

### H55 â€” "`QualitySettings.renderScale`" â€” REJECTED at compile time, and StaticChecks saw nothing

**What happened.** The first build of 1ik returned exactly one error:
`QualitySettings does not contain a definition for 'renderScale'` (CS0117). Render scale is not a
QualitySettings member at all â€” it belongs to the render-pipeline asset
(`UniversalRenderPipelineAsset.renderScale`).

**The part worth keeping.** `tools\StaticChecks.ps1` returned **0 candidates on the file that did not
compile**. Rule 3 says "review is not compilation" as a standing warning; this is the cleanest possible
demonstration of *what* review misses. The balance check counts braces and parens â€” it cannot know
which *type* a member is being looked up on, so an invented member name is invisible to it. There is no
amount of rereading that substitutes for a compiler here; the check was working exactly as specified
and still certified a broken file.

**A second finding fell out of fixing it properly.** The obvious fix is to reference the URP type
directly â€” but URP is referenced **nowhere** in `Assets\Scripts` (grep: zero hits), and this project
ships **two quality levels pointing at two different pipeline assets** (PC = render scale 1.0, Mobile =
0.8). So a single hard-coded number would report the wrong value on half the quality levels, and a
direct type reference would add an assembly dependency to a QA lane that exists to print one
diagnostic. The lane therefore reads it by **reflection off `GraphicsSettings.currentRenderPipeline`**
â€” the asset actually in force â€” and prints `n/a` when the pipeline is not URP. Degrading honestly is
the same absent-vs-zero rule the probe applies everywhere else (H54's sibling).

**What the settings dump revealed, which is not a renderScale story at all.** Both quality levels are
already favourable on paper: **vSyncCount = 0**, `antiAliasing = 0`, `m_SupportsDynamicBatching = 0`,
main light shadows **on at 1024**, and URP shadow distance 50. And the readout's `33.9 ms` with vSync
**off** means the frame is *not* quantised â€” so rule 7's clamp/bracket section should report "unclamped"
and section A's bracket never fires on this platform. The `lod sweep 0.62 / 60.56` reading remains the
1hk finding: a since-start monotonic max, most likely the initial fill sweep.

**The number that actually deserves suspicion.** `QualitySettings.shadowDistance` is **32 (PC) / 40
(Mobile)** while the streamed world spans ~300 m. Shadows are cheap here (a third of the frame budget at
1k) and the terrain is low-poly, so shadow *resolution* is not the problem â€” but this is the kind of
world-scale-versus-budget mismatch worth having a lane measure. It is **recorded, not acted on**: 1ik
measures, and changing a shadow distance is a separate task with its own before/after (rule 7 â€” never
ship the measurement and the fix in the same commit).

### H54 â€” "the fix is to re-enable `LowPolyFacets` (1ia left it off), so the near chunks go back to flat facets" â€” OPEN, and deliberately NOT acted on

**The hypothesis.** `LowPolyFacets = false` by default (1ia) means the near chunks render the *full 1 m
per-tile surface* with side walls and LOD children, and `PatchRegion` takes its per-tile skim. That is
a plausible cause of a heavy frame at 380 chunks. It is the single biggest lever I can name from the
code alone.

**Why I am not touching it in 1ik.** Two independent reasons, and the second is the important one:
1. It is a **render-algorithm** change, and rule 11 says that changes nothing already on screen â€”
   `_loadedChunks`, `_dormantChunks` and the far shell all hold old geometry until the resident terrain
   is dropped, and since 1hx the only honest remedy is a **play-session restart**. An A/B I cannot see
   in-session is not an A/B.
2. I have **no measurement** of which side owns the frame. Flipping a facet flag on a hypothesis, with
   a CPU/GPU fork unmeasured, is exactly the "a fix chosen without a measurement is a guess" failure
   rule 7 names. It may be correct and still change nothing.

So the flag stays off until 1ik's readout names the side. If the readout says GPU with a large
triangle count, this hypothesis graduates; if it says main-thread CPU, this hypothesis is irrelevant
and the lane points somewhere else entirely.

### The denominator bug, caught by rereading rather than by the check

My first verdict divided measured work by the **frame** time and thresholded it at a majority. Under
vsync that is not a share of anything: a frame can only be a whole number of present intervals, so
20 ms of work behind a 33.3 ms frame is a **full lost present** that reads as 0.60 â€” and a 0.5
threshold would refuse to name the side that actually did it. This is rule 7's "a verdict line that
conflates two different things makes a number unusable", in a new costume: same denominator, different
quantities. The fix was to measure dominance **between the sides** (CPU vs GPU), which needs no
reference frame and is clamp-proof, and to report the cost as a **bracket** under a clamp. Both habits
are now rule 7 bullets.

I also wrote a `sb.Clear()` mid-report while restructuring â€” which would have wiped every earlier line
of the readout â€” and caught it on reread, not from a check. StaticChecks is 0 candidates; it cannot see
a semantic mistake in string-building, which is exactly the class rule 7 warns about (a green check is
not evidence about the thing it does not model).

### The absent-vs-zero trap, and why the unit decides

The first `FbProbeMs` returned the raw value, so a **valid-but-never-filled** `ProfilerRecorder` would
have printed `0.0 ms` for the GPU â€” reading as "the GPU is free" for the one side the Editor cannot
see, which is the misreading that sends the next reader to the wrong subsystem. The fix: a time-valued
source maps 0 â†’ `n/a`; a count-valued source passes 0 through (0 draw calls is a real observation). The
branch is on the marker's own declared `UnitType`, not on which field is being read â€” the six call sites
would otherwise be six spellings of "is this a time or a count", which is rule 8's rotting second
spelling in numeric form.

## 1ic â€” the per-spell look-collision audit (F4) â€” VERDICT: OPEN (measurement shipped, awaiting readout)

### Why this section is separate from 1ibâ€“1ij

1ibâ€“1ij was planned as one block, and the plan's own step 1 was the audit: *"Add the read-only
per-spell look-collision audit lane **first** â€” one key, one number, cached on the HUD and logged â€”
so the jitter is tuned against a measurement rather than taste."* The working tree had 1idâ€“1ii
already written on top of the same eight files, so the audit's diff was indistinguishable from theirs.

Rule 7 says ship the measurement and the fix as **separate** tasks so the readout that justified the
fix stays in history. The user chose the split. Keeping 1ic alone also has a property I did not plan
for and only noticed while editing: the audit references `SpellLook` / `SpellImpactStyle` /
`SpellCastStyle` / `ProjectileShape` / `SkillCatalog.OfType` / `ClassSkillCatalog.All` â€” all of which
1ib committed â€” but the fx-budget HUD line I first wrote referenced `SpellImpactFx`, which does not
exist until 1id. That line would have made commit A non-compiling **on its own**, which is exactly
the kind of thing that only review catches and that a single squashed commit would have hidden. The
fx-budget line moved to 1id.

### H40 â€” "the collision metric can be `SpellLook.Fingerprint`, the hash 1ib already built" â€” REJECTED

**Hypothesis.** 1ib built `SpellLook.Fingerprint` for this purpose. Group the roster by it.

**Why it looked right.** It exists, it is a uint32 over the right axes, and it was written by me two
commits earlier with the audit in mind.

**Why it is wrong, and it is a category error.** A fingerprint is a *hash*. Hash collisions are
expected at 32 bits across 172 keys in the birthday sense, and more to the point a collision in the
hash says nothing whatsoever about whether two spells look alike â€” it says the buckets are full. So a
reported collision would be a claim about the instrument, not about the world. Rule 7's habit: *a
verdict line that conflates two different things makes a number unusable*. `LookKey` packs the actual
axes into 34 bits (3+3+4 for the enums, 8 per channel) instead, so "same key" means literally
"identical axes".

**Knock-on.** `SpellLook.Fingerprint` has **zero** callers after this and was only ever the measuring
instrument, so it is deleted in 1ig rather than left as an unused public API. That is also an
admission about 1ib: the plan said the audit would use it and the audit refused it.

### H41 â€” "Scale and Tempo should be part of the identity" â€” REJECTED

**Hypothesis.** Two spells differing only in `Scale` are different enough that the metric should see
them, since they are authored fields and the player can read a size difference.

**Evidence against.** A tick at scale 1.00 and a tick at scale 1.08 are the same picture. If the
metric counted them, `M` would report "distinct" while the screen shows two identical flashes â€” which
is precisely rule 7's *"gate a classifier on the width of its own test"*, where the test width (the
8-bit colour quantisation plus the excluded axes) is smaller than the real threshold the player is
being asked to see. The audit states its own axes in the headline so a screenshot cannot be
misread: `axes = impact+cast+shape+coreRGB@8bit (scale/tempo excluded)`.

**The cost, stated plainly:** the metric is therefore *blind* to a spell that differs only in tempo.
That is an accepted false-negative, and it is the correct direction to err â€” the failure mode is "the
audit says clean and the player sees a near-duplicate", not a fabricated finding.

### H42 â€” "colliding groups should be a count of spells, not groups" â€” REJECTED as the headline

A number of *groups* and a number of *colliding spells* are both true and they answer different
questions. "3 colliding groups" reads as severity (three places to go look); "14 spells collide" reads
as scope. The headline carries both the group count and the worst group size, which is what you need
to decide whether to retune one family or split one school. Also: `worst` was seeded at 1, so a run
with zero collisions printed `(worst 1)` â€” a number that looks like a finding and is the absence of
one. Now prints `(worst none)`.

### H43 â€” "group by the roster's own `Spell` objects, not by resolved looks" â€” CONFIRMED

The walk has to be the set of spells the player can *reach*. `SkillCatalog.OfType(SkillType.Magic)`
gives the 16 + 151 = 167 magic `Spell(...)` call sites; `ClassSkillCatalog.All` yields 6 constructed
class spells, **5 of which are reachable** â€” `MakeSpell` is called with `summon == true` from one site
that is commented out. The dead twin in `RaceSkillCatalog.MakeSpell` is kept for signature parity
with `ClassSkillCatalog.MakeSpell` and is *excluded from the denominator*: a duplicate signature is
not a second reachable spell, and including it would make `N` wrong in a way that looks like a
collision.

### H44 â€” StaticChecks check 1 reported the new audit as `parens 840/841` â€” FALSE POSITIVE, and it was on its first file

**What happened.** The audit's summary line ends `.Append("), ")`. Check 1 counted raw characters, so
a `)` inside a **string literal** read as an unmatched paren.

**Why this one mattered more than the number.** Rule 7's standing lesson, twice written down now: a
check that flags a false positive on the first file you add it to is a check whose silence has stopped
meaning anything. 1hy's check 4 reported every `out` parameter as an unassigned local; a reader
trained by false candidates waves through the next real CS0165. My first instinct on seeing
`840/841` was to write "expected" next to it in the doc. That instinct is the failure mode. The
number was wrong and the check was wrong, in that order of importance â€” the file was fine, and so was
my code in it.

**The fix.** Character-wise strip of comments (`//`, `/* */`), regular strings/chars (with `\` escape
handling, bailing at EOL on an unterminated literal), and verbatim `@"..."` (with `""` escapes). One
pass, not a regex: a regex cannot tell an escaped quote from a closing one, and it is exactly the
escaped quote that a naive `".*?"` would get wrong in a *different* file, which is a quieter way to be
wrong than the first one.

**Verified in both directions**, since a check that has never failed has not been tested:
- injected `if ((shape == TerrainShape.None || radius <= 0f) return;` into
  `WorldStreamer.Deform.cs:38` â†’ `!! Deform.cs braces 60/60 parens 278/277`, then
- `git checkout --` the file â†’ `ok Deform.cs braces 60/60 parens 277/277`.

The tree is 0 candidates. Note the second number: the raw count before stripping was 840 open / 841
close, and **after** stripping the audit file balances at all four â€” so nothing else was hiding behind
the one visible candidate.

### What 1ic does NOT establish

It is a *static* walk. It resolves looks into a dictionary and prints. It spawns nothing, casts
nothing, and reads no `SkillFx` state, so it cannot see a family that is chosen correctly but fails at
runtime (the 1hy lesson: a static proof about a generator is a claim about the premise, not about the
screen). `M == N` is therefore **necessary, not sufficient** â€” the play-test still has to fire one
spell per school and confirm the halo, impact family and body shape read as different and each match
its school's family.

## 1ij â€” the last colour consumers, and the bench that was lying about its own subject â€” VERDICT: OPEN (shipped, awaiting the 1ic number)

### H52 â€” "no skill was loaded for 1ibâ€“1ij" â€” CONFIRMED as an omission, not a decision

**The fact.** Three commits (`de09e10`, `97175e8`, `c209956`), ~20 C# files, zero Skill tool calls. The
user noticed and asked. That is the correct outcome of asking, and the wrong outcome of shipping.

**Why it is worth writing down rather than just fixing.** I had a *reason* â€” the installed set is
Scenario's DCC-heavy skills (Blender/Maya/ZBrush/Unreal/image-video-audio), and the nearest Unity ones
drive an editor over MCP or `-batchmode`, which rule 3 forbids. So the reason existed. What did not
exist was the habit of **saying** it, and `AGENTS.md` rule 6 requires a stated verification basis in
every closing summary. A reader of that handoff could not distinguish "I checked the set and nothing
governs a C# code-review task" from "I never thought about it" â€” and in my own reading of the
transcript, the second was the truth, with the first constructed afterwards. That is the failure: a
post-hoc rationalisation is indistinguishable in the record from a prior decision, so only a
**pre-commit** statement carries information. `AGENTS.md` rule 15 now requires the line
(`skills: none applied â€” <reason>`) at handoff time, and this entry is the counter-example.

**The distinction rule 15 asks for, stated once so it is reusable:** *authoritative* means the skill
governs both the artifact and the verification path. *Informative* means it can sharpen an approach but
cannot confirm the result. Loading an informative-only skill is allowed and sometimes worth it; the
failure is not the omission, it is the unstated omission.

### H49 â€” "the bench can keep using the (DamageType, ProjectileShape) overload; it shows the shape" â€” REJECTED

`SpawnMagicModels` called `CreateProjectileDisplay(skill.DamageKind, spell.Shape, â€¦)`. It looks
correct: it passes `spell.Shape`. But **that argument is dead on arrival** â€” the identity-less overload
calls `SpellLook.Resolve(type, shape)`, and `Resolve` treats any non-`Auto` shape as authoritative:
`display = shape != Auto ? shape : Pick(fam.Shapes, 0.5f)`. So the bench passed the *behavioural*
shape flag into a slot the resolver treats as the *drawn* body, which is the exact
`Shape` vs `DisplayShape` conflation 1ih was written to prevent. It is the rule-13 field-merge bug
reaching the QA surface.

**The part I should have caught before writing the loader.** The bench is the one screen whose entire
purpose is comparing per-spell bodies. It was rendering every Fire spell through the school stand-in â€”
so the bench could not have shown me the thing 1ih changed, and if the families had collided I would
have seen it as "identical bodies" and blamed the family tables. **A QA surface that takes a shortcut
around the feature it exists to inspect is worse than no QA surface**, because it produces a confident
wrong answer. This is the same shape as 1hy's section C: the check was running, printing real numbers,
and its premise was false.

Knock-on found by the grep: `CreateProjectileDisplay(SpellData)` â€” written in 1ig, documented as "the
bench's per-spell display" â€” had **zero callers**. So the loader is why the old overload was still
alive, and why `DecorateProjectile(DamageType, â€¦)` was genuinely dead rather than merely unused. The
"unused API" was the *new* API; the live one was the legacy path. **When something looks dead, check
whether the thing that replaced it was ever wired up** â€” the dead member and its replacement are often
the same bug.

### H50 â€” "SpellZone.BuildVisual(type) should take the spell's DamageType" â€” REJECTED: it was never
missing, it was a duplicate

`Initialize` already resolved the identity-less fallback into `_look`:

```csharp
_look = spell != null ? SpellLook.Resolve(spell) : SpellLook.Resolve(DamageType.Wind, ProjectileShape.Auto);
BuildVisual(spell != null ? spell.Type : DamageType.Wind);   // same information, again
```

and `BuildVisual` then did `_spell != null ? _look.Core : DamageNumber.ColorFor(type)`. Two sources of
identity, differing on the null path: `Resolve`'s fallback for `DamageType.Wind` versus a literal
`Wind` re-resolved through `ColorFor`. They agree today, and they would keep agreeing until someone
changed one.

**The habit:** a parameter whose only purpose is to feed a value already computed one line above is
either a leftover or a symptom of the resolver not being trusted. Here it was both. Deleting the
parameter deleted the possibility rather than documenting it â€” but the *class-level* lesson (1ie) is the
one in `AGENTS.md` rule 13: seed the fallback unconditionally into the field, read the field.

### H51 â€” the labels on the bench should keep school colour, because they label the SCHOOL â€” REJECTED

The row is labelled `skill.displayName` â€” the **spell's** name, not the school's. So a school-tinted
label next to a per-spell body is simply wrong: it claims the row is one Fire thing when it is a
specific spell. Same failure as the body, one layer up, and the same fix. (The *swatch* case is
`MagicTestMatrix`'s school header, which genuinely does label a school â€” that is why those two look
similar and are not the same edit. H48 vs H51.)

### The remaining `DamageNumber.ColorFor` sites are all non-spell â€” verified, not assumed

20 sites survive, and each was read rather than pattern-matched: class/race **skill** slash flashes and
rings (`ClassEffect`, `IEffect`, `RaceEffect` â€” `SkillEffect`s with a `DamageType`, no `SpellData`), the
**ranged-weapon** flight cone (`ranged.ShotType`), the skill-tree node tint and the UI node legend
(`CharacterInfoUI.Skills`), the Earth **debris accent** inside a projectile body builder, and the
identity-less fallback's own definition. None of them has a spell to resolve, which is exactly the
condition under which `SpellLook.Resolve(DamageType, ProjectileShape)` exists. Converting them would
mean *manufacturing* a spell-less identity to throw away â€” rule 13's precedence step 3 is for that,
not a general "use SpellLook everywhere" sweep.

## 1idâ€“1ii â€” the six visual-family tasks â€” VERDICT: OPEN (shipped, awaiting the 1ic number)

### The split, and what it cost

The working tree had 1icâ€“1ii already merged across the same eight files. The user chose to pull 1ic
out first (shipped as `de09e10`), which left the other six to be repaired and shipped as one commit.
Hunk-splitting six tasks inside eight files would have been guesswork; batching them is honest as long
as the batch's status says plainly that **none of it is verified until the 1ic readout lands**.

**The real cost of splitting 1ic out was a compile-order bug I introduced and then caught.** The
audit's HUD line originally printed `SpellImpactFx.DroppedSinceLaunch` â€” a class that does not exist
until 1id. Commit A alone would not have compiled. It only showed up because the staged file had to
be read line by line to confirm the audit's scope, not because any check fired. That is the same class
of near-miss as rule 3's "review is not compilation": a check that counts braces cannot tell you that
commit A depends on commit B.

### H45 â€” "`_look` is a field, so assigning it inside `if (spell != null)` is fine" â€” REJECTED

`SpellStorm` and `SpellSummon` both read `_look.Scale` *after* the guard. On the null path `_look` is
`default(SpellLook)`, whose `Scale` is `0f` (a struct field default, not a resolved fallback). So the
failure is not a crash â€” it is worse, a **silently wrong scale**: a spell-less storm's spawn ring drew
5Ã— too small, and a spell-less summon handed `SpellImpactFx.Spawn` the `Inherit` impact style, which
returns immediately, so it got no flash at all.

**The habit this exposes.** A nullable/conditional initialisation is only safe if you audit every
*reader* of the field, not every writer. Grep for `_look =` shows one assignment and looks correct;
grep for `_look.` shows the read outside the guard. `SpellZone`, `SpellBeam` and `SpellTornado` all
seed a named identity-less fallback on one line, which is what made the two outliers visible on a
sibling read rather than needing a null-check argument. **Prefer seeding a fallback unconditionally
over conditionally assigning**, because the fallback line is the thing a reader can see.

### H46 â€” "the rune ticks are guarded by `_runeTicks != null`" â€” REJECTED (this was live, not theoretical)

`CastingCircle.Build()`:

```csharp
_runeGroup = NewGroup("Rune");
_runeTicks = _runeGroup.transform;   // always assigned
if (shader != null) { ... create 8 tick cubes as children ... }   // conditional
```

and `Apply()`:

```csharp
if (wantRune && _runeTicks != null) { for (i < RuneTicks) _runeTicks.GetChild(i); }
```

Both `Shader.Find` calls returning null is not exotic â€” it is what a stripped URP build looks like â€”
and then `_runeTicks` is a **valid, empty** transform. The null check passes. `GetChild` throws
`ArgumentOutOfRangeException` every frame from `Apply`.

**The lesson, which is the general one:** `x != null` asks "do I have a reference?", but the code
needs "do I have the N things I am about to index?". Those are different questions and the second is
the one the loop is asking. A guard written against a weaker question than the code asks will pass
exactly when the code is broken. Counting (`childCount >= RuneTicks`) states the actual precondition.
This is rule 7's *"a check is only evidence if you have seen it fail on the thing it is for"*, applied
to an assertion: the assertion that was there had never been tested against an empty parent.

### H47 â€” "the identity-less release burst should get the Arcane school colour, since it is a
weapon and weapons are Arcane-flavoured" â€” REJECTED, and this one was my own regression

1if changed `Color color = DamageNumber.ColorFor(DamageType.Arcane)` in `PlayerController.Combat.cs`
to nothing (leaving `Color.white`) â€” or rather, 1if left `Color.white` and the uncommitted tree had
already turned it pink. The *reason* to change it is plausible: the sibling burst 20 lines above is
now look-derived, so the pair looked inconsistent, and "these two are the same event" is exactly the
kind of symmetry that makes a change look like a fix.

But the two bursts are **not** the same event. The one above is a *spell release* â€” there is a
`SpellData` and it has a look. The one below runs when **no spell is armed**: it is the plain weapon
release, and its whole job is to say "you released the weapon". A white burst reads as that. Arcane
pink reads as "an Arcane spell was cast", which is a false statement about what happened, and it is
the exact failure mode 1ib exists to prevent (a colour that asserts an identity nobody has).

**Kept from the sibling read, dropped from the sibling change:** matching the *shape* of the code
(one `var look = SpellLook.Resolve(spell)` + `float r` above, one plain literal below) is good. Matching
its *colour derivation* across a boundary where one side has no spell is not. Asymmetry is sometimes
the correct answer, and the comment has to say so or the next reader will "fix" it.

### H48 â€” "MagicTestMatrix should call `SpellLook.SchoolColor`; one palette is the whole point" â€” REJECTED
(user's call, and the reasoning was already in the codebase's own favour)

This is the one place where "no consumer re-derives a colour" has to yield, and the argument is not
about duplication at all:

- **It is a swatch, not a readout.** The matrix's per-school header exists so a tester can name the
  school at a glance. 1ib made the swatch identical to the thing being judged, so a mis-coloured
  spell became **invisible on the screen built to catch mis-coloured spells**. The dedup actively
  removed the QA signal. Four of nine schools drifted visibly.
- **1ib's own claim was "change nothing on screen".** This screen is not the game. Inheriting the
  palette bought nothing at all here, so the change was pure unrequested diff.

The cost is real and is stated in the comment: tuning a school colour now means touching two tables.
That is worth it, because the second table is the *control* in an experiment. It is debug-only and
save-invisible, so unlike a building save key there is no parity check needed â€” but the exception is
written down so the next reader treats it as deliberate rather than as the drift rule 13 is about.

### What this batch still cannot tell me

None of the six tasks has a runtime verdict. The families are **static tables** â€” a school owns N
members, a spell deterministically picks one â€” so "Fire and Ice cannot collide" is a claim about
tables, not about the screen. Only 1ic's readout speaks to the tables, and even that is necessary and
not sufficient: it resolves looks and prints, so it cannot see a family that resolves correctly and
then fails in `SpellImpactFx` (an `Inherit` leak, a material that fails to spawn, a pool that refuses).
The play-test list in `PROGRESS.md` is not a formality after the F4 number; it is the other half of
the evidence.

## 1ibâ€“1ij â€” per-spell visual identity for all 172 spells â€” VERDICT: OPEN (plan of record)

### The request

"Since you got a new skill set now, remodel the magics so each can be unique." Read as: **visual
identity**, not mechanical divergence â€” the user picked "Visual identity" when offered the layers.
Scope answered in the same round: impact + cast + projectile, and **deterministic-from-id plus ~21
hand-authored overrides**, because 172 hand-tuned profiles cannot be maintained honestly.

### H34 â€” the new Scenario skills give me asset generation â€” REJECTED

The premise is wrong in a way that changes the whole approach, so it is checked first rather than
assumed. `C:\Users\antic\.config\opencode\opencode.jsonc` contains `{"$schema": "..."}` and **no MCP
servers**; the 128 `scenario-*` skills are **markdown guidance** (patterns, pitfalls, checklists),
not generators, and no Scenario MCP or image/VFX tool is exposed in this session. So this task is
**code-authored**, which happens to be the project's stated house style: `SkillFx.cs:4-8` says it is
"Deliberately prefab-free so skills read without authored assets."

Worth keeping in the record because it is the kind of premise that silently produces a plan full of
"generate a prefab for each spell" steps that nothing can execute.

### H35 â€” "impact FX is one shared function for all 172 spells" â€” REJECTED, and this was the load-bearing catch

This was my central claim, and it was **false in its reach while true in its letter**. The call site
is exactly where I said (`SkillFx.ImpactSphere` at `SpellEffect.cs:313`, inside
`ResolveProjectileImpact`) and it is indeed the **only** `ImpactSphere` call site in the codebase.
But only the **39 Projectile** spells reach it. The real shared seam is **`SkillFx.RingFlash`, ~121
spells, from 13 call sites across 8 files** â€” three times the reach of the function I had built the
plan around.

The corrected map (`SpellDelivery` â†’ strike visual):

| Delivery | # | Strike visual today |
| --- | --- | --- |
| Zone | **77** | `RingFlash` at spawn + persistent geometry; **no per-tick FX** |
| Projectile | 39 | `ImpactSphere` |
| Storm | 14 | private pooled `StrikeFlash` + `RingFlash` per strike |
| Instant | 13 | 1 self-buff ring; ~12 have **no visual at all** |
| Beam | 11 | `RingFlash` at spawn; **no per-tick FX** |
| Summon | 10 | `RingFlash` at spawn; turret bolts â†’ `ImpactSphere` |
| Vortex | 8 | `RingFlash` at spawn; **no per-tick FX** |

Two things fell out that the wrong claim had hidden. First, the ordering of value is inverted from
what I assumed â€” the win is `RingFlash`, not `ImpactSphere`. Second, **Beam/Zone/Vortex/Storm give
no on-hit flash per tick at all**, only a damage number: that is a *feedback* gap, and answering
whether to fix it is a scope fork, not a detail. The user chose to fix it (see 1ih).

The lesson is 1i1's, one level up: a correct claim about a code path is a claim about a *premise*, and
I had the premise ("one function serves everything") without ever counting deliveries. Reach has to
be measured, never inferred from reading one call site.

### H36 â€” `SpellData.Shape` is only a visual â€” REJECTED, and it would have broken homing

`SpellEffect.cs:75-76`:

```csharp
if (_spell != null && _spell.Shape == ProjectileShape.Missile)
    _homing = true;
```

So `Shape` is **partly behaviour**. A deterministic shape picker that wrote back to `spell.Shape`
would silently turn **homing on** for arcane-missile-family spells â€” a gameplay change disguised as
a look change, invisible in a screenshot and lethal in play. Therefore `SpellLook` carries a
**separate `DisplayShape`** and the resolver **never writes `spell.Shape`**. The 36 explicitly
authored shapes are also real design work, so they win outright; determinism only fills the
`Auto` gap (3 spells: `magic_fireball_spell`, `magic_frostbolt_chill_spell`, `cls_mage_fireball`).

### H37 â€” a second colour map already exists, so adding a third would be rule 12 â€” CONFIRMED

`MagicTestMatrix.SchoolColor` (`MagicTestMatrix.cs:373-388`) re-spells the DamageTypeâ†’RGB table with
**different values** from `DamageNumber.ColorFor`: Dark `(0.70,0.55,1)` vs `(0.85,0.45,1)`; Wind
`(0.65,1,0.85)` vs `(0.7,1,0.95)`; Arcane `(1,0.55,0.90)` vs `(1,0.5,1)`; Ice `(0.55,0.85,1)` vs
`(0.5,0.85,1)`. Thirty-plus `ColorFor` call sites already exist. Shipping a new palette without
folding these in would leave **three** independently spelled tables drifting â€” exactly rule 12's
"second spelling that rots", and this work would be the one that tips it over. Folded in (user
confirmed).

### H38 â€” pooled FX can go through `ObjectPooler` â€” REJECTED

`ObjectPooler.SpawnTransient` keys its pool by **prefab** (`prefab.GetEntityId()`), and every one of
these FX is **built at runtime from primitives**, so there is no prefab to key on. The precedent to
follow is `SpellStorm.StrikeFlash` (`SpellStorm.cs:246-320`): a private static list, an `Acquire`
scan for an inactive instance, and an `Update` that fades then either re-pools or destroys.

Two constraints carried from reading it. **Its `PoolCap = 32` was sized for 14 storm spells** â€” with
Zone 77 + Beam 11 + Vortex 8 also flashing, ~101 spells share the load, so copying the cap would
thrash; 1id raises it and adds a **per-frame spawn budget**. And **each instance allocates its own
`Material`**, which is why it must never use the shared `SkillFx.SharedSpriteMaterial`: fades write
to the material in place, so a shared one would corrupt every live flash of that colour
(`SkillFx.cs:17` says this in as many words).

### H39 â€” put the Zone tick flash in the damage loop â€” REJECTED

`SpellZone.Tick` (`SpellZone.cs:67-104`) iterates **overlapping colliders**, not ticks. A flash placed
inside that loop fires **once per target per tick** â€” a Blizzard over eight enemies spawns eight
spheres every 0.5 s, i.e. 16/s from one spell, which is how "add per-tick feedback" turns into a
frame-rate problem. The tick FX therefore goes **outside** the loop: one flash per tick per zone.

### The plan of record

One file, one resolution site. New `Assets\Scripts\Combat\Effects\SpellLook.cs` holds a `SpellLook`
struct (core colour, scale, tempo, impact style, cast style, display shape) resolved in exactly three
steps â€” **authored â†’ deterministic (FNV-1a over `spell.id`) â†’ school default** â€” with a per-school
family table so a Fire spell never draws a crystalline shatter. `MagicTestMatrix.SchoolColor` folds
into it. Two invariants: `DisplayShape` never writes `spell.Shape` (H36), and the resolver is the
only thing in the project that derives a spell's colour or shape.

| # | Task | Ships |
| --- | --- | --- |
| 1ib | `SpellLook` + resolver + school family table; `SchoolColor` folded in | new file, zero behaviour change |
| 1ic | read-only collision audit on the test platform | distinct tuples vs spell count |
| 1id | pooled tick/impact FX, per-frame budget, raised cap | the ~101-spell load `StrikeFlash` can't take |
| 1ie | `RingFlash` seam | **~121 spells â€” the real win** |
| 1if | `ImpactSphere` + `CastingCircle` seams | |
| 1ig | projectile `DisplayShape`; identity-less fallbacks for `DecorateProjectile` / `CreateProjectileDisplay` | |
| 1ih | per-tick flashes on Zone / Beam / Vortex | newly accepted scope |
| 1ii | 21 authored overrides via trailing optional param; `RaceSkillCatalog.MakeSpell` twin parity | |
| 1ij | bench shows full identity; `game-design.md` Â§3.8 + AGENTS + PROGRESS + THINKING sync | |

### How this will be judged

**172 spells â†’ N distinct `(impact, cast, display-shape, core-colour)` tuples, with colliding pairs
printed.** If N is not 172, the deterministic jitter is under-tuned and 1ib is not done, no matter how
it looks on any single pedestal. That number is task **1ic**, and it ships *before* the visual work
precisely so the jitter is tuned against a measurement rather than against taste â€” the 1i-series'
standing lesson. Eyeballing 172 pedestals is not verification.

Two things that make this cheaper to ship than 1ia: **no restart is needed** (projectiles, rings and
flashes are built fresh per cast, so nothing is a stale resident mesh), and **no prefab, asset, shader
or `SpellData` serialization is introduced** â€” all 172 spells keep their delivery, damage, cost and
school, and the 167 `Spell(...)` plus 5 `MakeSpell(...)` call sites keep working untouched, because
every one passes â‰¤7 positional args and names every optional.

### Verified fact base for whoever implements this

- `ProjectileShape` (`SpellData.cs:50-88`): `Auto=0, Bolt=1, Sphere=2, Shard=3, Lance=4, Spear=5,
  Blade=6, Splash=7, Comet=8, Missile=9, Dart=10, Debris=11`. **Exactly one** switch on it â€”
  `BuildProjectileBody` (`SpellCaster.Projectiles.cs:120-136`) â€” and note **`Sphere` and `Auto` have
  no case**, both falling to `default` â†’ `Cluster("Orb", â€¦)`. "Authored shape" and "rendered body" are
  therefore not 1:1.
- `DamageType`: `Physical=0, Fire=1, Ice=2, Lightning=3, Holy=4, Dark=5, Wind=6, Earth=7, Water=8,
  Arcane=9`. `DamageNumber.ColorFor` has 30+ callers, listed in the 1ib log entry.
- `Spell(...)` (`SkillCatalog.cs:124-131`): 167 call sites (16 `SkillCatalog.cs` + 151
  `SkillCatalog.Magic.cs`), all â‰¤7 positional args, so a trailing optional param is additive.
- `MakeSpell(...)` exists **twice**: `ClassSkillCatalog.cs:126` (5 live call sites) and a
  byte-identical **dead twin** at `RaceSkillCatalog.cs:133-135` (0 call sites). Change one signature
  without the other and the twin goes stale silently â€” rule 12 again, pre-existing.
- `ObjectPooler` queues are **unbounded**; the cap of 32 belongs to `StrikeFlash`, and `DamageNumber`
  has its own pool capped at 256.
- Only **6 of the 16** "base" spells in `BuildMagic` are true Layer-0 roots (Fireball, Frost Bolt,
  Dark Bolt, Wind Gust, Water Bolt, Stone Shard); the other 10 resolve to L1â€“L3 via `ExpandTree`'s
  prereq-depth walk. The authored-override set is "the 16 hand-authored base pass", **not** "the 16
  Layer-0 spells".

---

## 1ia â€” revert the terrain render algorithm to pre-1hi â€” VERDICT: OPEN (shipped as a default flip, awaiting restart + F3)

### The request, and the reading I chose

"Revert the terrain algorithm to what it was before the first low-poly request, only that." The
interesting word is **algorithm**, and it is ambiguous in a way that changes the size of the change.

**Reading A â€” delete the 1hi work.** `git revert` the range, or hand-remove the flat-facet path, the
coarse root, the resample, the 1hi.2 winding fix. This is the most literal "undo".

**Reading B â€” make the world render the pre-1hi way again, by flipping defaults.** The code stays, one
boolean and two constants move.

I chose B, and I want the reason on the record rather than buried: **1i0-1i9 are about the corner
void, and deleting the render path would have destroyed the instrument.** F3 is a read-only
rendered-corner + void audit; it measures *what the renderer emitted*, so removing the renderer makes
the measurement meaningless and the experiment uninterpretable. Rule 7 says a fix chosen without a
measurement is a guess â€” but the converse also holds: a *revert* that deletes the thing being measured
is not a test, it is an unrecorded variable. And the user's goal is the corner gap, not a smaller
source tree. Under B, `LowPolyFacets = true` still brings the whole look back, byte-identical, so the
change is reversible in one edit and the deletion can be done later, deliberately, once the corner gap
is understood.

The cost of B, stated honestly: the source still contains a dormant render path, and a future reader
who greps for the facet code finds code that does not run. That is a real cost, and it is why the
dormancy is now written down in three places (the two tooltips, `AGENTS.md` rule 12, and `game-design.md`
Â§2.5a) instead of being left as a comment.

### H31 â€” "revert" is one value, `LowPolyFacets` â€” REJECTED, and this was the load-bearing catch

`LowPolyFacets` gates the flat-facet *path*, but not the far shell's *sampling step*.
`FarSectorStep(int span, int maxRing)` returns a constant with no reference to the flag â€” it ignores
its own parameters on purpose (that is the 1ej fix for T-junctions). So flipping the flag alone would
have left the far shell sampling every 6 m while every near chunk rendered at 1 m. Not a crash, and
not obviously wrong on screen: a coarser shell sampling is just a smoother, blurrier horizon. But it
means **the revert would not have been a revert** â€” the surface density of the far half of the world
would not have matched pre-1hi. Moved together: both to 3.

### H32 â€” the flag is the whole story, so the test platform can be skipped â€” REJECTED

`NewWorldTestGround.Awake` pushes `EnableLowPolyTerrain` and `LowPolyStep` onto the scene's
`WorldStreamer` *before* the first stream poll. It is the QA lane, and its two fields were still
`true` / `6`. So on the test platform the facet look would have been re-applied on every single
session, the revert would have appeared not to work, and the obvious next conclusion â€” "the revert is
broken" â€” would have been wrong. A mirror left stale is a silent inverse of the change. This is the
same shape as 1i9's H30 (a mechanism that cannot be live is not a cause; a mirror that cannot be
stale is not a mirror), and it is why the mirrors are in the same commit rather than "next task".

### H33 â€” no scene or asset overrides these fields, so the defaults are what runs â€” CONFIRMED

Checked rather than assumed, because a serialised `true` in a scene would have made the whole change
inert and invisible: `SampleScene.unity` contains neither `WorldStreamer` nor
`NewWorldTestGround`, and no `.unity`/`.prefab`/`.asset` in the project serialises `LowPolyFacets` or
`LowPolyStep`. The fields are plain C# defaults, so the defaults are live.

### H34 â€” the collider should be reverted too, for a clean pre-1hi â€” REJECTED, on purpose

Tempting, because "pre-1hi" is a clean boundary and the decimated collider arrived in 1hi alongside
the facets. But the collider is not a render-algorithm artifact: it is a **physics and budget** feature
(~4x cheaper PhysX cook), it has no visual effect, and the coarse-root coupling that made it
"ride the facets" is a fallback branch (`ColliderStep` returns the root's step *when coarse*). With the
facet path off, every chunk's step is 0 and the collider is the 2 m decimated lattice â€” which is exactly
what 1hi's smooth path did, i.e. what pre-1hi's *rendering* cares about. Reverting it would have thrown
away a performance win to make a diff look tidier, and would have made the A/B asymmetric in a way that
hides rather than reveals the cause under test.

### H35 â€” the missing side walls on the coarse root are the corner-gap cause â€” OPEN

This is the hypothesis the whole revert exists to test, and I am **not** claiming it. What is known:
1i1-1i8 localised the void to the near/far boundary, at the outer x/z corner tile of the loaded square
â€” one loaded quadrant meeting three far ones. The coarse-root path emits **no side walls** (documented
in Â§2.5: "no side walls, no per-tile blocks"), so wherever a loaded chunk and a far cell disagree in
height, the gap is see-through; the 1 m per-tile path *has* side walls, so the same disagreement is
filled. That is consistent with "a gap, not a crack".

What is not known, and what I want to be careful about: (a) whether the disagreement is a data-layer
error at all, as opposed to two owners legitimately describing the same surface at different
resolutions; (b) whether the 3 m vs 6 m step matters independently of the side walls â€” the revert
changes both, so a positive result does **not** by itself isolate the mechanism; and (c) per rule 7's
own warning, "drawn" and "flush" are separate properties, and a positive revert result would not tell
me which of the two the low-poly path was getting wrong. The revert separates *render path* from
*data layer* â€” a coarse but useful first cut. If F3 is clean after the restart, the next step is to
re-enable the look and instrument the seam, not to declare the side walls the cause.

### H36 â€” "refresh the map cache" means wipe the terrain save cache â€” CONFIRMED by the user, with a caveat

Asked what was meant rather than guessing, because the obvious target is destructive and irreversible
while the revert did not cause it. The four candidates and why the answer mattered:

1. **`worlds/{seed}/tc_*.dat`** â€” holds **heights**, i.e. data. 1ia was render-only and changed no
   heights, so nothing in here was stale; refreshing it could only ever lose edits.
2. **`_loadedChunks` / `_dormantChunks` / far shell** â€” the real holders of old-algorithm geometry, and
   rule 11 already says the remedy is a restart (the non-destructive wrapper was removed at the user's
   own request).
3. **`ChunkMeshGenerator`'s pooled-mesh pool** â€” rule 11: a `Mesh` there is a *buffer*, never a stale
   cache, because `UploadMerged` re-specifies every vertex and index on each upload.
4. **Minimap / `WorldMapUI`** â€” a compass strip and POI markers; neither samples terrain geometry, so
   the render revert cannot reach them. (Worth noting the *absence*: there is no terrain-render cache
   in this project by that name, which is why the request was ambiguous at all.)

The user chose the save cache and confirmed the edits are expendable â€” which is exactly the condition
rule 11 names for `ResetTerrainSaves()` being the right tool ("use it only when a pristine world is
the actual goal"). So the warning in the rule is satisfied rather than overridden.

Two things I checked before deleting rather than after:

- **Was Unity running?** No. This was the real hazard, and it is a trap worth writing down:
  `WorldStreamer.OnDestroy` flushes in-memory dirty chunks to disk "so quitting never loses a chunk",
  and there is a background save writer besides. Deleting the files under a live editor would have
  been silently undone by the next quit. `ResetTerrainSaves()` guards this internally with
  `FlushPendingSaves()` **before** the wipe (the 1es fix) â€” a raw file delete from outside has no such
  guard, so "Unity is closed" is a precondition, not a nicety.
- **What else lives in that save tree?** Rule 13's producer-outlives-consumer habit applied to a
  directory instead of a type: the seed dir held *only* the 13 `tc_*.dat` files, and
  `savegame.json` (a separate player save) was checked for terrain keys before being left alone â€”
  `time, player, inventory, gunAmmo, fields, buildings, quests`, no heights, so it cannot resurrect
  the edits being discarded. Deleting the whole `country life` folder would have taken the player
  save and Unity's analytics with it; deleting the whole seed dir would have been equivalent *this*
  time only by luck, and the code's own narrower `tc_*.dat` pattern is the defensible choice.

The wipe has a side effect worth noting because it is not obvious: **it retires the restart
requirement by accident rather than by design.** With the editor closed there is no resident terrain
to drop, so the next launch is already pristine *and* on the new algorithm â€” the two things rule 11
says cannot be had together. Had the user asked for this mid-session, the same wipe would have left
a mixed resident set that looked seamless and measured like a single generator, which is exactly the
premise failure F3's generator-revision fingerprint exists to catch.

### Verification stance

No build, per the project rule. What I did check: every gate that the flag controls, read by name â€”
`EffectiveRefineThreshold` returns `RefineThreshold` again; `EffectiveLowPolyStep` returns 0, so
`BuildMergedMeshData` cannot enter `BuildLowPolyMerged`; `FarSectorStep` returns 3; `ChunkObject`
derives `_meshStep` from it, so the patch branch and the LOD skip are both dead; and
`CraterFacetSkirt` early-returns on `facetStep <= 0` **before** its division, so 1i9's skirt is inert
with no divide-by-zero. `tools\StaticChecks.ps1` reports 0 candidates (and it does balance
`NewWorldTestGround.cs` and `WorldStreamer.FarShell.cs`, the two files with code changes). Per rule 11,
**none of this says the world on screen changed** â€” that requires a restart, and the F3 readout is the
only thing that can settle H35.

---

## 1i9 â€” why did the impact dent stop existing? â€” VERDICT: CONFIRMED (fix shipped, awaiting play-test)

### H30 â€” the directed-dig clip is eating the projectile crater â€” REJECTED by its own gate

`SpellEffect.cs:306` passes `_dir`, the projectile's *flight direction*, as the carve direction.
`Deform.cs:144` then multiplies a voxel-mode crater's influence by
`Clamp01(along / (radius*0.5) + 0.15)`, which is **exactly 0** for anything behind the cast plane,
and 0.15 at the centre â†’ an authored 1.1 m crater rendered as a 6.7 cm forward sliver. The
`1eu` commit that added the clip updated only the *tool* path (`TerrainDeformer.Dig` overload,
`ToolManager` passes `player.transform.forward`) â€” `SpellEffect.cs` is not in its file list, so the
projectile was never considered.

This had the shape of a real bug and I was one step from shipping a "fix" for it. It is gated on
`VoxelTerrainEnabled`, which is `false` in normal play: the only assignment in the project is the
opt-in test-ground lane, and no scene or prefab serialises it. **The clip cannot run, so it cannot
be the cause.**

The lesson, distinct from 1i4's: 1i4's defect was a construct in the wrong place, where every check
approved a thing that was not happening. Here the construct was in the right place and correctly
written â€” the *premise* was false. Both are invisible to reading, because both are statements about
whether code executes rather than about what it computes. So the habit is not "read more carefully"
but **"find the gate and read its value"** â€” a mechanism is only a cause once its enabling condition
is known to hold in the reported session.

### H31 â€” the crater is written to nodes the renderer never samples â€” CONFIRMED

`LowPolyStep = 6`; `EmitLowPolySurface` emits the visible surface from every `step`-th lattice node
at world `gx*step, gz*step`, giving `axis = 30/6 + 1 = 6` â†’ **25 quads, 100 vertices**. The user's
own F3 line read `verts 100 x243`, which is the readout that turned this from a hypothesis into
arithmetic. The dent's reach is `1.4 + 0.5 = 1.9 m`; the worst-case distance from an arbitrary
impact to a sampled node is `3âˆš2 â‰ˆ 4.24 m`. So ~69% of impacts move no rendered node, and the carve
is invisible however deep it is.

The "now" in the report is explained by the same number: at `step = 3` the worst case is 2.12 m,
so a 1.9 m reach *always* contained a sampled node and the dent rendered. 1hx moved 3 â†’ 6 for
facet-shading contrast and deleted the dent as collateral. Rule 12 already said "the edit
granularity is the step"; what it did not say is that a step change is a **deform** change, because
the deform's own reach is now below the sampling floor.

Two things I checked that would have spoiled the fix if I had skipped them:

- **The FX is outside the carve's gate.** The user saw the exploding sphere, which is spawned
  *after* the raycast branch (`SpellEffect.cs:313`). So "sphere plays, terrain does not" is not
  even evidence that the carve ran â€” it is the signature of a carve that was skipped, a carve that
  no-oped, and a carve that reverted alike. Two candidate mechanisms, one observation. The
  free measurement that separated them was asking whether the *shovel* still dug, because
  `TerrainDeformer.Dig` has no raycast gate: it would have located the fault in one step, and I
  asked it as a sub-question and then started reading instead of waiting for the answer.
- **A fix is not the widened reach.** The obvious move â€” make `reach` exceed `4.24` â€” silently
  multiplies the excavation rate, because a Crater ratchets `CraterStep` per cast: the "small
  universal dent" of 1bp becomes a deep pit, and the same widened bounds would re-footprint every
  raised shape. `Max` with the authored influence keeps the guarantee to exactly the case where the
  carve had no rendered geometry at all, which is the only case worth changing.

### H32 â€” the two coordinate spaces are not the same space â€” CONFIRMED, and it would have half-missed

The deform loop measures influence at `cx + 0.5` (a tile centre) but writes
`newHeights[EncodeCorner(cx, cz)]`, a corner *node* at world `(cx, cz)`. The facets are emitted at
`gx*step`, integers. So a skirt computed in the loop's own distance space peaks half a metre from
the node it is aimed at â€” which, against a 6 m falloff, would have left the guarantee technically
present and visually weak. The skirt measures the corner in the target's space instead. **A
guarantee must be expressed in the coordinates of the thing it guarantees.**

## 1i8 â€” is the edit in the save, or not? â€” VERDICT: OPEN (measurement shipped, awaiting readout)

### H27 â€” the resolver "cannot find" an edit that is provably in the file â€” the contradiction

On a uniform-stamp fresh restart, a chunk's *rendered* height **is** its *saved* height: a fresh
build reads nothing else. So for a `no-owner` node â€” edited side, pristine side, pristine side not
even modified â€” the edited chunk's save provably holds a non-pristine height at a tile touching that
node. The walk reads that save, examines exactly the four sharing tiles, and prefers any non-pristine
value over pristine. It should adopt. On 220 of 229 nodes it did not.

I eliminated every cause I could reach by reading: ordering (walk at :264 precedes the noise fill at
:290), seed (`Seed` captured at dispatch, `Streaming.cs:505`), the sanity band (Â±200 m, so it cannot
reject a 0.29 m dig), the unit question (`ChunkData.Size = 1f`), and the owner arithmetic (16/16 in
1i6). All pass. The measurement moved zero.

**That is the 1i4 shape again.** There, a check passed on a construct that was in the wrong place;
here, every check passes on behaviour that is not happening. The recurring lesson is not "read more
carefully" â€” it is that **a check I have never watched fail is not evidence.** I have now twice
derived a mechanism that every existing check approved and the world refuted, and both times the
defect was in something no check was watching. The response is to name the check the hypothesis
predicts should fail and go watch it, not to add a third mechanism to the list.

### H28 â€” I was wrong about the short-circuit, and the split said so in one line â€” CONFIRMED WRONG

I asserted `if (!float.IsNaN(corners[fx, fz])) continue;` was bypassing the seam walk, and I had a
plausible story: a mod stores all four corners, so a chunk modded nearby stamps the shared node with
a pre-edit height and the walk never runs. The measurement: `stamp-clobbered 9`, `no-owner 220`. Four
percent.

The story was not unreasonable, which is the useful part. It was a *hypothesis derived from reading
code* presented with the confidence of a conclusion, and it would have sent me to patch a loop
condition while the real fault sat elsewhere. The 1i7 split existed precisely to catch this, and it
did so within one readout. That is the argument for instrumenting before fixing: the split cost one
commit and one keypress, and it saved a wrong fix outright.

### H29 â€” my own verdict line has been the most expensive thing on screen â€” CONFIRMED

`far cells 0`, and the report still said `claimed-dead 3858 â€¦ which is a true hole`, with a nearest
ring and coordinates to walk to. B's "claimed" test is `FarCellForChunk`, the **ownership**
predicate â€” a cell *belongs* here â€” not a test that a cell *exists*. The two come apart exactly when
the shell has not filled, which is precisely when a player is most likely to press the key.

I have spent two rounds chasing a verdict that described ground the far shell had not reached yet. It
was ranked above the corner measurement because it was first, not because it was first *in importance*
â€” the same ordering error rule 7 warns about, where the aesthetic order of sections overrides the
premise order. Now B returns `B-unfilled` and D is tagged `[VACUOUS]`. **A verdict that cannot be
wrong about a premise is the most expensive line in a diagnostic.**

---

## 1i6 â€” how many owners does a shared corner have? Four, not one â€” VERDICT: OPEN (fixed, awaiting runtime)

### H25 â€” was 1i4's "canonical owner" the right single owner? No, and I had not enumerated them

1i4's comment asserted a rule: *the tile that owns a world node is the tile one metre back in each
axis, always as its NE slot.* That reads like a law of the lattice. It is not â€” it is **one of four
tiles** that share the node, and the lattice has no notion of an owner at all. A node is simultaneously:

    (wx-1,wz-1) NE | (wx,wz-1) NW
    (wx-1,wz  ) SE | (wx,wz  ) SW

The one-metre-back tile is merely the first, and picking it is a *policy*, not a derivation. Having
written the rule down, I treated my own policy as a property of the data and stopped looking.

I only caught it by doing the thing rule 8 asks for and 1i4 did not: enumerating the owners and
checking a concrete boundary node. On a west edge the split is 2+2 â€” two tiles in this chunk, two in
the neighbour â€” and 1i4 read one of the neighbour's two. And `if (fx != 0 && fz != 0)` skipped the
east and north edges entirely, so the seam only propagated westâ†’east, and only because the eastern
chunk happens to scan its own west edge. A local dig produces precisely the unhandled direction.

**The tell I should have caught immediately:** 1i4's fix was for a bug the user described as *"every
corner tile of real chunk"*, and the fix only ran on two of the four edges. The fix's scope did not
match the report's scope. AGENTS rule 7 already says this â€” *a scope is a claim about the mechanism,
so name which owner(s) the walk admits* â€” and I had written that lesson into the rules **two commits
earlier** after 1hy made the identical error by scoping a void walk to one owner. Same mistake, same
session, and the rule was in my own head.

### H26 â€” "this tile has a sane value here" vs "this tile edited here" â€” CONFIRMED, they differ

A `ChunkTileMod` stores **all four corners** of its tile, including corners the edit never reached.
So a tile edited *somewhere* still hands back a sane, correct-looking height for a node it never
touched â€” and it is that tile's *pre-edit* value, i.e. pristine noise. Taking the first sane value
would let an untouched corner outvote the one real edit and reintroduce the exact 0.29 m step the fix
exists to remove.

The fix therefore samples the node's pristine height once and treats any value differing by more than
0.01 m as the real edit, preferring it over any pristine-looking sibling. This is safe because the
audit already measured the shape: 345 of 400 nodes were *one side edited against pristine*, so
"exactly one non-pristine value" is the observed case, not an assumption. When nothing is edited the
first sane value is used rather than rolling independent noise per chunk, which keeps unedited seams
bit-identical too.

**Harness honesty.** The worked example failed twice before it passed â€” PowerShell's case-insensitive
variables made `$SLOT` and `$slot` the same variable, and then I compared a *local* tile corner
against a *world* node. Both were bugs in the throwaway check, not in the C#. Worth recording because
the instinct on a red check is to suspect the code, and here the code was right twice: a check that
reports FAIL is asking which of the two is wrong, and "my harness" has to stay on the list.

---

## 1i5 â€” CS0106, and the race hiding behind it â€” VERDICT: OPEN (fixed, awaiting compile)

### H23 â€” why did three checks miss a member declared outside the class? â€” because the error is not a balance error

`CS0106: The modifier 'private' is not valid for this item` at line 12. The method sat between the
`using` block and `public partial class WorldStreamer`. I had anchored the insertion on the first
`/// <summary>` in the file, which was the **partial class header** â€” the most plausible-looking
anchor in the file and the wrong one.

The interesting part is that every check I had was *satisfied*:

- grep found the symbol defined and called.
- brace and paren counts balanced, **because the method body and the class body were two separate
  well-formed constructs**. A balance check cannot see a construct in the wrong place; it only asks
  whether each construct is internally consistent. My "braces 25/25" line in the 1i4 commit was true
  and worthless.
- the review passed over it.

So the class of bug is not "I skimmed" â€” it is **every cheap check I have is blind to nesting, and the
one thing nesting determines is whether a member has a type to live in.** That is now check 7 (a
member at brace depth 0), and I verified it by putting the exact bug back and watching it fire, which
is the only way to know a green check means anything.

**Also surfaced by widening coverage.** Adding `FarShell.cs` to `$files` immediately reported a
pre-existing paren imbalance: a doc comment writing `[x0..xEnd)`, a half-open interval with a `)` and
no `(`. It had been in the file all along and invisible for exactly the reason above â€” a file nobody
was checking. Rule 3's "$files" instruction exists because coverage gaps look identical to passes.

### H24 â€” the race, and the idiom that made it invisible â€” CONFIRMED

`BuildChunkMeshData` is reached via `BackgroundGenerateChunk` on a **ThreadPool thread**. 1i4 preferred
a live `_loadedChunks` lookup for the owning chunk, and `Dictionary<K,V>` is not safe to read during a
concurrent write. The F3 audit reads `_loadedChunks` constantly and never races, because it is
main-thread-only and the lane is invoked from a key press. **The same line, in the same file, is safe
in one caller and a data race in the other.** A safe idiom is evidence about a call site, not about a
symbol.

Dropped the live path; the owner is read from its save file, which is what *this chunk* is already
built from one line earlier, so it costs nothing in fidelity. The residue â€” an un-flushed in-memory
edit â€” is the same bounded, self-correcting staleness 1i3 accepted, and it names a different root
cause (the save flush, not the lookup).

**Worth stating plainly:** I introduced this while writing a comment arguing that the live path was
"strictly better". It was strictly better *and* racy, and the argument had no thread in it. Speed of
reasoning and breadth of context are independent, and the second one is what the other habits buy.

---

## 1i4 â€” "every corner tile of real chunk" â€” the misreading, and the fix that should have been 1i3

### H21 â€” was the gap at the near/far boundary? â€” REJECTED. It was never there.

I asked the player to disambiguate "the outer z/x corner tile" and offered two options: the corner of
the loaded square, or the far rim. They answered with a third thing I had not offered: **"every corner
tile of real chunk."** That is the original 1hy report, word for word â€” *"in every chunk corner the
edge will not match"*. It was in the title of the very first entry.

**REJECTED, and the reason matters more than the fact.** My question was not wrong, it was
**leading**: I had two locations derived from my own hypothesis, and I asked which one, so the answer
selected between my theories instead of telling me the truth. The player chose the nearest of my
options rather than either, and I read their answer as confirmation. When a location cannot be
resolved from code, the useful question is what the artifact *looks like* â€” where it is in the world
is a conclusion, not a question.

The cost was concrete: 1i1 and 1i3 were both built and aimed at the near/far seam, and 1i3's own
commit message defers the 345 interior nodes as "a step the player has not reported seeing". I wrote
that sentence while the player was, in fact, reporting it. **Deferring a finding because I re-read
the report more narrowly than the reporter did is the worst of the rule-7 habits** â€” it inverts the
measurement back into a theory.

### H22 â€” the fix, and the arithmetic that nearly shipped wrong â€” CONFIRMED

The ownership rule turned out to be one line long: the stamp that writes a tile's corners is
`corners[LocalX+1, LocalZ+1] = Heights[1]`, so **the tile that owns a world node is always the tile
one metre back in each axis, always as its NE slot.** Every chunk at a seam can therefore ask the same
question â€” "what does the owner of this node say?" â€” and get the same answer, which is what makes the
seam converge regardless of build order. No reconciliation pass, no ordering constraint, no
`ReconcileNewlyLoadedChunk` hook: the corner becomes a *function of the world* instead of a *copy of
local state*. That is the actual difference between the two, and it is why this is a different shape
of fix from the `ReconcileModifiedBorders` approach I had been circling.

I hardcoded the owning tile as `(29,29)` in the first draft, on the strength of the four-chunk
worked example. Working a **west edge** through the same rule gives `(29, gz-1)`. The four-chunk
example is the one rule 8 tells you to use, and it is the one that hides this: at a corner every
quadrant happens to agree on the tile, and only an edge distinguishes them. Deriving the local tile
coords from the node is not defensive style, it is the contract â€” the copy's addressing *is* the
seam, and a seam rule verified at one node class is a seam rule verified at one node class.

**Shipped.** Boundary corners only, local writes never overwritten, resident owner preferred over
disk so the fix also self-heals the 1i3 class of staleness at the rim.

---

## 1i3 â€” the fix, and what it deliberately does not fix â€” VERDICT: OPEN (superseded in scope by 1i4)

### H18 â€” is 1i1's "far side is pristine by construction" actually the mechanism? â€” CONFIRMED, and it is smaller than I assumed

The run says `NO side edited 19` with `under 2cm noise floor 1`. I expected the corrected gate to
move most of those 19 into the noise floor. It did not â€” 19 stayed out. So the classification is not
simply being noisy, and the obvious conclusion ("2 cm is small, look elsewhere") is the one I should
refuse, because **small is not the question; attributable is.** A 0.21 m step that comes from a known
broken source is more worth fixing than a 0.4 m step that comes from a sound one.

### H19 â€” the third readout, read properly â€” CONFIRMED

Three things are now settled and none of them is the hole:

1. `B` reads `NOT DRAWN 0` over the identical band. The 2965 is gone entirely, so it was
   `FarShadowedByCoarse` and nothing else. **A retracted number that reappears as zero on re-measure
   is the only acceptable way to retire one** â€” and it is why I refused to write a replacement value
   in 1i2 rather than guessing one.
2. `D` still steps by `0.2084 m` with `no-far-surface 0`. The shell reaches the ring; it arrives at a
   different height.
3. `C` still shows 345 of 400 interior shared corners as one-sided edits.

Mechanism confirmed on both seams. Time to fix rather than measure.

### H20 â€” the fix, and the reason it is this shape â€” CONFIRMED by code, one asymmetry only

`BuildFarChunkCorners` reads mods from the **save file**; the real chunk renders its **live** corner
grid. That is the whole defect, and the reason it is *here* and not in the seam reconciliation is
positional: a span-6 far cell spans six chunks, so **the chunks at the inner edge of its own domain
are the loaded ring itself**. The far cell is not adjacent to the seam, it *contains* it. Which also
explains why the defect could only ever show at the rim: it is the one place where a far cell's grid
is built from a different source than the real chunk's, and the far cell's domain reaches back far
enough to touch a loaded chunk.

So the fix is not "reconcile the seam" â€” it is "stop reading a stale source". Prefer the live lattice
for loaded chunks' boundary corners. Two consequences I chose deliberately:

- **Boundary corners only.** Interior nodes of a loaded chunk are drawn *under* the real chunk, so
  correcting them changes nothing visible, and their lattice indices can differ on a refined tile.
  Paying an unverified risk for no visible gain is a bad trade even when the risk is small.
- **Not the interior 345.** Those are real-vs-real and this change cannot touch them. Bundling an
  unrelated fix into the one that is confirmed would make the next readout ambiguous about which
  change moved the number, and would deny the user a clean before/after on the gap they reported.
  Split them.

**The limitation I am shipping with.** Far cells are built once and cached, so a rim edit made after
a cell was built leaves the seam stale until that cell rebuilds. I considered invalidating far cells
on deform and rejected it: it widens the change into the streaming lifecycle, and the current source
was stale *by construction* (a save file), so this is a strict improvement with a known, bounded
residue. Recorded in the status block rather than buried.

---

## 1i2 â€” the readout's two loudest lines were both my own bugs â€” VERDICT: OPEN (lane corrected, awaiting clean run)

### H15 â€” "2965 footprints are not drawn" â€” REJECTED, and it was never about the world

The verdict said 2965 footprints had nothing drawing them, at ring 16 and beyond. That is a
half-kilometre-wide void ring, and the player, who had just pointed at a gap barely a chunk wide,
had not mentioned it. The two facts do not fit, so I read my own code instead of the world.

B required `!FarShadowedByCoarse(owner)` for a footprint to count as covered.
`FarShadowedByCoarse` is documented as "a coarser far cell that owns this footprint is already live
(1eq), so this cell must render nothing â€” it is a reserved shadow that takes over the instant the
coarser owner leaves". **It means the opposite of what I used it for.** I read it as "this cell draws
nothing and nothing replaces it" and used it as a precondition for coverage. Every reserved shadow in
the shell was therefore filed as a hole â€” 2965 of them.

**REJECTED as a world finding; confirmed as a lane bug.** And the shape of the bug is the point:
1hy's walk admitted loaded chunks with a live mesh and called everything else absent; 1i1's walk
admitted the one far cell the ownership predicate named and called everything else absent. Same
mistake, one level down. I had written the rule into AGENTS ("name which owner(s) the walk admits")
and then immediately committed it again one layer deeper. A rule you write down and then violate in
the next commit is worth less than a check that mechanically refuses the violation.

The honest residue: the corrected number is **unknown**, not zero. I retracted a wrong number; I did
not replace it.

### H16 â€” "19 nodes: no side edited, look at the lattice/seed" â€” REJECTED by arithmetic, not by evidence

This one nearly sent me after the corner lattice, which would have been an expensive dead end. It was
my own tolerance. `RenderedCornerTolerance` is 0.01 m, and I used it *both* as the "is this stepped"
threshold and as the "is this side pristine" test. Two values each within 0.01 of the same pristine
value can differ by up to 0.02 â€” so a node can be stepped *and* have both sides pristine. The
classification is only forced above **2 Ã— tol**, and 19 nodes fell in the 1â€“2 cm band where it is
not. They are the noise floor of two facets rounded to the same lattice, and the scary wording was
mine.

**REJECTED.** A classifier that can emit a confident mechanism name for arithmetic noise is worse
than no classifier, because it costs a real investigation. Gate it on the width of its own test.

### H17 â€” the surviving mechanism â€” CONFIRMED on both seams, and it is not a corner case

C: **345 of 400** shared corner nodes are one-sided edits, worst 0.2859 m, first at (30,60).
D: **17 of 76** boundary nodes are one-sided edits, worst 0.2084 m, `no-far-surface 0`.

Not a handful of seams â€” essentially every interior seam in the loaded square, plus the same fault
again at the near/far corner the player is actually looking at. Two independent seams, one mechanism,
and the D numbers are decisive on a point I had been treating as open: the far shell **does** reach
the loaded ring (`no-far-surface 0`), it simply arrives at a different height, because it reads
`ChunkTileMod`s out of the save files while the real chunk renders from live tile data.

**CONFIRMED.** 1i0's mechanism is real; 1i1's framing of where to look was wrong and is now
corrected. The fix is reconciliation, and 1i3 is where it belongs.

### Still open

- How much of the ring-16+ band is genuinely uncovered, now that the false positives are gone.
- Whether the one-sided edits are *all* from a single large flatten (plausible: the bench, the village
  pad, roads) or scattered. If scattered, the fix must handle per-tile cases; if one region, the
  geometry is incidental. **The fix's shape depends on this and the lane does not yet measure it** â€”
  that is the next thing to add, and it should be a count of one-sided nodes per contributing chunk,
  not a picture.

---

## 1i1 â€” "the gap only appears at the outer x/z corner tile" â€” VERDICT: OPEN (D shipped, awaiting readout)

1i0 was built to answer "why do 345 interior shared corners disagree by 0.29 m". The player's answer
redirected the whole entry, and it is worth recording how, because the redirect is the lesson.

### H12 â€” the reported location is the *near/far boundary* â€” CONFIRMED by the report, and it invalidates the framing of 1hy

I had spent 1hyâ€“1i0 treating this as a real-chunk-vs-real-chunk problem. Section C walks
`_loadedChunks` and compares chunks to each other, so "outer x/z corner tile" should have been
impossible to report against it. It is not â€” and the reason is that the outer corner of the loaded
square is exactly where the loaded set **stops** and the far shell takes over. A corner tile on the
rim of the 19x19 square has one loaded quadrant and three far ones. My lane compares only the
quadrants that are loaded, so the seam the player is looking at was never a candidate for a finding.

**Confirmed, and it kills my own framing.** H10 was about edits not propagating between two *real*
chunks. This seam is between a *real* chunk and a *far cell* â€” a different owner, a different build
path, and a different question.

### H13 â€” is section B contradicted? â€” NO, and the two now fit together

It is tempting to treat "B said 0 voids" as wrong. It is not. The far cell **is** live at the
boundary, so the footprint *is* drawn and B is correct. What B cannot say is whether the far surface
is at the same **height** as the real one. "Drawn" and "flush" are different properties, and a
see-through crack satisfies the first while failing the second. The low-poly root has no side walls,
so a step here shows sky.

That is the reframing that makes 1i1 obvious in hindsight: the defect is a **discontinuity between two
surfaces that both exist**, not a missing surface. My 1hy framing ("only a lifecycle failure can
produce a hole") was right about the hole and wrong about there being a hole.

### H14 â€” the mechanism, and why it is sharper here than in 1i0 â€” STRONG, measured by section D

`BuildFarSector` builds each far cell from `BuildFarChunkCorners`, which fills a corner grid out of
`ChunkTileMod`s read from the **save files**, falling back to `GetHeight` for anything unwritten. A
real chunk, by contrast, renders from its **live** `_loadedData`. So the two surfaces at the boundary
are not two readings of one thing â€” they are a save and a live buffer.

At an outer corner the asymmetry is forced. The loaded side may be edited; the far side is chunk
(10,10) and its neighbours, **past the loaded ring**, where `ChunkTileMod` can never exist â€” the far
band is outside collider ring 8 and the rim starts at 10, so a dig cannot reach it (the 1ek comment
in `BuildFarSector` says so explicitly). So the far surface is pristine noise by construction, and
one-sided edits are guaranteed to disagree with it. No far-side reconciliation exists:
`ReconcileModifiedBorders` repairs slab-wall bottoms between real chunks and is not in this code path
at all.

The pristine test from 1i0 is *sharper* here than it was in section C, which is the part I did not
expect. In C both sides could plausibly be edited, so "one side on pristine" narrowed the field. On
this seam the far side is **known** to be pristine, so a step with an edited real corner is not a
hint, it is a confirmation â€” and the expected result is the boring one: a few centimetres, a
`FlattenAt` feather rim applied while the far side had no save entry.

**OPEN until the readout.** The delta is the whole decision. Centimetres â‡’ one-sided edit â‡’ fix the
seam by giving the far cell's corners the same saved-mod reach the real chunks get (or reconciling
the loaded ring's outer edge against the shell). Metres â‡’ this is not an edit at all, H10/H14 are
both wrong, and the corner lattice itself is broken in a way rule 8 warned about.

### The methodological entry, which is the one I will actually keep

1hy's rule-7 discipline was "scope the walk to the band where a positive result is *possible*, and
print the band." I did that for section B and it worked exactly as designed. Then I scoped section C
to the loaded set â€” and **the scope silently encoded a hypothesis I never stated**: that the defect
is between two chunks that are both loaded. A correctly-scoped walk for the wrong family of owner
reports "clean" just as confidently as a correctly-scoped walk for the right one, and section C did
report numbers, which made it feel like evidence.

The generalisable form: **a measurement's scope is a claim about the mechanism, and it should be
written down as one.** 1hy printed B's band because B's band was a considered choice about void
possibility. C's scope was never printed and never argued, and it was the wrong one. Rule 7 has been
updated with this.

---

## 1i0 â€” the F3 readout: it eliminated the hole, and pointed at the save path instead â€” VERDICT: OPEN (one column short of a verdict)

The lane shipped in 1hy and the user ran it. Getting the numbers was worth more than the code was.

### H7 â€” the void hypothesis (section B) â€” REJECTED, and the rejection is the most valuable result

`B void  ring 0..32  footprints 4225  ...  NOT DRAWN 0`. Not one footprint in the entire owned band
has nothing drawing it.

I had put real weight on this one. The user's word was "**gap**", a void and not a crack, and I
argued in 1hy that in a chunk set whose corners are provably coincident, only a *lifecycle* failure
could produce a hole â€” a footprint that is neither a live real chunk nor a live far cell. I even
scoped the walk specifically so the answer would be unambiguous. The answer is: there is no such
footprint. **REJECTED.**

What this kills is not just the hypothesis, it is the whole *category*. The fix is not in streaming,
not in the dormant pass, not in far-shell shadowing. Those 4225 footprints are all drawn. **The gap
is not a hole â€” it is a step you can see the void *through*, on a mesh with no side walls.** The
low-poly root (1hi.1) deliberately emits no walls, so a height discontinuity at a corner is a genuine
see-through crack rather than a visible terrace step. That reframing is what made the rest readable.

### H8 â€” the corner lattice, the 1hk bug class, and the low-poly root â€” REJECTED AGAIN, now with the numbers

`own-lattice dY 0`. Every rendered corner vertex equals its own chunk's `LatticeY` to within
0.01 m, everywhere, in all 1444 corners. If `BuildCornerGrid` had an ownership bug, the *mesh* would
be built from the same wrong grid and this check would still read 0 â€” which is precisely rule 8, and
precisely why the cross-chunk check had to exist separately. It did its job.

### H9 â€” pristine noise, and why the MAGNITUDE was the tell â€” REJECTED, and the number did the work

Could adjacent chunks simply be sampling different noise? `SampleCornerHeight` and the pristine fill
both call `GetHeight(seed, exactWorldCorner)`, so only a per-chunk *seed* would do it. But that
predicts uncorrelated values â€” **metres** apart, since a different Perlin seed at the same point is
a different landscape. The observed worst delta is **0.2859 m**.

That single number is what collapsed the hypothesis, and it is worth being explicit about why: a
*small* delta is not a weak signal, it is a positive identification. Large deltas are ambiguous (any
big error fits); a delta of a few centimetres at a world corner has almost no candidates left. I did
not reason my way to "edits" â€” the magnitude eliminated everything else.

### H10 â€” where does a few-centimetres-at-a-shared-corner come from? â€” the save path. STRONG, not yet measured

`WorldStreamer.ChunkBuild.cs` assembles the `corners` grid from **this chunk's own** `ChunkTileMod`s
at chunk-local indices. So a world corner on a chunk boundary is written only by whichever chunk
holds the modified tile; the neighbour regenerates that same world corner from pristine noise.

I went looking for the mechanism that was supposed to prevent this, expecting to find it and be done.
`ApplyHeightEdits` *does* iterate world tile coordinates rather than chunks, so it propagates across
seams â€” but its own doc says "**Unloaded tiles are ignored**", and there is a caller contract
("callers must wait for the patch's chunks before flattening") that makes this a timing bug rather
than a logic bug. Then I looked for the load-time reconciler: `ReconcileModifiedBorders` exists, and
its name is exactly what I wanted, but it only rebuilds neighbours so a **slab wall** gets the right
bottom. Nothing anywhere reconciles a one-sided corner height.

`FlattenAt` closes the loop on the magnitude. Its feather drives per-corner influence `s` toward 0
at the rim, so a flattened region's boundary contributes edits of a few centimetres â€” a 0.29 m
one-sided delta is a feathered-rim edit that landed while the far side was unloaded, and 345 of 396
shared corners is a big edited region, which is entirely plausible around the test ground.

**This is still an inference.** The honest state is that I have a mechanism that explains the
magnitude, the locality and the count, and no measurement that confirms it. The two candidate fixes
are wildly different in scope â€” reconcile a one-sided corner against the neighbour's save data on
load, versus something in the lattice â€” so guessing would be expensive. Hence H11.

### H11 â€” measuring *why*, using the value the build itself uses â€” OPEN, this is 1i0's one column

The lane already had everything needed except one comparison. For each disagreeing node, ask of every
contributor: does your height equal **untouched world noise at this node**?

The test is exact rather than a proxy, and that is the part I care about. It calls the same
three-argument `GetHeight` overload (`baseHeight = 0f`) that `BuildChunkMeshData` uses to fill its
corner grid, so "pristine" means *the identical value the build would have produced had nothing been
written*. From that one number per contributor the class is forced:

- one on pristine, one off it -> a corner **written on one side only**. Confirms H10.
- none on pristine -> both sides were written and differ. Different bug, edit path still involved.
- all on pristine -> they are not actually different, or the disagreement is not in the heights. This
  would **destroy H10** and send the search back to the lattice and the seed.

I added a `_modifiedChunks` membership count beside it purely as a corroborating signal, and I noted
in the file that it is the coarse one â€” it says a chunk holds *some* mod, not that it holds *this*
corner's mod. The pristine comparison is the one that carries the argument; the set membership only
narrows down which of the two files to open next.

**Remaining OPEN until the readout.** If `one-sided edit` dominates, the fix is the cross-chunk
reconciliation and the cause is a save-path timing bug with no self-healing. If `NO side edited`
dominates, H10 is wrong and this whole entry's analysis is wrong with it.

---

## 1hy â€” "in every chunk corner the edge will not match, the player can see the void through that gap" â€” VERDICT: OPEN (measurement shipped, awaiting readout)

The report is old; the same report has been open since 1hj/1hk. What is new in 1hy is that the user
did the thing I asked for â€” **restarted the play session** â€” and the gap survived. That is worth a
whole section, because it deletes a hypothesis I had been treating as the default explanation.

### H1 â€” staleness: the resident chunks were built by an older generator (rule 11) â€” REJECTED

Rule 11 makes this the first thing to suspect for any terrain report: three holders of render output
outlive a code edit (`_loadedChunks`, `_dormantChunks`, the far shell), so a mid-session render change
leaves a world where resident chunks are OLD and later arrivals are NEW. It was also the standing
explanation for "in EVERY chunk corner", which reads like a systematic generator defect rather than
something that happens where you happen to be standing.

**For:** 1hx changed `LowPolyStep` 3 -> 6 in the same pass that deleted the F2/F3/F4 lanes, and the
earlier 1hv fingerprint existed precisely because this failure is indistinguishable from a renderer
bug by eye.

**Against:** the user restarted, and the post-restart HUD read `chunks 361  dormant 0  colliders 225
far cells 1224  rebuilds 0` â€” a complete, uniform, freshly-built ring with nothing dormant. Note
`rebuilds 0` is not the point; the point is that nothing had streamed in *over* anything, so there was
no population for a second version to have overwritten. **REJECTED**, and note what it cost: the 1hx
restart is the only way to observe a render change now (`DropResidentTerrainKeepSaves` was removed at
the user's request), so this was an expensive test. It was worth it â€” it is the one hypothesis a
static pass could never have closed.

### H2 â€” the corner lattice is stamped wrong (the 1hk bug class) â€” REJECTED, and I re-read it twice

The 1hk fix was exactly this signature: the east column stamped one metre north of itself, so the
NE/SE pair met at the wrong Y and a crack opened down the seam. Same shape, "every chunk corner".
Re-read `BuildCornerGrid` against the canonical slot order (`ChunkData`: 0=NW, 1=NE, 2=SE, 3=SW):
owner X/Z is correct in all four branches. Four-chunk node arithmetic: for a node at world multiple
30, the chunks touching it are X,Z in {n/30, n/30 - 1}. All four emit the same world vertex.
`LatticeY(30,30)` -> index 960 of a 961-entry grid, in range. **REJECTED.**

The low-poly root does not even go through that path for positions â€” it takes `corners.Y[s]` for
heights and computes positions as `gx*step`, so a position cannot be off by the lattice's mistakes.
**REJECTED twice**, and this is where I have to be careful, because of rule 8.

### H3 â€” the proof above is conditional, and the condition is the whole problem â€” OPEN, this is what 1hy measures

Here is the trap, and it is the reason rule 8 exists. I have proved the four-chunk node is
watertight **on the assumption that the mesh being drawn is the step-6 facet root**. Rule 8's exact
wording: *do not infer correctness from how pure the source is; a validator is only as good as the
arithmetic of the copy.* I was about to treat a static proof about a *generator* as a conclusion
about a *screen* â€” and the one mechanism this repo can produce on demand, a resident set built by two
versions, is exactly the thing that would break the assumption. 1hx is recent enough that this is
not hypothetical.

So the fingerprint is **section A and it runs first**, ahead of the two sections that look more like
tests. This is a deliberate ordering: it is the cheapest check and it validates the premise of
everything after it. If buckets > 1, sections B and C are evidence about a world that is not on
screen, and reading them first would be a category error.

### H4 â€” "a gap" is not "a slit", and the word picks the branch â€” CONFIRMED, and it reshaped the lane

I had been treating "gap" loosely. The user answered the question with one word: **"its a gap"** â€”
a void with nothing behind it, not a centimetre-scale crack. That is not pedantry; the two have
disjoint causes:

- A **slit** is a height disagreement â€” two surfaces that should coincide sit at slightly different Y.
  It is caught by R2 (cross-chunk or own-lattice delta), and it is a *rendering* defect.
- A **gap** is no surface at all. In a chunk set whose corners are provably coincident, only a
  **lifecycle** failure can produce one: a footprint that is neither drawn as a real chunk nor covered
  by a live far cell.

The second has no counterpart in the 1hv code, which only ever looked at corner vertices. So section
B is new, and it is the only section that can return a hole.

**The scope decision was the interesting part.** My first instinct was to scan "everything the player
can see", which needs the visible radius â€” and the only number for that is
`ChunkLodManager.EffectiveCullDistance()`, which is **private**. I nearly copied the formula, which is
rule 8's exact failure mode in reverse: a second spelling of a private constant in another class,
which rots the day the LOD side changes, and which would then silently mis-scope the audit.

Instead: scope by what the two owners **promise**. Real chunks to `view + 1` (hysteresis keep), far
cells from `near + 1` to `view + FarOuterKeep`. Their union is a band that is covered *by
construction*, which makes it the only place a "void" readout means anything. And it avoids the
nearest trap in the whole area: past that band, real chunks are DORMANT and hidden and no far cell
owns them, so a naive scan prints a ring of ~84 "voids" at 630 m. Correct, expected, invisible, and
completely drowning the signal. The band is printed in the readout so the number cannot be misread.
**CONFIRMED**, and it is the second rule-8 lesson of this task.

### H5 â€” two read-side judgement calls that would have produced confident nonsense â€” CONFIRMED, both from earlier history

1. **Disabled root renderer != void.** I first wrote the void test as "loaded + root renderer
   enabled". That is wrong, and it is wrong *by design of the LOD system*: `ChunkLodManager` disables
   the full root mesh while a detail band is active and draws a child mesh instead. Several rings
   would have reported as voids, all of them drawing perfectly. Split into a separate `rootHidden`
   count.
2. **Take the topmost vertex at a corner, not the min.** 1hv learned this the hard way: a smooth
   chunk's root carries side walls hanging *down* from the same edge, so a `min` reads the bottom of
   a wall as the corner's height and reports a false delta on every chunk. The low-poly root has no
   walls, so top == the only height there. Kept, and commented, because the next person to "simplify"
   it will not know.

### H6 â€” the static-check tool was wrong, and it was wrong in the direction that trains you to ignore it

Adding the new file to `StaticChecks.ps1` per rule 3 produced 4 immediate CS0165 candidates, all
false. Check 4 matches a bare local declaration and scans forward for `name =`. It cannot see that
**an `out` parameter is assigned by the callee's contract**, nor that a bare local written only
through an `out` call argument (the `TryGetValue` pattern) is assigned. Worth noting *where* they
were reported: the caller's locals in `RenderedCornerAudit()`, not the callee's parameters.

I could have written it off as "candidates, not verdicts" and moved on â€” the rule explicitly allows
that. But the failure mode is asymmetric and that is why I fixed it: a check that flags every `out`
parameter on the first file you add is a check whose "0 candidates" stops meaning anything, and the
next real CS0165 gets waved through with the rest of the noise. A tool's value is entirely in whether
its silence is trustworthy. Fixed both shapes; back to 0.

**H3/H4 remain OPEN until the user presses F3 and pastes the `VERDICT`.** The four live candidates
after the readout, in the order the lane tests them:
- **A** â€” mixed-version resident set (fingerprint buckets > 1). Would void the rest.
- **B** â€” a footprint with nothing drawing it. The real "gap", and a lifecycle bug, not a render one.
- **C-R1** â€” a chunk corner the surface never reaches. Coverage, not height.
- **C-R2** â€” a corner height that disagrees across chunks or with its own lattice. A crack/slit, which
  the user's "gap" answer argues against but which the lane will still rule in or out.

One question I could not answer from code and did not want to guess at: does the gap sit where **four**
chunk borders radiate from it or only **one**? Four means the 4-chunk node, where H3's proof applies
and section C is the place to look; one means a two-chunk mid-edge seam, where the proof never applied
at all and the search has to move. I raised it, and proceeded without it â€” the readout's worst-node
world XZ answers it by measurement instead of by eye, which is the better instrument anyway.

## 1hz â€” the staged feature removal did not compile, and one of the two failures was invisible â€” VERDICT: both fixed

A removal was already sitting in the working tree (29 scripts staged-deleted: all of
`Networking/` and `NightClub/`, `FastTravelSign`, `FastTravelMenu`, `HorseMount`,
`WorldBuilder.FastTravel`, `ClubExteriorBuilder`, `MapBuilder.Nightclub`, the two multiplayer UI
scripts). The user asked me to finish it rather than discard it. So this log is about the two
failures I had to find first â€” one loud, one silent â€” plus the questions I had to answer before
touching anything.

### H1 â€” the loud failure: grep `FastTravelSign` and delete every hit? â€” NO, three of the nine hits are documentation

`grep FastTravelSign Assets/Scripts` returned 9 lines across 4 files. The obvious move is delete all
nine. Rejected: **five are `<see cref="FastTravelSign"/>` XML doc comments**, not code. Deleting
them is right, but deleting them the same *way* as code is wrong â€” and one of them
(`POIGenerator.cs:14`) referenced `FastTravelMenu`, a *second* deleted type that the type-level grep
never surfaced. So: reword the doc references to `<c>` prose, delete the code. Confirmed the clean
state with a second grep that returns only `<c>` mentions.

The deeper point: **the grep for the deleted type's name succeeded and found exactly the right four
files.** Nothing about that grep said "you are not done". The signal that the removal was incomplete
came from a *different* observation â€” that the four files were producers feeding a deleted consumer
â€” and I only made that observation by asking, per hit, *what is this line for?* rather than *does it
name the deleted type?*. **CONFIRMED**, and it is the first clause of the new `AGENTS.md` rule 13.

### H2 â€” is `FastTravelNode` a producer too, and should it go with them? â€” NO, it is a separate consumer

`FastTravelNode` holds a `TravelSign` field and passes an `int index` into
`FastTravelSign.Index`, so it looked like the same category of leftover. But:
- it is still built by `POIGenerator.PlaceFastTravel` (`:145-150`),
- it is still found by the streaming cull sweep (`NewWorldSystems.cs:166`,
  `FindObjectsByType<FastTravelNode>`),
- and `PoiKind.FastTravel` still exists in the enum.

So it is reachable, just unpopulated â€” `POIRegistry.BuildAll()` registers **no** `PoiKind.FastTravel`
entry (I enumerated every `PoiKind.` in the roster: 4 Town, 4 Dungeon, 4 BossArena, 4 Fishing, 1
HiddenCave, 1 SkillBook, zero FastTravel). Deleting the class would have broken the cull sweep for
no gain. **REJECTED deletion; kept the class, dropped only its `TravelSign` and the `index`
parameter.** I dropped the parameter because with the sign gone nothing consumed the index â€” leaving
an `int index` in a 2-arg-shaped signature is exactly the kind of "looks intentional" residue that
invites the next reader to wonder what it was for.

### H3 â€” does `PoiKind.FastTravel` with no producer count as dead code? â€” NO, it is dormant, and the doc now says so

An enum member nothing constructs is a candidate for deletion, and deleting it would also let
`POIDefinition.IsFastTravelPoint` and `WorldMapUI`'s `âœˆ` go. But `IsFastTravelPoint` defaults to
`true` and `POIRegistry.Make` sets it for **every** roster POI, so the `âœˆ` marker is live on the
world map right now. The enum member is the hook a future fast-travel implementation would use.
**REJECTED deletion; documented in game-design Â§5.15 and Â§7.2 as "dormant, not dead code"** so the
next session doesn't "clean up" the marker and break the map.

### H4 â€” the silent failure: what happens to a pre-1hz save that contains a `NightClub`? â€” it corrupts the PREVIOUS building (this was the real find)

This is the one I would have missed. `WorldBuilder.Persistence.cs:233` had:

```csharp
if (build.type == "PlayerHouse" || ... || build.type == "Library" || build.type == "NightClub")
{
    RebuildEssentialBuilding(...);          // no NightClub case any more -> appends nothing
    var lastEssential = _buildings[_buildings.Count - 1];   // <- the PREVIOUS building
    lastEssential.CurrentHealth = build.currentHealth;
    ...
    ApplySavedDoorState(lastEssential.Entity, build.doorOpen);
}
```

No compiler error: a string `switch` with no matching `case` is perfectly legal, and `RebuildEssentialBuilding`
returns void so nothing about the call shape is wrong either. The game loads, the player sees a
village, and one building has the night club's health and an open club door.

Before fixing it I checked the alternative is actually safe, rather than assuming. The candidate fix
was to drop `"NightClub"` from the condition and let the entry fall through to the generic path at
`:254`. That path is safe **if and only if** `SpawnBuildingDirect` returns `false` for an unknown
type â€” so I read it: `CreateBuildingEntity` (`:429-430`) does
`Array.Find(_availableBuildings, d => d.Name == typeName); if (def == null) return null;`, and
`SpawnBuildingDirect` (`:551`) does `if (building == null) return false;`. The caller guards with
`if (SpawnBuildingDirect(...)) { ... }`, so nothing is stamped. **Chain verified end to end before
relying on it. CONFIRMED and fixed.**

This is the second clause of rule 13, and the general form is the nasty part: **a `switch` that
dispatches on a save key cannot fail to compile when a case disappears, so save keys need the same
discipline as struct part keys** (rule 9's existing concern) â€” one more level out, because part keys
at least have the builder-case parity check in `tools\StaticChecks.ps1` Â§6, and this one did not.

### H5 â€” 14 dead localization keys, but is a key really dead? â€” yes, after checking the *dynamic* lookup path

`Localization.cs` had a `// Horse & fast travel (Phase 3D)` block of 14 keys. Tempting to just delete
it: the comment names two features 1hz removed. But **localization keys are looked up by string at
runtime**, and I had just spent H1 learning that "the grep found what I expected" proves nothing.
`Localization.T(vn)` is a one-way `Translations.TryGetValue(vn)` â€” so a key is live if *any* code
passes that Vietnamese string, and the first grep I ran (regex with `.` wildcards against a
mangled console) returned **50008 matches**, i.e. useless.

So I re-ran it properly: `Select-String -SimpleMatch` per exact key, across all `.cs`. All 14 â†’ 0
references outside `Localization.cs`. Then, because "no literal in code" still isn't proof for a
*string-keyed* dictionary, I checked the two remaining ways a key could arrive from data:
- **dynamic `T()` call sites** â€” enumerated all 60+ non-literal `Localization.T`/`F` calls
  (`CraftingManager:158`, `MapBuilder:268`, `QuestManager`, `UIManager.Endings`, the NPC `_dialogQueue`
  dequeue pattern, `RandomEventManager:293`, â€¦). The building-name ones that mattered were
  `Localization.BuildingName(key)`, which is a **separate** dictionary keyed by *building type*
  (`"wood_wall"`, `"library"`, â€¦), not by these display strings. `MapBuilder._signDefs` uses
  `"NHÃ€ HÃ€NG"` / `"Cá»¬A HÃ€NG"`, not `"Trang Tráº¡i"` / `"Há»™p ÄÃªm & NhÃ  HÃ ng"`.
- **data files** â€” only two JSON files under `Assets/Resources` (performance-test settings) and no
  `.asset` carries these strings.

**CONFIRMED dead, deleted.** Worth recording *how* the first attempt failed, because a regex grep that
matches 50k lines is worse than no grep: it looks like a result.

### H6 â€” the folder metas: are they really orphans, or is Unity about to recreate the folders? â€” orphans

`git status` was clean for `Assets/Scripts/{Networking,NightClub,Vehicles}.meta` while the folders
themselves were staged-deleted. So the metas were tracked, still on disk, and pointing at nothing.
If Unity ever recreates one of those folders it would generate a *new* GUID anyway, so keeping the
old meta has no upside and risks a GUID collision later. **CONFIRMED, `git rm`-ed all three.**

### H7 â€” `_Recovery`: is deleting it from git the right call? â€” untrack, don't delete

`Assets/_Recovery/` held 50 tracked files (`0.unity` â€¦ `0 (24).unity` and their metas) plus 2
untracked. Two sub-questions:
- *gitignore or delete?* The user chose ignore. Independently: these are Unity **recovery** scenes â€”
  by definition throwaway artifacts of a crash or a bad scene edit, and the user still had 2 newer
  ones on disk (`0 (25).unity`), which is the behaviour of a folder that keeps being written to.
  Ignoring it stops the churn; deleting it would destroy the user's local safety net.
- *`git rm --cached` or `git rm`?* `--cached` â€” the point is to stop tracking, not to take the
  files away. Verified after the fact: 50 staged deletions, **52 files still on disk**, and
  `git check-ignore -v` confirms all of them (including the 2 untracked and the previously-modified
  `0 (24).unity.meta`) are now covered by `.gitignore:12`.

Nice side effect: the two loose ends I'd flagged â€” the stray `0 (24).unity.meta` edit and the
untracked `0 (25).unity` pair â€” stopped being problems without a separate decision.

### H8 â€” could I have removed `com.unity.multiplayer.center` too? â€” deliberately no

`Packages/manifest.json` still lists it. Nothing references it. Tempting to delete the line, but a
package removal is a different kind of change from a code removal: it can re-resolve transitive
dependencies and it is the kind of edit whose failure mode is "the editor reimports for ten
minutes", not "one line is wrong". It also wasn't part of the removal the user asked for. **Left
in place and documented in game-design Â§9.1 as unreferenced**, so it's a visible leftover rather
than a forgotten one. OPEN if the user wants it gone.

### H9 â€” the behavioural orphan, which neither grep can see â€” CONFIRMED, REMOVED in the follow-up

`RichManNPC` had a full club-hangout state machine â€” `ClubHangState` {None, WalkingToClub, AtClub},
`ClubIdleState` {Watching, Pacing}, `_clubStandSpot (8.5, 0, 98)`, two pace spots, `HandleClubHangout`,
`FaceToward`, and a 19:00â€“21:00 window â€” all of it pacing around a building 1hz deleted. **No grep
finds this.** The strings compile, the types exist, the code runs; it is simply walking an NPC around
in an empty lot. It's the third clause of rule 13, and the follow-up commit removed it: the state
machine, the pace spots, the idle timer, the watch/pace loop and both "quÃ¡n bar" (bar) strings are
all gone.

**One design consequence I had to decide rather than guess.** The hint at `RichManNPC.cs:228-231`
("PhÃº Ã”ng Ä‘ang á»Ÿ quÃ¡n bar... chá» Ä‘áº¿n Ä‘Ãªm khuya") is the player's *only* in-game cue for where and
when the 21:00 dealer meeting happens â€” the other bar string (`:531`, in `StartDealCamera`) only
fires once the player is already inside the Â±12Ã—Â±8 window, so it is a confirmation, not a discovery
aid. Deleting the hangout therefore deletes the *only* breadcrumb. Two options: leave the story
undiscoverable, or replace it with a day-gated, location-agnostic message. I chose the latter and
flagged it for the user rather than silently dropping a quest step or silently inventing new text.
**Shipped as chosen**: one toast per evening from `DEAL_HOUR - 2f`, day 3+, undiscovered â€” "PhÃº Ã”ng
hay ra ngoÃ i vÃ o ban Ä‘Ãªm... hÃ£y tÃ¬m háº¯n." / "The rich man slips out at night... find him." It names
no building, because the building is gone; the deal site stays a hard-coded vector the player is
expected to find by exploring, which is a weaker quest than the deleted hangout but a live one.

**Second thing the removal forced, and it is a rule 10 shape.** The hour 21:00 was written **twice**,
independently: `TryStartDeal` had a bare `TimeOfDay < 21f` and `ForceStartDealForWatch` had
`SetTimeOfDay(CLUB_WINDOW_END)` where `CLUB_WINDOW_END == 21f`. Deleting the window left the second
site with no constant to name, and keeping the first would have left a bare magic number guarding
the whole deal. Both now read `DEAL_HOUR`, and the hint's 19:00 is derived as `DEAL_HOUR - 2f` so
there is exactly one hour in the file. Same failure mode as the 84-unit Character Info band: a
metric two code paths must agree on, written twice, where the two only agreed by luck.

### H10 â€” a bug my own edit introduced, caught by grep rather than by reasoning

I removed the dungeon entrance sign block in `DungeonSystem.BuildLayout` including its
`Vector3 doorDir = new Vector3(1f, 0f, 0f);` local, on the reasoning that the sign was the only
thing placing anything along `doorDir`. Then, habit from rule 3, I grepped the removed block's
identifiers for *other* readers instead of trusting the delete: `doorDir` is read 20 lines later at
`:71` for the enemy spawn offset. Restored the declaration. This is the mirror image of H1/H9 â€” the
same discipline (grep the thing you removed for readers elsewhere) that found the incomplete removal
also caught the incompleteness of my own removal. Worth stating plainly because the alternative was
shipping a definite-assignment error on the theory that the local "looked" unused.

**Verdict: all hypotheses resolved except H8 (left open deliberately). 1hz shipped; H9 shipped in
the follow-up commit (`1hz follow-up` in PROGRESS.md).**

---

## 1hy â€” an uncommitted weapon-animation edit found in the tree â€” COMMITTED AS FOUND (dagger/gauntlets read as pastes, not tuning)

Not an investigation I ran: a diff in the working tree that had survived 1hv, 1hw and 1hx uncommitted.
The user chose to commit it rather than discard it, so the only question worth asking was **what is
actually in the file**, and whether it is safe to ship as-is.

### H1 â€” is it a compile risk? â€” NO, and the reason is structural
`WeaponAnimator`'s combo table is a `private static readonly` array of `WeaponAnimDef`s built from
`K`/`T`/`V` helpers. Nothing outside the file can see it: no signature changes, no public member, no
serialized field, no GUID, no save key, no part key. So the whole blast radius of a mistake here is
one frame of animation â€” which is exactly why it is worth committing with an accurate description
rather than "fixing" values whose intent I cannot know. Balance check: 97/97 braces, 656/656 parens.

### H2 â€” are the changed values *wrong*, or just different? â€” three of them read as wrong
The tempting move is to "tidy" the numbers. Rejected: these are the user's animation intent, and three
specific shapes are the kind a paste produces rather than a hand tunes:
- **`dagger`'s four variants are byte-identical to `iron_sword`'s four, comments and all.** A hand
  edit changes numbers; it does not copy the trailing `// 1. wind left/back -> slash across right`
  comment from another weapon. And the def kept `SlashLead = false` while wearing slashing poses â€”
  the signature of a copy that did not carry its flag.
- **`gauntlets`: four variants, one pose pair.** Variant 2 differs from 1/3/4 only by a missing space
  (`0f,0f,-90f` vs `0f, 0f, 0f`) â€” the fingerprint of four edits made by pasting the same line four
  times. A boxer chain that reads jab = cross = double = uppercut is not a tuning pass.
- **`fist` clip 4 opens with `K(0.01f, 360f, ...)`** â€” a full 360Â° shoulder-X rotation at t=0.01,
  unwinding to rest by t=0.99, replacing a `-140f` uppercut cock. Could be a deliberate spin-into the
  punch; could be a mistyped magnitude. Only the play-test distinguishes them.

**Not flagged:** `fist` clips 1 and 3 being identical (a *double jab* repeating the jab is correct,
and was true before the edit), `iron_sword`'s wrist `95 -> 125`, the chop/thrust swap, and the two
reformats. Those are all ordinary hand edits.

### H3 â€” the parameter trap that nearly produced a false flag
My first pass called the `fist` clip-2 value "a 90 in the wrong slot". Wrong: `K`'s 4th positional
argument is **`shZ` (shoulder Z)**, not a wrist or elbow axis (`WeaponAnimator.cs:114-115`), and a
shoulder-Z rotation is exactly what a side cross is. Reading the argument list before calling a value
out-of-range is the same discipline as every other "that number looks wrong" moment in this repo â€”
the check has to be against the *contract*, not the intuition.

### Verdict
SHIPPED AS FOUND (`PROGRESS.md` Â§1hy), with the three shapes named as play-test items rather than
quietly corrected. Lesson recorded for the next uncommitted-diff handoff: **describing an edit
precisely is worth more than repairing it**, because only the user knows which of "paste slip" and
"deliberate" a number is.

## 1hx â€” the low-poly look still read as smooth, and the measurement lanes came out â€” SHIPPED (facet step 3 â†’ 6; F2/F3/F4 removed)

Two asks that looked unrelated and turned out to be the same statement about the same thing.

### H1 â€” "the world still looks smooth" â€” CONFIRMED, and the triangle count was the wrong suspect
The instinct is to add detail: subdivide, or raise the facet count. Rejected on the spot by 1hi.1's
own numbers â€” the low-poly root at 3 m is already ~121 quads for a 30 m chunk, i.e. a quarter of a
triangle per square metre. The problem is not density, it is that **neighbouring facets are nearly
coplanar**, so the flat-shaded normal barely changes across a facet and the eye reads a smooth
gradient. Facet read = `curvature x span` over the facet, and the second factor is the one we control
by fiat.

### H2 â€” how coarse can the step get? â€” the divisor set, not taste
`LowPolyStep` and `FarSectorStep` are the same decision in two files (near chunks vs far shell). The
tempting failure is to move one and leave the other: the world then reads *inverted* (chunky
underfoot, fine at the horizon) with a density break at the rim. So the candidate values are
constrained by the grid, not by looks: the step must divide **30** (chunk side) *and* **90** *and*
**180** (the far cells' span-3 and span-6 boxes), else the last grid row falls short of a chunk
boundary and every chunk edge shows a crack. That set is {1, 2, 3, 5, 6, 10, 15, 30}. 6 is the
smallest step that doubles the span, which is exactly the lever H1 says is needed. Rejected 10/15/30:
each buys more curvature contrast but the collider rides the same step, so footing and prop
heights degrade with it. **6 is the first rung that fixes the look and still divides everything.**

### H3 â€” is 3 m really near-coplanar here? â€” plausible, not measured (and it does not need to be)
I did not sample the field to prove it. The reasoning stands on the recorded noise parameters alone
(base octave amplitude 55 m at frequency 0.0012, five octaves): a 3 m facet is far inside the
smallest octave's wavelength, so its two edges differ in height by a small fraction of a metre and
the cross normal is dominated by the local slope rather than by the facet's own tilt. If the user
reports the world *still* reads smooth at 6 m, the next lever is shading (flat-shade strength, a
height-ramp colour term), not a coarser step â€” and that is the honest fallback to record here,
because "reach for the span first" is a rule with a stopping point.

### H4 â€” the audit lanes: keep or remove? â€” the user removed them, and the docs must not pretend otherwise
1hj (F2) and 1hv (F3) exist because of rule 7: a report I cannot see gets a read-only measurement
first. 1hw (F4) then made rule 11 executable in one key. The user had all three taken out. That is a
legitimate call â€” three keys, three toggles and ~860 lines of measurement code is a lot of surface for
a QA affordance â€” but the *rule* the lanes served does not go away with them, and the honest way to
record it is: a gap report is now read **by eye against a freshly restarted session**, and a future
fix should expect to rebuild a measurement rather than read a cause off an existing validator.
Kept in `game-design.md` Â§2.2 as the lesson ("a check only speaks for the layer it reads"), dropped
as tooling. Also kept: `ChunkObject`'s QA accessors, because they are the only null-safe way to ask
those questions of a half-torn-down streamer, and nothing else uses them.

### H5 â€” the cost I am knowingly accepting
The collider rides the facet step, so 6 m means lumpier footing than 3 m; prop heights still sample
the **1 m** lattice, so a prop can float or sink by up to the facet error (roughly twice what it was
at 3 m). The 1 m grid stays canonical for saves and edits either way â€” but that is also the *edit
granularity* caveat: a 1 m dig only moves a facet vertex when the edited corner lands on the 6 m
grid. Recording it so a later "why did my 1 m dig do nothing" report is answerable from this section.

### Verdict
SHIPPED as documented in `PROGRESS.md` Â§1hx, and the two rules it forced into `AGENTS.md` are the
part that outlives it: rule 11 (a render edit needs a **restart**, and the two ways a session lies
about it) and rule 12 (the facet size is one decision in two files and must divide 30/90/180).

## 1ht â€” "make the tab buttons shorter, Change Class/Race use the same png, border the race/class/stats fields" â€” SHIPPED (band 40@10 â†’ 32@8; 3 buttons re-arted; 13 fields framed)

A follow-up to 1hs, and a smaller one â€” but two of the three asks hid a layout decision, and both
of my first answers were wrong in a way only arithmetic caught.

### H1 â€” "the band just needs to be shorter again" â€” CONFIRMED, and it was nearly free
`TabBarHeight` / `TabBarTopY` are already single constants read by both write sites (1hs), so this is
two numbers. The real question was *does anything have to move?* In 1hs I had put the Skills header
row at 236 to live in the 200â€“250 corridor. 40@10 gave a band bottom of 250; 32@8 gives 260. The
corridor is 10 units taller at the top and the same at the bottom, so the row at 236 and the viewport
top at 200 are both still clear â€” **no row moves.** That is the payoff of having measured the
constraint in 1hs instead of picking a number: the second shrink was free. The label box drops to
(32 âˆ’ 8) = 16, so the existing clamp `min(0.95 Ã— (height âˆ’ inset), â€¦)` yields 22.8 instead of 24.5 at
1080p â€” the label shrinks a little too, which is the correct consequence of a shorter button, and the
clamp already existed to make exactly that arithmetic.

### H2 â€” "Change Class and Change Race use a different png" â€” CONFIRMED, and it was not just those two
The interesting find: `MakeButton` lays down the **short** "stats menu button" art as a default, and
call sites are expected to override it with the full frame. Grepping every `MakeButton` call rather
than the two named ones found **three** exceptions: Change Class, Change Race, and â€” which the user
did not name â€” the Faith panel's **Switch Faith**. So the pattern was "8 of 11 correct", and the
user was describing a symptom of a convention that was applied by hand at each site. Fixed by
overriding all three; the remaining alternative (make `MakeButton` apply the full sprite itself and
delete the 8 overrides) was rejected as out of scope â€” it would touch 4 more files to remove 8 lines.

### H3 â€” "border the fields" â†’ how to draw a border? â€” 9-SLICING REJECTED, twice
First instinct was a 9-sliced ring sprite, which is the obvious Unity answer. Rule 10's last bullet
already records why it is wrong here, and the fields are the exact case that rule was written for:
the stat inputs are **64Ã—26** and the class/race rows are **700Ã—30**, in the same column of the same
panel. Unity scales a slice by the drawn rect's *own* dimension, so a 2-texel ring on a 24px sprite
is ~2 units on the 64-wide field and ~58 on the 700-wide one â€” the class row's "border" would be a
solid slab with a hole in it. **A sliced border is correct at exactly one shape.** Rejected;
four flat `Image` strips at a constant 1.5 instead. Five GameObjects per field, 13 fields, 65
GameObjects of decoration â€” acceptable, and correct at every size and every resolution.

### H4 â€” "where does the border go: inside the box or outside it?" â€” my first answer was WRONG
I first wrote the frame as `labelBox + pad on all four sides`, reasoning that a border should not
consume the row's own interior. That is geometrically wrong for a **pivot-top** rect: `MakeBodyText`
anchors at (0,1), so the box grows *downward* from `pos`. Growing the height pushes the bottom edge
down, not the top edge up â€” so the top border still lands exactly on `pos`, which is exactly where
the first line of glyphs starts. Caught by writing out the worked example: frame at y âˆ’96, top strip
at âˆ’96â€¦âˆ’97.5, class label ink starting at âˆ’96. **Flush is a slit, not a frame.**
Rejected; the border goes *inside* the caller's box, and the label is inset into it. The rows have
no room outside anyway: they are 4 units apart and the last is 2 units from the buttons under it, so
an outside border would land on a neighbour's glyphs (the exact collision rule 10 warns about, and
the class row is 4 units from the race row â€” one number, two neighbours).

### H5 â€” "if the border is inside, does the row still fit its text?" â€” NO, and this is what moved the rows
Inset 4 on every edge of a 30-tall row leaves a **22-tall** text box. The class/race font is
`max(16, Screen.height / 48)` = 22.5pt at 1080p, and a 22.5pt line box is ~27 â€” so the descent
crosses the bottom border the inset just paid for. The first fix I reached for was to shrink the
font, which would have made the class and race text visibly smaller than everything around it for
the sake of a decorative 1.5-unit line. Rejected.
Second fix: **grow the row to 34** (inset box 26, cap 23.4, so the resolution-scaled 22.5 still wins
at 1080p and nothing gets smaller). That needs vertical room, and there was none below â€” but there
was 33 units of empty space *above* the class row (the stat block ends at y âˆ’63). So the rows moved
up: âˆ’96/âˆ’130 â†’ âˆ’80/âˆ’118. Checked the new extents: stat block âˆ’63 vs class row top âˆ’80 (17 clear),
class bottom âˆ’114 vs race top âˆ’118 (4 clear), race bottom âˆ’152 vs the buttons' top âˆ’158 (6 clear).
The font cap is `ValueLineFontSize(rowHeight)`, stated against the *inset* box, so the row height and
the cap cannot drift apart the way the two tab-band write sites had drifted in 1hs.

### Open
- Shipped, but the user has not looked at it yet. The two things I cannot verify without eyes: that
  a 1.5-unit border at 1080p is *visible* (it is 1.4 px â€” thin by design, but thin) and that the
  class/race text at 22.5pt inside a 26-tall box looks right. If the border reads as too faint, raise
  `FieldBorderThickness`; if the text reads as cramped, raise the row height rather than the inset.

## 1hu â€” "whenever you edit the map rendering algorithm, clear the cached map so it creates anew" (terrain) â€” SHIPPED as AGENTS rule 11 (no code change)

The user said "map"; they meant the terrain. The request looked like a one-line rule, but the
interesting part is that the rule has to be *wrong in two directions* to be useful, and I only found
both by reading what actually holds render output.

### H1 â€” "what is the cached map?" â€” the obvious candidates are all wrong
`WorldMapUI` is a text list of POIs. `CompassMinimapHUD` is a compass strip, a circle sprite and a
player dot â€” it never samples terrain, so there is no cache to clear. `MapBuilder.*` is the legacy
village prop builder despite the name. The first three greps for "map + cache" returned nothing that
holds rendered terrain, which is the point: **the cache is not a map cache, it is the streaming
chunks themselves.** Everything else (the `ChunkMeshGenerator` mesh pool, the
`TerrainNoiseGenerator._offsetCache`) was a false positive â€” see H3.

### H2 â€” which holders of render output survive a code edit? â€” three, and they fail differently
- `_loadedChunks`: every `ChunkObject` owns an uploaded `RootMesh`, and the generator is only run
  inside `BuildOrLoadChunk`, which a loaded chunk never re-enters. CONFIRMED by reading the load
  path.
- `_dormantChunks` is the one I would have missed. The wake pass comment says dormant chunks are
  re-shown "in place â€” the same GameObject, same pooled mesh, same tile data", and
  `EnqueueChunkIfNeeded` *wakes* a dormant chunk instead of re-dispatching it. So a chunk that
  demoted 200 m ago still holds the mesh it was built with, and walking back re-activates it. This
  is also the reason the "just call ForceRebuild" instinct is wrong: `EnqueueChunkIfNeeded` alone
  would *wake* rather than rebuild, so the pair must be `UnloadChunk` first (which is why
  `ResetTerrainSaves` and `ForceRebuildChunk` both unload before re-enqueueing).
- the far shell's cells are sampled from the real chunks' surfaces, so they carry the old heights
  too â€” and a drop that skips `ClearFarShell()` leaves the rim disagreeing with the new near chunks,
  i.e. it *creates* a seam at the ring boundary while fixing the interior.

### H3 â€” is the mesh pool the cache? â€” REJECTED, and it is the intuitive answer
`_chunkMeshPool` (`ChunkMeshGenerator`, cap 48) is the only thing in the terrain system that looks
like a cache of rendered output. It isn't: `UploadMerged` re-specifies vertices and indices on every
upload and `Mesh.Clear()`s whenever the vertex count changed, so a pooled `Mesh` is a *buffer*.
Emptying the pool would show nothing and cost a little. Confirmed from `UploadMerged`'s own comment
and the `AcquireChunkMesh`/`ReleaseChunkMesh` pair.

### H4 â€” "clear the cache" â€” which existing API is it? â€” the obvious one is destructive
`ResetTerrainSaves()` does the right *sequence* (`ClearFarShell()` â†’ flush pending saves â†’ wipe save
files â†’ clear dirty marks â†’ unload + requeue every loaded **and** dormant chunk), and it is already
wired to the `EnableResetTerrainSaves` QA lane for a deliberate clean map. But `ChunkSaveManager.
ResetWorldSaves(Seed)` deletes the `tc_*.dat` files, and those files hold **heights** â€” data, not
render output. Using it after a renderer change silently throws away the player's terrain edits.
So the rule has to say: for a render edit, drop the render output and keep the saves, which is
`UnloadChunk(tc)` + `EnqueueChunkIfNeeded(tc)` per chunk â€” and that pair only exists privately, as
`ForceRebuildChunk`, with `ForceRebuildArenaLane` as the public single-lane precedent. **There is no
public "re-render everything, keep the saves" entry point.** That gap is now written down as the
reason the drop is a manual step, rather than left as something the next session re-derives.

### H5 â€” is the user's corner-gap report explained by this? â€” HYPOTHESIS, not confirmed
The user reported, in the same breath: "in every chunk corner it wont match the edge so player can
see the void through that gap." A mid-session render edit produces exactly that: chunks built by
two different versions of the generator, parting at their shared edges and worst at the corners
where four of them meet. It is the one mechanism this repo can *create* on demand, which makes it
the leading candidate â€” but it is a hypothesis, because it requires that the algorithm was edited
without a drop, and I cannot see the session that produced it. Rule 7 therefore applies: the
measurement ships as its own task (1hv) and the fix waits for the readout. A zero-code A/B also
exists: a fresh play session rebuilds every chunk through `BuildOrLoadChunk`, so **if the gaps
survive a restart, staleness is ruled out** and the cause is in the render path itself.

## 1hw â€” making the rule-11 drop a key instead of a play-session restart â€” SHIPPED

Triggered while 1hv waited on the user's readout. The drop itself was already known to be correct
(1hu H1â€“H3); what was missing was a way to *do* it without restarting, because a restart is the one
action that cannot answer the question the user is asking.

### H1 â€” why a restart is not a valid A/B â€” it rebuilds everything at once
The user's report is "gaps at chunk corners". If the fix for that is a renderer change, then
restarting the session rebuilds every chunk through the new code, and a world that comes back
seam-free is consistent with **both** "the render path was wrong and is now fixed" and "those chunks
were stale the whole time". The measurement (1hv) separates the two only if the stale chunks are
dropped *on demand*, while the player stands at the gap. So the drop has to be a key, and it has to
keep the saves â€” otherwise the second press erases the evidence (a sculpted/deformed world that
disappears is its own confound).

### H2 â€” the pair is private, so this is a small API addition, not just a bench toggle
`ForceRebuildChunk` (`UnloadChunk` + `EnqueueChunkIfNeeded`) is private; only
`ForceRebuildArenaLane` (three fixed coords) is public. So the honest shape is a public
`DropResidentTerrainKeepSaves()` in the same file as `ResetTerrainSaves`, written as *that method
minus its destructive middle* â€” which is the whole point of the exercise, and makes the diff
self-documenting: the two operations differ by exactly `FlushPendingSaves` + `ResetWorldSaves` +
`_dirtyTiles.Clear()`.

### H3 â€” is the dormant half real, or am I cargo-culting my own rule?
Checked, and it is real: `EnqueueChunkIfNeeded` returns early via `WakeChunk` when the coord is in
`_dormantChunks`, and `WakeChunk` re-shows the *same* GameObject with the *same pooled mesh*. A
loaded-only loop would therefore be a no-op for every dormant chunk â€” the exact half rule 11 says
matters, and the half you cannot see. `UnloadChunk`'s dormant branch (added in 1gc) removes it from
the dictionary and destroys the object, which is what converts the re-queue into a real rebuild. The
pair is also already proven in production by `ForceRebuildArenaLane`, so the sequence is not my
invention.

### H4 â€” what the drop CANNOT fix, and whether to make it fix it anyway â€” no
`DispatchPending` snapshots `VoxelTerrainEnabled` and `EffectiveLowPolyStep` at dispatch and passes
them to the worker (deliberately: "a chunk never changes shape mid-build"). An in-flight chunk is
therefore in neither dictionary â€” there is nothing to unload â€” and `EnqueueChunkIfNeeded` refuses to
re-dispatch it. I considered removing it from `_chunksInFlight` and re-queuing it, and rejected it:
the abandoned worker still enqueues its result, `FinalizeChunks` keeps whichever arrives FIRST and
discards the other, so that is a coin flip, not a fix. Determinism needs a per-chunk generation
counter compared at finalize â€” a change to the streaming core that every player would pay for, to
serve a QA key. **Verdict: report the straggler count and ask for a second press** (both audits
already print `inflight`, so "press again when it reads 0" is checkable, not a vibe). Rule 7's
"let the readout name the mechanism" applies to the limits of a tool too.

### H5 â€” two of my own verification steps were wrong, and said so in PROGRESS
- `Select-String -Path Assets\Scripts\**\*.cs` does **not** recurse in PowerShell â€” it reached two
  directory levels and never saw `World\Streaming\`, so it reported the new API as "declared zero
  times" while its call site sat in `Opt\`. Redone over `git ls-files "*.cs"` (431 files).
- `-Pattern ([regex]::Escape($s))` together with `-SimpleMatch` searches for the *escaped* text
  literally, so `Key.F4` returned "0 hits" because it looked for `Key\.F4`. Re-run as a regex.
  Both were caught only because the count I expected (a definition) came back as zero. A check that
  passes is not evidence; a check that returns a *suspicious* number is worth re-running.

### Open
- The corner fix itself is still withheld pending the F3 verdict. If that verdict is `R-stale`, this
  task supplies the remedy; if it is `R1`/`R2`, this task is what makes the eventual fix visible
  instead of invisible â€” a rebuilt-by-the-new-code world is checkable either way.

## 1hv â€” "in every chunk corner it wont match the edge so player can see the void through that gap" â€” MEASUREMENT SHIPPED, fix withheld pending the readout

Rule 7: a report I cannot see gets a read-only measurement lane FIRST, and the measurement and the
fix ship as separate tasks so the readout that justified the fix stays in history. So this task adds
the lane and changes **no** terrain geometry. Reasoning trail: `THINKING.md` Â§1hu H5 (the leading
hypothesis).

### H1 â€” "is there already a measurement for this?" â€” YES, and it reads the wrong layer
`WorldStreamer.SeamAudit()` (1hj) is exactly the "gaps between terrain chunks" instrument, on F2. It
has four sections: A realâ†”real corner heights, B roots, C interior holes, D far rim. So the first
move is not to add a lane but to ask what A *reads*: `ChunkObject.LatticeY(gx, gz)` â€” the **31x31
corner lattice**. That is the data layer.

**And that is exactly the trap AGENTS rule 8 warns about**, in a new costume: a validator is only
evidence about the layer it reads. A green "worst dY 0 OK" means the lattices agree; it says nothing
about whether the renderer drew those corners. A renderer that copies its corner vertex from the
wrong place produces a perfectly clean section A and a visible slit. B is no help either: it checks
root active state, vertex COUNT and X/Z bounds â€” it catches a root that is short or hidden, and
cannot see whether the four vertices meeting at a corner agree with each other. (Credit where due:
the 4-chunk corner node IS covered by A, because A walks both the east and the north edge of every
chunk, so all four chunks' values at a shared node are forced equal transitively. The gap is not in
A's edge coverage â€” it is in A's layer.)

### H2 â€” what should the lane measure? â€” the two questions A cannot answer
- **R1 coverage.** For each loaded chunk, is there a rendered vertex AT each of its four corners? A
  corner the surface never reaches is a hole no height comparison can reveal (both sides can agree
  on a height that is never drawn). "Expected" is the number of loaded chunks touching the node, so a
  chunk that is loaded but has no mesh is counted as *missing* rather than quietly excluded.
- **R2 agreement.** Two separate numbers, because they have different causes: the **cross-chunk
  spread** at a node (the render path is not sharing one corner height) and the **own-lattice delta**
  (the render path copies the corner from the wrong place â€” rule 8's "copy" contract, measured).
- **Staleness fingerprint**, from 1hu: the (facet step, vertex count) buckets, plus a step-drift
  count against the current `EffectiveLowPolyStep`. Buckets > 1 catches a MIXED resident set. Buckets
  == 1 does **not** clear it, because the other staleness shape is a *uniformly old* set where nothing
  ever streams in â€” hence the separate drift count. This is the one mechanism the repo can create on
  demand and it is indistinguishable from a renderer bug by eye, so it gets its own line.

### H3 â€” addressing arithmetic (the part that could have made the readout lie)
A chunk root sits at `(tc.X * 30 * ChunkData.Size, 0, tc.Z * 30 * ChunkData.Size)` (verified in
`CreateChunkGameObject`) and `ChunkData.Size == 1`, so mesh-local X/Z + `tc*30` == world X/Z and
**mesh-local Y == world Y**. That is what makes "compare the rendered corner against `LatticeY`" a
legal comparison at all, and it is the same assumption the seam audit's bounds check already makes.
Corners are mesh-local (0|30, 0|30); a world node is `tc*30 + local`; the chunks touching node (nx,nz)
are X,Z âˆˆ {n/30, n/30âˆ’1}, exact because nx/nz are multiples of 30 and C# integer division truncates
toward zero (so negative nodes still name the right pair).

### H4 â€” min or max vertex at a corner? â€” max, and the first answer was wrong
My first pass folded `min`/`max` over the vertices at a corner and compared the min to the lattice.
But a *smooth* root also carries **side walls** hanging down from the same edge, so the min at a
corner is the *bottom of a wall*, not the surface â€” a guaranteed false "own-lattice mismatch" on
every chunk. The low-poly root has no walls (1hi.1 skips them), which is why this would have looked
fine on one setting and lied on the other. Fixed to use the **topmost** vertex as the corner height;
the cross-chunk spread then folds min/max of those per-chunk tops.

### H5 â€” cheap enough to be honest, and still read-only
One `Mesh.GetVertices` per loaded chunk (~50â€“80 chunks Ã— 121â€“961 verts) through ONE reused list, and
`GetVertices` fills the caller's list rather than allocating a fresh array â€” so the lane is a single
frame of scanning and mutates nothing: no rebuild, no re-stamp, no forced poll, per rule 7. Two
dictionary-enumeration hazards were designed out rather than assumed safe: the first pass writes
existing keys (legal for `Dictionary`, but the second pass does not write at all), and the node
accumulator is a `struct` written back explicitly, so there is no reference-type aliasing.

### Open
- **The fix is deliberately not written.** If the user presses F3 and reads
  `VERDICT R1: â€¦` the cause is coverage and the fix is in the surface emitter; `R2` puts it in the
  corner-derivation arithmetic; `R-stale` puts it in rule 11's missing public "re-render everything,
  keep the saves" entry point, and the fix is a new API rather than a geometry change. The readout
  picks the branch; guessing would have picked all three.
- Zero-code A/B available meanwhile: **restart the play session** (every chunk then rebuilds through
  `BuildOrLoadChunk`) and press F3 again. Gaps gone â‡’ staleness. Gaps remain â‡’ the render path.

## 1hs â€” "reduce the tab button in the tab menu height and put their center higher" â€” SHIPPED (band 84@36 â†’ 40@10; Skills header row + Faith status re-datumed)

Rule 7 says measure a report I cannot see. This one I *could* derive from the code, so no
measurement lane â€” but the derivation is the interesting part, because the two numbers the user
asked for turned out to be the visible symptom of a layout that had no constraint in it at all.

### H1 â€” "the tab bar is just a big button; shrink it" â€” REJECTED as a complete answer
The two write sites are `BuildTopButtons` (anchoredPosition y 36, height 84Â·S) and
`OnLayoutFitted` (y **34**, height 84Â·S). Note they already disagree by 2: the bar is laid out
twice, from two private copies of the same numbers, and only `OnLayoutFitted` runs on a resize.
So "shrink the height" has two edit sites and a silent drift, and the drift is the more interesting
defect. â†’ whatever the new numbers are, they become named constants read by both sites. This is
the same shape as rule 9's helpers: a value that two places must agree on is stated once.

### H2 â€” "why is 84 tall?" â€” the band was sized by nothing, and it covers live content
Design space is 1066Ã—600 (1280/720 over `UiScale` 1.2) with the top edge at y +300. `Body` is inset
60/60, so panel content is centred on the same origin. The band: pivot is the TOP edge, so
y 36 â†’ 120 from the top â†’ **180â€¦264** centred.

Then I listed every widget whose y extent reaches into 180â€¦264 (`grep ", 2xxf)"` +
`, 1[7-9]xf)` across the five partials):

- Skills sub-tabs `P(-160, 250)`, `MakeButton` pivot (0.5, **1**) â†’ **220â€¦250** â€” inside the band.
- Skills readouts `P(-450, 222)` / `P(250, 222)`, `MakeBodyText` pivot (0, **1**) â†’ **194â€¦222** â€” inside.
- Faith title/status `P(0, 238)` / `P(0, 206)` â†’ **204â€¦238** / **180â€¦206** â€” inside.
- Info level `P(-330, 210)` 64 tall â†’ 146â€¦**210** â€” its top 30 units inside.
- Inventory equipment heading / storage header â†’ top of each inside.

Which raises the question that decides the whole task: **which of those is on top?** `Build()`
creates the panel then `Body`; `OnEnable` then calls `BuildTopButtons()` and only then
`BuildPanels()`. So the `Tab_*` buttons are the **later sibling** of `Body`, and Unity draws the
later sibling on top. â†’ the band was **covering** the Skills sub-tabs, both Skills readouts, the
Faith title/status and the top of the Info/Inventory headings. They were not "ugly", they were
invisible. Confirmed by draw order, not guessed from a screenshot.

### H3 â€” "so how short, how high?" â€” read the constraint off the panels, don't pick a number
The band cannot be sized in isolation; its bottom edge has to clear the tallest content row beneath
it. Two more datum lookups were needed:
- the skill tree viewport: `P(0, -80)`, 1000Ã—560, pivot centre â†’ top edge **y 200**, and it is a
  `RectMask2D`, so an overlap would clip the sub-buttons' bottom border, not merely overlap them;
- the Faith title: `MakeBodyText` is TopLeft in a 34 box, so its glyphs grow **downward** from
  y 238 â€” the box height is not the ink height. Same for the Skills readouts (22.5pt at 1080p).

So the usable corridor is y 200â€¦250, **50 units**, and the row that has to live in it is 30 tall.
Band 40 @ top 10 â†’ bottom edge 250 â†’ 14 units of air above the Skills row, 6 below it before the
viewport. Rejected alternatives: 52 @ 14 (bottom 234, row at 236 â†’ only 8 of air, and the Faith
title at 238 would sit 4 under the band); 44 @ 6 (bottom 250 but 6 from the screen top, cramped);
"band only, 56 @ 24" (the sub-tabs still under it â€” this is the option the user declined in favour
of fixing the rows too).

### H4 â€” "the two readouts and the three sub-buttons are two rows; can they be one?" â€” YES
Sub-tabs span x âˆ’220â€¦160 (120 wide, centred at âˆ’160/âˆ’30/100); the readouts span âˆ’450â€¦âˆ’250 and
250â€¦470. **Horizontally disjoint**, so they never needed two rows â€” they were stacked because the
author put the readouts at 222 and the buttons at 250 for no reason the geometry can explain. One
`SkillsHeaderY = 236` for all five widgets removes the second row *and* the choice. (Earlier I
briefly had the readouts at 200 to "sit under" the buttons â€” that is 4 units above the viewport
top, i.e. the readouts would sit **on** the dark tree box that is drawn after them. Caught by
re-deriving the viewport's top edge; the single row is the version that has no such collision.)

### H5 â€” "raising the band can only reveal things, so it is safe" â€” REJECTED, it created one defect
Revealing the Faith pair exposed a latent collision: title ink 200â€¦238 vs status ink 179â€¦206 at
1080p â†’ **6 units of overlapping glyphs**, hidden until now because the band covered both. Left
alone, my "cosmetic" change would have shipped a new visible defect, so the status moves 206 â†’ 192
(8 of air). Checked the other three revealed rows for the same trap and found none: the Info level
ink ends ~24 above the XP bar, the equipment heading (x âˆ’450â€¦30) is left of the storage grid
(x â‰¥ 120) on the same visual row, and the storage header keeps its existing 4-unit gap.

### H6 â€” "a 40-tall button can be outgrown by its own label" â€” CONFIRMED, clamped
`lt.fontSize = max(24, Screen.height / 44)` on a **height-matched** canvas is a second scaling on
top of the CanvasScaler's: 24.5 at 1080p, 32.7 at 1440p, 49 at 2160p. The old 84-tall box
(76 of text box) absorbed that up to ~3168 px of screen height; a 40-tall box (32) does not â€” 1440p
would already spill. â†’ `TabLabelFontSize()` = `min(0.95 Ã— (height âˆ’ inset), max(24, h/44))`, which
is **bit-identical to the old value at 1080p and 1440p** and only differs where the old value was
about to overflow. Unclamped `MakeBodyText`/`MakeButton` fonts elsewhere in the file are a
pre-existing, separate issue â€” not touched here.

## 1hmâ€“1hp â€” "redo the taoist temple and the church; the church has gaps in the structure; Taoism has 3 gods, so 3 statues not 1" â€” SHIPPED (1hm helpers, 1hn church, 1ho shrine, 1hp test-lane NPC heights); pagoda deferred by the user

User request, three parts: redo the Taoist temple, redo the church, and put **three** statues in the
Taoist temple instead of one. The user declined the rule-7 measurement lane (the gaps are provable
from the authored coordinates) and declined a pagoda fix in this pass.

### Which building is "the taoist temple"? â€” asked, because the codebase says two things
Two structures could be meant, and the iconography does not disambiguate: `BuildShrine` (`:186`) is
labelled "taoist shrine" by the test ground itself (`NewWorldTestGround.cs:46`) and holds the
**Taoist priest**; `BuildPagoda` (`:156`) holds the **Buddhist monk** and one seated gold Buddha
(`:1022`). Both hold exactly one statue, so "3 instead of 1" fits either. Asked; the user confirmed
**the shrine**, which is also the project's own naming and the only structure where the Three Pure
One**s** are iconographically at home. â†’ the shrine's `Shrine_Deity` becomes the Sanqing altar.

### The parts are dead data â€” a trap I nearly walked into
`_pagodaSubBuildings` / `_churchSubBuildings` / `_shrineSubBuildings` (`WorldBuilder.cs:368-437`) declare
`PartName`, `Offset`, `Size`, `Color`, `WoodCost`, `StoneCost` per part â€” and `BuildChurch` (`:171`)
reads **only `PartName` and `Offset`**, forwarding them to `SpawnStructurePart` (`:662`), which builds
geometry from a `switch` on the type name. So every declared `Size` in the table is documentation that
is already wrong, and `SpawnStructurePart` instantiates **no prefab** â€” each part is raw primitives via
`CreatePartCube` (`:937`). Two consequences:
- Reading the table to "fix the sizes" would have changed **nothing** in the world. Real geometry lives
  in the `switch` bodies (`:960-1818`).
- A part name that has no `case` falls into the generic `else switch` and builds nothing, **silently**.
  Since part type strings are what `SaveManager` persists and replays, renaming a part is a silent
  content-deleting change. â†’ new content goes **inside** existing parts; no renames (now AGENTS rule 9).

### Root cause of the gaps: two authoring conventions, not two bugs
Every gap reduces to one of: a block placed by its **centre** Y, so the bottom face has to be re-derived
by hand and misses its support; or a roof panel placed by a **tilt sign**, which hides an inverted
pitch. Both are invisible at authoring time and obvious in the world, and both are invisible to review
because a reader has to redo the arithmetic. That is the same failure shape as rule 8 (a copy whose
addressing rule is the contract), one layer up: here the "copy" is the bottom face, copied out of a
hand-computed centre.

**Decision (1hm):** fix the convention, not just the instances. `CreatePartBoxOn` (bottom-referenced),
`CreatePartPanelBetween` (underside-endpoint-referenced â€” the roof's two contact points *are* the
authored data, so an inverted pitch is no longer expressible), and `CreatePartGableSteps` (the stepped
closure, each step overshooting 6 cm because **a 6 cm intersection is invisible and a 6 cm gap is a
slit**). `CreatePartPanelBetween` derives its rotation from `Quaternion.LookRotation(slope, width)`,
which maps the panel's local +Z onto the slope and its local +X onto `width`; for a Z-rising panel
`LookRotation(0, sin, cos)` gives `Euler(-atan2(dy,dz),0,0)`, which is why the +Z panel's correct
authored sign is **negative** â€” the pagoda's positive sign is the bug, not the church's.

### Unity rotation signs, worked out once (this is where the pagoda's roofs die)
`R_x(Î¸)Â·(0,y,z) = (0, yÂ·cosÎ¸ âˆ’ zÂ·sinÎ¸, yÂ·sinÎ¸ + zÂ·cosÎ¸)`. For a panel at +Z, the **+Z end goes UP when
Î¸ < 0**. So `Euler(+14)` at Z=+4.2 (`:1144`) raises the outer eave and lowers the ridge: a **valley**.
Corner check on `Pagoda_Roof1` (half-extents 8.6/0.275/4.7, centre local (0, 0.425, 4.2)): top-inner
corner â†’ local (x, âˆ’0.445, âˆ’0.427), top-outer â†’ (x, 1.829, 8.694) â€” monotonically *rising* outward, so
the four pagoda roofs are butterfly roofs. The church (`Euler(+24)` at Z=+0.9) and the shrine
(`Euler(+14)` at Z=+3.2) use the opposite sign and are correct hip roofs; I confirmed the church's two
panels meet exactly on the ridge (top surfaces at Z=0, y 1.174 local, both sides) â€” **rejecting** the
sub-agent's claim that they interpenetrate by 4.43 m. It confused each slab's Z extent with its
surface position: two slabs meeting at a ridge always overlap in extent and that is what a gable *is*.
Cost of the mistake if believed: I would have "fixed" a correct roof.

### The church defect list (all re-derived by hand, `WorldBuilder.Blueprints.cs:1395-1630`)
Headline, and the thing the user is actually seeing: **the roof only covers the middle 8 m of a 13 m
nave.** Panels reach Z Â±4.02 (`:1500`); the front/back walls are at Z Â±6.15. ~4.3 m of nave is open to
the sky at each end. Then, in the order I found them:
- **The gold spire is 6.2 m off-axis from its own spire roof.** `Belfry` (`:1537`) and `Spire` (`:1568`)
  build at local Z = âˆ’6.2 (on the tower); `SpireRoof` (`:1554`) builds at Z â‰ˆ 0. Spire footprint
  Z âˆ’7.1..âˆ’5.3; spire-roof panels reach Z âˆ’4.39 (3Â·cos38 + 0.45Â·sin38 = 2.188, centred at Â±2.2) â‡’
  0.91 m of horizontal gap, no overlap in Z at all, plus a 1.00 m vertical gap (spire base bottom
  12.60 vs spire-roof top 11.601) â€” the spire + cross hangs 2.30 m in the air beside the tower. Four
  separate `Offset`s each looked locally right; nothing ever checked that a stack shares an axis.
- **Ridge beam floats 0.45 m** above the roof (I re-derived this; the sub-agent's "0.054 m" compared the
  beam against each panel's highest *corner* at Z=âˆ“2.01, which is not on the ridge) and the two 14 m
  eave trim bars at Z Â±6.6 sit 2.46 m outside the roof's reach and ~2 m above the wall tops, touching
  nothing.
- **0.50 m slot under both side walls** â€” bottoms 0.90, foundation cap top 0.40, floor only Â±6.5 wide
  so it never reaches the walls at |X| 6.625. Open to the sky, 11 m long, both sides.
- **0.425 Ã— 0.475 m hole at all four wall corners** â€” side walls end at Z Â±5.5, front/back panels start
  at |X| 6.2 vs the side wall's inner face 6.625. 4 m tall, open. The nearest bridging candidates are
  1.475 m apart, so nothing covers it.
- **Floating furniture:** 12 pillar bases 0.345 m, 6 pews 0.22 m, altar **1.02 m**, 12 buttresses 0.50 m;
  the 2 rear buttresses (Z 6.75..7.65) are off the back of the foundation entirely and hang over bare
  ground; rear tower pieces 0.15 m; nave floor 0.08 m; internal spire gaps 0.15 m and 0.05 m.
- The **nave floor is 13 Ã— 10 inside a 13.6 Ã— 12.3 wall box**, so 0.95 m of bare foundation shows at the
  altar end and the entrance.

### Design for the rebuild (1hn/1ho) â€” one frame, one datum ladder
All church/shrine part roots move to the site origin (Offset 0) and every part is authored in **site
coordinates** (y = 0 = platform top), so the assembly is auditable in a single frame instead of
thirteen overlapping local frames. Datum ladder for the church: `slabTop 0.35` â†’ `capTop 0.40` â†’
`floorTop 0.50` â†’ `plinthTop 1.10` â†’ `wallTop 4.30` â†’ `corniceTop 4.60` (the roof's bearing) â†’ ridge
underside 7.351. Roof solved from its contact points rather than its angle: eave underside 6 cm **inside**
the cornice (4.54) so the panel cannot leave a slit at the wall, ridge underside 4.54 + 6.15Â·0.4571 =
7.351, and the vertical depth `0.50Â·âˆš(1+0.4571Â²) = 0.550` is what puts the panel's top face at 7.901.
Gable triangles appear on the **side** walls (a Z-sloping roof's gable ends are the X-normal walls) and
close with `CreatePartGableSteps` built from **the same eave/ridge numbers the panels were built from**,
so the steps cannot drift off the real pitch.

### 1hn resolved â€” and the gable helper I shipped in 1hm was wrong
Confirmed on the 1hn build: the *centred*-step gable formula cannot work, and I had hand-verified it
against the wrong criterion. Reasoning: a centred step spans `|z| â‰¤ zHalf`, so its **inner edge is
`z = 0`**, where the roof underside is at its maximum (`ridgeY`). To cover the strip `[âˆ’zHalf, zHalf]`
its top must therefore be â‰¥ `ridgeY` â€” every step becomes as tall as the peak and the corbel profile
degenerates into a solid triangle. My 1hm check only asked "does the step's top stay inside the panel's
vertical depth" (yes, 0.256 < 0.547) and never asked "does it cover the roof *between* the step's outer
edge and the next step" (no â€” the gap at the closure's edge was 6.8 cm). **Rejected:** keeping centred
steps and just adding a wider full-span first step; that only fixes the eave strip, and the same
argument then fails one band in. **Confirmed fix:** *uncentred* bands, mirrored about `z = 0`, each
reaching the roof underside at its **own inner edge** (the highest point of the roof anywhere over that
band), plus the 6 cm drive-in. Checked with courses 10, halfSpan 6.15, rise 2.811: band 0.615, band 0
covers `|z| 5.535..6.15` with its top at 4.951 (the roof there spans 4.54..4.90 â‡’ covered, and 4.951 <
the panel's top 5.09 so it stays inside the slab); the last band covers `|z| 0..0.615` with its top at
`ridgeY + 0.06`; no band is left uncovered. Lesson for AGENTS rule 8's cousin: a validator that checks
only "does the copy stay inside its container" is not a validator for "does the copy cover its target".

### 1hn â€” four more contact defects the first draft introduced, caught by rereading case by case
Rule 8's arithmetic-of-the-copy discipline applied to my *own* new numbers, which is where it earns its
keep: all four were invisible in the source and would have been visible in the world.
1. **Front buttresses off the apron.** Authored at `|X| 4.70` on a 7.00-wide apron (`|X| â‰¤ 3.5`) â€” they
   floated over the podium. Fixed by widening the apron to 7.80 (a wider stepped base reads better
   anyway) and moving the buttresses to `|X| 3.35` so they hug the 6.20-wide tower body.
2. **Applied detail centred on the face plane.** The tower's door, rose window and side lancets were
   authored at the face centre, which puts them *inside* the solid tower â€” the exact bug the old side
   walls had. Fixed by a stated rule: applied pieces sit 5 cm proud of the face and are 30 cm deep, so
   they bury 10 cm. The solid-band walls (front/back) get applied lancets for the same reason; only the
   side walls are built with real openings, because they are the ones built from piers.
3. **Spire's third gold ring** landed 0.9 m above the shaft (the loop stepped 2.00 m from a 5.40 m
   shaft's start). Re-anchored the three rings to `apexTop + 1.80 + iÂ·1.70` and checked all three sit
   inside 13.746..19.146.
4. **Coplanar eave edges.** The belfry cornice was 5.20 wide and the spire panels 5.44 â€” the panel ends
   and the cornice edge were coplanar below 10.60 (z-fighting on a 0.15 m sliver). Widened the cornice to
   5.60 so the panels finish inside it. Same class found elsewhere and fixed the same way: the pew backs
   were flush with the seat's outer face (moved 0.09 inboard), the ridge beam was replaced outright
   because a horizontal beam on a peaked roof can only touch along the peak line, and the pulpit's second
   step was *higher* than the first (flipped, so the climb rises toward the platform).

### 1ho resolved â€” the shrine stops being a pagoda, and three things I had to re-derive
The old shrine was a two-tier pagoda-style hall: tier 1 at `y 0.85`, an upper floor part at `5.9`,
walls at `6.5`, a second roof at `8.2` and a 6.5 m spire from `9.8`, under a roof part declared
`14.4 Ã— 0.5 Ã— 5.2`. Two things are wrong with that on its own terms, before any gap: a **5.2 m deep
roof on a 12 m podium** does not cover the hall (it is 2.6 m deep per side of a 10.8 m interior), and
the tier-2 floor at `5.9` leaves a **3.1 m** ceiling over the deity â€” which is exactly the "statues
floating under a low lid" the user was describing. â†’ **CONFIRMED**, by arithmetic on the declared
sizes alone. **Decision:** a single-storey hall with the ridge lantern doing the vertical accent, and
the second tier's four part keys **repurposed rather than renamed or deleted** (they are save keys;
`Shrine_Tier2Floor` / `Shrine_Tier2Walls` / `Shrine_Roof2` / `Shrine_Spire` are the lantern's plate,
walls, roof and spire). That is the only way to get a tall silhouette without a save-breaking rename
and without a second storey to cramp the Three Pure Ones.

**The gable helper I generalised in 1hm was still wrong â€” three times, and the third one bites.**
(1) The 1hm signature took the wall's half-width *as* the roof's half-span, because the church's side
walls and its eave happened to be the same 6.15. The shrine's are not: the back wall is 6.0 wide and
the facade piers 6.1, under a 6.60 eave. With one number the bands trace the **roof's** line, not the
wall's, and the closure's outer edge stops 0.6 m short of where the wall actually is. **Fixed** by
taking the wall's plane point + normal and a **separate `gableHalfSpan`** (`roofHalfSpan` only sets the
pitch), and by re-checking the church's own call: its two numbers are equal, so its pitch is unchanged.
(2) Each band's **bottom** is `wallTop` and its top is the roof's underside at the band's inner edge â€”
two different datums, and I had only ever tested them where they are 6 cm apart (the church). The
shrine's eave hangs **64 cm below** its wall top, so out at `|x| > roofHalfSpanÂ·(1 âˆ’ 0.58/2.144)
= 4.815` the underside is already *under* the wall top and the band's height goes **negative**: a
mirrored cube whose BoxCollider is inside out. **Fixed in the helper**, not at the call site, because
the pagoda and anything after it have the same exposure and the church is safe only by 6 cm of luck:
clamp the run to where the underside meets `wallTop`, re-band inside that span, and `continue` on any
band that would still invert. (3) `courses` was silently a *maximum*, not a count, once (2) applies â€”
the church drops from 10 bands of 0.615 over 6.15 to 10 of 0.602 over 6.019 (that 0.13 m of board was
inside the roof slab either way, so nothing visible changes) and the shrine's back gable from 10 of
0.600 to 9 of 0.535 over 4.815. This is rule 8 exactly: the pitch and the extent are two different
pieces of data that happened to be equal once, and the band's height is a *derived* value whose two
inputs live on different ladders.

**The roof has to bear on the architrave, and "hanging it below" is what makes it bear.** The eave
underside is placed **64 cm below** the plate's top (`plateTop âˆ’ 0.64 = 4.05`), *not* the 14 cm the
church's cornice uses. Reason: the rake rises 0.3249 per metre, so the underside reaches the plate's
top plane at `6.60 âˆ’ 0.64/0.3249 = 4.63`; the architrave beams run to 6.20, so the panel is embedded
0â€“51 cm over **1.57 m of bearing**. **REJECTED first:** the church-style 14 cm drop, on the reasoning
that a smaller drop means a shallower bite. It is the opposite â€” a smaller drop moves the crossing
**outward**, to `6.60 âˆ’ 0.14/0.3249 = 6.17`, and the beams stop at 6.20, so the panel's underside would
reach the plate's top only at the very tip of a beam, about 1 cm deep: a tangent, not a seat. (My first
note of this comparison claimed the crossing lands at 6.77, "past the end of every beam" â€” wrong
arithmetic, caught re-deriving it; 6.77 corresponds to *adding* 0.17 m, not subtracting 0.14. The
conclusion survives, the number did not.) Deeper is the only direction that buys bearing.
**Same class, one level up:** the eave drop is not a free parameter â€” it is `bearing length Ã— tan`, and
picking it by eye ("hang it just below, it looks right") is how the 1.0 cm tangent gets authored.

**The lantern is a saddle on the cap, and "straddling the ridge" is a slot.** First draft sank the
lantern plate 0.45 m into the roof to make it bear on both slopes. **REJECTED by arithmetic:** the
ridge underside is 6.194 and the plate's bottom became 6.229 â€” **3.5 cm above the roof's own ceiling**,
so the plate had a 3.5 cm slot running its whole 3.2 m length along the ridge, visible from inside the
hall. Two ways out: bury it deeper (worse â€” the plate's top then clears the roof's top surface and the
plate *floats*), or **sit it on the ridge cap**: bottom at `ridgeTop âˆ’ 0.08`, i.e. 0.23 m of overlap
with the cap's own 0.22 m depth, overhanging 0.8 m each side. Also narrowed the plate 3.0 â†’ 2.6 for the
same reason in miniature: the crown drops 32 cm a side, so every centimetre of width past the cap is
cantilever. (A 0.8 m corbel overhang with a shadow under it is a deliberate cornice, not a slit.)

**The interior pass found two solids occupying the same space, both caused by my own new facade.**
Rule 8's discipline again, applied to numbers I had just written: (1) the censer at `z = âˆ’3.60` has a
2.0 m bowl spanning `âˆ’4.6..âˆ’2.6`, which runs straight **through** the new facade at `z = âˆ’3.9` â†’ moved
to `âˆ’2.60`, still in the entrance's path but inside the door line; (2) the Three Pure Ones' dais at
`z = 3.60` spans `2.6..4.6` and the **rear centre column's 0.95 base** occupies `3.425..4.375`, so the
middle figure was standing inside a column â†’ dais and figures to `z = 2.20`, altar to `âˆ’0.20`, its
step to `âˆ’1.30`, kneeling mat to `+0.70`. Also dropped the floor part's own threshold strip: the facade
already carries one, and two overlapping gold lips on the same line is a doubled step.

**A latent ship-stopper in the offset plan, found by asking what `Offset` is *for*.** Setting all 12
offsets to zero (the 1hn convention) puts every part's root at the site origin, so all 12 footprint
boxes test the same spot. If `CanPlaceBuilding` tested a part against its *siblings*, the shrine could
never be placed at all. **CONFIRMED it does not**: `PlaceStructureBlueprint` (`:88-105`) collects every
sub-plan â€” testing each against the world â€” and only then adds them, so siblings never see each other.
Checked before committing, because the failure mode is "the shrine silently does not build" rather than
anything visible. The corollary, recorded in the table comment: since the boxes are all centred on the
origin, the plan reservation is carried by the outer parts (Foundation 13.2 Ã— 19.2, Roof 13.2 Ã— 13.2);
the interior parts state their true sizes for the ghost and the footprint test but do not need to
contain their own geometry. Also corrected the church table's comment, which claimed `Size` was "only
used for damage volumes, never for placement" â€” it is used by the placement test; there are no damage
volumes at all.

### 1hp â€” whose datum is the NPC's feet? (the last item of 1hmâ€“1hp)
The queued fix said "raise the three faith NPCs by 0.915 m". **Rejected as stated**, for the reason
rule 8 exists: 0.915 is a *number copied out of a rig* (`shoes at local y âˆ’0.88`, height 0.07), and a
copy is only correct until its source moves. The honest fix is to ask the rig where its own lowest
point is â€” `StandOnGround` reads `Renderer.bounds.min.y` over the built hierarchy and drops the root
by `groundY âˆ’ minY`, so the figure's feet land on the platform *by construction* and a future skirt or
a taller hem changes nothing. Two more faults in the same three lines, both only visible once you ask
what the structures' footprints actually are: the taoist at 8.6 m south of the shrine was standing **on
1ho's new stair** (its treads reach `z = âˆ’9.6`), and the monk 2 m west of the pagoda's centre was
standing **inside the pagoda's 14 m podium** and facing `Euler(0, âˆ’90, 0)` â€” away from the pagoda he
belongs to. `WorldBuilder.CreateWorld` places the same three NPCs at `y 0.93/0.93/1.815`, which is
*probably* the same 0.915 m sink in the legacy world, but the legacy ground height there is unverified
and rule 4 keeps this project off the legacy village â€” recorded, not touched.

### Two compile errors I shipped in 1hm/1hn â€” and why review missed them
Unity was the first compiler to run over 1hm/1hn/1ho; it reported exactly two errors, and both are
invisible to every check this project uses. `CreatePartPanelBetween` does
`return CreatePartCubeRotated(...)` while `CreatePartCubeRotated` is declared `void` (**CS0029**) â€”
a mismatch introduced in the same commit that created the caller, missed because the convention here
is "grep the symbol, confirm the signature", and grep confirms the name exists in both places. And
`Church_Spire` read `apexY`, a local declared inside `Church_SpireRoof`'s case (**CS0165**): a
switch section's locals are scoped to the *whole switch*, so the read looks legal and the
definite-assignment analysis only fails on the path that enters `Church_Spire` without the roof case
â€” i.e. exactly the case the compiler is *for*, and the one thing hand-review cannot see, because
"did the other case run first?" is not a question anyone asks while reading geometry. Fixed by
promoting `sprHalf / sprThick / sprEaveY / sprApexY` to method-scope consts beside `sprTan`/`sprVert`
(`sprApexY = 10.45 + 2.72Â·0.7813 = 12.575`, unchanged), so both cases read the same names â€” the 1hn
"one datum ladder" rule applied to a case boundary. Then scanned all three structure builders for the
whole class (a name declared in one case and read in another): **one** instance, and no name declared
twice across cases. **The lesson, generalisable:** signature agreement and brace balance are not
compilation, and a per-case local is a *hidden* dependency between cases â€” the geometry reads fine
and only the control flow disagrees.

### 1hr â€” can the review that missed two compile errors be *mechanised* instead of retyped?
The 1hq lesson ("grep proves a symbol exists, not that a signature matches") only helps if the
next session reads it before writing the same grep again, and a session that retypes a check is a
session that eventually retypes it wrong â€” my first arity script searched for the call's `(` using an
index computed on the **untrimmed** line and so counted `new Vector3(`'s commas as arguments: 486
false mismatches, which is how a check becomes noise and gets ignored. Two conclusions, one about
the tool and one about the thing the tool was checking. The tool: a check that can emit hundreds of
false positives is worse than no check, so the script lives in `tools/StaticChecks.ps1` with the
paren-matching done by a real scan (so index drift cannot happen) and it is *overload-aware*
(`CreatePartBoxOn` has both a 5-arg and a 6-arg form â€” a naive "declared 5" reading invents
mismatches that are not there). The thing being checked: check 6 turned out to matter more than
expected, because rule 9's silent no-build is the one failure in this project that produces **no
error at all** â€” a renamed part key compiles perfectly, runs perfectly, and draws nothing. Extending
it from the shrine's 12 keys to all three structures (12/12, 13/13, 14/14) costs one regex. And the
1hp placement audit is the same instinct applied to my own fix: I moved three NPCs to new coordinates
and verified them against *every* other lane on a 120 m platform rather than against the one
obstruction I had already found â€” the taoist's new Z (`cz âˆ’ 50.5`) happens to be within a metre of
`SpawnBoss`'s Z (`cz âˆ’ 50.4`), which is harmless only because the boss is 30 m east. **A fix verified
against the bug it fixes, and not against the field it moved into, is half a fix.**

### Open / not done
- **Pagoda butterfly roofs + `Roof1`'s centre cap floating 0.57â€“1.44 m over its own panels** â€” a real
  defect of the same class, found while auditing, **deferred by the user's choice**. Recorded as a
  follow-up in PROGRESS so it is not lost. `Roof2/3/4`'s caps happen to plug their own valleys, so only
  Roof1 visibly floats; the inverted pitch affects all four.
- **Stained glass is buried inside a solid wall** â€” **FIXED in 1hn** for the church's side walls (sill
  band + lintel band + seven piers leave six real openings, glass inside the hole). The front/back walls
  remain solid bands, so their lancets are applied proud of the face; that is a deliberate, stated
  compromise, not an oversight.
- The three faith NPCs in the test lane are sunk 0.915 m into the platform (`NewWorldTestGround.cs:682`
  places the roots at bare `baseY`; every rig puts its feet 0.915 m below the root, `MapBuilder.NPCs.cs`
  `:203/258/313`). Queued as 1hp. â†’ **SHIPPED in 1hp**, and the number was replaced by a measurement
  (`StandOnGround`) rather than a literal, per rule 8.
- **Shrine (1ho) â€” SHIPPED.** Single-storey hall on one datum ladder, the four tier-2 part keys
  repurposed as a ridge lantern, a real front facade with a doorway, and three Sanqing figures on a
  dais at `z = 2.20`. The gable helper gained a separate `gableHalfSpan` (see above), the interior was
  re-spaced around the new facade and the rear column, and `_shrineSubBuildings` moved to
  zero offsets with true sizes.

---

## 1hj â€” "permanent see-through slits/holes at ground level between terrain chunks" â€” measurement shipped, readout received (resolved in 1hk)

User report, in their own framing: the terrain has **permanent gaps/holes you can see through when
you are up close**, not a distant-horizon artefact and not something that follows a dig. Asked what
triggers it they declined to pick from my menu (dig / far rim / LOD band / moving), which is itself
information: the symptom is *always* there, near ground.

### Where I started: the design says this cannot happen â€” which is exactly the trap
Re-read the seam path end to end and it is watertight **by construction**:
- **One owner per corner.** Pristine heights come from the 5-octave Perlin surface sampled at world
  corner positions (Â§2.3), NOT from the neighbour's mesh. `BuildCornerGrid` stamps a 31x31 lattice per
  chunk from canonical noise + persisted whole-corner edits. Neighbour A and neighbour B at a shared
  world tile run the same function on the same inputs â‡’ the same float.
- **Exact placement.** A chunk's root GO sits at `chunk.X * 30 m` (1el fixed the span-cell
  misplacement), so node (30, k) of chunk (cx, cz) *is* node (0, k) of chunk (cx+1, cz) in world
  space, not approximately.
- **Node spacing divides 30.** `EffectiveLowPolyStep` rejects any step that does not divide 30 (only
  1/2/3/5/6/10/15/30 survive, in practice 3), LOD children use 2 m, the smooth root is 1 m. No
  T-junction can be created by a mismatched step.
- **Mesh pooling is whole-mesh.** `AcquireChunkMesh`/`ReleaseChunkMesh` re-specify every channel on
  reuse; there is no partial/lazy upload that could leave half a chunk's geometry behind.

So every "obvious" cause is already refuted by the code, and the two defects I *can* see
(`ApplyHeightEdits` not writing corners to the owner tile at `cx-1`/`cz-1` when the bbox edge is a
whole metre, and `CurrentHeightOf`'s half-tile noise fallback) only bite **after a dig**. The user
did not report a dig. **That is the whole reason I refused to just fix those two**: a correct fix for
a cause that isn't the cause is indistinguishable from a no-op, and it burns the session's only real
instrument â€” the user's eyes on the gap.

### Hypotheses carried into the audit (each is a mechanism the readout can name, not a belief)
- **A â€” corner divergence.** A deformed chunk's lattice disagrees with its neighbour's at a shared
  world tile â‡’ a slit along the boundary. *Pro:* the seam is a *slit*, which is what a lattice
  mismatch looks like. *Against:* only reachable via an edit, and the user named no edit. **OPEN.**
- **B â€” short/hidden root.** 1hi.1/1hi.2 rewrote the real-chunk root twice (coarse lattice surface,
  then a winding fix) and `PatchRegion` re-samples the *whole* root from the lattice
  (`ResampleLowPolySurface`) on every patch. A bad re-sample or a stale LOD/collider interaction
  could leave a root that renders a partial footprint â€” "hole where a chunk should be" without any
  lattice problem. *Pro:* recent root rewrites; the symptom is "at ground level" (a partial top face
  with a side wall missing shows sky under the lip). *Against:* unverified. **OPEN.**
- **C â€” interior hole.** A chunk position in the loaded ring with nothing in it. *Pro:* streaming is
  dictionary-driven, and `StreamAround`'s unload/demote/dormant handoff has several passes; a
  mis-ordered demote could leave a ring-`keep` position with neither a real chunk nor a live far cell.
  *Against:* the user says permanent and visible from ground level, which suggests looking *down* â€”
  consistent. **OPEN.**
- **D â€” far/real rim step.** The far shell is built from the same canonical noise at 3 m, so an
  *undeformed* rim is seamless; a dug chunk at the rim would step. *Against:* the user is near ground
  and did not name the rim. **OPEN, low prior.**
- Also carried, rejected: **backface/winding** (1hi.2 already fixed the whole lattice family, and a
  one-sided backface reads as "invisible from above", not "see-through hole"); **LOD band** (Lod1/Lod2
  are decimations of the same lattice and always meet the neighbour at the shared boundary â€” and the
  user would have named the 30â€“270 m band, not the ground underfoot); **z-fighting** (a coplanar seam
  sparkles, it doesn't look through).

### Why the audit is shaped the way it is
- **Read-only, hard line.** No rebuild, no patch, no re-stamp, no forced poll. A measurement that
  perturbs the thing it measures is worthless: the number would describe the audit, not the frame.
- **Node-by-node, not per-tile.** A corner mismatch is a *node* property; comparing rendered vertices
  would fold two 3 m facets into one number and hide a 1-node slit.
- **A skipped node must never read as a passing node.** Voxel chunks have no lattice, so they are
  counted (`noLattice`) and excluded rather than silently contributing 0.
- **Section C's hole test is deliberately biased against false positives.** A position is a hole only
  if â‰¥3 orthogonal neighbours are LOADED. The streaming frontier has unloaded neighbours by
  definition, and a chunk sitting in `_readyChunks` is in neither `_pendingChunks` nor
  `_chunksInFlight` for one poll â€” a weaker test would flag normal loading as a hole and train me to
  ignore the section. A non-empty ready queue prints an explicit transient caveat instead.
- **Section D scans ring `keep` as well as ring `near`**, because the far cell covering the diagonal
  chunk `(cx+1, cz+1)` is no axis-neighbour of any ring-`near` chunk â€” a missing *corner* cell is the
  most visible hole of all, and it is exactly the one an axis-only scan misses.
- **Far-mesh reads are guarded** (`isReadable`) and the far step comes from `FarSectorStep` rather
  than a hard-coded 3, so the audit follows the pipeline instead of a snapshot of it.
- **One number per mechanism + a VERDICT line**, listed offenders capped at 6 per section: a readout
  that names a few offenders is a screenshot; one that names 400 hides them.

### Dead end worth recording
- I started to add a public `TryGetFarCellForChunk` helper to the streamer. `ChunkDiagnostics`
  (1gh) already answers exactly that question for a single coord, and the audit can call the private
  `FarCellForChunk` directly as part of the same partial class. **Dropped it** â€” a second public way
  to ask the same thing is future drift, not feature.
- The user's chosen key was **F8**; F8 is bound to an editor cutscene ending in `GameManager`
  (`#if UNITY_EDITOR`, F5â€“F12 all taken, F1 is a skill hotkey). Kept the key serialized, defaulted to
  **F2** (free in the codebase today), and used the **new Input System** â€” this project has zero
  legacy `Input.*` calls, so `Input.GetKeyDown` was not even an option.

### Verdict: A CONFIRMED and fixed in 1hk; the "pristine â‡’ 0" premise was WRONG; D measured, still open

**The readout (user pressed F2, 361 loaded chunks, 684 pairs, 21204 nodes, zero skipped):**
```
A real<->real  pairs 684  nodes 21204  WORST dY 0.5693 m at TChunk(1,-2)-NTChunk(1,-1) node 30
B roots  checked 361  bad 0  OK         C holes  interior 0  frontier 4  OK
D rim  rimChunks 72  cells 76  missing 0  noFarCell 0  compared 758  worst dY 9.6813
VERDICT A: real<->real corner mismatch
```
A â‡’ hypothesis A is the mechanism, and 0.5693 m is exactly the right order for the 1 m height gradient
of the 5-octave surface â€” which is what a **positional off-by-one** predicts.

**Hypothesis A as I wrote it was wrong in its evidence line, and that is the interesting part.** I had
written "only reachable via an edit" â€” the shared corner could only diverge if a re-stamp reached one
side and not the other. The trace killed that: `BuildCornerGrid` never samples noise at all. It
**copies a vertex out of an owner tile's stored mesh data** (`:911-919`), so the lattice agrees with
the neighbour only if the owner rule picks the tile corner standing on the node's own world corner.
And it didn't:

```
node (cs, gz)  ->  owner tile (cs-1, gz), slot 1 (NE)  ->  world corner (Ox+cs, Oz+gz+1)   WRONG
node (0,  gz)  ->  owner tile (0,  gz), slot 3 (SW)  ->  world corner (Ox+cs, Oz+gz)       right
```
Same lattice position in space, two different world corners, **always** â€” no dig, no save, no race.
The fix is one token: `slot = 1` â†’ `slot = 2` (SE) in the build path and its patch twin. I spent all
of 1hj refusing to "fix the deform bug" on the grounds that it was the wrong suspect, while the right
suspect was a single wrong constant in a function I had already read twice. **Lesson: when a
seam-invariant argument says "these cannot differ", check the arithmetic of the claim before looking
for a race.** I had verified *placement* (exact block origin) and *step* (divides 30) and treated
"canonical noise" as an automatic guarantee â€” it is not, when the value is *copied* rather than
re-derived. game-design Â§2.2 asserted the guarantee in writing and was wrong; it now states the
ownership rule itself as the contract.

**Why the readout's location matched the bug so precisely** (this is what made me confident before
touching anything): the worst node was index 30 of a **north** edge â€” the 4-chunk corner (30,30). Its
four owners stamp it as (30,30)â†’SE âœ“, (0,30)â†’NW âœ“, (0,0)â†’SW âœ“, and (30,0)â†’ the broken east column's
`gz == 0` row, reading `h(60,-59)` instead of `h(60,-60)`. So the loudest error in the whole ring
appears on the north seam of the chunk *below* the broken column. The readout pointing at a **north**
pair for an **east-column** bug is exactly what the mapping predicts; guessing at "north seams" would
never have found it.

**Why it presented as a see-through HOLE, not a step.** The low-poly root is built from this lattice and
emits no side walls, and `BuildBorderCorners` feeds only the side-wall pass, which the low-poly path
drops entirely (`:477-478` short-circuits before `border` is read). A 0.5 m disagreement with nothing
to close it = a slit you can see through from ground level, permanently, everywhere. The smooth 1 m
root is unaffected (it renders from tile data, always exact) â€” which is why this read as a mystery
rather than a bug with an obvious blast radius: the broken surface is the DEFAULT one.

**Also refuted on the way:** `ChunkValidator` checks **tile** heights tile-vs-tile (`:60-77`) and tiles
are exact, so the project's own no-gap validator passed the whole time. A validator on the wrong layer
is worse than none â€” it manufactures confidence. Section A exists because that layer is unvalidated by
construction.

### Section D's 9.6813 m â€” a real disagreement, and NOT this fix
~30Ã— too large for a 1 m gradient, and the non-voxel far builder emits **tops only** (the wall/skirt
code belongs to the voxel path, off by default), so there is no wall vertex to blame. Surviving
mechanisms: (i) a far cell built before a dig was flushed â€” the far shell's own comment calls this
expected; (ii) `RelaxLegacySlabTile` relaxing a legacy flat-modified tile's corners in memory only,
which the real lattice inherits via `owner.Vertices[slot].y` while the far grid re-reads the unrelaxed
save â‡’ dY = 0.5Â·|slab âˆ’ noise|, so 9.6813 implies a ~19.4 m legacy slab step at a chunk border.
**OPEN**, candidate 1hl. Part of D's number was also the east-column bug (far grid correct, real east
column not) and should shrink after 1hk â€” the remainder is the open part.

### Two defects in my own audit, found by reading the readout instead of trusting it
- Section D addressed the real lattice with **cell**-local coords. Safe only because every gathered
  cell is span-1; a coarser cell at the boundary would read a shifted node, get skipped as NaN, and
  report a **false clean**. Now derived from the world node.
- Section D compared every vertex on a node position with no surface test. Now top-surface only
  (`n.y <= 0` â‡’ wall), inert while voxel is off but it would have produced a huge phantom if the flag
  were flipped. **A measurement that can report a confident wrong answer is worse than no
  measurement** â€” the same reason rule 7 demands the readout name a mechanism, not just a number.

### Verdict: CLOSED for the reported symptom
A confirmed (positional ownership bug) â†’ fixed in 1hk. B and C measured clean. D measured, real,
**open** (1hl candidate). The three edit-dependent seam defects from the 1hj trace (deform bbox
low-edge owners, seam-straddling persistence, `CurrentHeightOf`'s tile-centre fallback) remain **open
and unmeasured** â€” they only bite after a dig, the user reported no dig, and shipping them unmeasured
would repeat the exact mistake this task was built to avoid.

---

## 1hi.2 â€” "the chunk around the player is upside down, invisible from the top, visible from below, and the player falls right through"

### Hypothesis A â€” "the 1hi.1 root winding is back-facing" â€” CONFIRMED (shipped in 1hi.2)
Symptoms = textbook ONE-SIDED backface-culled surface. `GroundMaterial` keeps Cull Back ("Real chunks keep
GroundMaterial (Cull Back)", WorldStreamer.cs:34-38), so a mesh whose winding fronts DOWN is invisible from
above and visible from below. The 1hi.1 root's `EmitLowPolyIndices` copied the far-shell/LOD-child winding
verbatim; that family is emitted first-corner-first (SW, SE, NE, NW; tris (00,10,11)/(00,11,01)) â€” the
OPPOSITE geometric ordering of the proven smooth tile family (NW, NE, SE, SW; tris (0,1,2)/(0,2,3),
ChunkMeshGenerator.cs:152-164), which is the one-sided surface the player has always stood on. The collider
(`BuildDecimatedCollider`) shares the lattice winding â€” hence the fall-through. DECISIVE evidence the far
shell's SAME winding is the known-bad one: it needed the Cull Off `FarGroundMaterial` (1ei) â€” the comments
literally say it renders the decimated far terrain "from above regardless of mesh winding/culling artifacts
that once hid it from the upper face" (GameBootstrap.cs:106-112; FarShell.cs~1354). So the inversion is a
FAMILY trait â€” masked for the far shell by its double-sided material, latent in the smooth-mode Lod1/Lod2
children since 1e6 (never zoomed in on the 30-270 m band under one-sided culling in view, and the
double-sided far shell covers the same angle from ring 10 out).

### Dead ends REJECTED
- NaN heights / flipped bounds: `SanitizeHeight` clamps every corner (ChunkMeshGenerator.cs:118-123) and
  `BuildDecimatedCollider` sanitizes again â€” no NaN path; `EmitLowPolySurface` bounds track min/max of the
  sanitized lattice only.
- Collider-cook / threading: collider arrays are produced on the worker and uploaded main-thread via the
  pooled mesh; no negative-scale transform exists anywhere in the pipeline, so no non-winding mechanism
  can flip the surface. A down-facing winding is the only production path matching the report.
- "Same winding as the far shell, which the user SAW work in 1hi": the far shell is double-sided (Cull
  Off), so its winding tells us nothing about orientation under one-sided culling â€” the user's far-visuals
  praise was satisfied by the double-sided material, not by a correct winding. This was the trap that made
  the original 1hi.1 review overlook the inversion.

### Fix (shipped 1hi.2)
Re-emit the three REAL-chunk lattice surfaces in the smooth-tile up-facing ordering (NW, NE, SE, SW;
exact BuildMeshData (0,1,2)/(0,2,3) two-triangle pattern): `EmitLowPolyIndices` â†’ (v+3, v+2, v+1)/(v+3,
v+1, v+0); `BuildDecimatedCollider` â†’ (i01, i11, i10)/(i01, i10, i00); `BuildLodChild` â†’ same grid form.
Normals were already authored +Y and stay untouched. Far shell deliberately left alone (double-sided is
already correct from above). Verdict: closed by `1hi.2`.

---

## 1hi.1 â€” "the surface is currently too smooth to be called low poly terrain" (coarse near-ring facets; shipped)

### Hypothesis A â€” "1hi's flat-shading still reads smooth because normals aren't flat enough" â†’ REJECTED
Check by re-read of the emitted surfaces: the near ring was ALREADY per-tile flat quads (1 h threshold
knockout), and flat normals on a **1 m-dense grid** over a continuous 1 m corner heightfield produce a
perceptually smooth slope â€” the human eye integrates 1 m facets into a smooth surface. The far shell
(at 3 m facets) is what already read chunky. So the missing ingredient is not shading/normals â€” it is
**geometric density**: the rendered faces themselves must be bigger. Verdict: coarsen the near ring's
GEOMETRY, not its normals.

### Hypothesis B â€” "make the whole 1 m grid coarser (2-3 m world grid everywhere)" â†’ REJECTED
That rewires saves, edits, props, deformation and colliders away from the 1 m corner lattice â€” exactly
the "too Minecraft-like/blocky" rejection that un-defaulted the voxel model at 1ev. The 1 m lattice is
the single source of truth the rest of the game touches (DeformAt/FlattenAt corner heap, ChunkSave,
prop heights, PatchCornerGrid). Keep it canonical.

### Hypothesis C (adopted) â€” "render the REAL chunk roots as the lattice facets themselves" â†’ CONFIRMED
Reuse the exact recipe `ChunkObject.BuildLodChild` already implements (ChunkObject.cs:455-480):
sample the 31x31 corner lattice every `step` nodes (`s = gz*step*31 + gx*step`), flat +Y-dominant
per-quad normals, seamless at chunk borders because `step` divides 30 (the far corner lands exactly on
the boundary). The far shell's 1hi flat branch emits EXACTLY this at 3 m â€” so the near root at the same
step shares the identical market language AND identical world corner nodes across the near/far seam.
Extra wins that fell out:
- The collider already IS a lattice decimation (`BuildDecimatedCollider`, 1hi) with a step parameter â†’
  just pass the root step so you stand on the visual (no new machinery).
- `PatchCornerGrid` (1ew) already re-stamps the lattice on every deformation â†’ the coarse root can just
  re-emit from the restamped lattice; `PatchRegion`'s whole 1 m per-tile skim becomes unnecessary.
- The root at 3 m is ~121 quads (vs ~1800 tris full) â†’ the only per-chunk render surface, so the
  Lod1/Lod2 bands add nothing.

### Sub-decisions (evidence)
- **Step = 3, not 2** (user picked 3): 3 divides 30 (seam-safe) *and* matches the far shell's `FarSectorStep`,
  giving a single uniform facet language across ring 0â†’horizon; a 2 m root would read subtly nearer-band
  than the 3 m far band and would require children at 6m/10m for the divisor rule. `EffectiveLowPolyStep`
  default 3, test-ground knob allows 2.
- **Skip LOD children** (user picked skip): the root already sits at Lod2 density; keeping bands would
  need divisor-safe coarser steps (6/10) for a negligible save. Verified `ChunkLodManager.ApplyBand`
  is null-safe â€” when the tracked detail is missing it falls back to `rootMr.enabled = true`
  (ChunkLodManager.cs:259-263), and `FetchVisual` only registers children whose name starts with "Lod"
  (ChunkLodManager.cs:82), so zero children â†’ Details empty â†’ no toggling. `SetVisualActive`/
  `Release` already null-guard `_lod1Go/_lod2Go`.
- **Side walls dropped on the coarse root**: the 1 m builder emits vertical walls only where a tile is
  taller than its neighbour; the heightfield is a *function*, so a coarse facet between a high and a
  low node is a slanted face â€” never a hole. The far shell's flat band also emits no walls, so
  dropping them keeps the whole world visually consistent; a vertical cliff-face between two adjacent
  3 m corners is bridged by the facet (look, not watertightness issue).
- **UV/colors sampled from the lattice, not recomputed**: `BuildCornerGrid`/`PatchCornerGrid` already
  stamp per-node `UV` + `Colors` (TerrainBandColor) â€” sampling them makes the build, patch and
  far-shell paths read identical colors with zero extra noise samples, and PatchRegion can re-emit
  without a height memo or tile origin.

### Pitfall caught in review (value-type mutation)
`ResampleLowPolySurface` was first written `void (MergedChunkMeshData md, ...)` writing into the
parameter â€” but `MergedChunkMeshData` is a struct, so writes would vanish on return and `PatchRegion`
would keep the pre-edit arrays. Flipped to return the updated struct and assign:
`_merged = ChunkMeshGenerator.ResampleLowPolySurface(_merged, _meshStep);`. (grep-verified the
call site.)

### Pitfall caught (pooled-mesh vertex-count flips)
A chunk that toggles between full-res (961+ verts) and low-poly (400 verts) roots reuses its pooled
`Mesh` â€” `UploadMerged` clears whenever `mesh.vertexCount != md.Vertices.Length`
(ChunkMeshGenerator.cs:1230), so the mode is switchable mid-session without buffer corruption.
PatchRegion's re-upload for the low-poly path keeps equal lengths (400) â†’ no clear, cheap.

### Open risk to record
- 1 m corner-grab edits: a corner that does NOT sit on the 3 m grid moves no visible vertex of the
  low-poly root (heights still save/restore on the 1 m lattice). This is the accepted edit-granularity
  trade-off; watch it in play-test (item 3 in the 1hi.1 status checklist).
- Hot mid-session toggling of `LowPolyStep` only affects chunks built AFTER the flip (workers snapshot
  the step like the voxel flag); play-test guidance says flip before streaming (same as 1hi/voxel).

## 1hi â€” "low poly terrain + does it speed things up?" (look + perf; shipped)

### Question and the honest frame
User asked: make the terrain "more low poly vibe", and would that somehow increase performance.
First hypothesis worth killing: "low-poly â‡’ fewer draw calls â‡’ faster". Evidence against: the whole
view is ~150k triangles of trivially-clipped mesh; each far cell/real chunk is ONE draw call already.
GPU raster cost is noise. The REAL costs that scale with triangle count (read at the four poll-stage
call sites): (a) the synchronous **MeshCollider cook** of the full merged surface on the gameplay
frame (`MaxColliderCooksPerPoll = 2`, WorldStreamer.cs:144; MeshCollider rides the render mesh â€” all
`_mc.sharedMesh = ...` writes at ChunkObject :71/:192-196/:302-303 feed the render mesh); (b) the
**finalize / LOD runway** mesh upload + `lodSweep` band switches; (c) background chunk/far generation.
Verdict: flat-shading alone is LOOK only; making the actual collider/lattice coarser is where real
perf lands. â†’ shipped both (A visual facets, B decimated collider) as one knob.

### Hypothesis â€” "fewer triangles changes draw calls" â†’ REJECTED
Every cell is one mesh = one draw call regardless of density; vertex count has no per-frame cost at
this scale. Frame time is dominated by the synchronous cook + uploads (above), which DO scale with
triangle count â†’ so decimation (B) is a real win while (A) alone is cosmetic.

### Where exactly does the smooth haze live? FAKTEN (evidence gathered by re-read)
- Near ring = per-tile flat quads (already flat); 1ew adaptive refinement subdivides steep tiles
  (2x2) into sub-quads.
- Lod1 (2 m) / Lod2 (3 m) children sample the 31x31 corner lattice â†’ already flat facets.
- **Far shell = the ONLY smooth layer**: coarse 3 m geometry with central-difference normals
  (FarShell.cs smooth path, seam instead via WorldHeight cross-cell pulls).
- So the flat-facet branch belongs in `BuildFarSector` ONLY; near-band "flat language" = knock out the
  1ew split via threshold 0. `BuildMeshData`'s 0-threshold branch is the documented disable mode
  ("0 disables refinement" in the field tooltip, checked at ChunkMeshGenerator.cs:149
  `refineThreshold > 0f && IsRefinable`) â€” no degenerate path.

### Facet-branch design decisions (and why)
- **Keep step 3 (the uniform lattice) for flat band.** `FarSectorStep` returns constant 3 for all
  spans (verified FarShell.cs:244-247): span-1 axis=11, span-3 axis=31, span-6 axis=61. Building flats
  at the SAME step keeps triangle count identical and lets adjacent cells agree on the world corners
  â†’ seam-proof by construction. Cost: vertices 4x for far cells â€” accepted because a far cell uploads
  ONCE per cell lifetime (finalize), never per frame, so the per-frame budget (1ei/1eh time-caps) is
  untouched.
- Normal = cross of the +X/+Z edges, flipped to +Y for steep faces, degenerate-safe (`1e-12` guard).
  Same winding as the smooth path `(00,10,11)+(00,11,01)`.
- Colors from `TerrainBandColor` per world corner under each vertex (mirrors the merged builder),
  height memo shared. Bounds reuse the existing tail formula.

### Decimated-collider design decisions
- Source = the SAME `ChunkCornerGrid` the LOD children sample (31x31), stride 2 â†’ 16x16 = 256 verts /
  450 tris. Sharing the lattice means the physics surface is seam-proof across chunks BY CONSTRUCTION
  (neighbours agree on boundary nodes), and after a patch `PatchCornerGrid` restamps the lattice so
  the collider re-derives and tracks excavations. Initial dead-end avoided: sampling the merged VERTEX
  array fails post-1ew because refined blocks break the fixed stride â€” the lattice is the right source.
- Thread-safety: build arrays on the worker thread (in `BuildMergedMeshData` right after
  `BuildCornerGrid`) and carry them on `MergedChunkMeshData`; `ChunkObject` uploads on the main thread
  into a second pooled `Mesh _colliderMesh` (same acquire/release + overwrite-only discipline as the
  render mesh; `vertexCount`-changed clear). Lazy: a chunk that never enters the collider ring
  allocates nothing.
- **Bug found in review:** crib RefreshCollider v1 skipped re-upload when `_colliderMesh` already
  existed â€” but a rebuild (FullRebuildChunk / 1ea save-scan) re-applies FRESH merged arrays onto a
  chunk that's already collider-enabled, so the cooked surface would silently stay pre-rebuild
  geometry until the next patch/Deform. FIX: the apply path (`md.ColliderVertices != null`) always
  re-uploads before the nullâ†’assign cook. (This is the "hypotheses/evidence/verdict" dead-endâ†’fix the
  task's THINKING log earns noting.)
- Guard `VoxelMesh`: voxel columns are already chunky + its merged data has no collider arrays; render
  mesh stays the collider there, identical to pre-1hi.

### Threshold routing â€” all 4 consumers swapped to the property
`job.Refine` (Streaming.cs:172), dispatch builder (ChunkBuild.cs:137), Deform re-skims (Deform.cs:255
and :513). `RefineThreshold` keeps its serialized field identity (save/scene ABI stable); only the
routed reads go through `EffectiveRefineThreshold`, so the look flips via the knob without touching
the serialized value. Grep-verified zero remaining raw consumers.

### Verification performed (rule 3 â€” no build)
grep + reread: `LowPolyFacets` reads only in FarShell.cs:714 (worker, same pattern as the voxel flag)
and the property; `BuildFarSector` has exactly one call site; every `_mc.sharedMesh` write is inside
the three new methods + Release; new symbols collision-free; far-cell/VoxelMesher producers never set
collider arrays; the 1ew 0-threshold disable path confirmed clean. Docs synced (PROGRESS 1hi,
game-design Â§2.5/Â§2.10).

---

## 1gh â€” "chunk -8_3 is invisible for no reason" (FIXED by the cull invariant â€” play-test pending)

### The report
Inside the 1gg window, the user reported terrain chunk `-8_3` (world x â‰ˆ -240..-210, z â‰ˆ 90..120;
Chebyshev ring 8 from the platform) is invisible. Asked two discriminating questions: the GameObject
`TerrainChunk_-8_3` **exists but is invisible** (ruling out a never-finalized chunk), and console
generation-failure spam is unknown/not noticed.

### Hypothesis 1 â€” "the chunk never finalized (corrupt tc_-8_3.dat save â†’ infinite retry)" â†’ REJECTED
`BackgroundGenerateChunk`'s catch re-enqueues every poll, so a persistent failure would mean NO real
chunk â€” but the user confirmed the GameObject exists. Far cells only exist beyond the near ring, so a
missing real chunk inside ring 8 would be a bare hole; that matches the FALL-THROUGH symptom but NOT
"exists but invisible". Ruled out by the user's hierarchy check.

### Hypothesis 2 â€” "the LOD sweep cull hides a chunk the streamer still retains" â†’ CONFIRMED (the trigger)
`ChunkLodManager.Update` hides (whole-root `SetActive(false)`) any registered chunk beyond
`EffectiveCullDistance`. The old formula: `max(CullDistance, (RenderDistance.Radius + 1) * ChunkSize)`.
Evidence:
- The streamer LOADS real chunks out to `NearRingRadius` 9 and KEEPS them through ring 10 (hysteresis
  + dormant depth). The LOD cull term is driven by the scene's `RenderDistance` asset only.
- With a small asset (e.g. radius 7 â†’ cull 240 m), a ring-8 chunk at ~250 m IS inside the retained
  real-chunk band but BEYOND the cull â†’ sweep deactivates it.
- No far cell exists inside the near ring (`FarCellForChunk` span-1 requires ring â‰¥ near+1), so no
  cover replaces it â†’ invisible hole.
- `SetActive(false)` also kills the collider â€” consistent with the 1gg fall-through report at the
  same frontier.
- The class default radius (30 â†’ 930 m cull) makes this invisible at defaults, which is why it
  presents as "this ONE chunk is gone for no reason" only around ring 8-9 whenever the scene asset is
  small -- exactly where -8_3 sits.

### Hypothesis 3 â€” "coarse far cell z-fights/obscures the real chunk at rings 6-9" â†’ LEFT OPEN
The span-6 far cell covering rings 6-12 draws coarse ground under live real chunks at the frontier;
the 1es-era shadow re-scan was scoped to span-1 rims. If the real chunk were OVERDRAWN it would
FLICKER, not sit consistently invisible â€” and the user said invisible. Not needed once H2 is fixed,
but if H2's fix shows no change, this is the next suspect (would present as widespread frontier
flicker, not a single chunk).

### Why the fix is the invariant, not a band-aid
`EffectiveCullDistance` now floors at `max(CullDistance, (Radius+1)*30, (NearRingRadius + 1 +
DormantRingDepth)*30)`: LOD may only hide a real chunk where a coarse far cell LEGITIMATELY takes
over (beyond the near ring); inside the near ring the real chunk is the sole surface for its cell. At
full render distance the floor is dominated by the render term (no behavior change); at small
distances the retained ring-8/9 frontier can no longer be culled, and dormant chunks were already
skipped by the sweep.

### Diagnostics so it is never a guessing session again
`WorldStreamer.ChunkDiagnostics(tc)` (public, reuses the private `FarCellForChunk` + live-sector
check) plus `ChunkLodManager.BandIndexOf(root)` feed a `NewWorldTestGround.EnableChunkDiagnostics`
overlay line for `ChunkInspectX/Z` (default -8/3): real loaded/dormant/absent, root active, renderer+
mesh, Lod1/Lod2 on/off, LOD band, collider, far owner live/MISSING. Read-only; no world writes.

### 1gh verdict
H1 rejected (user check), H2 confirmed and fixed by the cull invariant, H3 held open as a fallback
suspect. Diagnostic line shipped for confirmation. Status OPEN until play-test confirms ring-8/9
ground returns with a small render distance.

## 1gg â€” "player can fall right through the ground with the collider not loading" (FIXED â€” play-test pending)

### The report
During sprinting, the player falls through visibly rendered ground â€” no collider under them. This
shipped as a distinct bug DURING the 1gf window (user message after 1gf was pushed).

### Hypothesis 1 â€” "the far shell / dormant band has no collider and the player reaches it"
Far cells are render-only (no collider), and `DemoteChunk` switches a real chunk's collider off when
it sleeps. But both live OUTSIDE the real near ring (NearRingRadius 9) and outside the collider ring
(7): the player is the walk's centre, so physically they can only ever stand at cheb ~0 â€” the dormant
band/far shell is 300-450 m out. REJECTED as the trigger (the player can't reach it without already
standing on collidable ground).

### Hypothesis 2 â€” "the collider walk's budget gating starves the player's own chunk" â€” CONFIRMED
Tracing the collider-on-demand queue:
- `CreateChunkGameObject` builds streaming chunks **collider-less** (`buildCollider` false). The ONLY
  assigner is `ReconcileCollidersIfChanged`, once the chunk enters ring 7 (â‰ˆ3-14 s before a walker
  arrives â€” normally plenty of lead time).
- The 1es/1ge shared-budget gate (`if (_streamCapped) defer`) skips **every** enable on a heavy poll,
  and `MaxColliderCooksPerPoll = 2` caps the fill regardless. During a sustained heavy sprint
  (finalize/far/props draining the pool every poll) the lead time collapses: a chunk that entered ring
  7 mid-crossing can keep failing its first cook for the whole crossing, and by the moment the player
  steps onto it (it is now cheb 0, or it finalized at their feet), `_collidersDirty` has been
  re-arming the walk but `_streamCapped` has kept blocking it. The order of the walk is a
  dictionary iteration â€” no spatial priority, so the centre cell is not specially favoured.
- The fall itself is physically "sane": gradual descent, no blast â€” so `EnforcePhysicsSanity`'s
  150 m / NaN tolerance never fires, no far-cell collider exists below, and the player falls forever.

### Why the fix is two layers
- **A (root cause):** player-floor exemption + closest-deferred guarantee in `ReconcileCollidersIfChanged`
  makes the centre cell + 2-chunk floor cook unconditionally (bounded 6/poll) and keeps BOTH the
  centre cell and the approach-front â‰¥1 cook per poll no matter the budget. Trade: up to ~3-4
  synchronous PhysX cooks can run on a crossing's heavy poll (~1-3 ms each) â€” the price of physical
  correctness over streaming smoothness; charged to the shared budget so the accounting stays honest
  and the prop stage still sees the real spend. Bounded, so it cannot regress the frame.
- **B (backstop, user-confirmed):** `VoidFallFloor` -300 m revert in `EnforcePhysicsSanity`. Any real
  surface lives above the sanitized -200 m band; crossing -300 unambiguously means void. Chosen as a
  fixed constant (not a per-sample terrain comparison) so it never depends on noise evaluation during
  a fall and can't misfire on carved pits (SanitizeHeight keeps those â‰¥ -200).

### 1gg verdict
Hyp-H2 confirmed; fix A + B shipped. Both pending play-test confirmation; status OPEN until the
sprint/no-fall-through checks pass.

---

## 1gf â€” residual sprint lag after 1ge: instrument before guessing (SHIPPED â€” verdict per-hypothesis pending play-test numbers)

### Where we are
1ge (and 1xd before it) closed the invisible-chunk-hole, the cadence cut, the collider-cook burst and
the stamp paths. The user still reads the sprint / fast-crossing hitching ("still the lag"). Every
heavyweight stage now runs on the decoupled 20 Hz coroutine under one shared 4 ms budget + per-stage
caps + hard slices, so a pure code read can no longer point at one guilty stage with confidence â€” the
remaining cost distribution needs measurement. Hence: $0 behavior change this task, just a read-only
per-stage ms split on the bench HUD.

### Hypotheses still on the table (each gets confirmed/rejected by the readout)
- **H-1 â€” always-on far-shell scan width.** `FarShellTick` step 1a (shadow sync over the ~1,000 live
  cell boxes â€” though trimmed to span-1 re-checks per 1es), removal scan, ring walk (~3.9k
  consider-calls), pre-warm and dispatch all run at FULL WIDTH every live poll and only CHARGE the
  shared budget after the fact; they are never interrupted mid-pass (visibility-critical). If
  `farScan` reads high on EVERY poll (even while walking), the fix is dirty-flagging the shadow-sync
  or widening the far step near the player.
- **H-2 â€” per-crossing real-chunk finalize burst.** `FinalizeChunks` (ChunksPerFrame â‰¤ 12 new GOs +
  merged-mesh uploads + 900-tileÃ—3 dict registration each) is the fill column at a sprint. If `final`
  peaks only at crossings, throughput is already capped by the shared budget; further spread would
  shrink per-poll work, slowing fill.
- **H-3 â€” far finalize burst at ring cuts.** 1er pre-warm was supposed to dissolve it; `farFinal`
  peaking at a crossing proves the pre-warm is not keeping up at top speed.
- **H-4 â€” gameplay-frame LOD builds.** `ChunkLodManager.Update` (NOT on the decoupled clock) rebuilds
  decimated LOD meshes synchronously on band change (`RefreshLodMeshes` â†’ `BuildLodChild` +
  `UploadMeshData`) for every newly registered chunk. `lod sweep` peak is the ^-proof; if high, LOD
  builds must move onto the streamer cadence or be deferred.

### Why instrument BEFORE the third targeted fix
1gd and 1ge were both aimed at "the obvious stage" from code reading; the lag outlived both. A ~40
line read-only split (one `public` value struct + out-params at the single `FarShellTick` call site +
four floats on the LOD manager) localizes the third round and keeps the eventual A/B fix minimal.

### 1gf verdict
Instrument shipped (`656b6cf`). H-1..H-4 each await the sprint readout numbers; status OPEN until the
user reports the peak line under a hitch.

---

## 1ge â€” 1gd follow-up: "still the lag" + a group of chunks goes invisible (FIXED by 1xd â€” play-test pending)

Pre-plan questions (from user): what terrain the run used (SMOOTH), the shape of the loss (a GROUP of
chunks goes invisible), HUD watched? (no), toggle A/B'd? (not yet). Answers shape below.

### H-1 â€” seam-rebuild stamp collision makes a chunk's mesh vanish â†’ WEAK
Evidence AGAINST: a rebuilt/ApplyMerged mesh is a REAL mesh on the object; a wrong apply changes the
surface, it cannot make the chunk INVISIBLE. Invisibility == `SetVisualActive(false)` with nothing
covering it â€” a State made only by DemoteChunk. Stamp collision can't produce it. Kept only as a latent
hardening bug (see H-5).

### H-2 â€” rebuild applied to a dormant chunk re-surfaces it â†’ WEAK
Evidence AGAINST: that would show EXTRA chunks (a visible chunk inside the dormant band + z-fight with
the far cell), not missing ones. Fix D (drain rejects `obj.Dormant`) is belt-and-braces only.

### H-3 â€” far-shell static extent gap over the dormant band â†’ REJECTED
Checked `FarShellTick`: `keep = view + FarOuterKeep`; ring walk is `near+1 .. keep`, and `RequiredFarCell`
for span-1 needs `maxRing >= near+1` with no required coarser owner. At default (view 30, near 9) the
span-1 cells over rings 10-14 (covering the dormant band 11-12) ARE required and built as inactive
shadows. The removal scan retains required cells; nothing deletes them at demote ring. No static gap.

### H-4 â€” demote runs while the required far cell's async build hasn't landed â†’ CONFIRMED (the visible-group hole)
Evidence FOR: `DemoteChunk` (Streaming.cs) hides the chunk and removes it from `_loadedChunks` with NO
cover check; the far active-shadow sync then activates the span-1 cell under it THE SAME POLL â€” but
only if that cell is already in `_farSectors`. If the far finalize is still pending/backlogged (the 1gd
"cool-down after any busy poll" cut the effective poll rate ~15-30% at speed), there is NO cover and
the region reads invisible until the far build lands (~50-400 ms). A whole trailing column crossing
together = "a group of chunks goes invisible". The far shell's own coarse tenant demote is guarded
(`FarCoverageReady`); the real-chunk demote path had NO such guard â€” an asymmetry, not an accident of
timing.
Verdict: root cause. Fix A = gate the demote on `_farSectors` containing `FarCellForDemote(...)`; a null
cover (position beyond the far annulus, small render distance) must NOT backlog forever â†’ treat null as
un-gated demote.

### H-5 â€” stale reload stamp collision (latent, kept as hardening) â†’ CONFIRMED as a bug, fixed cheaply
`MeshRebuildStamp` was a per-object int starting at 0 each lifecycle; two lifetimes of the same coord
could both reach 1, letting a stale in-flight result pass the drain's stamp check over a fresh chunk.
Now the stamp comes from a shared monotonic counter (`NextMeshRebuildStamp`) â€” values never repeat.

### H-R1 â€” remaining lag = every-busy-poll cool-down â†’ CONFIRMED (cadence)
`StreamLoop` cooked `yield return null` after ANY busy poll (StreamOnce returned "did work") â†’ ~10 Hz
effective during catch-up â†’ rings filled slower â†’ player keeps closing on unready ground â†’ polls keep
hitting. Fix B: StreamOnce returns HEAVY (`_streamCapped`) so only budget-exhausting polls cool down;
light busy polls keep 20 Hz.

### H-R2 â€” remaining lag = unbudgeted collider cooks â†’ CONFIRMED (partial)
`ReconcileCollidersIfChanged` cooked up to `MaxColliderCooksPerPoll` (2) synchronous PhysX meshes per
poll AFTER all budgeted stages â€” every poll the ring moves at speed. Now gated on `_streamCapped`
(defer, `_collidersDirty` keeps the walk alive).

### 1ge status:
- All four fixes implemented (demote gate, heavy-only cool-down, collider budget gate, global stamp +
  drain dormant-reject). Verified by grep + reread. Play-test still required: sprint the dormant band
  with/without the toggle; confirm no invisible group + no frame spike on a wide crossing.

---

## 1gd â€” "immense lag at higher player speed" (SHIPPED â€” first play-test reported lag-kept + invisible group; root cause found in 1ge)

User reports: smooth while walking, immense lag at higher player speed. "Player map renderer" =
the 3D terrain renderer (`WorldStreamer`), NOT the corner minimap. Pre-plan asked to "separate the map
renderer from the gameplay update so fast player speed doesn't cause lag".

### H-A â€” the lag is the UI minimap (ugui / render texture / second camera) â†’ REJECTED
Evidence AGAINST: no `RenderTexture`, no `RawImage`, no second camera exists in the codebase. The
corner minimap is `CompassMinimapHUD` (UGUI mask + Image), the world map is `WorldMapUI` (text/panel).
Both are cheap, and their update cadence is event/time-gated. The reported symptom (walk smooth /
sprint lag) tracks streaming work, not UI. Verdict: not the component.

### H-B â€” the smoking gun is the synchronous edited-terrain rebuild during load-reconcile â†’ CONFIRMED
Evidence FOR: walking into edited/saved terrain hit `ReconcileNewlyLoadedChunk` (WorldStreamer.Mesh.cs:141)
which called `FullRebuildChunk` (Deform.cs:231) SYNCHRONOUSLY whenever the chunk is in `_modifiedChunks` â€”
a 900-tile re-emit + `BuildMergedMeshData` + full mesh+collider upload on the main thread; and
`ReconcileModifiedBorders` (Deform.cs:347) chained up to four MORE `FullRebuildChunk` calls. Both fired
per-POLL, i.e. repeatedly while moving at speed through previously edited ground (every newly-passed
chunk triggers them). The 1es 4 ms `StreamBudgetMs` pool does NOT include these â€” they're outside the
budgeted stages, so a poll that streamed normally could additionally stack 1-5 full rebuilds.
Evidence AGAINST alternatives: chunk generation was already async (`BuildOrLoadChunk` on ThreadPool);
far shell work is budgeted/sliced; LOD/collider passes are capped. Only this path ran uncounted
full-chunk mesh work synchronously on the main thread at the exact moment high speed is crossing new
chunks. Verdict: confirmed causal path.

### H-C â€” "just limit the loading speed" (anchor-lag the streamer to the player) â†’ REJECTED BY USER
User explicitly rejected slowing the loading speed: "i dont want to limit the loading speed, think of
another way so the rendering wouldn't affect the gameplay". So the design went to (1) an independent
renderer clock + (2) moving the rebuild WORK off the main thread, rather than spreading the same load
over more time. Verdict: rejected as the solution shape.

### H-D â€” coroutine clock: does a WatchForSeconds(1/20) coroutine actually isolate gameplay frames? â†’ CONFIRMED w/ caveat
The old `Update()` poll fired inside a gameplay frame (timer accumulate â‰¥ 0.05 s), so its multi-ms
work doubled that frame's cost no matter what. A coroutine ticked by its own `WaitForSecondsRealtime`
still runs DURING some frame â€” but (a) the tick is decoupled from the gameplay update heartbeat
(no longer exactly every Nth frame), and (b) an extra `yield return null` after any BUSY poll inserts
a cool-down frame, so a heavy slice can never land immediately adjacent to a gameplay-poll's slice.
That is "rendering can't affect gameplay"; loading RATE is preserved. Caveat scored: the cool-down
frame adds ~one frame of latency to the stream â€” an acceptable, invisible trade (it only appears on
busy polls). Confirmed.

### H-E â€” snap raw ChunkData refs to the worker vs snapshot-heights copy â†’ raw refs CONFIRMED
Thread-safety analysis: the worker only calls `ChunkMeshGenerator.BuildMeshData` + `BuildMergedMeshData`
(reads `Heights[]`, `IsValid`, `ChunkX/Z`, `Seed` + a snapshot border dict). The rebuild snapshot is
taken on the main thread while the chunk is loaded; the chunk won't be unloaded mid-flight because the
chunk just entered the ring and the fit is a one-shot (deferred into the same growing ring). The ONLY
mutation hazard is a player deformation landing on the same chunk mid-flight â€” guarded by
`ChunkObject.MeshRebuildStamp` (captured at snapshot in the job, compared at drain; `ApplyMerged` and
`PatchRegion` both bump it), so any newer apply discards the stale result. A deep copy would double the
GC churn for a state we can invalidate for free. Verdict: raw refs + stamp guard.

### H-F â€” voxel mode must not lose its rebuild path â†’ route RequestChunkRebuild to the sync voxel path
`FullRebuildChunk` dispatches to `FullRebuildVoxelChunk` when `VoxelTerrainEnabled`. If the async pipe
bypassed it, voxel chunks crossing edited terrain would keep stale slabs. Kept the whole voxel mode on
the synchronous path (opt-in experimental); only smooth (default) terrain uses workers. Confirmed.

### H-G â€” the extra `if (_rebuildPending.Count==0) _chunkUnloadBacklog = _chunkUnloadBacklog;` â†’ REJECTED, removed
Leftover from drafting (compat no-op that did nothing). Removed during the same pass. Dead-end, kept as
the "don't ship no-op polish" note.

### H-I â€” pooled per-poll temp lists vs keeping the lazy `if (list == null) list = new ...` pattern â†’ pool CONFIRMED
The lazy pattern allocates once per list-per-poll-condition and holds the lists between polls anyway.
Pooling the field explicitly is the same allocation profile with clearer clearing semantics (a fresh
poll must re-scan, so the lists must clear + refill every poll â€” the lazy pattern did this implicitly).
No behavior change; slightly less GC headroom ambiguity. Confirmed (micro).

### 1gd status:
- H-A rejected, H-B confirmed (cause), H-C rejected (approach), H-D/H-E/H-F/H-I confirmed (design),
  H-G rejected (no-op), matched the implementation order Aâ†’F. Left OPEN for the play-test (flagged in
  PROGRESS 1gd-status): whether the cool-down frame + async rebuilds actually read as smooth at speed,
  and whether the `rebuilds` counter on the bench HUD tracks the seam fixes as expected.

---

## 1gc â€” chunks "disappear then generated right back" at the edge of the close range (SHIPPED â€” play-test pending)

Report: while moving, at the edge of the close range the chunks would disappear then be generated
right back. Initial lead handed in pre-plan: "the collider-on-demand system destroys/recreates
chunks when moving." User asked: "can't it just remove or add the mesh directly?"

### H-A â€” the collider ring destroys/recreates chunks â†’ REJECTED (initial wrong lead)
Evidence AGAINST: `ChunkObject.SetColliderActive` (ChunkObject.cs:37) only assigns/clears
`MeshCollider.sharedMesh` on the same long-lived chunk object; it never destroys or regenerates the
chunk. `ReconcileCollidersIfChanged` iterates `_loadedChunks` and only flips that sharedMesh field
(paid by a 1-3 ms PhysX re-cook per toggle; capped 2/poll). The collider ring (7 â‰ˆ 210 m) is INSIDE
the mesh ring (9 â‰ˆ 270 m), so its toggling cannot make a chunk visibly vanish. Verdict: rejected as
the causal path; it only matched the phrase "close range" coincidentally. (Kept as a perf note:
colliders were already never the destroy/regen source.)

### H-B â€” the pop is the real-chunk â†” far-shell handoff at the near+1 hysteresis ring â†’ CONFIRMED
Evidence FOR: `StreamAround` keeps chunks loaded to `keep = near+1 = 10` (WorldStreamer.Streaming.cs:50),
then `UnloadChunk` (:228) flushes dirty tiles, drops 900 per-tile dict entries, `obj.Release()`
(destroys props, returns the pooled mesh, destroys Lod children) and `Destroy(gameObject)`. Walking
back one chunk re-enqueues the coord â†’ `BuildOrLoadChunk` on a worker â†’ new GameObject â†’ mesh upload.
Sequential destroy + full regenerate on every boundary crossing == the exact symptom, and the band
(~270-390 m) reads as "the edge of the close range". The far cell fills the gap the same poll
(active-shadow sync), so there is no void â€” only the resolution swap + the rebuild hitch/churn.
Evidence AGAINST alternatives: LOD band swaps at 30/60 m are too near and belong to the dense LOD
system (not destroy/regen); far-shell span-3/span-6 ownership swaps at rings 13-36 are farther and
1eq/1er already pre-warm + retain them. Verdict: the near+1 boundary is the destroy/regen source.

### H-C â€” the fix shape: "keep the chunk, just add/remove the mesh" â†’ dormant keep-ring CONFIRMED
User asked "how about disable the collider instead?" â€” answered NO: the collider toggle and the mesh
lifecycle are separate rings; disabling colliders (already ring 7) cannot keep a chunk from being
destroyed at ring 10. Three variants costed + offered (plan): (1) dormant keep-ring â€” retain the
loaded chunk hidden out to keep+DormantRingDepth while the coarse far cell covers it; (2) keep fully
rendered + collider-only within a radius â€” fixes the pop but returns full draw/mesh/prop cost;
(3) near-ring bump â€” moves and rarifies the churn, still regenerates on crossing. User chose (1).
KEY design decision: REMOVE dormant chunks from `_loadedChunks` (into `_dormantChunks`) rather than
overlay a flag on the loaded ring â€” then every existing "does the real chunk cover this?" consumer
(far active-shadow FarShell.cs:389/438/1242, prop ring Props.cs:52/77, collider reconcile
WorldStreamer.cs:316, LOD registration NewWorldSystems.cs:116) automatically treats the chunk as
absent, so the far cell shows over the hidden chunk with ZERO far-shell/prop/collider edits.
Retaining the tile dicts + pooled mesh + VoxelStore makes wake a pure dict move + SetActive
(`DemoteChunk`/`WakeChunk`), and since wake re-enters `LoadedChunks`, `NewWorldSystems` re-registers
with `RegisterChunk` (BandIndex=-1) â†’ the LOD band re-applies correctly with no staleness.
Bug caught during implementation (verified by reread): `NewWorldSystems.Update` only diffs LOD
registration every `RegSyncInterval` seconds, so between demote and unregister the `ChunkLodManager`
band sweep could `ApplyBand` and leave a dormant chunk's renderer enabled over the far cell â€” the
exact z-fight the active-shadow rule exists to prevent. Fixed with `ChunkObject.Dormant`
(`[System.NonSerialized] public bool`) + a one-bool skip in the sweep scan loop.
Verdict: dormant keep-ring implemented; destroy churn eliminated inside the dormant band (rings
11-12 at default depth 2); the only destroy left is the deep-unload past keep+DormantRingDepth
(capped 6/poll with the existing backlog gate).

---

## 1gb â€” "magic projectile hit â†’ 3 object floating up then disappear": identify the floating-object effect and replace it with an exploding, fading sphere (SHIPPED â€” play-test pending)

Report verbatim: "currently when magic projectile hit something it will generate an effect of 3
object floating up then disappear, change that effect into an exploding sphere then disappear, as
it explode the transparency will increase".

### H-A â€” the "3 objects" is the crater excavation's cube burst (SpawnCraterDebris), NOT the impact-prefab path â†’ CONFIRMED
Evidence FOR: every projectile impact carves an impact dent via
`TerrainDeformer.Apply(..., TerrainShape.Crater, ...)` (SpellEffect.cs); `DeformAt` (Crater) then
calls `SpawnCraterDebris(center)` which spawns `Random.Range(3, 6)` (so "3" matches the minimum)
up-biased rigidbody cubes ("floating up") destroyed after ~2.5 s ("then disappear"), tinted by the
terrain band â€” matches the report exactly.
Evidence AGAINST the alternatives: `SpellEffect.cs:280` spawns `spell.ImpactEffectPrefab`, but grep
finds NO assignment of `ImpactEffectPrefab` anywhere (code/scene/asset) and glob finds ZERO
`.prefab` files in the project â†’ that call is null-inert. `SkillFx.FallRock` shards are a sky-rock
landing (Cast.cs:227), not a projectile hit. `WorldBuilder.SpawnRockDebris` is pickaxe rock-mining
only. Verdict: the debris burst is the effect to change.

### H-B â€” scope: projectiles only; tools/zones/strikes unchanged â†’ CONFIRMED by user answers
Same `SpawnCraterDebris` fires for shovel/pickaxe digs, storm strikes (SpellStorm.cs:117), summon
dents (SpellCaster.Cast.cs:117), zone impacts (Cast.cs:263), and the test-ground lanes
(NewWorldTestGround.cs). Changing DeformAt's default would alter tool feel the user never mentioned.
Fix: new trailing `bool emitDebris = true` on `DeformAt` + `TerrainDeformer.Apply`; only
`SpellEffect.ResolveProjectileImpact` passes `emitDebris:false`. The user's "keep both" answer was
read as keep the dent + replace the cubes (sphere replaces, not stacks) â€” confirmed by the
dent-question wording.
REJECTED alternates: removing the dent entirely (user re-affirmed keeping it); replacing debris
globally (touches tool digs / zone strikes); wiring up the dead `ImpactEffectPrefab` prefab path
(no prefab to assign; adding one contradicts the prefab-free FX convention).

### H-C â€” effect design: expanding sphere whose transparency increases as it explodes â†’ CONFIRMED (user choice: element color, dent kept, scale with spell radius)
Implemented as `SkillFx.ImpactSphere` + `ImpactSphereFader`, pattern cloned from the existing
`RingFader`/`ShardFader` (per-instance material so the alpha fade never races the shared
`SharedSpriteMaterial` cache; `Sprites/Default` shader; collider stripped; self-`Destroy` at t=1).
Local scale grows 0.25Ã— â†’ 1Ã— radius (SmoothStep from 0 to 0.45 of lifetime) while material alpha
goes 0.9 â†’ 0 â€” transparency increases as it explodes, then it vanishes. Scale = spell radius; color
= `DamageNumber.ColorFor(_spell.Type)` so fireball/frost/stone shard read school-colored.

### Verdict
H-A confirmed; H-B/H-C implement the user-asked scope. Fix shipped as 1gb (SkillFx.cs,
SpellEffect.cs, WorldStreamer.Deform.cs, TerrainDeformer.cs, docs). Play-test pending (rule 3 â€” no
build).

---

## 1ga â€” Earth Wall "vertical on player view" + "circle preview for some reason": ridge orientation & aim-preview gating (SHIPPED â€” play-test pending)

Report verbatim: "the earth wall create walls with vertcal on player view, and have circle preview
for some reason, fix it".

### H-A â€” wall orientation: ridge built ALONG the cast reads edge-on â†’ CONFIRMED (spec/UX mismatch, not a code fault)
Evidence: `WorldStreamer.DeformAt` projected `dir` onto XZ (~:61) and the Wall branch used
`wallDir` directly for the `along`/`perp` split â†’ spine parallel to the cast â†’ looking at the aim
point, the ridge recedes along the sight line and only its narrow end shows â€” a screen-vertical
slab (the user's "vertical on player view"). game-design Â§3.8 said "along the cast direction", so
the behavior matched the old spec. Asked along-vs-across in plan mode; user chose **ACROSS**.
Fix: local `ridge = (-wallDir.z, 0, wallDir.x)` inside the Wall branch only.
REJECTED alternates: rotating `wallDir` itself (the voxel crater directed clip reuses it at
~:137-141 â€” a global rotate would flip that dig half-plane 90Â°); rotating `dir` at the call site
(multiple callers: spells, tools, test lane â€” and the crater clip shares it).

### H-B â€” circle preview: `UpdateAoePreview` gates only on delivery, ignores TerrainShape â†’ CONFIRMED
Evidence: `PlayerController.Combat.cs` showed `AoeAimPreview` for any Zone/Vortex/Summon/Storm
regardless of shape; Earth Wall is Zone â†’ the disc shows while a round footprint is meaningless for
a directional ridge. User chose **no preview for walls** over a new ridge-shaped footprint (less
geometry; radial shapes keep their disc). Ruled out as the reported "circle": the `CastingCircle`
hand halo (legitimately shows for all armed magic) and `SpawnZoneRing` impact flash (post-cast,
not a preview).

### Verdict
Both confirmed; fixes shipped together as 1ga (WorldStreamer.Deform.cs ridge rotation,
PlayerController.Combat.cs preview gate) with docs synced in the same pass. Play-test pending
(rule 3 â€” no build).

---

## 1fz â€” "magic deformation leaves a see-through hole at the cast site": full-pipeline audit â†’ stale far-band LOD is the only provable defect (SHIPPED â€” hypothesis pending play-test)

User report: casting an earth spell occasionally leaves an uncovered/see-through area at the cast
site; walking closer makes it disappear; it persists after unload/reload; intermittent ("sometime",
"close up"). Working hypothesis list and the evidence trail:

### H0 â€” refined-lattice corner-slot mapping (`BuildCornerGrid`/`PatchCornerGrid` refined vertices) â†’ REJECTED (falsified)
Was the original plan's premise: refined tiles place tile-corners at slots {NW:8, NE:13, SE:6, SW:3},
so the lattice sampler supposedly read the wrong vertex on refined tiles. Falsified by reading
`IsRefinable` (ChunkMeshGenerator.cs:204-221): refinement is restricted to **strictly interior**
tiles (`gx > 0 && gx < cs-1 && gz > 0 && gz < cs-1`); border-ring tiles are ALWAYS coarse. Interior
lattice nodes only ever read slot 3 (= SW corner), which is exact in BOTH the coarse
(NW,NE,SE,SW = 0-3) and refined (row-major sub-quads) layouts. Mapping table would have been a
no-op. Also confirmed by construction: `BuildMergedMeshData`'s defensive fill + `BuildOrLoadChunk`'s
NaN-guarded corner stamping.

### H1 â€” worker-thread race on `_loadedData` â†’ REJECTED
`DeformAt`/`ApplyHeightEdits` run main-thread only; `BackgroundGenerateChunk` (worker) reads save +
noise only, never `_loadedData`. `FlushDirtyChunk` clones heights per-tile before publish.

### H2 â€” chunk-GO pooling reuse of stale meshes â†’ REJECTED
`UnloadChunk` (Streaming.cs:228-254) does `obj.Release()` + `Destroy(obj.gameObject)` â€” no pool;
reload builds a fresh GO, fresh `BuildOrLoadChunk` (deterministic from save+noise).

### H3 â€” far shell never invalidated by deform â†’ NOTED, OUT OF SCOPE
Far cells (â‰¥~300 m) never get deform invalidation, but the report says the hole is at the cast
site seen "close up" â€” far shell can't be what the user is seeing. Recorded, not changed.

### H4 â€” `RegisterChunk` snapshot timing (`entry.Details` empty) â†’ OPEN alternate
`NewWorldSystems.SyncChunkRegistration` snapshots LOD children at registration, but children are
built lazily post-registration â†’ `entry.Details` may be empty â†’ some far bands fall back to the
full Lod0 mesh (a performance bug, not a hole). Not the reported symptom; left as an open lead.

### H5 â€” stale far-band LOD child after deformation â†’ CONFIRMED as the only provable defect, SHIPPED
`ChunkLodManager.Update` called `RefreshLodMeshes` only inside `ApplyBand`, which runs only when
`band != chunk.BandIndex`. A chunk already showing Lod1/Lod2 (30-60 m / 60 m+) that got deformed
kept its **pre-deform decimated surface** until the player happened to cross a band boundary â€”
directly contradicting the invariant the code itself documents (ChunkObject.cs:58-59,
ChunkLodManager.cs:178-180: "a far band never renders a pre-excavation hole") and game-design Â§2.2.
FIX: `ChunkObject.LodDirty => _lodDirty` accessor; manager `else if (band > 0 && chunk.Chunk != null
&& chunk.Chunk.LodDirty) chunk.Chunk.RefreshLodMeshes();` â€” polled every scan tick; cheap (one bool
read) because `RefreshLodMeshes` early-outs when the flag is clear, and every path clears the flag.
This matches three of the four reported symptoms (visible hole at range, disappears when walking
closer across a band boundary, intermittent â€” only when the cast lands in an already-degraded far
band). **"Persists after reload" is NOT explained by H5** (fresh GO on reload) â€” so the section
stays with open alternates:

### OPEN alternates if play-test still shows the hole
- (a) legacy/corrupt pre-1fx save `ChunkTileMod` heights regenerating as noise inside deformed
  regions â€” deterministic across reloads â†’ persists; check `IsSaneHeight` noise-fallback paths.
- (b) sub-LOD-step thin deform features (a narrow wall line) vanishing in the 2x/3x decimated
  far-band sampling â€” feature is below the LOD sample rate, so the mesh simply doesn't resolve it
  (would "persist" as long as the band stays selected).
- (c) H4 registration timing showing stale/empty detail children.

### Verdict
H5 shipped as the 1fz fix (see PROGRESS Â§1fz for the play-test checklist); H0-H3 rejected with
evidence; H4 + (a)-(c) remain open leads for the "persists after reload" symptom. Section marked
SHIPPED-with-open-alternates (task committed; play-test validation pending).

---

## 1fx â€” "the map under the player no longer loads": real-chunk ring near the player missing (CLOSED â€” root cause CONFIRMED + fixed in the 1fx commit)

User report right after 1ew shipped (previous session): "only chunks near the player are missing" â€”
distant terrain still shows; then after the diagnostic commit: a ~300Ã—300 m hole around the spawn
point, only that region missing, no invisible ground. Root cause CONFIRMED by the instrumented worker
stack in one session:

```
System.IndexOutOfRangeException â€¦ at ChunkMeshGenerator.BuildCornerGrid
  at ChunkMeshGenerator.BuildMergedMeshData at WorldStreamer.BuildOrLoadChunk at BackgroundGenerateChunk
```

**ROOT CAUSE (1ew regression): `BuildCornerGrid`'s corner-ownership branch order.** The loop visits
all 961 lattice nodes including the far corner (gx=cs, gz=cs)=(30,30). The old order:
`if (gx<cs && gz<cs) â€¦ else if (gz == cs) â€¦ else if (gx == cs) â€¦ else â€¦` matched (30,30) with the
`gz == cs` branch FIRST and computed `ownerIdx = (cs-1)*cs + gx = 29*30 + 30 = 900` â€” indexing the
900-length tiles array out of bounds. That fired for EVERY chunk build, so EVERY real chunk failed to
build â†’ the entire near real-chunk ring (NearRingRadius 9 â‰ˆ 300 m) never materialized â†’ the far shell
(independent, skips cells over pending/in-flight chunks, FarShell.cs:468-472) rendered everything past
~300 m â†’ the "300Ã—300 hole at spawn" (the user's three reports â€” "under the player", "near-player
chunks missing", "300Ã—300 hole" â€” are the same single defect at different zoom/scales).

The 1fx instrumentation that exposed it (full `ex.ToString()` in the worker catch, main-thread
try/catch+`LogException` in `FinalizeChunks`/boot `GenerateChunkSync`) stays in place as safety nets.

### H1 â€” worker-thread throw in the 1ew build path â†’ CONFIRMED (the one real defect)
Was OPEN/instrumented. The user's Console read produced the exact stack: `BuildCornerGrid` OOB (line
712 pre-fix). Mechanism matched prediction: the worker catch dropped the chunk from in-flight and it
re-dispatched every poll â†’ persistent missing chunks + warning spam; most chunks "worked" only in the
sense that nothing told us they failed (stack was discarded pre-fix).
FIX (this commit): make the boundary branches mutually exclusive â€” `else if (gx < cs)` (north edge),
`else if (gz < cs)` (east edge), final `else` = far corner â†’ `tiles[(cs-1)*cs+(cs-1)]` slot 2 (SE),
exactly what the retired `WorldCornerIndex` produced â†’ LOD surfaces bit-identical to pre-1ew. Applied
the SAME exact-corner ownership to `PatchCornerGrid` (the pre-fix `gz == cs` branch there claimed
(30,30) for tile (30,29); the region-bounds check then skipped it, so the chunk's NE lattice node was
never re-stamped after a patch â€” silent stale-LOD-corner bug, same root).
Verified by grep+reread: no other `(cs-1)*cs` / `== cs` owner-index site remains in Assets\Scripts.

### H2 â€” main-thread throw in FinalizeChunks freezing the whole poll â†’ NOT the cause (guard kept)
The new `FinalizeChunks` try/catch+`LogException` never fired â€” the failure was on the worker. Guard
stays as a resilience net.

### H3 â€” boot chunk (`GenerateChunkSync`) throws â†’ NOT the cause (guard kept)
Boot completed; the hole was the whole near ring, not one spawn chunk. Guard stays.

### H4 â€” not an exception at all (rendering/collider side) â†’ NOT the cause (never reached)
The instrumented session produced an exception, so the render/culling/collider pivot branch was never
exercised â€” and the user's "no invisible ground" observation (standing in the hole means falling
through, chunks genuinely absent) is consistent with the confirmed absence cause.

### Dead ends (checked, likely irrelevant)
- **FarShell**: builds its own lattice/meshes (`BuildFarChunkCorners` etc.) â€” INDEPENDENT of the 1ew
  merged block table/Corners; confirmed by grep (no shared symbols) â†’ not the "near" failure.
- **Deform height sampling** (`CurrentHeightOf`/`GetDigDepth`/`DeformAt`): reads `_loadedData`
  (4-corner `ChunkData`), not the merged layout â†’ not implicated.
- **`Editor.log`**: only entry is a Unity LICENSING startup crash BEFORE the 1ew push ("return code 1") â€”
  no logged play session of the regression exists to mine a stack from (this drove the decision to
  instrument rather than guess).
- **Idle-gate / budgets**: `FinalizeChunks` early-outs on `_streamCapped` but the pooled budget and caps
  are unchanged by 1ew; a permanently capped stream would starve ALL stages equally, not just the near
  ring.

---

## 1et â€” voxel terrain: how to fix "the height field's one stretched quad" without losing the whole streaming pipeline (SHIPPED as P1 in `1et`; Phase 2+ pending play-test)

Started right after 1es-fix. The user's real complaint (observed in-play): natural steep slopes render
as a single un-editable stretched face â€” the heightfield stores 4 corner heights per tile and
interpolates a quad, so a 100 m cliff over ~2 tiles is one or two huge quad slopes you cannot carve
into like a wall. Asked flat-vs-volumetric; user chose **volumetric / voxel**. Verdicts below are the
reasoning, not the shipped summary (PROGRESS Â§1et / game-design Â§2.9).

### H1 â€” flatten the world and call it a day â†’ REJECTED (user)
I offered flatten-as-fix first (it deletes every cliff, fast, no mesh change). User explicitly wanted
the terrain to keep its shape AND be editable â†’ volumetric, with a 1 m step look mentioned as fine.

### H2 â€” dense 3D voxel grid per chunk (900Ã—Â±something arrays) â†’ REJECTED for P1
An honest voxel world usually stores a grid of solid flags per cell. 30Ã—200Ã—30 = 180,000 cells/chunk
light; but the whole live world already runs NO collider per air cell and streams bilboards; the real
cost here is (a) memory for hundreds of chunks and (b) every tool/ spell/ shovel gate reads the
4-corner `ChunkData` height API â€” rewriting all of them to voxel ops is Phase 3+ work.

### H3 â€” sparse column runs with a height-field ADAPTER (P1) â†’ CONFIRMED
Store per column ONE run `[ColumnBaseY..Top]` (lists, null = pristine). Every chunk keeps filling its
900 tiles with flat 4-corner heights (= the integer column top), so DeformAt/FlattenAt/
ApplyHeightEdits/GetDigDepth/ColliderOnDemand and the save path all keep working UNCHANGED â€” the
mesher is the only thing that changes (steps instead of smooth quads). This is the whole P1 trick:
"volumetric on the inside (columns), height-field on the API surface". P2 adds multi-run sculpt /
directed carve on top of the same store.

### H4 â€” material stored per column â†’ REJECTED (derived instead)
A run that must also store material per level is no longer 2 ints and bloats saves. The game already
derives grass/dirt/stone from depth below the noise surface in `TerrainBandColor`; depth below the
PRISTINE surface is deterministic from (seed, wx, wz) â€” derive it, store nothing. CONFIRMED.

### H5 â€” pristine columns must be a stored sentinel â†’ REJECTED (null = noise)
Rounding world-colour noise to integer tops (`RoundNoiseTop`) is deterministic. Null columns re-derive
it, so (a) saves stay sparse, (b) EVERY chunk touching a world column derives the same value â†’ seams
level with zero communication. `SetColumnTop` treats "wrote the column's own pristine value" as a
clear. CONFIRMED.

### H6 â€” cross-chunk seam walls â†’ real border map when loaded, noise fallback when not â†’ CONFIRMED
Two edited neighbours share a face: the higher owes a wall (left/right handedness). The border map is
the one-column ring computed from THIS chunk's sides via `VoxelTopFromTile` (rounded avg of sane
corners) using only loaded tiles (mirrors BuildBorderCorners' loaded-only policy). Missing neighbour â†’
the deterministic RoundNoiseTop the neighbour would itself derive â†’ untouched seams never phantom.
Full rebuild always recomputes it (cost trivial: ~120 lookups).

### H7 â€” initial build needs the border too â†’ REJECTED for the first load
`BuildVoxelChunk` renders the initial mesh WITHOUT a border map (neighbours not in memory yet), then
the existing border-reconcile (`HadLoadedMods` â†’ FullRebuildX) runs once the second chunk of a pair
arrives â€” same mechanism the smooth path already uses to fix seams. Chosen over sampling during build
(worker thread, neighbours immutable mid-build).

### H8 â€” save format â†’ v2 column runs, v1 migration on read â†’ CONFIRMED
Two files per world would lose cross-mode continuity; one file per chunk with a version int keeps
paths/folders/wipes intact. `Version=2`: NWTC|2|seed|cx|cz|colCount|(idx,top)â€¦ The voxel reader
accepts 2 AND 1 (v1 = reduce each tile's sane 4 corners to rounded-avg top; the ONLY sensible
height-fieldâ†’column projection and it matches what FullRebuildVoxelChunk does live). Smooth reader
stays gated at version 1 (writing v2 from the smooth path is impossible â€” it has no columns; a v2 file
found in smooth mode is simply "invalid file â†’ regenerate", documented). Dead-end considered:
version 3 with a per-chunk "mode" byte â€” pointless, the mode lives in the mesh toggle, not the file.

### H9 â€” the mesh itself
- Top faces merged as row runs (a 30Ã—30 flat pad = ~30 quads, not 900). CONFIRMED.
- Walls emitted on planes between columns; 1 band per metre of drop (self-similar to the flat-slab
  walls), >32 m drops quantized to 16 bands. Winding from the band quad cross product (matches merged
  builder). CONFIRMED â€” with two fixes found in verification: the first draft lerped bands with a dead
  `bandH` variable (removed) and a later edit dropped `ref` off a `TrackY(...)` call (compile error,
  fixed).
- Concern: two perpendicular walls with different drop sizes subdivide their shared 90Â° edge into
  different band counts â†’ a cosmetic crack at that corner. Accepted for P1 (the smooth path's corner
  bands behave similarly); note for P2 if visible.
- LOD/far shell: the decimated LOD grid indexes smooth TOPS-FIRST vertex order, so stepping voxel
  chunks under it corrupts; gate `RefreshLodMeshes` via ChunkObject.VoxelMesh (no-op) and keep root
  mesh rendering. Far shell stays smooth (P1), so far-smoothâ†’near-stepped transitions are expected.

### H10 â€” save flush edge case worth NOTING
A voxel chunk that resolves to full pristine deletes its shared file â€” but if that file was a legacy
v1 smooth save, the v1 edits are discarded too (the v2 writer already replaced or will replace it).
Real risk window: edit a chunk in smooth mode, flip to voxel, restore all its columns exactly to
noise, unload. Accepted (rare, and the file is being rewritten anyway on an edit); documented in
PROGRESS. Also: `HasModifications` is authoritative for the delete test, so a pristine chunk with a
stale v2 file from a previous world wipe-race cleans itself on next flush.

## 1ef â€” the far shell: how to get a deep, crisp 2 km view without fog and without an 18.9k-chunk real stream (SHIPPED in `1ef`)

User wanted the view deeper AND crisp (no fog). Working from the 1ee baseline (idle-zero-cost ring at
radius 67 would be 18,961 real chunks vs 1ee's 31-44 streamed world). Verdict keys are written like the
rest of this file â€” raw reasoning, not the shipped-design summary (that lives in PROGRESS Â§1ef /
game-design Â§2.5).

### H1 â€” raze the real ring to a near ring and fill the far ground with coarse cells â†’ CONFIRMED
A cheap-but-honest depth trick: keep real `ChunkObject`s (full fidelity, colliders, props, LOD) only
out to a fixed near ring (9 = 270 m), and let the ground beyond be ONE decimated mesh per aligned cell,
generated in the background. This scales render distance WITHOUT scaling the real-chunk stream, its LOD
layers, its collider ring (8) or its prop ring (4) â€” all the 1a/1dq/1di/1e6 wins stay exactly where the
player plays, and the far ground costs ~1,400 small static meshes. Evidence the seams line up: real Lod2
uses a step-3 lattice; the rim cells use step 3 on the same lattice â†’ gapless at the real/shell edge.
VERDICT: CONFIRMED â€” shipped as the span hierarchy + ring walk.

### H2 â€” "no hide logic needed" was WRONG for the chunk the player just left â†’ REJECTED, active-shadow substituted
First design draft claimed the rim needed no hide logic because the far shell's keep (+1) never loaded
ring 10, so the rim's first ring was 11. That left a HOLE: real chunks keep one hysteresis ring loaded
(the near+StreamAround keep pocket), so the chunk the player walks away from is STILL a loaded real
object at ring 10 while the shell claims ring 10+ â€” nothing rendered it after the shell went under it.
Two fixes considered:
  (a) deactivate the far cell when its real chunk is loaded (active = !loadedChunks), re-activating the
      same poll the real chunk unloads;
  (b) exclude ring â‰¤ near+1 from the shell entirely (keep a permanent real ring 10) â€” doubles the real
      stream, no.
(a) is O(span-1 cells) per poll and needs FarShellTick AFTER FinalizeChunks so a just-materialized real
chunk hides its cell in the same Update. VERDICT: (a) CONFIRMED â€” the span-1 "active shadow".

### H3 â€” a span-3/6 box straddling a band boundary z-fights its own children â†’ hierarchy needed
First pass chose cells per-chunk: chunk ring R in band B â†’ span-3 cell. But a span-3 CELL whose box
straddles, say, ring 14/15 (due to the focus being anywhere inside it) can contain chunks in two bands
â€” if a child chunk independently decided to be span-1 and the parent also rendered, both draw over the
same ground. And a span-3 parent whose box reaches ring â‰¥ FarBandBMin would be required while its
ring-13 child was also required â†’ overlap. FIX: one `RequiredFarCell` predicate used by BOTH generation
and the per-chunk mapping â€” a coarser required parent suppresses its finer children, so every chunk
maps to exactly one owning cell. Critical comfort check (the "spread facts"): a span-3 box spans â‰¤2
rings and a span-6 box â‰¤5, so a required parent can only suppress fine cells at ring â‰¥ 12 (B) or â‰¥ 35
(C); the rim (â‰¥10), near bands and any shadow the player sees at ring 9-10 are never coarsened. Also
verified: a required parent box can never contain a hole/loaded chunk (required span-6 has far-ring â‰¥36
â†’ its box is â‰¥ ~31 away; required span-3 â‰¥ ~13) so coarse cells never z-fight the real ring either.
VERDICT: CONFIRMED â€” `RequiredFarCell` + `FarCellForChunk`.

### H4 â€” a rim cell destroyed while its real chunk re-loads leaves an approach-edge hole â†’ keep-while-queued added
Removal scan would destroy the rim cell as soon as the real chunk stops being "required" â€” but the real
chunk sits in `_pendingChunks` / `_chunksInFlight` for a moment (the near ring re-approaches), so the
shell could vanish RIGHT BEFORE the real chunk materializes â†’ a one-or-two-frame hole facing the player.
FIX: a span-1 cell is never removed while its real coord is queued or in-flight (the real chunk's
finalize then shadows it next poll). VERDICT: CONFIRMED.

### H5 â€” how coarse is coarse enough, and which seams are acceptable â†’ step ladder + T-junction accept
Rim step 3 (matches Lod2, gapless against real Lod2 at â‰¥60 m AND against ring-10 real chunks), band B
3/6/9 by far-ring (â‰¤21/â‰¤27/else), band C 12/15 (â‰¤47/else). Steps divide the tile span 30/90/180 so grid
rows land on chunk boundaries and adjacent same-step cells share their edge lattice. Different-step
neighbors (e.g. step-6 vs step-3, step-12 vs step-15) have T-junction rows along their shared 90/180 m
edge â€” sub-pixel at â‰¥600 m (band B step rises at radius â‰¥ ~660 m, band C at â‰¥ ~1,080 m). Micro-seams
accepted. The initial fill rate (3 finalized Ã— 20 polls = 60 cells/s) â†’ ~1,400 cells â‰ˆ 20-25 s; the
teleport/far-jump re-fill is amortized while the player moves. VERDICT: CONFIRMED (blocking on the
locked `MaxFarFinalizePerPoll = 3` â€” documented, not "fixed"). âš ï¸ **REOPENED by `1ej`**: the "sub-pixel"
assumption was WRONG â€” the user later reported permanent thin lines along every chunk edge in the far
shell, which are exactly these T-junction cracks (the step rises sit at 630/810/1,410/2,100 m, all
clearly visible). FIXED in `1ej` by removing the ladder entirely (one uniform 3 m step).

### H6 â€” worker threads must never read shared state â†’ epoch/seed/maxRing passed by value
`BuildFarSector` computes step from maxRing + span, reads saves via `ChunkSaveManager.TryLoadChunk`
(static/bg-safe, path pre-cached by the existing `Warmup`), and only touches local arrays + statics â€”
consistent with `BuildOrLoadChunk`. `epoch`/`seed`/`maxRing` are captured on the main thread and passed
in the lambda; `_farEpoch` is bumped on a world reset and a stale `_farReady` entry is dropped at
finalize (main thread), where `_farInFlight` is also released. One trap avoided: `_farStale` as a field
would need concurrency care â€” it is a plain per-poll local list instead. VERDICT: CONFIRMED.

### H7 â€” the idle gate must not starve a shrinking far shell â†’ far queues join `working`
Without them, an idle player whose render distance shrank would never destroy the excess sectors (the
gate returns before the removal scan). `_farPending`/`_farInFlight`/`_farReady`/`_farUnloadBacklog`
join the 1ee `working` flag; each poll's scan removes â‰¤24 and clears the backlog when the surplus is
gone, so the extra polls are finite. The `_lastStreamCentre` default of (0,0) chunk would have also
skipped the first FarShellTick at the world origin â†’ sentinel `int.MinValue`. VERDICT: CONFIRMED.

### H8 â€” the committed 1ef "CS0029/CS0022 fix" left an invalid array declarator â†’ REPLACED
`ac5bb2d` renamed `float[,][] grids` to `float[span, span][,]` in `BuildFarSector` â€” but C# only
allows sizes inside the `new` expression; a declaration type carrying `[span, span]` is a compile
error (CS0650/CS0022 class), so Unity would have failed to compile the far shell again. The working
tree corrects it to `float[,][,]` (rank-2 array whose elements are the per-chunk `float[,]` corner
grids), which matches both `grids[cz, cx] = BuildFarChunkCorners(...)` and
`grids[czi, cxi][lx, lz]`. No semantics change â€” same index shape, just a valid declarator.
VERDICT: FIXED by the follow-up commit after `1ef` (PROGRESS Â§1ef-status); re-grepped the file for
any other size-in-declaration typos â€” none.

## 1eg â€” the far view still reads as "not fully loading" â€” slow closest-first fill + horizon wash (FIXED in `1eg`)

User play-test of `1ef`: "the map visual is not fully loading, i can see some loaded from far away but
they're not complete". The 1ef design had explicitly ACCEPTED the slow fill (~20-25 s); the user now
rejects the acceptance. Reopen + re-derive.

### H1 â€” the 1ef fill speed/order makes the horizon finish LAST â†’ CONFIRMED (was an accepted tradeoff)
Evidence: `MaxFarFinalizePerPoll = 3`, `MaxFarInFlight = 12` (WorldStreamer.FarShell.cs:62-64) â†’ â‰ˆ60
cells/poll tie [`~1,400` cells â†’ ~20-25 s] â€” and the ring walk (steps (2)) + dispatch FIFO (step (3))
fill **closest ring first**, so the cells at the visible far band are the very last created. Standing on
the platform looking out, the far view stays patchy for the whole fill; walking lags it further (the
fill is near-first but the player looks outward). Also the leading-edge cells while walking are exactly
the high-ring cells â€” the slowest to appear. VERDICT: CONFIRMED â€” fixed in `1eg` by (a) raising the
budgets (48 in flight / 16 finalize / 32 unloads â†’ ~320-960 meshes/s, whole shell ~1.5-5 s) and (b)
reversing the dispatch iteration so the farthest rings finalize first.

### H2 â€” the horizon tonal lift washes the outer band toward sky â†’ CONFIRMED (primary "not loaded" look)
Evidence: `_HorizonStart 1400`, `_HorizonEnd 2100` with a full `lerp(..., horizonBlend)` â€” since the
shell edge is ~2,070 m and the far plane 2200, every cell beyond 2,100 m is 100% tinted and 1400-2100
ramps to it; against the sky color (0.78,0.83,0.90) the outer ~700 m reads as blank/missing geometry
rather than "atmosphere". Material is runtime-built from shader defaults (GameBootstrap.cs:99
`Shader.Find("NewWorld/TerrainLayered")`), so editing the defaults is effective (grep found no
serialized override). VERDICT: CONFIRMED â€” fixed in `1eg`: band 1600â†’2050 (removes the full-tint
plateau below the shell edge) and Ã—0.6 peak blend.

### H3 â€” a real coverage hole (required cell never generated) â†’ REJECTED (not observed), watch-list
Audited the ring walk â†’ dispatch â†’ finalize path for a permanent gap: every poll rebuilds
`_farPending` from the full ring walk near+1..keep via `RequiredFarCell`; a required cell not yet in
`_farSectors`/`_farInFlight` is re-added, and once dispatched it stays in `_farInFlight` until its
`FarMeshData` is finalized or dropped (stale epoch / no-longer-required). There is no "lost cell" state:
in-flight entries are only removed on finalize or on worker exception (which re-dispatches next poll).
Stale-epoch drops only occur after `ClearFarShell`. So the fill terminates with every required cell
created â€” no permanent holes. VERDICT: REJECTED for the reported symptom (which matched H1+H2).
REOPEN if play-test shows the `far cells` counter freezing well below ~1,400.

### H4 â€” reversing the pending dispatch order could open a hole at the near rim â†’ REJECTED
Dispatching farthest-first delays the span-1 rim cells (rings 10-14). No hole: real chunks keep ONE
hysteresis ring loaded beyond the near ring (ring 10) in the direction of travel, and the rim cell only
needs to be active where its real chunk has UNloaded â€” which requires the focus to move farther, by
which time the shell is fully filled (â‰¤5 s). Near-mid fill follows within the same window. VERDICT:
CONFIRMED SAFE â€” shipped as the reverse iteration in `1eg`.

## 1eh â€” "too lag to play": far-finalize main-thread spike, physics body count, far draw calls (SHIPPED in `1eh`)

User play-test right after `1eg`: lag standing still AND hitches while moving (Editor session). No
profiler access â€” reasoning from code review only.

### H1 â€” 1eg's fill-throughput bump made the main thread lighter? NO â€” heavier per poll â†’ CONFIRMED (main moving hitch)
`1ef` finalized 3 far cells/poll; `1eg` raised it to **16/poll** (+48 in flight). Each finalize =
`CreateFarSector` â†’ `new GameObject` + `MeshFilter` + `MeshRenderer` + `AcquireChunkMesh` +
`UploadMerged` (mesh fill + bounds + upload) on the MAIN thread. At the 20 Hz poll that's up to 320
GameObject/mesh creations per second during the ~1.5-5 s fill AND at the leading edge while walking â€”
a plain per-poll hitch the previous cap had spread thin. Evidence: 1eg shipped 16/poll; the user's
lag report came immediately after 1eg. FIX: keep the cap but add a **per-poll ms budget**
(`FarFinalizeBudgetMs` 2.5 â€” mirrors the real-chunk `AdaptiveBudgetMs`), stop mid-fill when exceeded.
Fill slows a little (~3-8 s â€” still ~3-5Ã— faster than 1ef's 20-25 s); hitches bounded. VERDICT:
CONFIRMED â€” shipped in `1eh`.

### H2 â€” standing-still Editor lag is physics bodies + draw calls (trees, chunks, far cells) â†’ PARTIAL
Physics: ~289 chunk MeshColliders (`ColliderRingRadius` 8) + per-branch tree BoxColliders (user chose
"keep as-is", so untouched) = every `CharacterController.Move`/interaction ray sweeps a large
broadphase. Draw calls: ~1,400 far cells + 361 chunks + ~2k prop cubes + ~60 magic-pedestal TMP
labels (test-platform QA lane on by default). Levers taken: collider ring **8â†’7** (289â†’225 â€” under
near ring 9 so no collider-on-shell regression), magic models **default off**, livestock CCD
ContinuousDynamicâ†’Discrete. VERDICT: CONFIRMED for the parts acted on; tree colliders remain per user
choice (reopen if the profile still shows Physics dominating).

### H3 â€” static-batch the far shell: ~1,400 renderers â†’ few sub-meshes â†’ CONFIRMED, with a hard constraint
`StaticBatchingUtility.Combine` can be called ONCE per GameObject (no re-bake). So the design bakes
only after the shell is fully settled (`_farIdlePolls â‰¥ 20`), bakes only **span-3/6** cells (span-1
excluded: they own the active shadow â€” a baked rim would keep drawing under a loaded real chunk â†’
z-fight), and **retains baked cells on shrink** (removal scan skips `_farBakedCells`) â€” they persist
past keep until `ClearFarShell` wipes the batch. Two sub-risks checked: (a) if Combine shares the
combined mesh into each child's `MeshFilter`, `DestroyFarSector`'s pooled-mesh release would corrupt
the chunk-mesh pool â†’ `ClearFarShell` destroys baked cells WITHOUT pool release; (b) a too-big mesa
splits automatically at 65k verts (Unity internal), so ~1.1M verts â†’ ~17 sub-meshes, still a massive
draw-call win. VERDICT: CONFIRMED â€” shipped in `1eh` (TryBakeFarShell); watch-list: the one-off bake
spike occurs once while idle in the Editor; baked cells persist on radius shrink until a world reset.

### H4 â€” livestock CCD cost â†’ CONFIRMED (cheap win)
7 livestock using `ContinuousDynamic` (most expensive CCD mode) + interpolation. Animals wander slowly
near the player; Discrete suffices, interpolation retained. VERDICT: CONFIRMED â€” shipped.

### H5 â€” far-shell idle gate could still cost per poll â†’ REJECTED
`FarShellTick` fully closes the idle gate once `_farPending/_farInFlight/_farReady/_farUnloadBacklog`
are clean (verified re-read), so a settled shell (even baked) costs ~zero per frame in the streamer.
VERDICT: REJECTED â€” no change needed beyond the bake gating.

### H6 â€” the static-baked far shell renders only from below â†’ CONFIRMED by play-test; bake DISABLED, root cause OPEN
User play-test of the `1eh` commit (bake shipped as H3): "the visuals generated beside the chunks around
the player can only be seen from the surface under" â€” confirmed via clarifying question that the WHOLE
far shell ~250 m+ (i.e. the baked span-3/6 batch, which is every far cell outside the unbaked rim band)
vanished from above once the shell settled ~1-2 s, and showed only from underneath. Before that, the
identical dynamic per-cell meshes (1ef/1eg) rendered correctly from above for the same user, and the
winding of `BuildFarSector` is byte-identical to the real-chunk LOD meshes (both CW-from-below) â€” so the
geometry itself is verifiably fine and the delta is `StaticBatchingUtility.Combine` itself (the only
rendering change in `1eh`; finalize budget / collider ring / pedestals / CCD can't flip winding).
Root-cause candidates considered (NOT concluded â€” no Unity access): combined-mesh winding/bounds baked
from the child world matrices, UV/vertex-color channel reconciliation (our far meshes are UV-less +
color-rich), or the `TerrainLayered` shader interacting with static-batch lightmap channels. None is
provable by grep. MITIGATION (shipped): a `FarBakeEnabled = false` switch â€” `TryBakeFarShell` and the
(5) settle-hook are gated, so every far cell renders dynamic again (exactly the 1ef/1eg state). H3's
watch-list items (retention on shrink, bake spike) are moot while disabled. VERDICT: H3's bake =
CONFIRMED-BROKEN at runtime, REOPENED for root cause. EXPERIMENT to run in Unity before re-enabling:
flip `FarBakeEnabled` to true, then (a) read the Console for a `[WorldStreamer] Far shell static bake
failed` warning (our try/catch swallows the real cause â€” temporarily log the inner exception + stack),
(b) if no warning, select `FarBaked` and inspect one child's `MeshFilter.mesh` winding/normal direction
in Scene view, (c) sanity-test a MINIMAL repro: Combine two sample planes with and without vertex colors
on the same shader.

## 1ei â€” bake-disable did NOT clear "only visible from below": the far shell still hides its upper face (FIX in `1ei`)

The Â§1eh H6 fix (bake disable) shipped, but the next play-test reported the far chunks beyond the real
ring STILL "can only see from the surface below, can't see from the upper face", plus move-time loading
"too laggy". Reasoning trail:

### H1 â€” the far-cell mesh geometry (winding/heights/colors) is wrong â†’ REJECTED by direct re-read
`BuildFarSector` (FarShell.cs:462-476) builds triangles with the SAME index pattern as the real-chunk
LOD meshes in `BuildLodChild` (ChunkObject.cs:294-305): `i00,i10,i11 / i00,i11,i01`, row-major +X next
column, +Z next row â€” byte-identical. Both share `GroundMaterial` (Cull Back). Since real LOD meshes
render from above (the player stands on them), the far cells' own faces cannot be mis-wound. Heights
(GetHeight on world coords) and band colors (TerrainBandColor + memo, FarShell.cs:454 vs
ChunkMeshGenerator.cs:372) match the real path. VERDICT: REJECTED â€” geometry is fine.

### H2 â€” the missing upper face is a winding/culling RENDERING artifact (pooled-mesh or material side); bake was only one trigger â†’ CONFIRMED as the actionable model
The symptom's only structural explanations left are per-mesh culling (spuriously culled faces) or a
stale Editor compile of the disabled bake. Both are insensitive to the mesh's own winding. VERDICT:
partially confirmed â€” instead of chasing the exact mechanism in the dark, make the far terrain immune:
render far cells double-sided (Cull Off material variant) so their top surfaces draw regardless of
winding/culling. Shipped: `_Cull` shader property (all 3 passes `Cull [_Cull]`), a `FarGroundMaterial`
(Cull Off) created in GameBootstrap next to GroundMaterial, consumed by CreateFarSector with a
`?? GroundMaterial` fallback. Real chunks keep Cull Back (their upper faces provably render). If the far
cells STILL hide on the next play-test, that proves the artifact is NOT face-culling â€” next check is a
paused-play Hierarchy inspection of a `FarCell_*` renderer (enabled? cull value? bounds?) per PROGRESS
Â§1ei-playtest.

### H3 â€” "too laggy when chunk loading while moving" â†’ the 1ei levers (CONFIRMED cheap)
The stream is individually budgeted but bursts in one 20 Hz poll: â‰¤12 chunk finalizes (~6 ms adaptive) +
â‰¤16 far finalizes (2.5 ms) + â‰¤120 prop tiles (3 ms) + **â‰¤4 synchronous MeshCollider cooks (~1-3 ms each,
least-budgeted / worst single spike)**. Two low-risk cuts shipped: (a) far cells `ShadowCastingMode.Off`
â€” ~1,400 far cells were drawing into the sun's shadow map every frame via the shader's ShadowCaster pass
(no gameplay value beyond the real ring; big per-frame relief in the Editor); (b) `MaxColliderCooksPerPoll`
4 â†’ 2 so ring-crossing cooks spread one extra poll (already resumable via `_collidersDirty`). VERDICT:
CONFIRMED as taken; further spread (staggering Finalize/FarShell/props across separate polls) is a bigger
change and stays out of scope unless the next play-test still hitches.

## 1ej â€” permanent "thin lines along every chunk edge" in the far shell = the step ladder's T-junction cracks (FIXED in `1ej`)

After 1ei shipped, the play-test came back sharper than before: the far ground beyond the real ring has
gaps between itself and the loaded chunks around the player, AND cells further out show "gaps with every
chunks". Follow-up clarifying answers: (a) the gaps are **permanent** â€” standing still 10-15 s never
fills them; (b) they look like **thin lines/cracks running along every 30 m chunk-edge**. Those two facts
rule out the fill-latency theories (1eg horizon-first was about transient banding) and point at a
systematic per-boundary geometry mismatch. Reasoning trail:

### H1 â€” same-step sibling far cells are watertight â†’ CONFIRMED (the baseline, not the bug)
`BuildFarSector` samples heights from one `BuildFarChunkCorners` grid per covered chunk
(FarShell.cs:414-419); `SampleHeight` clamps a boundary tile to `min(t/cs, span-1)` with
`lx = t - cxi*cs`. A shared-edge vertex between two sibling cells lands at the same world coordinate in
BOTH cells' boundary-chunk grids (row `gx=0` on one side, `gx=30` on the other), and both
`BuildFarChunkCorners` (FarShell.cs:544-556) map slot â†’ world identically (`(tc.X*cs+gx)*ChunkData.Size`)
â€” byte-identical to the real chunk builder (ChunkBuild.cs:93-95), same NaN seed + save stamping. Two
independent samples of the same deterministic function at the same argument â†’ **same height â†’ coincident
row â†’ no seam**. Also the far corner grid is world-anchored, so "the row is offset by 1" style bugs are
ruled out by the identical slotâ†’world arithmetic. VERDICT: CONFIRMED â€” siblings sharing a step are not
the source.

### H2 â€” adjacent cells with DIFFERENT steps V-crack along their shared row â†’ CONFIRMED as the report
`FarSectorStep` (pre-1ej) chose each cell's step from ITS OWN maxRing: 3/6/9 for span-3 (rises at rings
21/27), 12/15 for span-6 (rise at 47). Two cells that touch across a step-rise ring boundary use
different lattices along their shared 30 m-aligned row: the finer cell puts vertices every 3 m, the
coarser every 6/9/12 m. The coarse cell's edge is a straight chord between its sparse vertices while the
fine cell's edge kinks at every intermediate vertex â€” along a noisy terrain the heights at the fine
vertices sit off the coarse chord, so the two edge polylines diverge â†’ an open **T-junction V-crack**
along the ENTIRE shared row. These cracks are permanent (geometry, not fill), sit on the world-aligned
30 m lattice, and ring outward at the step-rise radii (~630/810/1,410/2,100 m) â€” matching "permanent
thin lines along every chunk edge" and the "spaces in the ring around the player" (the first rise,
~540-630 m, is just beyond the real ring's 270 m). Note Â§1ef H5 accepted these as "sub-pixel at â‰¥600 m"
â€” REOPENED and shown WRONG; the fine edge diverges by up to the terrain's per-3 m slope Ã— step, easily
pixel-width at any distance. VERDICT: CONFIRMED â€” this is the bug.

### H3 â€” fix by making EVERY cell share one uniform lattice â†’ CONFIRMED, shipped
Any two different steps on a shared row V-crack, so no ladder tuning (e.g. only using divisible steps)
can guarantee watertightness â€” the coarse chord never respects the fine cell's intermediate detail.
The bulletproof fix is ONE world-aligned 3 m step for every span/ring: every adjacent pair becomes a
same-step pair, which H1 proves watertight. Cost check: uniform step 3 â†’ span-6 cells go from 16/13 to
61Ã—61 = 3,721 verts; total far shell â‰ˆ 2.2 M verts vs the previous ~1.4 M â€” negligible next to the ~1,400
draw calls that actually dominate (and strictly cheaper memory-agnostic than a batching win). Kept the
`span/maxRing` parameters on `FarSectorStep` for call-site stability though the body returns 3. All other
behavior (budgets, horizon-first dispatch, active-shadow ring, bake disabled) untouched. VERDICT:
shipped in `1ej` â€” see the section header; remaining risk: if thin lines STILL show after this, the
source cannot be the far shell's internal seams (geometry provably watertight) and the next check is
per-chunk save/noise divergence between neighboring `BuildFarChunkCorners` grids â€” not the step.

## 1ek â€” "loads as a square ring, the inner stays empty forever" = dispatch-order starvation, not geometry (FIXED in `1ek`)

Play-test after 1ej: "the ring is bigger but still have spaces, and the chunk beyond the ring still
split into grid". Clarifying answers flipped the diagnosis completely: (a) the far shell "loads as a
square RING not loading the inner, except the chunks around the player spot"; (b) "some squares stay
empty FOREVER" standing still; (c) the ring = the boundary at ~270 m around the player. That is NOT a
crease and NOT a height mismatch â€” it is **missing cells**: only the outer fringe of the square renders,
the rim/near-and-mid shell never appears for good. Reasoning trail:

### H1 â€” geometry still watertight; seams/normals are NOT the "empty interior" â†’ CONFIRMED, then set aside
Re-read `SampleHeight` (FarShell.cs:424-433) + the chunk-grid slotâ†’world math again: a shared
edge vertex lands on the same world cell in both neighbors (the `min(t/cs, span-1)` clamp keeps
`lx = cs` on the far side), and slotâ†’world is byte-identical to the real builder â€” same deterministic
GetHeight â‡’ coincident rows. Pool/mesh paths exonerated by direct read too: `AcquireChunkMesh` falls
back to `new Mesh()` (can never block/exhaust), `UploadMerged` `Clear()`s on size change (safe reuse).
The far-cell edge normals ARE a real artifact (one-sided clamped difference â†’ a crease line along every
shared row, "split into grid") â€” but that explains LINES, not MISSING CELLS. The "empty interior" +
"permanent" facts force a generation-side cause. VERDICT: normals kept as a secondary fix; the missing
cells come from H2.

### H2 â€” horizon-first dispatch starves the near cells â†’ CONFIRMED as the report
`FarShellTick` walks rings into `_farPending` CLOSEST-first, but dispatch iterates it in
REVERSE so the farthest cells grab the first 48 flight slots every poll. 1ej's uniform 3 m step
turned the outer span-6 cells into the heaviest objects in the build: each needs 36 chunk corner grids
(~34.6k noise samples) + 3,721 vertices, so the 48 slots churn on the slow outer fringe while the near
(rim/span-3) cells at the FRONT of the list wait â€” under movement and in the Editor they wait forever â†’
the exact "square outer ring + empty interior + permanent empty squares next to the player". All earlier
"not fully loading" complaints are the same mechanism; 1ej's weight increase made it the dominant
symptom. VERDICT: CONFIRMED â€” a dispatch-ORDER bug, not geometry/retention (the requirement predicate
is provably symmetric between generation and retention).

### H3 â€” fix: near-first dispatch + more in-flight slots â†’ CONFIRMED, shipped
Flip dispatch to iterate `_farPending` FORWARD (closest-first), and raise `MaxFarInFlight` 48 â†’ 96.
Near cells (rim = 11Ã—11, fast) then finalize within one poll, the interior closes immediately, and the
distant fringe fills as slots free â€” a transient far-edge gap during fresh load/teleport is far less
visible than a permanent void around the player. Also confirmed cheap: finalize remains 2.5 ms/16 cap
(no main-thread change), 96 slots only parallelize worker builds.

### H4 â€” kill the residual far-cell crease with cross-seam normals â†’ CONFIRMED, shipped as part of 1ek
For seam rows/cols (`gx == 0/axis-1`, `gz == 0/axis-1`) the missing slope side is now sampled from
`TerrainNoiseGenerator.GetHeight(seed, wx Â± step, wz)` â€” pure world function, matching exactly what the
neighbor cell's grid yields (no save mods can exist beyond ring 10), so both cells compute
byte-identical edge normals. Interiors keep the grid read (no perf cost; only the boundary rows/cols
take the â‰¤2 extra noise calls). This removes the lighting crease at far-cell boundaries AND at the
rimâ†”real junction, addressing the earlier "split into grid / thin lines at every chunk edge" words that
survived the 1ej uniform-step fix because they were never geometric.

VERDICT (1ek): near-first + in-flight 96 + cross-seam normals shipped in the section header. Remaining
risk: if a void STILL reads as "interior empty" after near-first, the next suspects are (a) the 2.5 ms
finalize budget in the Editor starving creation under load, or (b) a specific `FarCellForChunk` mapping
gap for boundary-adjacent cells â€” both further re-checks, not the fixed order starve.

## 1el â€” "the chunk ring has an offset of 300 x/z, there is no chunks loaded in there" = the far-shell cells were placed + sampled 3x/6x too far (FIXED in `1el`)

Play-test of 1ek came back with the far ground STILL not filling beyond a ~300 m ring. This finally
traced to a coordinate-convention bug in the far shell that had been present since `1ef` â€” behind the
"holes / not fully loading / square ring / empty interior" complaints all along. The 1efâ†’1ek fixes
(addressed: fill order, budget, step ladder, dispatch starvation, seam normals) were all real but were
pointing DOWNSTREAM of the same missing band.

### H1 â€” a ring at EXACTLY 300 m (10 chunks) emptied by the rim active-shadow â†” hysteresis interplay â†’ REJECTED, replaced by H2
First reading: ring 10 = 300 m is the near+1 hysteresis ring where real chunks and span-1 far cells
overlap. Walked the active-shadow sync (`active = !_loadedChunks.ContainsKey`) + StreamAround keep
(radius+1): a real chunk at ring 10 shows itself, and the far cell activates the same poll it unloads;
a never-loaded ring-10 far cell renders its mesh directly. No combination of those states leaves ring
10 permanently bare. VERDICT: REJECTED â€” the visible ring at ~300 m is simply the *correctly placed*
rim band (rings 10-14), i.e. the seam between "far shell works (rim)" and "far shell broken (beyond)".

### H2 â€” span-3/6 cells are RENDERED AND SAMPLED at 3x/6x their true block â†’ CONFIRMED (the bug)
`FarCellForChunk`/`RequiredFarCell`/`FarCellRings` all read `FarCell.X/Z` as the block's **min chunk
coordinate** (parents built as `FloorDiv(x,6)*6`, `FloorDiv(x,3)*3`; rim = raw chunk; rings from
minX=cell.X to maxX+Span-1). But `BuildFarSector` sampled `cell.X * span + cx` and
`CreateFarSector` placed the GO at `cell.X * cell.Span * ChunkSize` â€” i.e. the same value used as a
**block index**, one factor of span too big. Because placement AND sampling share the factor, each
cell loads *self-consistent correct terrain where it lands* (heights = pure world function of the
position actually drawn), so nothing looks cracked â€” the shell just paints the right surface at the
wrong distance and leaves the unfilled rings behind:
- span-3 cell owning chunks 15-17 (450 m) â†’ sampled chunks 45-47 + drawn at 1,350 m.
- Visible span-3 strips land at chunks 45/54/63 = 1,350/1,620/1,890 m ("some loaded from far away",
  1eg report); span-6 at ring ~216 = 6.5 km (off-view).
- Band 450 m â†’ ~1,300 m permanently empty â†’ "chunk ring offset, no chunks loaded in there".
VERDICT: CONFIRMED â€” root cause.

### H3 â€” why the rim (span-1) was the only correct band â†’ CONFIRMED, explains every past symptom
Span-1 makes the two conventions coincide (`cell.X * 1`), so rim cells at rings 10-14 always rendered
at their true 300-450 m â€” the persistent "square ring at 300" the user kept describing. The 1ef fill
slowness, 1eg horizon-first, 1ej step ladder (a REAL separate T-junction bug, fixed), and 1ek
starvation were each real but each overlapped the empty mid-band; now that near-first (1ek) fills the
rim instantly, the remaining defect reads plainly. VERDICT: CONFIRMED.

### FIX
All three over-scale sites now use `cell.X/Z` as the block min chunk: `BuildFarSector` grids
`cell.X + cx`, `cellTileOriginX/Z = cell.X/Z * cs`, `CreateFarSector` position
`cell.X/Z * ChunkSize * Size`. Rim cells arithmetically unchanged. Adjacent blocks then abut at
chunk-min boundaries and siblings share the uniform 3 m lattice world-anchor â†’ the Â§1ej watertightness
argument (identical world-anchored corner grids) now actually applies to the WHOLE shell. Re-derived:
span-3 block 15 â†’ 450 m covering chunks 15-17; block 18 â†’ 540 m (abuts); span-6 block 36 â†’ 1,080 m
covering 36-41; band suppression unchanged (span-3 â‰¤ ring 35, span-6 â‰¥ 36).
VERDICT: FIXED in `1el`.

## 1em â€” "within 300 from spawn there were no chunk spawn beside the one closest to player" = real-ring DISPATCH starvation: finalized chunks were re-dispatched forever, locking the ring at a ~24-chunk bubble (FIXED in `1em`)

After 1el the far shell fills 300 mâ†’2 km, so the immediate question was why the real near ring
(rings 0-9, the ONLY geometry inside 300 m â€” far cells start at near+1 = ring 10) was still empty
except around the player's feet.

### H1 â€” real chunks are generated but hidden by the LOD/cull system â†’ REJECTED
`ChunkLodManager` is the only system that toggles real-chunk root `SetActive`/renderers
(`SyncChunkRegistration` registers real chunks; far cells are static and never registered). Traced
`ApplyBand`: `Details` is populated at `RegisterChunk` time by scanning already-existing children;
LOD children ("Lod1"/"Lod2") are created LAZILY by `RefreshLodMeshes` only on the first band apply â€”
so `TryGetValue` always fails and the fallback keeps `rootMr.enabled = true`. Cull distance
`EffectiveCullDistance = max(120, (radius+1)*30)` = 2,040 m. So a loaded chunk can NEVER be made
invisible. VERDICT: REJECTED â€” if the chunks existed, they would render.

### H2 â€” background generation throws on worker threads (sync chunk survives) â†’ REJECTED
`BackgroundGenerateChunk` catches + retries, logging warnings; the user reported NO warnings and the
far shell (same ThreadPool + same noise path) built hundreds of cells. VERDICT: REJECTED â€” generation
is healthy; only the real-chunk DISPATCH lacks a guard the far shell has.

### H3 â€” real chunks are never GENERATED because the near ring never fills (dispatch loop) â†’ CONFIRMED (the bug)
In `DispatchPending` (WorldStreamer.Streaming.cs:116-144, logic dating to the 90afbbb merge-era):
- the dispatch loop checks ONLY `_chunksInFlight.ContainsKey(tc)` â€” no `_loadedChunks` guard;
- the trailing cleanup removes an entry only when `_pendingChunks.Contains(c) && _loadedChunks.ContainsKey(c)`;
- but `FinalizeChunks` removes the pending mark at finalize, so a finalized chunk is `loaded âˆ§ Â¬pending`
  â†’ the cleanup can NEVER remove it â†’ it stays in `_chunkDispatchOrder` and is re-dispatched (full
  regeneration) every poll.

Why that starves the ring rather than just wasting work: dispatch is nearest-first
(`_chunkDispatchOrder.Sort` by distance each poll) and the loop stops once `_chunksInFlight.Count ==
MaxInFlight` (24). The 24 nearest chunks are permanently in flight; whenever finalize frees k slots,
the next poll's scan refills them from the FRONT of the sorted order â€” the same just-finalized nearest
chunks (which re-generate and are then discarded by `FinalizeChunks`'s already-loaded `continue`).
The scan never reaches index â‰¥24 while slots remain, so the outer rings NEVER dispatch â†’ a permanent
~24-chunk bubble that follows the player. `_pendingChunks` is effectively vestigial in this loop.
The idle gate never idle-s either (`_chunkDispatchOrder` never drains), so the churn also burned CPU
every poll â€” silent, matching "no warnings". VERDICT: CONFIRMED â€” root cause; fixes the whole 1ef-1ek
"holes / square ring / not fully loaded" family, which were always the real ring, masked by (1ef+) now-working far shell.

### H4 â€” why the far shell never showed this (and what to check if a stall persists after the fix) â†’ CONFIRMED (asymmetry)
`FarConsiderCell` skips `if (_farSectors.ContainsKey(cell)) return;` (FarShell.cs:380) â€” completed
cells are excluded from the pending walk, so far dispatch naturally advances. That exact missing
guard in the real-chunk loop is the bug. Residual risk if `chunks` still stalls below ~360 after
`1em`: the finalize time-budget (`AdaptiveBudgetMs`, ~6 ms base, shrinks on slow editor frames) pacing
generation-not-yet-finalized in the first seconds â€” a pacing check, not a re-block. VERDICT: OPEN follow-up (play-test).

### FIX
`DispatchPending`: skip `_loadedChunks` in the dispatch loop; trailing cleanup drops ALL loaded chunks
from `_chunkDispatchOrder` (+ pending marks). Ring fills nearest-first to completion, then the list
drains to zero and the stream idles. VERDICT: FIXED in `1em`.

## 1en â€” "tree and stone generation range does not match the chunk generation range" = prop ring had no runtime floor (serialized 4 left a 120â†’300 m prop-less band) AND the spawn budget was stale for the 1dm density (FIXED in `1en`)

Two independent causes; the first charmed me for a while because the source default looked fine.

### H1 â€” `PropRingRadius` default 4 is just a tuning choice the user dislikes â†’ REJECTED (symptom of H2)
The field sits at 4 with a comment about popping, but nothing assigns it at runtime (grep found no
`PropRingRadius =` besides the declaration) and Unity serialized it in the scene â€” so even raising the
C# default would NOT have changed the game. The effective ring was whatever the scene stored (4),
baked independently of `NearRingRadius` (9) and `RenderDistance`. Two independent serialized values
with no derivation = the exact "matches?" bug class. VERDICT: REJECTED as the root cause; it is the
*manifestation* of H2 (no derivation).

### H2 â€” props should be a DERIVED ring (floor = real chunk stream), and they weren't â†’ CONFIRMED (the range bug)
Re-read the real stream: chunks 0..`near` stream as real geometry, PLUS a keep ring `near+1` loaded
for hysteresis (ring 10 still a real mesh until the far active-shadow takes over). The far shell has
no props by design (`no props, no deformation` â€” FarShell.cs; and you can't dig them). So "props
match chunks" = props on every chunk that renders real geometry = rings 0..`near+1`. The fix is a
FLOOR, not a value swap: `ring = max(PropRingRadius, near + 1)` so an inspector value can still widen
the prop-only fringe but can never re-create the trailing mismatch, and scene-serialization is
irrelevant. VERDICT: CONFIRMED â€” fixed in `1en`.

### H3 â€” the ~2-minute prop fill is a separate pace bug: the tile budget was sized for the OLD density â†’ CONFIRMED (the pace bug)
With `PropTilesPerTick = 120` at 20 Hz the scan moves 2,400 tiles/s â‰ˆ 2.67 chunks/s â†’ a 441-chunk
ring â‰ˆ 135 s. But that budget predates `1dm`'s 5x density cut (1/200 â†’ 1/1000): each tile costs two
Random.Next (~nanoseconds) and a spawn fires every ~1000 rolls, so 120 tiles expected ~0.24 props â€”
the budget was throughput-bound only in the old 1/200 regime. Scaling the scan 15x (1800 tiles)
keeps expected spawns at ~3.6/tick â‰ˆ ~1 ms of `BuildTree`/`BuildStone` cube allocation under
`PropBudgetMs` 3. Fill = 441 Ã— 900 / 1800 ticks = 220 ticks â‰ˆ 11 s â€” the "props trail the terrain by
a few seconds" the docs always promised. VERDICT: CONFIRMED â€” fixed in `1en`.

### H4 â€” prop colliders must follow the terrain collider ring (Part 4) â†’ REJECTED for now
Tied-to-terrain-colliders would make ring 8-10 props un-choppable precisely in the new range the user
asked for, and the 1/1000 density caps the added bodies at ~5-8k (~1% of the 450k the 1di note quotes)
â€” physics is not the binding constraint here. Keeping colliders everywhere props render also reuses
the untouched 1du keep-alive path. VERDICT: REJECTED (deliberately skipped; re-open only if the body
count ever shows up in the profiler).

## 1eo â€” "reduce the max range of terrain loaded" + "change the generate statistic so the tile that went up has a higher chance to generate the same tile as next to it" (FIXED in `1eo`)

Two asks in one ticket: (a) cut the loaded terrain down from 67 chunks, and (b) make the height
noise COHERENT â€” a raised tile should sit among raised tiles, not next to a flat one. The user pinned
the details via clarifying questions: **radius 30 (900 m)** and **noise rebalance only** (no
deformation-blend/representation change).

### H1 â€” the live range is `GameBootstrap` only; scene/asset overrides were a red herring â†’ CONFIRMED (range)
Grep for every `Radius =` / `MaxRadius =` / `RenderDistance` reference: only `GameBootstrap.cs` assigns
(67/160), `RenderDistanceController` holds class defaults (Radius 30, MaxRadius 160, `[Range(1, 160)]`),
and no `.unity`/`.asset` file mentions either. So the boot default and the "settings slider ceiling"
both trace to these two files. The user picked 30 as the max â€” so the honest move is `Radius = MaxRadius
= 30` in the bootstrap AND the class default/attribute, so no future scene asset can resurrect 160.
VERDICT: CONFIRMED â€” fixed in `1eo`. LOD cull (ChunkLodManager, `(radius+1)*30` â‰ˆ 930 m) and near ring
(9/keep 10/prop floor `near+1`) all derive from the same `Radius`, hence untouched. Far shell cells at
radius 30: rings 10..32 â‰ˆ ~900-1,000 vs ~1,400 at 67; span-6 (ring â‰¥ 36) is now unreachable code-path-
wise and stays documented as high-radius-only.

### H2 â€” the "one tile up, one tile flat" speckle came from the high-frequency Perlin octaves â†’ CONFIRMED (correlation)
`TerrainNoiseGenerator.DefaultLayers` (code-only static array; the far shell, real chunks and saves all
read heights through the same `GetHeight` corner grid). Detail (0.02 / 5.0) and Roughness (0.08 / 1.5)
run at wavelengths under a 30 m chunk â€” a neighbor vertex is dominated by independent Â±1.5-5 m noise,
so an elevated tile need not lift its neighbor. That reads as checkerboard sub-chunk bumps and makes
deformation edits sit on jagged ground. H2 is only "confirmed" per-mechanism (I cannot run Unity); the
numeric re-weight is judgment: mass into long wavelengths (Continental 0.0012/55, Hills 0.004/22) while
cutting the per-1 m slope ~7x (Detail 0.012/3.5, Roughness 0.03/0.6, PivotAngle 0.008/1.5). Same 5
octaves, same seed derivations, so saves/spells still bind to the same mechanism; amplitudes sum to
Â±82.6 m max (was Â±63.5) â€” inside the Â±200 m `IsSaneHeight` band, so no sanitization consequence.
VERDICT: CONFIRMED (mechanism) â€” fixed in `1eo`.

### H3 â€” deformation needs a "blend/deform-stat" change too â†’ REJECTED
The user's wording could be read as "make deformation output correlate" (a spell raising one tile should
pull neighbors up). The clarifying question resolved it to **noise rebalance only** â€” deformation stays
exact per-tile (that's its point: precise craters/pillars/roads). Mixing a correlation term into
`DeformHeights` would soften edits and conflict with the 1dc/1dd edit semantics. VERDICT: REJECTED by
user choice; can revisit as a separate magic-design task.

### H4 â€” shrinking the radius breaks the camera far plane / LOD / shadow expectations â†’ REJECTED (no change needed)
Far plane 2200 m was sized for 2 km; at 900 m it just clears the shell with margin. LOD cull is derived.
The horizon tonal lift (1600-2050 m) simply never engages now. All three degrade gracefully; only the
DOCS described the old numbers. VERDICT: REJECTED as code work â€” comments + `game-design.md` updated in
`1eo` only.

### Verification (rule 3 â€” no build)
- Grep sweep for `67` / `160` / `2 km` / `2,070` / `1,400` / `Â±63.5` / `span-6` across `Assets\Scripts`
  and `game-design.md`: live code clean; every remaining hit is an explicit "was ..." history marker.
- `DefaultLayers` referenced only via `TerrainNoiseGenerator.GetHeight` (grep) â€” one shared feed.
- No scene `.asset`/`.unity` touchpoints for radius or layers (grep).
- Play-test needs **`ResetTerrainSaves` once** (world look shifts; saved edits persist), then verify
  the 900 m horizon, coherent rolling terrain, no far-edge crack, unchanged near ring/perf.

## 1ep â€” "the faces that make up the player body will be different in sizes but in the final still cover all the area" = NON-UNIFORM facet lattice, still watertight (FIXED in `1ep`)

The ask, unpacked: the low-poly mosaic should look hand-cut â€” panels of DIFFERENT sizes â€” while the
surface still closes completely (no holes). The current player parts read orderly because the
`Rings 7 Ã— Segs 12` lattice uses fixed 30Â°Ã—30Â° patches everywhere; the 1dx jitter (0.03) and the 35%
triangle split only break up the interior of that fixed grid. Clarifying questions pinned it: **all
parts** (ellipsoid, torso, cylinder, hair/eyes) and **subtle Â±20%**.

### H1 â€” the right lever is the parametric LATTICE spacing (not more jitter, not triangulation) â†’ CONFIRMED
More jitter just wobbles the existing same-size cells without changing their size; splitting cell
counts (adaptive subdivision) changes sizes but touches adjacency bookkeeping and can crack. The
minimal, watertight-safe change is to keep the exact same topology and re-emit the corner grid from
NON-UNIFORM angular/height steps: every interior corner stays one shared position (nothing to
re-sew), so the surface still covers its area exactly; only the cell sizes change. Two schedules:
phi (band heights, poles fixed at 0/Ï€) and theta (segment widths; ONE schedule shared by all rows so
columns stay in vertical planes â†’ quads remain near-planar, flat normals stay valid). VERDICT:
CONFIRMED â€” the 1ep implementation.

### H2 â€” torso rows can also go non-uniform WITHOUT breaking the silhouette/pivot contract â†’ CONFIRMED (endpoints preserved)
`BuildTorso` places rows at uniform `t` and reads W/D via `Silhouette` (piecewise-linear over the
control points). Moving the interior rows onto the Â±20% `t` schedule with `t[0]=0`, `t[bands]=1`
fixed keeps the hip row + crown disc exactly where the neck/pivot/dome docs expect them; the moved
interior rows just sample the piecewise-linear silhouette at new heights, so the shape is
cosmetically equivalent at finer grain. VERDICT: CONFIRMED â€” endpoints fixed in code, caps/band loop
byte-compatible.

### H3 â€” the seam-caps and part-overlap joints break â†’ REJECTED
Checked the joints that could show cracks: the hair parts sit OVER the skull (overlap, not joined
edges), the neck cylinder TUCKS UNDER the crown disc (overlap), the eyeballs seat into dent sockets â€”
none of them rely on matching edges between two different meshes; each part is a closed watertight
surface on its own, and the spacing change keeps every part closed. The only "seams" are within a
part, exactly where the shared-corner lattice already guarantees adjacency. VERDICT: REJECTED (no
risk found by reread; play-test will confirm visually).

### H4 â€” texture mapping: uniform UVs would stretch â†’ handled
Parts are solid color today, but `CornerUV` used the uniform `s/Segs, lat/Rings` fractions, which
would no longer match the actual panel sizes once a texture gets added. UVs now come from the same
schedule arrays (`Î¸/2Ï€`, `1âˆ’Ï†/Ï€`), so texel density tracks panel size. No visible change now;
avoids a future texture-stretch bug. VERDICT: handled in-pass.

### Verification (rule 3 â€” no build)
- Grep: all 15 `CornerUV(` call sites pass the schedule arrays; no `lat * Mathf.PI / Rings` or
  `s * (2f * Mathf.PI) / Segs` survive; new salts 0x1E0F01/0x1E0F02 unique vs existing (0x1234AB,
  0x5678CD, 0x5EEDF, 0xCAFE); only consumer remains `MapBuilder.MakePart` (shared cached mesh), and
  `MakeBlock` (creatures/props) is untouched.
- Re-read `Generate` / `BuildTorso` / `BuildCylinder` end-to-end: arrays sized (uf=Segs, vf=Rings+1
  ellipsoid / rows torso), pole-fan wrap `uf[s % Segs]` matches the old wrap, torso crown index
  `vf[bands]` in range, cylinder caps use the same theta schedule. All deterministic per profile
  (same AnchorSeed + Hash01), cached meshes unchanged in contract.
- Play-test: hand-cut mosaic, no see-through, shoulder dome/crown/hair overlap unchanged, pattern
  stable across plays; strength knobs are `BandIrregularity`/`SegIrregularity`.

## 1eq â€” "when player move, chunks that in the range disappear and rendered right back" = far-shell ownership-swap holes at the span-1â†”span-3 boundary, not the real chunk stream (FIXED in `1eq`)

User play-test of `1ep`: while moving, terrain in range vanishes then comes back. The far shell's
`RequiredFarCell` is a hard Chebyshev ring cut relative to the INTEGER-chunk focus, so every 30 m chunk
the player crosses flips ownership of a whole band: span-1 rim cells promote into span-3 boxes on the
approach side (~rings 13-16) and span-3 boxes demote back into their 9 span-1 children on the trailing
side. Replacements build asynchronously (~50-400 ms). Reasoning trail:

### H1 â€” the blink IS the far-shell ownership swap, both directions â†’ CONFIRMED (the bug)
The committed 1ep removal scan destroyed ANY non-required cell immediately. On a PROMOTE, the span-1
tenant was destroyed the poll its span-3 box became required â€” the box only finished generating
~50-400 ms later â†’ a blank region on the approach side. On a DEMOTE, the span-3 was destroyed the poll
it lost requiredness â€” its span-1 children (newly required) were only enqueued â†’ a blank region on the
trailing side. Both read as "chunks in range disappear and render right back", and they cascade for the
whole swap band every 30 m step because the ring-cut is absolute (no hysteresis in the far predicate).
The real-chunk stream was ruled out by re-read first: hysteresis keep ring (near+1), the 1em
loaded-guard on dispatch, the ring-10 span-1 real-chunk-pending keep and the active-shadow sync all
prevent real-chunk destruction/reload churn â€” the blink zone (~400-480 m) is beyond every real-chunk
guard anyway. VERDICT: CONFIRMED â€” H1 is the far shell.

### H2 â€” the 1eq demote branch had a missing tenant-retain â†’ CONFIRMED (found during the fix audit)
In-progress 1eq code retained the fine tenant on promote (good) and had `CompleteFarHandoff` for the
demote â€” but the removal scan's demote branch was `if (cell.Span >= 3 && FarCoverageReady(...)) {
handoff; continue; }` with NO `else continue`: a demoted span-3 whose replacements were still building
fell THROUGH to `stale.Add(cell)` and was destroyed immediately. The branch's own comment ("until then it
keeps rendering the ground") contradicted its behavior â€” that exact fall-through WAS H1's demote-side
hole, unfixed. FIX: the spanâ‰¥3 branch now (a) retains as tenant while `!FarCoverageReady` AND the
coarser owner is not live, (b) hands off atomically once every replacement is live, (c) plain-destroys
only when covered by a LIVE coarser owner (already hidden â€” region covered by the coarser mesh).
VERDICT: CONFIRMED â€” the code fix.

### H3 â€” a leftover fine/coarse pair can double-draw during a swap â†’ REJECTED by construction
Promote hides siblings the SAME poll the coarse cell is created (`HideFinerChildren`, synchronous inside
`CreateFarSector`, before any frame renders) and destroy follows next poll; the active-shadow sync
re-enforces `FarShadowedByCoarse` every poll as belt-and-braces. Demote births children HIDDEN while the
tenant is still live (reserved shadow); the handoff activates them only AFTER removing the tenant from
`_farSectors` in the same poll, so `FarShadowedByCoarse` turns false exactly then and the children's
activation is consistent. No frame can render two owners. VERDICT: REJECTED â€” no overlap by construction.

### H4 â€” the new tenant-retain `continue` must not over-retain stale cells â†’ CONFIRMED (safe cases)
`FarCoverageReady` is vacuous-true when NO child is required, so a demoted box that slid past keep
handoffs (tenant destroyed, nothing activated â€” nothing should render beyond keep). A cell under a LIVE
coarser owner is already hidden by the active-shadow sync and its region is rendered by the coarser mesh
â†’ plain destroy. A span-1 whose own ring still holds is `RequiredFarCell`-true (kept at the top of the
scan); a span-1 whose chunk turned REAL is either pending/in-flight (kept by the span-1 guard) or
already loaded (hidden + destroy safe). Each was traced so the `continue` only ever retains a cell that
is genuinely the visible tenant. VERDICT: CONFIRMED â€” no retention leak.

Follow-on to the 1ea lag sweep â€” re-read the per-frame paths in `WorldStreamer` / `UIManager` /
`PlayerController` hunting work that burns CPU even when the player stands still on the test platform.
Target: **Unity Editor Play mode** (user's environment), no gameplay change.

### H1 â€” the 0.05 s streaming poll re-ran the ENTIRE pipeline even when nothing changed â†’ CONFIRMED
Pre-1ee `WorldStreamer.Update` (WorldStreamer.cs:198-220) recomputed `StreamAround` (walks the whole
radius ring + `_loadedChunks` dict every poll), `DispatchPending`, `FinalizeChunks`,
`ReconcileCollidersIfChanged`, `SyncPropRing`, `StepChunkProps` on every poll unconditionally. Of these
only Reconcile had change-guards (1ea). Evidence: no movement gate existed in the Update body.
FIX: gate the whole pipeline on (a) focus still in the SAME chunk centre as the last poll, (b)
`_worldDirty` false, (c) no queued / in-flight / ready-to-finalize chunk. `SetFocus` re-arms dirty. Since
a chunk box is 30 m, a moving player still re-arms the pipeline every 30 m of travel â€” streaming cadence
is unchanged, only the standing-still cost dropped to a timer check + a few comparisons.
VERDICT: FIXED by `1ee` (working flag extended with the far-shell queues in `1ef`).

### H2 â€” the HUD clock rebuilt its TMP text every frame â†’ CONFIRMED, root cause a never-matching guard
`UpdateTimeText(day, hour)` guarded on `Mathf.Approximately(hour, _lastTimeHour)`. `hour` is the game
clock advancing every frame and `_lastTimeHour` stored the previous RAW value â€” they can never be
approximately equal, so the guard never bailed and TMP rebuilt + repainted "NgÃ y X - HH.MM" every frame.
The displayed `ToString("00.00")` already rounds to 0.01 h, so the fix quantizes `hour` to that SAME step
before storing + comparing (`Mathf.Round(hour*100f)/100f`) â€” the label now repaints only when the shown
text would change, which is what the guard was supposed to mean.
VERDICT: FIXED by `1ee`.

### H3 â€” the player root still ran raw GetComponent<CombatController>() in several hot paths â†’ CONFIRMED
The 1dr sweep cached Stats/Combat/Passives/Caster/Camera, but later edits added fresh
`GetComponent<CombatController>()` in `PlayerController.Interactions.cs` (cancel-charge, dual-mode,
auto-arm, RMB block, two-hand X toggle, pending-rig, fists), `PlayerController.Combat.cs` (EnsureFists
guard, ReApplyWeaponPose) and `PlayerController.Animation.cs` (model-reload rig capture). All target the
PLAYER ROOT â€” exactly the object `CombatCached` (`PlayerController.cs:41`) already caches â€” so every swap
is semantically identical and shares the once-per-instance lookup. Verified by grep: after the sweep,
`GetComponent<CombatController>` in `Assets\Scripts\Player` has exactly ONE hit (the cache initializer).
`WeaponRigBuilder` / `CharacterInfoUI` / `WeaponDragHandle` / `MagicWheelUI` / skill / AI GetComponent
calls are on OTHER objects (rigged playerRoot param, UI, enemies) â€” deliberately untouched, not the same
cache.
VERDICT: FIXED by `1ee`.

### H4 â€” Tab open/close ran a per-press scene scan for CharacterInfoUI â†’ CONFIRMED, cheap fix
Both Tab consumers (already-open menu â†’ Close at `PlayerController.cs:204`; toggle at
`PlayerController.Interactions.cs:508`) called `Object.FindAnyObjectByType<CharacterInfoUI>()` per press.
New `CharacterInfoRef` property caches the component; a destroyed UI object (Unity `==` overload returns
null) nulls out the cache and re-finds. Grep-verified: only the `CharacterInfoRef` initializer hits remain.
VERDICT: FIXED by `1ee`.

### H5 â€” the perf overlay should be visible without a manual tick â†’ CONFIRMED (QA decision)
`EnableFpsStats` defaulted false since 1ea; the user's workflow is Editor A/B with the overlay. Flipping
the default to true makes the 1ee baseline immediately readable; it is a read-only screen-space overlay so
default-on is harmless (flippable off). VERDICT: FIXED by `1ee`.

## 1er â€” "it is causing lag to render new ground when player moving, cant you use async or smth" (SHIPPED in `1er`)

Context: incoming hot on the tail of `1eq` (which stopped the swap band from BLINKING). Same symptom
area â€” the ~330-480 m far band the player confirmed as "mid-far band" when asked. `1eq` made the rebuild
churn covered, not removed: the demote path still BUILT ~90-135 fresh span-1 cells right at the crossing.

### H1 â€” the generation itself wasn't async, so adding "async" would fix it â†’ REJECTED
The far shell ALREADY generates on the ThreadPool (`BackgroundGenerateFarCell` under
`MaxFarInFlight=96`) â€” same as the real chunks. The main-thread hop is `CreateFarSector` â†’
`AcquireChunkMesh` + `UploadMerged` (`Mesh.SetVertices/Normals/UVs/Colors` + `UploadMeshData(false)`),
which Unity requires on the main thread. So the honest answer to "use async" is: the last hop can't be
moved; the fix has to make the main-thread work a trickle instead of a storm.

### H2 â€” the hitch is the swap band rebuilding on the crossing â†’ CONFIRMED (by construction)
Ring-cut requiredness flips a WHOLE band of boxes each 30 m step. The ring walk (`FarConsiderCell` over
`_farSectors`-missing cells) discovers every suppressed span-1 replacement the same poll the player
crosses: per step that is the leading ring-13-16 arc â‰ˆ ~90-135 cells. Step 3 then fires up to 96
`ThreadPool.QueueUserWorkItem` jobs in ONE poll, and step 4 drains them at 16/2.5 ms for ~6-8 polls,
all at the exact moment the player is moving â€” stacked on the real-chunk finalize, collider cooks, and
prop spawns. That is "lag to render new ground while moving", and 1eq's retention did not touch it.
Verdict: the churn is a burst AT the cut, so pre-compute off the critical path.

### H3 â€” dropping the span hierarchy (all span-1 rim cells to ring 32) would kill the swap entirely, chosen? â†’ REJECTED
Removing span-3 means every chunk ring 10..32 gets its own 11x11 cell â‰ˆ ~3,800 GameObjects/draw calls
vs ~1,000 today (earlier perf passes fought draw-call count at 1,400). Not acceptable at radius 30.
The swap is inherent to having a coarse band; the lever is to make each swap cost ~0 (SetActive).

### H4 â€” pre-warm the swap band ahead of the cut + retain fine shadows â†’ CHOSEN
- **Pre-warm**: a live span-3/6 box whose `maxRing <= FarBandBMin/CMin + FarPrebuildAhead` (2 rings
  ahead of its demote) enqueues its finer children as RESERVED builds every poll until they exist.
  The window is 1-2 ring cuts early â‰ˆ long enough to finish on the ThreadPool, and player speed
  (~1 chunk per 2-6 s) gives far more than the ~50-400 ms per-cell build. On the demote,
  `FarCoverageReady` is true â†’ `CompleteFarHandoff` becomes a SetActive swap in one poll.
- **Reserved flag must ride the payload**: `_farPending` and `_farReserved` are rebuilt every poll, so
  a cell that sits in-flight across a poll boundary would lose its reservation if dispatch re-read the
  set. `FarMeshData.Reserved` is captured at dispatch (closure) and survives to finalize â€” verified
  by following the lifecycle poll N (prewarm) â†’ N+k (finalize create-inactive).
- **Retention**: extend keep-alive to ALL spans (the first-draft restructure only kept
  `cell.Span >= 3` under a live span-6 â€” the span-1-under-span-3 case, the important one, still fell
  to stale and was destroyed; caught on re-read before commit). Retained fine cells are hidden by the
  existing active-shadow sync and reactivated by the demote handoff, so trailing demotes and
  turn-arounds re-use the SAME meshes. Bounded: fine cells under a live box only ever exist inside
  the shell's live-box region (~hundreds), never the distance walked.

### H5 â€” dispatch pacing so the ramp itself doesn't storm â†’ CONFIRMED NEEDED, added
Per-poll caps: 36 on-demand + 12 reserved (`MaxFarDispatchPerPoll`/`MaxFarPrebuildPerPoll`), shared
`MaxFarInFlight` still gates the total. Invariant checked: ring-walk on-demand cells strictly precede
pre-warm reserved cells in `_farPending` (ring walk fills first, pre-warm appends last), so the
`break` on the on-demand cap can't skip reserved work.

### H6 â€” movement-only concern: idle must be unchanged â†’ CONFIRMED
The idle gate skips `FarShellTick` entirely when the focus hasn't crossed a chunk, so pre-warm and the
slightly larger `_farSectors` (retained shadows) cost nothing at rest; step-1a sync iterates the
retained set only while streaming. VERDICT: OPEN â†’ shipped without idle regression; play-test will
confirm the standing-still baseline.

Waste on turn-away: pre-warmed in-flight children of a box the player turns away from finalize inactive,
then the removal scan destroys them as stale â€” small, self-cleaning, bounded by the 2-ring window.
VERDICT: accepted (cheap vs. the crossing burst it removes).

## 1ew â€” smooth terrain: why a steep slope is ONE un-editable stretched face, and how to split it into multiple faces without the blocky voxel look (SHIPPED Phase A `1ew`; editor lattice + v4 saves `1ex`; polish `1ey`) â€” section OPEN until 1ex ships

Context: 1ev restored the smooth default, but that leaves the ORIGINAL `1et` complaint unfixed in
smooth mode â€” natural steep slopes render as one huge stretched quad. User requirement this time:
"NOT a blocky world â€” turn the stretched face into MULTIPLE faces so it can interact normal again".
That explicitly REJECTS the voxel route (1eu's blocky read is what 1ev reverted). Researched the real
pipeline by reading the code, not the (stale) docs.

### Evidence gathered (all confirmed by reread)
- Chunk = 30x30 tiles of 1x1 m, each tile ONE quad from 4 corner heights (`ChunkData.Heights`,
  `BuildMeshData` = 4 verts/6 indices). **`game-design.md` Â§2.2 was STALE** (claimed a 5-vertex +
  center-vertex chunk â€” an older design); fixed in the same pass.
- Editing (`WorldStreamer.Deform.DeformAt`) is a vertical XZ cylinder grabbing **world-corner keys**
  (`EncodeCorner`) â†’ `ApplyHeightEdits` â†’ `RebuildChunkRegion` â†’ `ChunkObject.PatchRegion`. So an
  interior face point has NO representable height in the smooth API â€” only the 4 corners are levers.
  `GetDigDepth` samples the tile's SW corner under the point â†’ meaningless mid-cliff.
- CONCLUSION: splitting faces only helps if (a) the MESH gets interior points and (b) EDITS can move
  those points. (a) ships in 1ew, (b) is 1ex.

### H1 â€” uniform finer grid (every tile â†’ NxN) â†’ REJECTED (user chose adaptive)
Would pay ~16x tri/collider cost across a mostly-flat world, and a finer lattice would have to span
the chunk border ring (shared cross-chunk nodes duplicated in two chunks' saves) for zero benefit on
flat land. User picked model A: split ONLY the over-stretched tiles.

### H2 â€” adaptive stretch-split: delta > threshold â†’ 2x2 sub-quad block â†’ CONFIRMED
`max(corner) - min(corner) > RefineThreshold` (default 2.5 m) â†’ emit 16-vert/8-tri block; else the
coarse 4-vert quad. Flat world pays nothing; a cliff pays 4 faces instead of 1. `0` disables.

### H3 â€” derived (bilinear) interiors in 1ew, STORED fine lattice in 1ex â†’ CONFIRMED (phased)
- Derived: fine heights = bilinear of the 4 coarse corners â†’ deterministic, saves byte-identical
  (no format change in 1ew), pristine re-rolls exact, an edit that moves a coarse corner moves the
  whole refined patch with it.
- CRACK PROOF: a bilinear surface's EDGE is linear in its two edge corners, so a refined tile's
  mid-edge point equals the coarse neighbour's straight-edge point â‡’ zero gaps, and cross-chunk
  shared corner heights are untouched by refinement.
- Stored lattice (v4 save + DeformAt/FlattenAt/GetDigDepth fine writes) deferred to 1ex because it
  changes the save format AND the edit API â€” the phased plan user approved.
- Dead end noted: persisting fine nodes but only for interior sub-quads in 1ew would have forced an
  asymmetric save reader; derived-first keeps 1ew format-neutral (v1 saves keep loading).

### H4 â€” refine across the chunk border ring â†’ DEFERRED (interior-only rule in 1ew)
Refining a border tile would put a fine node ON the shared edge that both chunks must agree on.
1ew restricts refinement to local index 1..28 so the cross-chunk contract is unchanged. Note (checked):
the derived midpoints would ALREADY match across chunks (edge-linearity), so the interior-only rule is
about the future EDIT-ownership of fine nodes, not crack-safety â€” 1ex can lift it later.

### H5 â€” merged mesh fixed stride breaks â†’ block table â†’ CONFIRMED (and one bug caught)
`BuildMergedMeshData` laid tops at fixed `(tileIndex * 4)`; refined tiles are 16 verts â‡’ stride dies.
Fix: `TileVertexBase`/`TileVertexCount` per tile, `PatchRegion` re-skims through the table.
**Caught in reread:** the first draft built the table BEFORE the defensive null-tile fill, so fallback
tiles' triangle indices were undercounted (allocation too small â†’ IndexOutOfRange risk). Moved the
table build to AFTER the fill.

### H6 â€” LOD children break too â†’ 31x31 corner lattice (ChunkCornerGrid) â†’ CONFIRMED
`BuildLodChild` sampled merged verts via `WorldCornerIndex(gx*step, gz*step, cs)` â€” fixed stride again.
Fix: `MergedChunkMeshData.Corners` (31x31 Y/Normals/UV/Colors) whose ownership rule mirrors the
retired `WorldCornerIndex` exactly â‡’ LOD surface bit-identical pre/post 1ew; `PatchRegion` re-stamps
nodes it owns via `PatchCornerGrid`. Value-type trap checked: `ChunkCornerGrid` is a struct copied by
value, but the arrays INSIDE are references â‡’ `PatchCornerGrid`'s writes land in `_merged.Corners`.

### H7 â€” an edit can flip a tile's split state â†’ FULL REBUILD on mismatch â†’ CONFIRMED
`PatchRegion` re-skims but cannot RESIZE a block, so `RebuildChunkRegion` compares fresh
`ChunkMeshGenerator.IsRefined(tile)` vs `obj.IsTileRefined(lx,lz)`; any flip â†’ `FullRebuildChunk`
(which passes `RefineThreshold`). The fallback-tile build path omits the threshold â†’ mismatch â†’ full
rebuild â€” safe by construction.

### H8 â€” walls vs refinement â†’ no conflict â†’ CONFIRMED
`EdgeIsRaised` fires only at whole-metre discontinuities (legacy slabs). Slab tiles are flat (deltaâ‰ˆ0)
â‡’ never refined; refined tiles are steep â‡’ shared corners stay equal â‡’ never raise. Checked against
both wall passes.

### Verification (rule 3: grep + reread, no build)
`RefineThreshold` def + all 3 `BuildMeshData` call sites; `TileVertexBase`/`TileVertexCount` writers/
readers (voxel builders leave null, all readers null-guard); `ChunkCornerGrid` build/patch/LOD paths;
`WorldCornerIndex`/`topVertsPerTile`/`ChunkMeshData.DefaultY` removed with zero remaining references;
stale Â§2.2 doc fixed. Verdicts: H1 rejected; H2/H3a/H5/H6/H7/H8 confirmed; H3b+H4 deferred to `1ex`.
Section stays OPEN pending user play-test + `1ex`.

## 1ev â€” user rejects the voxel-block look: un-default voxel, restore smooth as the default (shipped in `1ev`)

Context: 1eu made `VoxelTerrainEnabled` default ON, so the fresh world renders as 1-metre stepped
columns. User: "the last work has turn the game to somewhat similar to minecraft where terrain are made
of blocks, i dont want that." This is the raw reasoning trail, NOT the shipped summary (PROGRESS Â§1ev /
game-design Â§2.9).

### H1 â€” the blocky look is the 1eu voxel default, not a regression in the smooth mesh â†’ CONFIRMED
Candidate causes: (a) the smooth height-field mesh regressed, (b) a new shader/step artifact, (c) the
voxel mode is simply on. Evidence: the smooth path is untouched and fully gated on one bool;
`WorldStreamer.cs:53` had `VoxelTerrainEnabled = true` since 1eu with a tooltip literally claiming
"the voxel model is now the world's default terrain"; every branch (build/rebuild/flush Streaming.cs,
LOD Mesh.cs, far shell FarShell.cs, Deform.cs, sculpt Voxel.cs) routes on that flag. 1eu H8
("default ON: safe flip or keep experimental OFF?") already flagged exactly this risk. VERDICT:
confirmed â€” the Minecraft-ish look IS the voxel path, on by default.

### H2 â€” reversing the default is one-flag + docs, no code surgery â†’ CONFIRMED
Debated deleting the voxel system entirely vs. reverting the default. Delete = remove VoxelChunkData/
VoxelMesher/WorldStreamer.Voxel.cs, voxel far-shell + LOD branches, the v3 save writer/reader, the
sculpt API and ChunkObject.VoxelStore â€” high blast radius (grep rule 5) for zero gameplay gain. Revert
= `VoxelTerrainEnabled = false`; smooth builder/reader/render are the original 1et path and take over
immediately; the voxel experiment stays reachable via the QA toggle. User picked "Revert default, keep
opt-in (Recommended)". VERDICT: confirmed; shipped as the one-line flip.

### H3 â€” voxel save files under the smooth reader regenerate cleanly â†’ CONFIRMED (documented, not new)
The smooth reader rejects v2/v3 column-run saves (1et limitation), so any chunk persisted in voxel
format re-derives from noise when the world streams in smooth mode. That is intended behavior, not a new
hazard; docs point the play-tester at `EnableResetTerrainSaves`/New Game if a leftover chunk looks off.
VERDICT: confirmed; left to the play-test checklist.

Context: P1 (`1et`) shipped the single-run stepped voxel world. The user asked to "continue until
complete all phase" â€” so P2 (real carve/sculpt into the column data), P3 (voxel LOD + far shell),
P4 (QA lane + voxel-on default + docs) were the plan. This section is the raw reasoning, NOT the
shipped summary (see PROGRESS Â§1eu / game-design Â§2.9).

### H1 â€” P2 needs multi-run columns, but the 4-corner adapter is sacred â†’ CONFIRMED
A dig that punches a cave through a hillside cannot be expressed as one `[ColumnBaseY..Top]` run â€”
the roof stays solid above the void, the floor stays below. So each column becomes a sorted run list
(lists, null = pristine, mirroring P1's "null = untouched" trick so saves stay sparse and pristine
chunks re-roll). The 4-corner `ChunkData` API (tools/spells/save gates) only ever sees the TOP run,
so the whole legacy pipeline is untouched again. VERDICT: confirmed; implemented as `RemoveSolid`/
`AddSolid` boolean ops + `RunsAt`/`VisitColumns`/`SetColumnRuns`.

### H2 â€” a cave = RemoveSolid with a roof shelf â†’ CONFIRMED
Volume carve per column: remove solid `[topFloor .. givenBottom]` but never cut above
`surfaceTop - roofThickness`; `SculptVoxelCave(center, radius, roof, height)` is RemoveSolid over the
sphere's filled range leaving an untouched roof. Raise is the dual AddSolid. Across two adjacent
chunks each column works in its own store; the per-chunk rebuild + flush handles the seam (real
border tops). VERDICT: confirmed; public `SculptVoxelCave`/`SculptVoxelRaise` in WorldStreamer.Voxel.cs.

### H3 â€” surface dig overlay must NOT use SetColumnTop (would collapse caves) â†’ REJECTED mid-implementation
The rebuild/flush overlay (surface edits via the 4-corner DeformAt path) can't replace the whole
column â€” that would destroy a sculpted chamber beneath the new top. Correct: `SetSurfaceTop` diffs
the current TOP run only (shave `[top..old]` / add `[old..top]`), preserving buried runs below.
Verified with the negative `lx` case: dig into a chunk from outside, `lx = wx - tc.X*cs`,
`FromTile` floors negatives (`-1 â†’ 29`), so `lx` lands 0..29 â€” the same miscalculation traps every
older "dig near the border" bug. VERDICT: SetSurfaceTop implemented, SetColumnTop kept for the
v1/v2-migration load path only.

### H4 â€” directed dig = clip the crater influence sphere to the cast half-space â†’ CONFIRMED
A shovel "dig forward" must scoop the slope in front, not a full sphere under the player's feet.
Crater branch in voxel mode: `influence *= Clamp01(along / max(0.25, radius*0.5) + 0.15)` where
`along > 0` picks tiles ahead of `dir` (signed dot via the existing per-tile delta). Smooth mode
keeps the old full crater. VERDICT: confirmed at WorldStreamer.Deform.cs Crater branch; the dig tools
pass `player.transform.forward`.

### H5 â€” v3 save format vs in-place v2 upgrade â†’ CONFIRMED v3
The P1 v2 format stored one integer per column (top). Multi-run needs `runCount + (YBot,YTop)*`.
Tagging it onto v2 would confuse the "version" meaning; a clean `VoxelSaveVersion = 3` reader accepts
v3 AND migrates v2 AND v1 on read (same load-time conversion, now two callers). Smooth path never
writes voxel versions; its reader still rejects them (documented). VERDICT: v3 with 3-way read, write
kept sparse (`VisitColumns` skips pristine).

### H6 â€” cavity rendering: pass 1b/1c floors+ceilings, walls deferred â†’ CONFIRMED (with a documented hole)
Per-run floors (every non-topmost run's top) + ceilings (runs whose bottom sits above the column
floor) give a cave its chamber floor and roof without inventing occlusion. But the existing wall pass
reads only the TOP run, so the cavity side walls aren't meshed â€” the rim of a carve reads as a slot
into the void. Option A: rewrite the wall pass to run per-run (large, risky for P2 review);
Option B: floors+ceilings now, per-run walls later. VERDICT: B, documented as a P2 limitation.

### H7 â€” LOD children + far shell in voxel mode â†’ CONFIRMED (both ship in 1eu)
P1 left LOD children + far shell smooth ("intentionally"). With voxel now default that would relight
the seam every crossing: near stepped / far smooth + a boxy grid inside a smooth disc. So a
decimated `BuildVoxelLodChild` (2Ã—2 blocks, rounded-mean top) routes through the same pooled LOD
mesh, and `BackgroundGenerateFarCell` gains `BuildVoxelFarSector` (steps the same integer column
tops on the far 3 m grid, merged top rows + terrace walls â€” a duplicate builder, not a refactor of
the verified VoxelMesher, keeping the far tri budget independent). The gate fix
(`if (!_lodDirty || VoxelStore == null)`) covers the voxel branch. VERDICT: confirmed.

### H8 â€” default ON: safe flip or keep experimental OFF? â†’ CONFIRMED ON, toggle stays
The whole point of "complete all phase" is the voxel world becoming THE world. The flag flips to
true; `NewWorldTestGround.EnableVoxelTerrain` still exists for QA and the OFF path keeps the smooth
streamer entirely (no code path deleted). Legacy WorldBuilder village + streamed terrain untouched
(rule 4). VERDICT: default flipped, docs updated, deleted-symbol grep clean.

### H9 â€” QA lane placement: clear of the platform + village â†’ CONFIRMED
`EnableVoxelSculptDemo` runs directed dig + cave + raise on streamed terrain off the platform's west
edge (the existing slab/enemy/terrain demo lanes own the south/east), HP-needless (pure
WorldStreamer public API), markers as simple stone pegs. VERDICT: confirmed.

## 1es â€” "player lag when travel the world" â€” the crossing spike and where it REALLY lives (SHIPPED in `1es`)

Context: after 1er the swap-band build churn is pre-warmed, but the user still reports periodic lag
"every chunk boundary". First move was to nail down the shape of the report (periodic â†’ crossing-bound
vs random â†’ a rogue system). User confirmed: **periodic spike roughly every ~30 m**, and chose
**smoothness over fill speed**. So: what actually fires at a 30 m crossing, and can it be made to
spill instead of spike?

### H1 â€” "it's the CPU generating the new chunk" â†’ TRIVIALLY TRUE but already async, so not the fix
Chunk generation (real + far) already runs on the ThreadPool; "use async" can't move Unity's
main-thread mesh uploads. The periodic ~30 m signal points at everything that REACTS to a ring cut on
the main thread in one poll. Not the root candidate by itself â€” the real question is why it doesn't
already spread.

### H2 â€” the budget pieces were each capped, but they were independent and all re-zeroed the same frame â†’ CONFIRMED, the actual architecture bug
Walking 1 world/edge leaves the old ring: the next poll hits `StreamAround` (already-dirty), the
trailing arc of out-of-range chunks UNLOADS **all at once** (no cap at all â€” `UnloadChunk` destroys the
whole list), `FinalizeChunks` drains pending real meshes its own fixed slice, `StepChunkProps` builds
props its own 3 ms, far finalize its own 2.5 ms, collider cooks 2/poll. Every slice was individually
bounded, so per-pass arithmetic SAID "constant", but at a crossing all of them fire at their cap in the
SAME poll and the pieces that ran all finish in that poll. The spike is a RE-SYNC: the crossing is a
phase where many independently-capped streams legitimately need work, and there was no shared limit.
VERDICT: the fix must pool ALL main-thread streaming spend against ONE budget so a crossing spreads
over polls.

### H3 â€” one shared wall-clock budget, charged by whatever ran, with a deferral flag â†’ CHOSEN
`StreamBudgetMs` (4 ms) pool reset each poll; every streaming step spends its measured wall time; when
dry, `_streamCapped` makes each step stop before its next unit. Entry/conclusion gates per step
(`if (_streamCapped) return;`, `while (... && !_streamCapped ...)`). Frame-time benefit survives even
with rough measurements because the DRY signal is what stops the storm â€” not the precision of each
charge. Not gated at entry (deliberate): the far-shell pass itself and the step-1a shape sync are
visibility-critical; they keep their own caps and only charge their scan time. The idle-throttle gate
(`_worldDirty`/`working`) is orthogonal and preserved, so resting is still zero-cost.

### H4 â€” the unload storm (incoming half of the crossing) was uncapped â†’ CONFIRMED, capped
One `deactivateAndUnload` sweep destroyed the ENTIRE trailing arc in one poll (the outgoing "new
ground" counterpart of the old re-dispatch bug). VERDICT: cap `MaxChunkUnloadsPerPoll = 6`, flag
`_chunkUnloadBacklog`, fold it into the `working` idle gate so the spread-out drain isn't skipped as
"idle". A ~60-chunk turn spills over ~10 polls instead of one GIANT unmount.

### H5 â€” far finalize could still breathe around the shared pool, worst at crossings â†’ CONFIRMED, same pool
The far mesh-upload loop kept its own 2.5 ms slice. A crossing legitimately needs far handoffs TOGETHER
with real finalize; two separate budgets times two demands = spike again. VERDICT: fold far finalize
into the shared pool (spend + `!_streamCapped`), and charge the scan phase's elapsed time before the
finalize loop so the crossing's heavier scan thins the drag. Also considered: far uploads with
`UploadMeshData(true)` â€” `CreateFarSector` builds from ThreadPool-written vertex arrays and never
re-reads them on the main thread, so the CPU mirror looked free. VERDICT: **REJECTED by play-test
(`1es-fix`)** â€” a far mesh uploaded non-readable is returned to the SAME capped `_chunkMeshPool` as real
chunks, and a real chunk re-specifies its pooled mesh on load AND on every deformation re-upload
(`ChunkObject.ApplyMerged`), which throws "Not allowed to access normals/vertices (isReadable is false)".
The `markNoLongerReadable` flag was removed from `UploadMerged` entirely â€” pooled meshes MUST stay
readable for 1dv in-place reuse, so far cells upload `false` like everything else (the "win" was a few MB
of RAM; not worth a permanent poisoning hazard).

### H6 â€” "edit a chunk â†’ walk out â†’ unload flushes its .dat" froze a frame â†’ CONFIRMED, saves moved to a worker
`ChunkSaveManager.SaveChunk` wrote the file synchronously ON the unload path. The vertex/serialization
stays main-thread (it's cheap and needs the tile data), but the disk `File.WriteAllBytes` is now queued
(`_saveQueue` ConcurrentQueue) and drained by ONE ThreadPool worker per batch
(`Interlocked` flag + `QueueUserWorkItem`). Flush-at-shutdown / New Game drain the queue synchronously
(locks serialize the worker's writes). Path rule respected: the file path string is still captured once
on the main thread (`Warmup`) â€” background threads read the cached string only, per Unity 6 rules.
VERDICT: on-unload flushes become near-0 ms on the crossing.
- Sharp-edge subclass: `ResetTerrainSaves` must flush BEFORE `ResetWorldSaves` clears the move/remove
  bookkeeping, or a reset could drop (or worse, re-route) a pending write for the OLD world. Confirmed
  by reading both code paths before wiring the order.

### H7 â€” was any crossing-phase earlier slice BROKEN by the unification? â†’ RECHECKED
- `_streamBudgetRemaining` resets every poll and only spent-ran pieces reduce it â†’ a long chain of
  crossings each starts a full budget, so no starvation; deferral is to the NEXT poll, never to "never".
- Props' own `PropBudgetMs` (2) and the shared pool now BOTH cap it â€” redundant but harmless ceilings
  (time-based slice already dominates).
- `AdaptiveBudgetMs` (the 1di self-shrinker during hitches) now sizes the shared pool too, so the whole
  system tightens together on a bad frame.
- Unload cap can momentarily keep collider-scan/OOB tiles registered a poll longer than "instantly gone"
  â€” acceptable; the idle gate holds `working` until the backlog clears, so the hysteresis keep can't
  be skipped.

### H8 â€” why didn't the previous per-pass caps already do this? (post-mortem)
Each cap was sized for ITS OWN step's normal load ("a prop poll should take ~3 ms", "a finalize poll
~2.5 ms"); nobody summed them because each was "under budget" individually. The design never had a
crossing as a budgeted phase, so a crossing showed up as the arithmetic sum of every step's cap in one
frame. 1es changes the unit of measurement: budget by poll, spend by what ran. VERDICT: post-mortem
recorded; doc note in `WorldStreamer.Mesh.cs` `AdaptiveBudgetMs` says the pool is the primary ceiling.

## 1ea â€” "Still too laggy" â€” where is the mileage actually left after 1e5/1e6? (SHIPPED in `1ea`)

User: "it still is too laggy. Can you do more?" 1e5 covered boot + per-frame HUD hotspots, 1e6 covered
chunk LOD + pooling + missile throttle. This pass re-read the RENDER config and the streaming/frame
path hunting for standing-still costs. Context shaping every verdict: target = **Unity Editor Play mode**
(user's play environment), and the user chose **"take the FPS"** for the visual tradeoff. Editor-side
overhead (GfxDevice, editor passes) is NOT removable by game code and persists regardless â€” the game-side
wins below still win, but the overlay A/B should be read with that floor in mind.

### H1 â€” full-res SSAO is a per-frame GPU tax Editor rendering still pays â†’ CONFIRMED
`PC_Renderer.asset` (the ACTIVE forward renderer â€” QualitySettings level 1 â†’ `m_CurrentQuality: 1` â†’
PC_RPAsset guid `4b83569d`) ships SSAO **enabled at full res** (`Downsample: 0`, `Samples: 1`, so no
half-res shortcut) â€” a full-res depth-sampling + blur pass every frame. The stylized banded terrain
(`TerrainLayered`, flat colours) barely reads AO; cost-benefit bad. Grep: nothing samples
`_SCREEN_SPACE_OCCLUSION_TEXTURE`. FIX: `m_Active: 0`. Pure win.

### H2 â€” MSAA 4x + depth/opaque copies are separate half-frame taxes â†’ CONFIRMED, safe to cut
`m_MSAA: 1` (4Ã—) = 4x supersampled resolve on every opaque surface + edge work; `m_RequireDepthTexture`
and `m_RequireOpaqueTexture` author extra full-frame resources (opaque is a real copy). Critical gate
before killing them: grep the whole project for `_CameraOpaqueTexture` / `_CameraDepthTexture` â†’ **zero
hits** (the old pick-pixel code never sampled the camera copies). FIX: all three off. No known consum
er â†’ no visual regression. (HDR kept ON as the safer load-bearing default.)

### H3 â€” shadow cost is out of proportion for a mostly-solo-directional world â†’ CONFIRMED, value-safe
2048 main map + **4 cascades** + soft shadows (`m_SoftShadowQuality: 3`) + additional-light shadows ON.
Main light = the sun; the only non-sun realtime lights are rare torch/POI lamps. FIX: 1024 + 2 cascades
+ soft off + additional-light shadows off (atlas 512 for the rare case); `shadowDistance 40 â†’ 32` so the
two cascades don't under-resolve out to the old 40 m. User pre-approved the visual trade.

### H4 â€” the collider reconcile walks the FULL world map every Update â†’ CONFIRMED, the biggest standing-still CPU cost
1dq introduced collider-on-demand, but its `ReconcileColliders` looped `_loadedChunks` **every frame**,
computing ring distance + registry probes per chunk. Idle with the world fully streamed â†’ the full O(chunks)
walk still ran forever. Worse, any new finalize/unload re-triggered it and at that point it cooked EVERY
collider that had to turn back on **in one frame** â€” a PhysX mesh-cook burst on a boundary crossing
(exactly the spike a player on the move feels).
- H4a (walk-on-change): track `_colliderLastX/Z`, `ColliderRequestRegistry.Version`, and a `_collidersDirty`
  flag raised by `NoteChunkSetChanged` (`CreateChunkGameObject` + `UnloadChunk`) â†’ `ReconcileCollidersIfChanged`
  early-outs when nothing relevant changed. The registry is main-thread-only with a monotonic Version bumped
  in `Request`/`Release` â†’ race-free signal.
- H4b (cook budget): `MaxColliderCooksPerPoll = 4`; enables spread across polls (excess re-flags dirty so the
  walk continues), disables still apply instantly. Deadlock check: the deferred path re-runs because dirty
  stays true for as long as work remains. Open risk (play-test): a runner crossing many chunk boundaries can
  outrun the budget and land tiles without colliders for a few frames â€” accepted under the FPS directive.

### H5 â€” a newly loaded chunk re-materialises every neighbour's full tile list â†’ CONFIRMED, worst-case O(chunksÂ²)
Old `ChunkHasModifiedTiles` had, when called for border re-stitch, to materialise the full local tile list
of the neighbour to test for saved edits â€” on EVERY chunk load, in `ReconcileNewlyLoadedChunk`. On a stream-in
(N chunks) Ã— (M neighbours) of per-query tile work with no retained state. Grep: the only caller was that
reconcile. FIX: retained `_modifiedChunks` set + `HadLoadedMods` on `TerrainChunkMeshData` (set by `ChunkBuild`
only when the save actually contained mods):
- `ReconcileNewlyLoadedChunk(tc, hadLoadedMods)` seeds the set only when true;
- `ApplyHeightEdits` does `UnionWith(rebuiltChunks)`; `UnloadChunk` removes; `ResetTerrainSaves` clears;
- `ChunkHasModifiedTiles(tc)` = `_modifiedChunks.Contains(tc)` â€” O(1).
Edge: un-modded worlds now touch nothing extra on load; the modded path decides in O(1) instead of O(tilesÃ—M).
(1cs strata + 1dq residents re-grepped: `ChunkHasModifiedTiles` had no other callers to keep consistent.)

### H6 â€” chunk-LOD audit is a full-map scan per refresh + a per-switch GetComponent â†’ CONFIRMED, band rules untouched
`ChunkLodManager.Update` evaluated every chunk on every refresh frame and `ApplyBand` did
`GetComponent<MeshRenderer>()` on the root at each band entry/exit. FIX: rolling burst of `ScanBudget = 1024`
per refresh (`_scanCursor` wrap-around, null roots dropped in place via RemoveAt â€” the shift keeps the cursor
valid), squared distances end-to-end (`BandForSq` compares `StartDistanceÂ²` against `distSq`, cull is `distSq â‰¤
cullSq`), cached `Mr` on `ChunkEntry`. CRITICAL preserve: `ApplyBand` still calls `RefreshLodMeshes()` before
showing a far band (1e6: deformation must never render a pre-excavation hole). Rolling-burst risk: a newly
streamed distant chunk's band now corrects up to (count/1024) refresh cycles late â€” one extra frame of the
nearer band, not a gameplay issue.

### H7 â€” POI cull-candidate scan ran unthrottled â†’ CONFIRMED, cheap fix
The `FindObjectsByType<PointOfInterest>` block (3Ã— per type) under `IncludePoisAsCullCandidates` ran EVERY
FRAME; `CullManager.AddCandidate` dedupes via `_candidates.Contains`, so re-scanning identical POIs is pure
waste. FIX: `PoiScanInterval = 2` s + `_poiTimer` gate. POIs still join cull candidates â‰¤2 s after spawn.

### H8 â€” EnemyHealthBarHUD 0.5 s scene sweep â€” examined, NOT changed â†’ OPEN (parked)
It already sweeps at 0.5 s and re-projects at 30 Hz (1e5). The residual cost is a `FindObjectsByType<Enemy>`
pass every 0.5 s + per-candidate reads. A proper fix = an enemy/billboard registry (a real refactor); ROI
too low for this pass. Re-audit with the bench overlay after play-test if it still shows up.

### Measurement / A/B plan
- `NewWorldTestGround.EnableFpsStats` (default off) â†’ `SpawnFpsStats()`/`UpdateFpsStats()`: 4 Hz readout of
  avg FPS, frame ms, loaded chunks, active colliders â€” the user can diff before/after standing still,
  sprinting a chunk boundary, digging, and on a fresh world stream, without a profiler.
- Expected: an idle fully-streamed world now has ~zero game-side per-frame maintenance (H2/H4/H6 idle
  budgets gone, H5 load penalty gone). Residual Editor-side floor remains â€” that's H-driven: if the overlay
  still shows spiky low frame times with idle-zero game work, the NEXT levers are HDR off + render-scale,
  then the H8 registry.

---

## 1eb â€” "Remove the particle effect of magic and make the projectile detail" (SHIPPED in `1eb`)

User request riding 1ea's perf pass â€” a narrow, well-fenced FX cut. Context: the user plays in Editor
Play mode and said "take the FPS" (1ea), so this is another cost cut, not a fidelity push. First map
what actually is a "particle" before deleting anything â€” game code uses the word loosely.

### H1 â€” what IS the magic "particle effect"? â†’ CONFIRMED: exactly one ParticleSystem, the projectile exhaust
Project-wide grep of runtime `ParticleSystem` use: (a) `SpellCaster.AttachProjectileParticles` â€” the
cone billboard exhaust on every default projectile, up to **700 live particles for a Fireball**,
simulated per frame while any bolt flies; (b) the bench pedestal exhaust â€” same builder via
`CreateProjectileDisplay`; (c) cutscene demon smoke (`CutsceneManager.EndingDemon.cs:117`) â€” an ending,
not magic combat; (d) `ObjectPooler`'s replay guard (`TryGetComponent<ParticleSystem>`). Everything
else that *looks* like FX (SpellBeam, SpellZone funnel, storm bolts, summon pillar, RingFlash/SkillFx)
is built from primitives/LineRenderer â€” NOT a ParticleSystem. Verdict: "remove the particle effect of
magic" = delete (a). (b) follows automatically (shares the builder). (c) and (d) are out of scope.

### H2 â€” what is the real per-frame cost of projectile "detail"? â†’ CONFIRMED: OrbFx on every child + the ParticleSystem sim
Every shaped body adds the `OrbFx` MonoBehaviour to the root and most children (Debris adds 5-7 chunk
`OrbFx` + root). Each runs `Update()` **per frame**: `Mathf.Sin` pulse, `localScale` write, and a
`Rotate()` for Shard/Swirl/Tumble (Tumble = per-object random-axis rotation). A multi-cast volley or a
tower of turrets Ã— 9 cube segments per Bolt = a solid per-frame cost. The ParticleSystem itself is the
second cost (per-frame simulation + billboard batch). Both are pure visual â€” the projectile root keeps
no collider and all flight/raycast/damage lives in `SpellEffect` â†’ stripping them touches nothing
gameplay-adjacent. Verdict: confirmed, both go.

### H3 â€” delete the code vs. gate behind a toggle? â†’ CONFIRMED: delete
AGENTS rule 4 puts QA/test knobs on the test platform, but this is a permanent gameplay tuning the
user asked for (same class as "take the FPS" in 1ea), not a test feature. Every helper is private/
static and grep-confirmed single-caller (`AttachProjectileParticles` only from
`AttachDefaultProjectileVisual`; the five switches only from it; `OrbFx` only within this file) â†’ a
full deletion leaves zero dead code and zero public-API churn. Bench interplay: `CreateProjectileDisplay`
loses its OrbFx-strip loop because the type ceases to exist â€” good, bench models and live casts become
identical by construction. User's projectile-level choice (keep the shaped bodies, kill the animation)
keeps spell identity while removing all per-frame pulse work.

### H4 â€” the impact "poof": is it particles? â†’ NO, and the user chose to keep it
Impact FX = pooled cube debris (`SpawnCraterDebris`, 2.5 s `ObjectPooler` lifetime) + a terrain dent â€”
not a ParticleSystem. `SpellEffect`'s `SpawnTransient(_spell.ImpactEffectPrefab, â€¦)` sites are no-ops
with the default (null) prefab, so there is literally nothing particle-like about the impact left to
remove. User explicitly picked "keep". `SpellZone`/`SpellStorm`/`SkillFx` flashes are primitive-based
one-shots â€” kept (they're brief and pooled).

### Out of scope, recorded so the path isn't walked again
- Ranged arrows/bolts (`RangedWeaponBehavior`, `RangedProjectile`) â€” not magic, separate visual path,
  no shared FX â†’ untouched.
- Cutscene demon smoke â€” ending #2's atmosphere, not combat magic â†’ stays.
- Aim previewers (ProjectilePathPreview/AoeAimPreview/CastingCircle â€” LineRenderer rings) â€” targeting
  aid, not FX â†’ stay.

---

## 1ec â€” "Rework the magic projectile model... fire ball would be multiple cubes, smaller stacked on the back" (SHIPPED in `1ec`)

Follow-on to 1eb (static bodies, no particles). User wants the projectile *shape reworked*, and gives
the model: a fireball = multiple cubes, the smaller ones **stacked on the back**. Reasoning trail below;
verdicts marked. Not cited as implemented behavior anywhere â€” docs of record = Â§3.8 of game-design.md.

### H1 â€” what exactly reshapes: is this another FX-layer change (like 1eb) or a per-shape restyle? â†’ CONFIRMED = per-shape restyle, all builders, no new state
The whole visual layer is data-free shape builders in `SpellCaster.Projectiles.cs`; 1eb already removed
the only FX state (OrbFx). The user's "the projectile detail" from 1eb was the *particles*; now they
want the *silhouette* itself reworked. Hypothesis: apply the cluster-fade idea to EVERY shape so no
body contradicts the new look. Confirmed by scoping with the user: all shapes get the cube-cluster
treatment; bright front / darker back; **fully static** (no animation â€” preserve the 1eb win). The old
default `Orb` (stacked shrinking cylinders) and the sphere drops in Splash/Missile/Comet were the
inconsistent leftovers â€” `Sphere` primitives retired from projectiles entirely.

### H2 â€” how should the cluster read? Two candidate formulas â†’ chose save-at-construction static, quadratic taper
- H2a: keep a per-frame restyle (un-lerp sizes each frame). Rejected â€” contradicts 1eb's static
  directive, per-frame cost returns.
- H2b: build the whole cluster ONCE in the builder (static). Accepted. Formula: `t = i/(count-1)`,
  `scale = Lerp(lead, minCube, t*t)` â†’ quadratic shrink (front big, fast falloff so the "stack" reads),
  color `Lerp(color, black, t*fade)` with `fade â‰ˆ 0.75` (darker to the rear), back cubes get jitter +
  random Z-rotation for a hand-stacked voxel feel. Leader stays full school color, no jitter.

### H3 â€” what to do with the elongated/composite bodies (Bolt/Lance/Spear/Blade/Dart/Comet-rock/Missile/Debris)?
Three options considered (reject â†’ adopt):
- Reject: flatten every shape back to a plain `Cluster` â€” loses the bolt-diamond/lance/blade reads that
  the names depend on (Â§3.8 spells are named after their shapes).
- **Adopt for composites**: keep the structural silhouette (shaft+tip, cross-blade, segment chain) and
  add the same dark-fading **trailing flecks** behind it (`AddTrailingFlecks`, 2-3 small cubes,
  `Lerp(color, black, 0.45+0.2f)`). Bolt/Debris/rock-comet were already cube chains â†’ structural
  no-change, just doc fix ("Tumbling" clump â†’ "Clustered").
- **Adopt replacements**: the leftover spheres convert to the cluster look â€” Splash drop â†’ leading flat
  cube + 3 darker cube drops; non-rock Comet core â†’ 3-cube mini `Cluster` (`0.2, 3, 0.1, 0.02, fade 0.6`)
  under the existing streak; Missile volley â†’ each dart a 2-cube mini `Cluster` (`0.12 + 0.07`); Shard
  â†’ translucent glass lead chip + 2 dimmer glass chips (keeps the glassy frost read, THINKING 1e8-era
  design) â€” confirmed via scoping that chipping the "frost = translucent glass" look was NOT wanted.
- rockBody Comet (Meteor/Asteroid/sky-rocks) stays boulder+cubes+tail: it is deliberately a *rock*, and
  the user's cluster idea targets the magic-ball silhouettes, not the rock summons.

### H4 â€” safety: do the reworks risk gameplay, flight, or shared consumers? â†’ CONFIRMED SAFE
`SpellEffect`'s flight/raycast/damage reads only the root (no collider on bodies; root colliderless per
1eb). The bench (`CreateProjectileDisplay` in `NewWorldTestGround`) and turret summons (`DecorateProjectile`)
call the SAME builders â†’ rework propagates for free, no signature change. No public/protected API or
saved data touched. Verified by grep: `Orb(` gone (only `Cluster(` remain), `PrimitiveType.Sphere` 0
hits inside `SpellCaster.Projectiles.cs` (remaining spheres are Fishing bobber, world props, cutscene
eyes, beam end-orb, summon head, storm FX â€” separate systems, correct to keep as spheres).

---

## 1e9 â€” "The upper body bending when moving the cursor up/down is reversed" (SHIPPED in `1e9`)

User report right after 1e8 (they now see the torso clearly). One-line class of bug: sign inversion.

### H1 â€” the cursor-to-torso sign is inverted at the animator â†’ CONFIRMED
Traced the sign chain, no build:
- `PlayerController` pitch source: `_pitch -= delta.y * ...`, clamped Â±60; `LookPitch` documented
  "positive = looking down, negative = up" (PlayerController.Camera.cs:26-27) and applied straight to
  the camera pivot `Euler(_pitch, 0, 0)` â†’ +X at the camera = nose down. Consistent.
- Torso pivot: identity local rotation on the +Z-facing model (`Torso` under root, Euler(0,0,0)), so a
  positive X rotation moves the torso top (head end) toward +Z = the facing = a FORWARD lean.
- `PlayerAnimator` line 146 negated it: `lookTilt = -clamp(pitch)`. Look down (+30) â†’ âˆ’15Â° X on the
  Torso â†’ backward pitch; look up â†’ forward pitch. Exactly "reversed", and it contradicts the
  function's own comment ("looking down bends the torso forward").
- Grep: `LookPitch` is consumed only by PlayerAnimator; `lookTilt` feeds idle (161) and moving (239)
  explicitly. No other consumer to keep sign-consistent with.

### H2 â€” the sprint "forward lean" (-12) used the same flipped convention â†’ CONFIRMED (user chose to fix)
Line 239 `Euler(-12f * runBlend + lookTilt, 0, 0)` is commented "cartoon run top body: forward lean".
Under +X = forward, âˆ’12 is a backward arch. The user confirmed they want a genuine forward lean â†’
flipped to `+12f * runBlend`. The head-bob constant (line 241, `-2f * runBlend`) left untouched: with
the torso now +12, the âˆ’2 head term just keeps the head slightly less pitched than the body during a
sprint (reads "charging ahead, head up"), and is a separate 2Â° subtlety to tune only if the sprint
still reads odd in play-test.

### FIX
`lookTilt = Mathf.Clamp(_pc.LookPitch, -60f, 60f) * TorsoLookBlend;` (146) and
`Euler(12f * runBlend + lookTilt, 0, 0)` (239). Both values stay within existing clamp ranges.

## 1e8 â€” Play-test of 1e7: "some faces not loading" + "shoulder joint too narrow" (SHIPPED in `1e8`)

Two user reports right after 1e7 (first real render of the shouldered torso). Two separate root
causes, both confirmed by reread + silhouette math, no build.

### H1 â€” "some faces of the torso is not loading" â†’ CONFIRMED: the crown-cone band is never emitted
Looking for the hole in `BuildTorso`'s emission, not the lighting/winding first. Structure:
`bands = 8` â†’ 9 rows (lat 0..8), side bands loop `for (int b = 1; b < bands; b++)` â†’ 7 iterations,
band b connects rows (bâˆ’1, b) â†’ bands (0,1)â€¦(6,7). Row 8 (the t=1.0 crown ring, W 0.20) connects to
nothing below: the cap loop only fans it to the crown CENTRE (closing lid) while row 7 (t=0.875,
W 0.74) connects down to row 6. So the crown cone â€” the actual "dome slope" in 1e4's wording â€” is a
hole ring around the top; the crown lid floats. That's the visible see-through. The ellipsoid
`Generate` loop `b < Rings` (1..6) has no such hole because its lat=0/Rings rows are degenerate poles.
- FIX: `b <= bands` (emits rows 7â€“8 cone). Indices: corners length = 9Ã—12 = 108, band 8 reads rows
  7â€“8 â†’ max index 107 âœ“. This restores the intended dome slope geometrically. OPEN (visual, not
  logical): whether `EmitQuad`'s single-flat-normal cells on the STEEP crown cone render clean â€” they
  should (nearly-planar 30Â° frustum cells; centroid-flip consistent) but is in the play-test list as
  a contingency (escalate band 7â€“8 to per-triangle emission if glitchy).

### H2 â€” "shoulder joint too narrow, overlap with torso" â†’ CONFIRMED: ball buried by the 1e7 dome
Numbers: standing pivot (Â±0.28, 0.35) â†’ part-local y 0.22 â†’ t = 0.22/0.8 + 0.5 = 0.775 (NOT 0.90 â€”
that was a slip dividing by height wrong; t = localY/sizeY + 0.5 = 0.275+0.5 = 0.775). W(0.775) on the
old rows (0.80, 0.74) â‰ˆ 0.788 â†’ world 0.44Â·0.79 = 0.347 (male) / 0.40Â·0.79 = 0.315 (female). Ball:
size 0.14 â†’ radius 0.07, center 0.28 â†’ outer edge 0.35. Male surface 0.347 â‰¥ outer 0.35âˆ’Îµ â†’ ball
flush/invisible; female shows a 3.5 cm sliver. Matches "too narrow / overlapping the torso".
- Hypothesis options (asked the user): (A) bigger balls + outward pivots, (B) bigger balls only,
  (C) narrow the dome so the SAME balls poke out. USER CHOSE C â€” keep joint sizes/pivots as authored,
  reduce the silhouette so the ball reads as a cap.
- FIX (Body): rows t=0.75 0.80â†’0.70 and t=0.875 0.74â†’0.60 (D: 0.48â†’0.44, 0.44â†’0.38), crown 0.20
  untouched. New W(0.775) = lerp(0.70, 0.60, 0.2) = 0.68 â†’ world 0.299 male / 0.268 female â†’ poke
  0.35âˆ’0.299 = 0.051 / 0.082. Chest (0.72) stays the widest upper point â€” taper, not inverted.
- FIX (Chest for the sit model): sit pivots at t = (0.40âˆ’0.42)/0.28 + 0.5 = 0.429; old W â‰ˆ 0.71 â†’
  world 0.39Â·0.71 = 0.277 vs ball outer 0.25+0.065 = 0.315 â†’ 3.8 cm sliver. New mid rows (0.375â†’0.66,
  0.5â†’0.62, 0.625â†’0.60): W(0.429) â‰ˆ 0.643 â†’ world 0.250 â†’ poke ~6 cm âœ“.
- Seated Body (pivot t = 0.22/0.6 + 0.5 = 0.867) inherits the Body rows: W â‰ˆ 0.607 â†’ world 0.34Â·0.61
  = 0.206 â†’ ball clearly outside (âˆ’0.24 pivot, radius 0.065) âœ“.
- OPEN (until play-test): cap readability â€” the ball is shirt-colored on a shirt-colored dome, so it
  reads through SILHOUETTE + facet normals, not color; if it still reads weak, the knobs are the wB/dB
  top rows or (revisited) ball size, not the pivots.

## 1e7 â€” "The shape of torso is still the same" â€” why the shouldered torso never changed (SHIPPED in `1e7`)

User report after 1e4 shipped: the torso STILL looks the same. Every prior silhouette change
(1e2 shoulders, 1e4 dome/crown) appeared to do nothing in play. Hypothesis investigation below.

### H1 â€” "the user just didn't notice / the dome is too subtle" â†’ REJECTED
The dome slope + crown spans only the top ~12% of the part height above a 0.80-wide shoulder shelf â€”
conceivable as "too subtle". BUT the report says the torso looks the SAME as before, not "slightly
different". The stronger prior: the shape change never reached the renderer.

### H2 â€” "a stale cached mesh / another model path wins" â†’ REJECTED (dead end, right symptom wrong cause)
Checked the player-model builders (`MapBuilder.PlayerModels`): the torso parts go through
`MakePart(name, parent, size, pos, color, profileId)` with profile ids `"Body"`, `"SitTorso"`,
`"Chest"`. NPCs/enemies use `MakeBlock` (cube) â€” not the same path. Stale mesh caching is
impossible (meshes live in an in-memory static cache; a recompile is a fresh domain). So the id path
itself is the suspect.

### H3 â†’ CONFIRMED â€” `BuildTorso` was unreachable dead code; every torso part rendered as a plain ellipsoid
`BuildEllipsoid(profileId)` opens with `if (empty || !_profiles.ContainsKey(profileId)) profileId = "HairBand";`
then `Generate(profileId)` â€” and `Generate` only branches to `BuildTorso` when the id is exactly
`"Body"/"SitTorso"/"Chest"`. The three torso ids are intentionally NOT in `_profiles` (they aren't
dent-sculpted ellipsoids; they're the separate silhouette builder), so *every* call with a torso id
was remapped to `"HairBand"` first and the `BuildTorso` branch was unreachable. The torso â‰¥ 1dw was
always the plain unsculpted ellipsoid â†’ identical silhouette through 1e2/1e3/1e4. The 1e2/1e4 "taller
Body so pivots sit on the dome" sizing was sized against a mesh that never drew.

Evidence trail (all grep/read, no build):
- `_profiles` keys (read): Skirt, SkirtHem, Head, Neck, UpperArm, Forearm, Hand, Thigh, Shin, Shoe,
  Hair, HairSide, HairBack, HairBand, Cylinder, Ponytail, EyeWhite, EyeIris, Joint â€” no Body/SitTorso/Chest.
- `BuildEllipsoid` fallback = "HairBand"; `Generate("HairBand")` skips the BuildTorso branch; HairBand
  has empty dents â†’ plain ellipsoid. â†’ confirmed reachability failure.
- Only caller of `BuildEllipsoid` is `MapBuilder.MakePart` (grep) which passes the raw profile id.

### FIX â€” route torso ids before the fallback (no shape change)
`BuildEllipsoid` now short-circuits `"Body"/"SitTorso"/"Chest"` to a cached `BuildTorso` build (same
`_cache` + `HideAndDontSave` pattern as the ellipsoid path). The silhouettes themselves were already
sized correctly â€” no values touched. Post-fix pivot-vs-silhouette sanity (reread math):
- Standing `Body` (size.x 0.44/0.40, centre root y 0.13): shoulder pivot local y = 0.35âˆ’0.13 = 0.22 â†’
  t = 0.72 â†’ W â‰ˆ 0.78 â†’ world 0.44Â·0.78 = 0.344 (female 0.312) â‰¥ pivot Â±0.28 â†’ joints sit ON the dome band.
- Sit `Chest` (centre y 0.42): pivot local y = 0.40âˆ’0.42 = âˆ’0.02 â†’ t = 0.48 â†’ W â‰ˆ 0.70 â†’ world
  0.39Â·0.70 = 0.275 â‰¥ pivots Â±0.25 â†’ shoulders carried by the Chest plateau as designed.
- Seated-car `Body` (size.x 0.34, centre y 0.25): pivot local y = 0.47âˆ’0.25 = 0.22 â†’ t = 0.72 â†’ world
  0.34Â·0.78 = 0.266 â‰¥ Â±0.24.
- `SitTorso` sits under the `Chest` (top W 0.46 tucks under Chest bottom W â‰ˆ 0.70) â†’ reads as layered,
  no seam.

OPEN (until play-test): visual fit/"reads" of the newly-rendered shoulders vs the joint balls and the
neck crown â€” if the silhouette reads wrong on screen, tune the dome/shelf arrays, don't re-plumb.

## 1e6 â€” Structural render/physics: is the LOD/pooling layer worth wiring? (SHIPPED in `1e6`)

The second half of the 1e5 audit (render/physics budget + the Phase 9 managers that were never
consumed). Verdict: the LOD layer was a genuine no-op bug and the pooler was pure dead code â€” both
worth wiring; the rest of the "audit suggestions" were rejected with evidence.

### H1 â€” "ChunkLodManager already saves triangles" â†’ REJECTED (it saved nothing)
Read: `RegisterChunk` only indexes children named `Lod*`; nothing in the codebase ever CREATES a
`Lod1`/`Lod2` child (grep across Assets: only `RegisterChunk`). So every chunk had an empty Details
dictionary, `ApplyBand` fell to the `useDetail == false` branch forever, and â€” worse â€” the branch
that DID run only did `root.GetComponent<MeshRenderer>().enabled = true` (already true), never
disabling the root when a detail *was* present. Net: bands 30/60/120 existed on paper, zero triangle
saving, ~3.7k chunks at full ~1800 tris each. â†’ CONFIRMED dead; fixed by building real LOD children.

### H2 â€” "decimate the merged mesh with a simple vertex stride" â†’ REJECTED (layout is not a grid)
The merged mesh is per-tile quads (4 verts/tile, fixed offset `(lz*cs+lx)*4`) followed by side-wall
verts â€” NOT a uniform vertex grid. A naive stride would sample across quad boundaries and tear the
surface. Also, the top-block corners are DUPLICATED per tile (each tile stores its own 4 corners), so
a stride over raw vertices is meaningless. FIX: sample the *tile-corner grid* â€” grid point (gx,gz)
maps to a specific tile's SW/NE/NW/SE corner via `WorldCornerIndex`, and the decimated mesh shares
one vertex per grid point â†’ watertight inside itself, exactly on the chunk edges.
- Sub-bug caught in review: step 4 does NOT divide the 30-tile chunk (0,4,...,28) â†’ Lod2 stopped 2 m
  short of the edge and would leave a seam against the neighbour. Switched Lod2 to step 3 (0,3,...,30)
  and documented the "step must divide cs" contract.

### H3 â€” "build LOD children eagerly in ApplyMerged" â†’ REJECTED (cost for chunks that never use them)
A radius-30 ring is ~3.7k chunks; most sit in band 0 and never show a detail. Building 2 extra meshes
per chunk on every apply would ADD work for the majority. FIX: lazy build, `_lodDirty` set by every
apply/patch, `RefreshLodMeshes()` called from `ApplyBand` only when a detail band is first selected.
This also solves deformation: a pit dug while near (band 0) marks the LOD stale; walking away past
60 m triggers the refresh so the far view shows the pit, not a closed-over surface.

### H4 â€” "ObjectPooler is broken (EntityId keying)" â†’ OPEN-but-safe, left as-is
`GetEntityId()`/`EntityId` are not defined anywhere under `Assets` (grep) â€” they resolve from a
project-wide source the pooler already relied on. If a clone's id differs from its prefab's, pooling
silently degrades to `CreateNew` every Get (correct, just no reuse); if they match (the pooler's
authoring assumption), pooling works. Either way the wiring is safe; not worth chasing an external
type for a perf-only fallback. NOTED as an assumption, not a bug.

### H5 â€” "pool enemy death debris and loot drops" â†’ REJECTED
Enemy `ExplodeModel` debris is the enemy's OWN rendered model parts (each `Renderer.gameObject`
detached + given a Rigidbody), not clones of a prefab â€” pooling them would mean returning model parts
to a pool and re-parenting them into the model factory, a structural rewrite for a per-death-only
burst. Loot drops (`DropDrop`) are persistent (parented to the world, consumed by pickup logic, not
stateless on wake). Both are low-frequency vs. spell impacts/digs. Skipped.

### H6 â€” "add a prop-collider distance band" â†’ REJECTED
The 1di prop ring already restricts live props to a radius-4 Chebyshev ring (~600 m) â€” roughly 80
chunks Ã— ~2 props â‰ˆ **~160** live prop GOs/colliders near the focus, not the ~450k broadphase bodies
of the old full stream. A further band would buy almost nothing while making distant trees
walk-through and letting spell rays pass through them (contradicting 1di's "everything inside the
ring keeps its colliders", and the tool ray is only 10 m â€” `ToolManager.UseRayDistance`). Skipped.

### H7 â€” "throttle the whole SpellEffect Update to 30 Hz" â†’ REJECTED as stated, narrowed
Movement + the two detonation probes must stay per-frame or a fast bolt tunnels/overlaps past a
target between steps. What IS safely throttleable is the homing guidance re-lock scan (RaycastAll +
OverlapSphereNonAlloc, `UpdateMissileTargeting`) â€” a soft "who do I chase" read. Throttled that to
1/3 frames, steering still per-frame. CONFIRMED narrower fix.

---

## 1e5 â€” Performance deep-dive: is there real mileage left after Phases 0-5? (SHIPPED in `1e5`, structural work deferred to `1e6`)

Three parallel audits (boot timeline, per-frame hot-path grep, render/physics budget) produced a mix
of real wins and false alarms. The adjust-the-ratios verdict was: startup had genuine leftover cost
(an actual triple-build), the per-frame field was mostly already clean, and the render/physics layer
holds the remaining big items (â†’ 1e6).

### H1 â€” "Boot is slow because of ~24 FindAnyObjectByType scans" â†’ REVISED
Evidence: GameBootstrap.cs:17-54 does ~24 `FindAnyObjectByType<T>()` sweeps. But each sweep is ~0.1ms
on a sparse early scene â€” the whole scan block is ~1-3ms, small v. the init bodies. VERDICT: the
scans are NOT the main cost; the main cost is that `InitializeUI` (120-180 GOs + 8 textures, 15-35ms),
`ToolManager.Initialize` (53 tool models) and `SoundManager.LoadSoundClips` (8 sync Resources.Load,
No-alloc â€” each may be 0.5x-5x ms) run MULTIPLE times: UIManager's own `Start()` (UIManager.cs:236),
`GameBootstrap` (line 65/66), AND `GameManager.Start`â†’`AutoResolveReferences` (line 272/273). Three
UI builds. The registry (#17 documented approach) is still the right mechanism to remove the sweeps,
but the dedup guards are the real seconds-fraction win. â†’ CONFIRMED-by-read, shipped.

### H2 â€” "Adding an `_initialized` guard to AutoResolveReferences is safe" â†’ PARTIAL-CONFIRMED with a catch
The catch found by re-reading Start(): `AutoResolveReferences` is the ONLY place that fills `Pets` from
the scene (`FindObjectsByType<PetController>`) before `SpawnDefaultPets()` checks `Pets.Count`. An
early-return guard would stop a pre-placed scene pet from being counted â†’ `SpawnDefaultPets` would
spawn an extra default pet that never existed before. FIX: guard only the side-effecting init cluster
(menu/UI/tool/cutscene/randomEvent), leave field resolution + Pets scan per-pass. â†’ confirmed.

### H3 â€” "Deferring the non-critical boot inits to a BootInitDeferrer is safe" â†’ CONFIRMED with exclusions
Evidence read: `QuestManager.InitializeQuests` is self-guarded (`_quests.Count > 0`), and
`GameManager.Start`â†’`StartNewGame` already calls `ResetQuests()`+`InitializeQuests()` + `Karma`/`Religion`
Initialize on frame 1 â†’ re-queueing those from bootstrap would DOUBLE them. Excluded. `WifeNPC.Initialize`
only builds the dialog canvas; `WifeNPC.Start` only starts coroutines, and `ResetForNewGame` only resets
fields â†’ delering Initialize/LoadState to frame 2 is invisible. `CutsceneManager.Start` only starts a
coroutine, `Initialize` prebuilds driving assets when enabled (deferred OK). â†’ confirmed by read.

### H4 â€” Audit's per-frame "SkillBarHUD RefreshEntries Clear+AddRange+Sort per frame" â†’ REJECTED as noise
Re-read: SkillBarHUD.Update only runs while the skill bar is visible, `RefreshEntries` writes into
pre-allocated lists (AddRange onto a zero-capacity list reallocates once then reuses; Sort on â‰¤10
items), and label repaint is already change-guarded (`_entriesDirty`). Not worth touching.

### H5 â€” Audit's "InteractionPrompt LateUpdate GetComponent<Collider> + FindNearest each frame" â†’ REJECTED as already-mitigated
Re-read `UI/InteractionPrompt.cs`: the crosshair raycast already runs every 3rd frame with a cached
last-hit; text writes are change-guarded; the only true-every-frame cost is `ResolveEKeyLocKey`'s
`SittableSeat.FindNearest` when pointing at empty terrain (small linear scan) and one
`GetComponent<Collider>` guarded by Unity's per-type cache. Left untouched.

### H6 â€” "EnemyHealthBarHUD runs 24 WorldToScreenPoint/frame" â†’ CONFIRMED, throttled
The 0.5s full-scene `FindObjectsByType<EnemyController>` rescan is intentional (`1dr`); the per-frame
cost was the screen-space projection for up to 24 bars. Throttled projections to 30Hz (position
stale for â‰¤1 frame only) + skipped bars for enemies > 60m + fill writes stay change-guarded.
CONFIRMED the previous narrowing (PlayerBars/MagicWheel already throttled by 1dr).

### Dead end recorded (so it isn't re-walked)
`SittableSeat.FindNearest` per-frame was hypothesized as the "interaction prompt" cost â€” the actual
per-frame prompt already caches it behind the raycast gate. Camera per-frame items were real but small.
The big render/physics findings (LOD meshes never created â€” `ChunkLodManager` band children don't exist;
~3,700 chunk draw calls; per-cast collider re-cook + save flush; ObjectPooler with zero consumers;
per-projectile `new Material`) are the structured second-half work â†’ **1e6**, separate commit.

---

## 1e4 â€” shoulder-dome torso: kill the flat-collar look WITHOUT stranding the shoulder pivots (SHIPPED in `1e4`)

Motivation: the 1e2 flat top plateau read as a collar ring / hat brim around the neck base. 1e4
replaces it with a sloped shoulder dome + small crown. The SELF-REVIEW trap: the first 1e4 cut shipped
only "silhouette math looks right in the doc comment" â€” it did NOT recompute the shoulder pivots, which
turned out to sit exactly on the crown row.

### Step 1 â€” H1 "the crown covers the pivots": REJECTED by recomputation
- Standing shoulder pivot: torso-local (Â±0.28, 0.35) â†’ root (Â±0.28, 0.40). Standing `Body` is
  size (0.44, 0.6, 0.25) @ root 0.05 â†’ center root 0.10, top root 0.40. So the pivot is EXACTLY at the
  part top â†’ t = 1.0 â†’ the crown row (W 0.20) â†’ world 0.44Â·0.20 = **0.088** vs pivot Â±0.28 â†’ freed.
- Seated: body (0.34, 0.5, 0.28) @ root 0.25 â†’ spans [0, 0.50]; pivot (Â±0.24, 0.47) â†’ unit y 0.44 â†’
  t 0.94 â†’ silhouette ~0.46 â†’ world 0.156 vs Â±0.24 â†’ freed.
- Sit: `Chest` (0.39, 0.28, 0.26) @ root 0.42 â†’ spans [0.28, 0.56]; pivot (Â±0.25, 0.40) â†’ t 0.43 â†’
  W â‰ˆ 0.71 â†’ world 0.277 â‰¥ Â±0.25 âœ“ COVERED (only this one was fine).
- Lesson already written in 1e2's log: the flat plateau W 0.80 held through t=1 WAS load-bearing for
  the pivots (1e2 Step 2). The 1e4 dome narrows to 0.20 at t=1 â†’ any silhouette-only change was
  doomed to regress 1e0/1e2. The WIP doc comment even claimed coverage with a half-worked fragment
  ("0.65Â·0.44=0.286 â‰¥ Â±0.13") that was never true arithmetic.

### Step 2 â€” H2 "just keep the top wide": viable but fails the mission
- Constraint: SHARED `"Body"` silhouette must satisfy BOTH standing (needs â‰¥ 0.28/0.44 = 0.636 at
  t 1.0) and seated (needs â‰¥ 0.24/0.34 = 0.706 at t 0.94 â€” the binding one). A dome that stays
  â‰¥ 0.72 at the very top covers both BUT the crown is gone â†’ reads like the old plateau again. So a
  true small crown REQUIRES moving the pivot down the silhouette (taller body OR lower pivot).

### Step 3 â€” H3/H4 "taller body" vs "lower pivot": chose taller body (H3)
- H3 â€” build `"Body"` taller so pivots land on the dome band (t 0.78â€“0.87) instead of the crown row.
  Standing body center moves 0.05â†’0.13 (TORSO-LOCAL; the Torso pivot sits at root 0.05, so root
  center 0.18) and size.y 0.6â†’0.8 â†’ spans torso-local [âˆ’0.27, 0.53]: pivot torso-local (Â±0.28, 0.35)
  â†’ unit y (0.35âˆ’0.13)/0.8 = 0.275 â†’ t 0.775 â†’ W 0.79 â†’ world 0.44Â·0.79 = 0.347 â‰¥ 0.28 (F 0.315 âœ“);
  crown 0.088 tucks under the neck+head base. Seated body 0.5â†’0.6 (root center 0.25, spans
  [âˆ’0.05, 0.55]): pivot (Â±0.24, 0.47) â†’ t 0.867 â†’ W 0.744 â†’ world 0.252 â‰¥ 0.24 âœ“; crown flush under
  [0.50, 0.60]. Effects verified by inspection: hip pivots (standing âˆ’0.25, seated 0.06) stay inside
  the taller bodies; female skirt top (âˆ’0.22/âˆ’0.13 â€¦) still overlaps the waist; `ApplyRaceLook` scales
  root + silhouettes equally â†’ coverage invariant. CONFIRMED.
- H4 â€” drop the pivots 10â€“15 cm (standing 0.35â†’0.20, seated 0.47â†’0.36): best margins but moves every
  arm/hand/weapon chain and re-risks the driving cutscene reach. REJECTED (kept as the C-option).
- Verdict: SHIPPED in `1e4` (H3). Silhouette arrays unchanged from the 1e4 WIP â€” the fix was purely
  the two `MakePart("Body"â€¦)` size/pos lines + the corrected doc text. No CLI build (rule 3); user
  compiles in Unity. Play-test: shoulder seams stand/seated, longer-torso waist/shirt-hem look, race
  spread > 1.2 coverage, skirt seam, ponytail clearance.

---

## 1e3 â€” hair refit: scalp cap that hugs the skull, not floating slabs (SHIPPED in `1e3`)

User: "need a way to fix the hair since its not fit the head at all". Chose "cap that hugs the
skull (Recommended)". During implementation I found the RECOMMENDED nesting-under-Head variant has a
hidden trap; resolved by position-fitting instead (same hug, zero hierarchy risk) â€” recorded here.

### Step 1 â€” measured the float, disproved the nesting shortcut
- Standing head centre (0.65, r 0.15, crown ~0.81 with cranium dent); the old crown slab sat at y
  0.88 â†’ floated 0.04â€“0.07 above the scalp. Sides (Â±0.19) and nape (âˆ’0.16z) also missed the shell.
- H1 â€” nest hair under Head with head-local offsets. FOUND BROKEN: parenting under Head multiplies
  every child localScale by Head's `localScale` (0.3 for the standing head, and per-race `headScale`
  later) â†’ the same world look would need local sizes â‰¥1.0 (0.32/0.3 â‰ˆ 1.07), ugly and fragile, and
  a solid ellipsoid "cap" can NEVER hug a sphere from above without either floating above the crown
  or covering the eyes (a shell cap with a face window needs a cutout our solid parts can't make;
  the crescent between eye-line y 0.76 and crown y 0.81 is only 0.05 tall). REJECTED nesting; kept
  parents (torso/root).
- H2 â€” the "scalp cap" IS best built as a **thin oblate lens**: sized so its widest band sits on the
  crown at the PLATEAU (skull top pierces the lens's lower half, upper rim rises ~0.05 â€” attached,
  no float), with front rim ry around 0.10 at the eye line (rz â‰ˆ 0.10 < eye z 0.155 â†’ face clear).
  That's just an ellipsoid with the RIGHT size/position. ACCEPTED: values tuned per head (standing/
  sit crown 0.80â€“0.92, seated 0.87) from actual head hull numbers, not guesses.

### Step 2 â€” measured-value table (standing shown; seated/sit scaled for their heads)
- `Hair` lens 0.36Ã—0.12Ã—0.30 @ (0,0.80,0): rim at y 0.74, half-x 0.18 â†’ at y 0.755 (eye top) the
  lens radius is ~0.12x/0.10z << eye Z 0.155 â†’ eyes clear; at the crown it covers to rx 0.177 â‰¥
  cranium 0.162 â†’ crown buried with margin. Verdict: the "cap hugs" claim is now arithmetic, not vibes.
- `HairL/R` Â±0.18 @ y 0.70: inner face 0.14 < skull side 0.141 â†’ 1 mm buried; outer 0.22 = visible
  hair mass. `HairBack` @ (0,0.66,âˆ’0.16) w 0.11: inner face buried 0.05 into the back shell, outer
  protrudes 0.05 = nape volume. `Band`/tail re-anchored on the same numbers.

### Step 3 â€” verification
- Grep + reread (rule 3): 18 hair MakePart lines updated; no consumer outside the builders touches
  `Hair*`/`Ponytail*` names except the `headScale` prefix check (unsafe to change) and NPC hairdos
  (MakeBlock, separate). No hierarchy change â†’ zero contract risk.
- Left OPEN for play-test: lens thickness reads "thick hair" not "helmet"; side/nape panels blend at
  the skull seams; big-head races (headScale) keep the relative fit (numbers scale with Head because
  both head and hair get the same ratio recursion scaling).
- Verdict: SHIPPED in `1e3`. No CLI build (rule 3); user compiles in Unity.

---

## 1e2 â€” shouldered torso silhouette: torso/chest stop being ellipsoids (SHIPPED in `1e2`)

After 1e1, the user reported "the torso still has gaps, change the shape" and separately that the
hair "doesn't fit the head at all". 1e2 = torso; 1e3 = hair.

### Step 1 â€” why 1e0's dents couldn't close the remaining gaps
- H1 â€” the shelf/flare dents just need to be stronger. COMPUTATION splits the hypothesis: dents push
  a corner along its RADIAL by â‰¤ `Strength` (0.15 unit). The standing shoulder pivot in unit space is
  (Â±0.28/0.44, (0.36âˆ’0.05)/0.6) = (Â±0.64, 0.52) â†’ radius â‰ˆ 0.82 against a 0.5 sphere. Even a corner
  at the shoulder cap (radial 0.5) can only reach 0.65. CONFIRMED: dents can never bridge the shell.
  REJECTED as the fix.
- H2 â€” tuck pivots further in. Rejected: shoulder x was already tucked 0.33â†’0.28; going to â‰¤0.15
  breaks arm/body proportions and misplaces the whole arm/weapon reach.
- H3 â€” REPLACE the ellipsoid with a shouldered torso silhouette whose shoulder band physically
  reaches Â±0.8 unit (world 0.35 at size.x 0.44). ACCEPTED (user: "change the shape"; chose
  "moderate athletic"). Because world half-width = `size.xÂ·W`, keeping size vectors means the reach
  scales automatically for all variants (reasoned through each: standing pivot needs â‰¥0.64Â·ux,
  seated 0.71, sit-chest 0.64, sit-torso clamped top 0.70 â†’ one 0.80 Body curve + 0.72 Chest +
  0.70 SitTorso covers all).

### Step 2 â€” the shape
- Kept the 1dx visual language: 12 segs Ã— 7 flat bands, same jitter seeds â†’ watertight mosaic
  identical in feel to the limbs. Reused the mosaic emission block verbatim from `Generate`.
- TOP: instead of a pole, a flat **shoulder plateau** disc (W 0.80 held to t=1). The `"Cylinder"`
  neck passes through its middle; the plateau annulus reads as the collar ring, and the neck base is
  half-buried so no see-through sneaks around the pirce point (the neck cylinder occludes the cap's
  centre triangles). Collar-vs-hat rim flagged for play-test.
- BOTTOM: closed cap at the waist/hip ring (female skirt + male pants cover it anyway).
- Height still spans y Â±0.5 â†’ `size.y` scaling unchanged; the torso is the ONE mesh that breaks the
  [-0.5,0.5] half-cube doc contract (shoulders 0.8) BY DESIGN â€” updated in game-design.md and the
  class doc rather than hiding the exception.
- Sit model: two stacked parts (Torso + Chest). Chest gets a mid-band 0.72 plateau because the sit
  shoulder pivot (0.25, 0.40) lives ~at the Chest's vertical centre, not near its top â€” verified the
  pivot's unit height (0.40âˆ’0.42)/0.28 = âˆ’0.07 â†’ tâ‰ˆ0.43 â†’ Chest silhouette 0.72 â†’ world 0.28 â‰¥ 0.25.
  SitTorso's top 0.70 keeps the hip band covering sit hips (Â±0.13, bolted by hip ball).

### Step 3 â€” joint balls + pivot pucks
- Shoulder/hip balls shrank ~15% so they sit EMBEDDED in the plateau/hip band instead of poking free
  (the plateau skin now reaches past their centres). Standing JShoulder 0.16â†’0.14, JHip 0.15â†’0.13;
  seated 0.14â†’0.13, 0.13â†’0.12; sit 0.15â†’0.13, 0.14â†’0.13.
- Shoulders dropped â‰¤1 cm so the ball's top sits under the plateau: standing 0.36â†’0.35 (unit height
  0.5 = plateau row), sit 0.41â†’0.40; seated stayed 0.47 (already inside, unit 0.44 â†’ tâ‰ˆ0.94). The
  ~1 cm drop is arm-chain-following, reach-length-neutral; flagged for play-test.

### Step 4 â€” verification
- Grep + reread (rule 3): three profiles gone from `_profiles`; no other caller references them
  (`MakePart` strings only live in the three PlayerModels builders); `BuildTorso` routed for exactly
  Body/SitTorso/Chest; all tucks/ball sizes single-edit verified; the Emit doc-header fix confirmed.
- Left OPEN: plateau collar read, extreme race spread (>1.3) shoulder float, skirt/waist cap seam.
- Verdict: SHIPPED in `1e2`. No CLI build (rule 3).

---

## 1e1 â€” neck switched from pillar to a cylinder (SHIPPED in `1e1`)

Follow-up to 1dz. User: "change the neck into cylinder". This REPLACES the 1dz square pillar
(removed) rather than layering on top of it â€” no clarifications needed: a cylinder is a round column.

### Step 1 â€” mesh design
- H1 â€” taper/reuse the pillar's box-scale silhouette, just round the shaft in 8â€“12 facets.
  ACCEPTED (12 facets = same band count as the body's mosaic â†’ consistent flat-shading scale).
- H2 â€” soft-shaded lathe (many segments, smooth normals). REJECTED: fights the flat-facet look
  established in 1dx; the pre-1dz round neck was already rejected as a plain sphere, a smooth
  cylinder would read as a leftover primitive.
- Winding: sides + caps all go through `EmitQuad`/`EmitTriangle` per-panel centroid flips â€” no global
  orientation assumptions (same reasoning as 1dx/1dz). Verified by hand for the top cap (normal +Y)
  and bottom cap (normal âˆ’Y) before relying on the flip logic.
- Top/bottom caps use the pole point duplicated per triangle (fan) â€” watertight, no polar cap gap.

### Step 2 â€” cleanup + why removal is safe
- `BuildPillar` + `BuildBox` + the `"Pillar"` profile were only reachable from the neck (grep showed
  the only `"Pillar"` consumers were the three `Neck` MakePart calls + docs). Swapped the profile key,
  deleted the dead generator + box helper â†’ no orphan code, no fallback risk. Documented the 1dz
  removal as a supersede note rather than deleting the PROGRESS entry (record authority).
- Sizes kept identical to the pillar (standing 0.15Ã—0.16, seated 0.13Ã—0.10, sit 0.14Ã—0.12): the unit
  cylinder's width = size.x (it spans Â±0.5 in xz), so the column now has radius size.x/2 (0.075) â€”
  slightly slimmer than the pillar's 0.09 shaft but reads as a proper neck; head/torso overlap
  unchanged (same height envelopes as verified in 1dz).

### Step 3 â€” verification
- Grep + reread (rule 3): zero remaining `Pillar`/`BuildPillar`/`BuildBox` references under
  `Assets\Scripts`; `"Cylinder"` profile + `BuildCylinder` + Generate branch present; three `Neck`
  MakePart calls use `"Cylinder"`. No API/name/collider changes; the `Neck` GameObject name kept â†’
  race-ratio counter-scale + all consumers untouched.
- Left OPEN for Unity play-test: 12 facets on a ~0.1-scale neck reads "cylindrical" vs "octagon";
  cap shading vs head shadow; overlap flushness under the head.
- Verdict: SHIPPED in `1e1`. No CLI build (rule 3) â€” verified by grep + reread; the user compiles in
  Unity.

---

## 1e0 â€” seal the torsoâ†”limb gaps (SHIPPED in `1e0`)

Follow-up to 1dy. User: "the torso and limbs has gaps either change the shape of torso". Chose:
all junctions (shoulders + hips; elbows/knees flush already verified), reshape + tuck pivots.

### Step 1 â€” why the gap appeared
- The 1dw ellipsoid swap rounded the torso corners; the old shared cube's sharp top/side corners
  physically covered the arm-attach region, masking the shoulder pivot offset. The dent mosaic kept
  the rounded silhouette â†’ surface receded from the pivot.
- Quantified (by reread of sizes/positions, standing): Body ellipsoid radii (0.22, 0.30, 0.125) at
  centre (0, 0.05); shoulder pivot (Â±0.33, 0.37). Radius along the pivot direction u=(0.67,0.74):
  1/sqrt((0.67/0.22)Â²+(0.74/0.30)Â²) = 0.255; pivot distance 0.418 â†’ gap â‰ˆ 0.16 world units; the
  0.13 (r 0.065) JShoulder ball covered < half â†’ visible pinch. 1dy's narrower torso worsened it
  (0.25â†’0.22 half-extent).

### Step 2 â€” fix design decision
- H1 â€” enlarge the joint balls enough to bridge everything. REJECTED as the sole fix: would need
  balls much bigger than the limbs â†’ bloated knobs, not seams.
- H2 â€” only reshape the torso (bulge skins out to the pivots). REJECTED as the sole fix: dent push
  is bounded (radial, â‰¤ ~strength), physically can't reach the pivots that sit past the surface.
- H3 â€” three-part stack: (a) torso **shelf dents** bulging the skin toward the pivots, (b) tuck the
  pivots in a few cm so they land inside the (bulged) surface + ball radius, (c) enlarge the
  shoulder/hip balls slightly so they lap the seam. ACCEPTED â€” each part is modest, together they
  overlap rather than leave a gap. Proportions kept ball â‰ˆ limb cross-section (shoulder 0.16 vs
  upper arm 0.12/0.26 tall â€” reads as a shoulder ball in a socket).
- Dent placement care: shelf anchors at unit Â±(0.34, 0.34) with radius (0.28,0.22,0.26) â†’ influence
  at chest centre â‰ˆ 0 (offset magnitude beyond radius) â€” verified by reread of the Dent math, so the
  chest/waist profiles don't bloat. Hip flare anchors Â±(0.26,-0.36) keep the pelvis subtle for the
  female skirt + male trunk.

### Step 3 â€” pivot tuck safety
- Tucked only the spawn POSITIONS of Shoulder/Hip pivots; names, rotations, and all child chain
  offsets untouched. PlayerAnimator does rotation-driven DoF â€” the ~2â€“4 cm origin shift is within
  the swing envelope. WeaponRigBuilder measures shoulderâ†’hand reach from these pivots; the shoulder
  tuck shifts reach by < 4 cm (arm length chained off the pivot) â€” acceptable, flagged for play-test.
- Race ratios: `ApplyRaceRatioRecurse` sets pivot `localScale`/`localPosition` (spread, arm, leg)
  AFTER build â€” it multiplies into whatever base we baked, so the tuck and ratio spread compose.
- The shelf bulge is on the SHARED `"Body"` mesh: female/seated benefit too; ratio-scaled bodies
  scale the same unit mesh â†’ proportionally the shelves belong to the same silhouette family.

### Step 4 â€” verification
- Grep + reread (rule 3): shelf+flare dents only on `"Body"` + `"SitTorso"`; shoulder position values
  per builder distinct + tucks applied (0.33â†’0.28 etc.); JShoulder/JHip sizes bumped per plan (0.13/0.16,
  0.12/0.14, 0.13/0.15; hips 0.14/0.15, 0.12/0.13, 0.13/0.14); no other profiles touched; no API/
  name/collider change. One near-miss walked: an earlier JHipL edit's oldString matched two builders
  (sit 0.13 and standing-onâ€¦ actually stood 0.14 / seated 0.12 / sit 0.13 â€” the sit one collided only
  with the seated 0.13 after seated edit? no â€” it matched once, DISAMBIGUATED by position context;
  the "multiple matches" error surfaced and I anchored with the hip localPosition line). Caught by
  the tool â€” good example of why edits want surrounding context.
- Left OPEN for Unity play-test: whether the shelf bulge reads as deliberate breadth vs a hump at
  small races; ball override on the forward-reached seated arms; first-person arm visibility at the
  shoulder.
- Verdict: SHIPPED in `1e0`. No CLI build (rule 3) â€” verified by grep + reread; user compiles in
  Unity.

---

## 1dz â€” neck switched to a pillar shape (SHIPPED in `1dz`)

Follow-up to 1dy. User: "the neck switch to pillar shape". No clarifying questions asked â€” "pillar"
in this block-style game reads as a straight square masonry column, and switching just the neck's
mesh keeps every contract intact.

### Step 1 â€” what shape is a "pillar"
- H1 â€” a plain box (the old pre-1dw cube) for the neck. REJECTED as the sole option: identical to the
  legacy cube, reads as a regression rather than a deliberate column.
- H2 â€” a round cylinder column (lathe). REJECTED: "pillar" here implies square masonry (the world's
  pillars are box/square); a cylinder is "column", and it'd fight the flat-shaded block teshetic.
- H3 â€” a **square masonry pillar**: foot slab + straight shaft + cap/abacus, all flat-faced.
  ACCEPTED: unmistakably a pillar, still occupies the [-0.5, 0.5] unit cube â†’ `localScale` = size
  vector contract unchanged, cache still keyed by profile, race ratios transform-only.
- Taper idea (entasis): a true frustum needs non-axis-aligned faces â€” extra generator code, no real
  gain at neck scale. Dropped; straight shaft between the slabs already reads as a pillar.

### Step 2 â€” mesh build
- Reuse `EmitQuad` for each of the 6 faces of three stacked axis-aligned boxes. EmitQuad already does
  the per-panel outward winding check against the panel centroid â€” for a box face the centroid dot
  its outward face normal is positive, so orientation is automatically correct with NO new winding
  logic (verified by reasoning, not by build â€” rule 3).
- Interior faces between slabs (base-top/shaft-bottom, shaft-top/cap-bottom) are emitted but buried
  inside solid geometry: backface-culled and invisible, so the mesh is fully closed and watertight
  with zero special-casing.
- Profiling: 18 quads â‰ˆ 72 verts â€” trivial; cached once under `"Pillar"` like every other profile.

### Step 3 â€” builders
- Only the `Neck` `MakePart` calls change (profile `"Pillar"`, sizes bumped so the column reads:
  standing 0.15Ã—0.16, seated 0.13Ã—0.10, sit 0.14Ã—0.12). GameObject NAME stays `"Neck"` â€” the name is
  what `ApplyRaceRatioRecurse` matches for head/neck counter-scaling and what `PlayerAnimator`-adjacent
  lookups use; changing it would break the race ratios. The old `"Neck"` dent profile stays in the
  dict (harmless, documented unused).
- Overlap checks (neck top vs head base, computed by reread): standing neck spans y [0.32, 0.48],
  head base 0.50 â†’ clean 0.02 gap; seated spans [0.50, 0.60], head base 0.60 â†’ flush;
  sit spans [0.56, 0.68], head base 0.63 â†’ 0.05 tuck (invisible under the head). Acceptable.

### Step 4 â€” verification
- Grep + reread (rule 3): `"Pillar"` profile present; `Generate` routes to `BuildPillar`;
  `BuildBox` call sites = 3; the three `Neck` MakePart calls use `"Pillar"`. No public API change;
  `BuildEllipsoid`/cache/fallback untouched; the pillar is a separate cached mesh keyed by profile.
- Left OPEN for Unity play-test: whether foot/cap slab proportions read at neck scale (~0.1 world
  height) or want slightly thicker slabs; race-ratio counter-scale of a box neck (visual only).
- Verdict: SHIPPED in `1dz`. No CLI build (rule 3) â€” verified by grep + reread; the user compiles in
  Unity.

---

## 1dy â€” slimmer torso, +8% overall size, faceted ball joints at limb pivots (SHIPPED in `1dy`)

Follow-up to 1dx. User: "reduce the torso width abit then increase the total size, add sphere with
similar skin generate to between parts as joints". Clarified: moderate width cut (âˆ’12%) + +8% whole
model, joints colored to match the adjacent part, limb joints only (shoulder/elbow/hip/knee), applied
to standing + seated + sit variants.

### Step 1 â€” how to make the model bigger without breaking the grounded feet / car-fit
- H1 â€” raise each part's size vector by the factor by hand. REJECTED: ~40 MakePart call sites across
  three builders, easy to miss; feet re-plant (ApplyRaceLook) and the car/chair fits would drift in a
  piecemeal way.
- H2 â€” a single root `localScale` multiplier (a module const `PlayerModelScale`) applied to every
  root's scale AND position, plus the same factor in the standing `calibrateFeet` re-plant. ACCEPTED:
  scale cascades to every child offset + part size exactly like the old unit-cube contract; one
  constant to revert is the car-cutscene clipping fallback; human (K=1) is the old no-op shape.
  Caveat logged: `ApplyRaceLook` reads `modelRoot.localScale` (already includes K) for Height/Bulk so
  ratios compose correctly; only the y re-plant needed the K factor.
- H3 â€” change the `scale` default/arg of the builders. REJECTED (mostly): all callers pass scale=1;
  scaling a position/root would still need the calibrateFeet factor, and cutscene parents would
  silently drift. K baked in the builders matches the "make the model bigger everywhere" intent.

### Step 2 â€” torso width
- Standing male `Body` x 0.50â†’0.44, female 0.46â†’0.40 (âˆ’12%). Kept shoulders at Â±0.33 â†’ arms sit a
  touch wider than the torso; the shoulder JOINT balls (1dy) land exactly there, which is what makes
  the wider-than-torso arms read as articulated shoulders rather than a bug. Seated `Body` 0.38â†’0.34
  and sit `Torso` 0.42/0.46â†’0.37/0.40 + `Chest` 0.44â†’0.39 proportion the same cut. No neck-joint
  sphere (user: limb joints only) â€” the Neck part already bridges head/torso.

### Step 3 â€” the joint spheres: placement, naming, layer routing
- Placement: `AddJoint(...)` parents a `"Joint"` profile part at the pivot transform's ORIGIN
  (localPosition zero) â€” zero pose work, rotates with the pivot in every animator/cutscene pose.
  Sizes tuned per joint around the arm/leg cross-section (shoulder 0.13, elbow 0.11, hip 0.14,
  knee 0.12; slightly smaller in the seated car model so the profile stays dainty in the car).
- Naming: `JShoulder/JElbow/JHip/JKnee` â€” the `J` prefix guarantees no collision with
  `PlayerAnimator` (`Torso/ShoulderL`, `ElbowL` under shoulder), `WeaponAnimator.FindOwnerShoulder`,
  `WeaponRigBuilder`, or `ApplyRaceRatioRecurse`'s Head/Neck/Eye/Hair/Ponytail/Shoulder/Hip patterns.
  A `StartsWith("Shoulder")` joint would be safe as a name but I chose `Jâ€¦` anyway â€” belt and braces.
- Layer routing: `IsArmUnderShoulder` walks UP from a renderer to a `Shoulder*`/`Elbow*` ancestor;
  a `"Joint"` part on `ShoulderL` has parent `ShoulderL` â†’ layer 7 (visible first-person) exactly
  like the arm parts. Hip/knee joints on `HipL`/`KneeL` stay layer 6 (not arm chains). Confirmed by
  rereading `PlayerController.Animation.cs` (arm-chain walk is ancestor-based, so child name is
  irrelevant).
- Race ratios: `ApplyRaceRatioRecurse` scales `ShoulderL/R` (yÂ·arm, xÂ·sw) and `HipL/R` (yÂ·leg) â€”
  joints are CHILDREN of those pivots (not matched themselves), so they inherit the scaled pivot and
  stay proportional for Dwarf/Gnome/Fire Giant etc. Good.
- "Similar skin generate": reuse the SAME faceted mosaic via a new `{ "Joint", new Dent[0] }`
  profile (plain faceted sphere, no dents â€” joints are mechanical balls, not knobbly flesh).

### Step 4 â€” traps hit / caught during the pass
- First drafted the sit-builder root edit as oldString that ALSO swallowed the `female/race/color`
  declarations, replacing them with just the scaled-root lines â†’ would have been a compile error
  (`race`/`shirtC` undefined downstream). Caught on the follow-up read, restored the block verbatim in
  the same pass. Lesson: when widening an edit's oldString, re-check the following lines still get
  their declarations.
- game-design edit briefly left a stray `----- "still reads" marker -----` line and a duplicated
  "Part renderers" bullet; both removed in the same pass. (No leftover markers/dups verified on
  final reread.)

### Step 5 â€” verification
- Grep + reread (rule 3): `PlayerModelScale` used in all three builders + calibrateFeet only;
  `AddJoint` defined once, used for all 8 joints Ã— 3 builders; `"Joint"` profile exists in
  `PlayerPartMesher`; no public API/name/contract changes; no new colliders. Two method-of-record
  greps (`BuildPlayerModel(|BuildSeatedPlayerModel(|BuildSitPlayerModel(`) still resolve at their
  unchanged call sites.
- Left OPEN for Unity play-test: joint sphere sizes vs their parts (clip/float), whether +8% clips the
  car cutscene or chair seats (one-line revert via `PlayerModelScale`), and how the narrower torso +
  ball shoulders read on the small races.
- Verdict: SHIPPED in `1dy`. No CLI build (rule 3) â€” verified by grep + reread; the user compiles in
  Unity.

---

## 1dx â€” faceted low-poly player skin: chunky triangle/square mosaic with deterministic jitter (SHIPPED in `1dx`)

Follow-up to 1dw. User: the model "is currently only plain original shape" â€” wants "multiple surface
triangle, square shape generate all over the skin". Clarified: chunky low-poly facets, slight
hand-cut jitter (deterministic), all parts.

### Step 1 â€” what "triangle and square shapes all over" means technically
- H1 â€” flat-shading only: drop `RecalculateNormals()` so the existing smooth 9Ã—16 grid shows hard
  edges. REJECTED: the grid is a plain lat/long mesh â€” every band cell is already 2 thin triangles
  pointing at the poles; flat shading would read as slivers, not "triangles and squares".
- H2 â€” re-topology into a panel mosaic: chunkier lattice, each cell becomes a QUAD (square) panel or
  two TRIANGLE panels, all flat-shaded via duplicated vertices with face normals. ACCEPTED: gives
  exactly the requested mixed mosaic and a stylized low-poly read; keeps the ellipsoid silhouette
  and the 1dw dents (sculpt happens on the lattice corners first).
- H3 â€” jitter per panel with independent corners (cracked/shattered gem look). REJECTED: single-sided
  solid meshes â€” any gap between panels shows the backfaces-culled hole straight through to the
  background/collider. Instead jitter the SHARED lattice corners: boundaries warp organically, mesh
  stays watertight, and it's trivially deterministic.

### Step 2 â€” panel winding, correctness traps
- Old code had ONE global winding check on the first face. New emission is per-panel: compute the
  face normal from the (pre-swap) triangle, compare against the panel centroid's outward radial,
  swap two indices + negate the normal when inward. This is safe for the pole fans too (south fan is
  emitted in the same loop direction as north â€” the per-panel flip absorbs the handedness), and for
  quads the single averaged normal is oriented consistently with the diagonal-triangle order used for
  the index buffer.
- Quad "square" read: both triangles share one flat normal, so the light seam that would split a
  quad into two shaded triangles disappears â€” it renders as one square. Coplanarity is only
  approximate on a curved ellipsoid cell (they're not planar), but at chunky scale with a shared
  normal this is invisible.
- Determinism: `Hash01(x, seed, salt)` (integer hash â†’ [0,1)) with `AnchorSeed(profileId)` means the
  facet layout is byte-identical across sessions/processes â€” no `UnityEngine.Random` (results would
  change every play session and every rebuild). Cache stays keyed by profile only.
- CS0136-class shadowing audit (learned from 1dv): the method uses `s` (seg counter), `s1`, `b`,
  `lat`, `k`, `n`, `sm`, `h1/h2`, `t1/t2`, `iv`, `nn` â€” no redeclaration of an outer variable in the
  same block; the split branch redeclares nothing that collides. `0x5EEDF`/`0xCAFE` are plain int
  salts â€” fine.
- Latency: per-panel vertex duplication â‰ˆ 400 verts / ~500 tris per part (quads ~47Ã—4 + split tris
  ~50Ã—3 + caps 24Ã—3), built once per profile and cached â€” trivial.

### Step 3 â€” verification
- Grep + reread (rule 3): `MakePart`/`BuildEllipsoid`/cache/profiles untouched; `RecalculateNormals`
  gone from `PlayerPartMesher`; the three `MapBuilder` builders + all part/pivot names unchanged;
  parts still span [-0.5, 0.5] so `localScale` sizing (race ratios, weapon hand-scale) is intact.
- Left OPEN for Unity play-test: whether 6 bands Ã— 12 cells reads "chunky" enough at every part size
  (the Head at 0.3 has the densest-eye region â€” hang looser than chunky on small parts), and whether
  35% triangle split vs squares feels right in-game.
- Verdict: SHIPPED in `1dx`. No CLI build (rule 3) â€” verified by grep + reread; the user compiles in
  Unity.

---

## 1dw â€” smooth player model: ellipsoid part surfaces with dent sculpt (SHIPPED in `1dw`; generator superseded by `1dx`)

Request: player model is "too blocky" â€” keep the existing spine/pivots for animation, generate the
visual surface of each part as an ellipsoid shape, and sculpt each part with multiple dents like the
terrain dents. Follow-up: MUST account for race-ratio differences.

### Step 1 â€” how to scope it without breaking the animated rig
- Constraint A (user): keep the spine/bone pivots so `PlayerAnimator`/`WeaponRigBuilder`/cutscenes
  don't move; only the visible surface changes.
- Constraint B: race ratios (`ApplyRaceLook`/`ApplyRaceRatioRecurse`) size parts through
  `Transform.localScale`; `RaceRig` scales the whole root; `WeaponRigBuilder.ScaleForHandScale`
  compensates hand size for draw/stow. If the mesh baked a fixed radius, every one of these breaks.
- H1 â€” replace each cube's MeshFilter with a pre-sized ellipsoid mesh (radius = size vector) at build
  time. REJECTED: bakes sizes into the mesh, so race/ratio/gender/weapon scale changes would need a
  mesh rebuild per part and the cache becomes size-dependent; also the older "ratios only move
  Transforms" invariant (1co) would be violated, and weapon-hand compensation targets a scale that
  would now be double-applied (mesh radius + localScale).
- H2 â€” unit-space ellipsoid (occupies the same [-0.5, 0.5] cube) + keeping the exact old size vectors
  as `localScale`. ACCEPTED: `localScale = old size` reproduces identical world dimensions, so every
  consumer that multiplies/averages part scales keeps working with zero change. Cache key becomes pure
  profile id; gender/race/model variants share one mesh per part.
- H3 â€” sculpt = hard boolean carve. REJECTED: adds per-vertex inside/outside tests, risky winding, no
  analogue in the codebase. Generalizing the existing terrain dent (`WorldStreamer.DeformAt`) was the
  natural fit (same codebase language, same smoothstep, only the falloff goes ellipsoid in 3D instead
  of a heightfield crater) and keeps meshes manifold.

### Step 2 â€” sculpt math = the DeformAt carve, generalized
- DeformAt: for a tile, influence from normalized distance to a pit center, `s = tÂ²(3âˆ’2t)`
  smoothstep, then height is lowered along the terrain normal. Here: per-vertex influence from
  normalized ellipsoid distance `n = sqrt((dx/rx)Â² + (dy/ry)Â² + (dz/rz)Â²)`, `t = 1 âˆ’ clamp01(n)`,
  and the vertex is pushed along its ORIGINAL radial (so positive strength bulges out, negative
  carves in â€” the same sign convention as height up/down). Anchors/radii live in unit space and scale
  linearly with the part (visual check: eyes seated in the Head's dents at Â±(0.13, 0.02, 0.16) align
  with the EyeWhite/EyeIris positions at zâ‰ˆ0.155â€“0.165 of a 0.3-wide head â€” they sink consistently).
- Winding: sphere-grid generation could come out inside-out depending on Unity's handedness reading.
  Cheap guarantee: compute the first triangle's normal, dot against the first vertex position; flip
  the whole index list if it points toward the origin. Confirmed correct against Unity's
  counter-clockwise-front convention and cheap enough to leave always-on.
- After sculpting, `RecalculateNormals()` (the DeformAt path already does the same) so the smoothed
  surface gets correct normals; `RecalculateBounds`.
- Eye parts: old "EyeWhite" was a flat box (0.09 x 0.07 x 0.03) on the head front. As an ellipsoid
  it would stick through the dents, so EyeWhite/EyeIris get a front-bulge dent only (thin disc look);
  the head's sockets accept them.

### Step 3 â€” caching and threading
- Meshes are Deterministic/static; built lazily on first `MakePart`. H4 â€” cache per profile in a
  static Dictionary with hideFlags HideAndDontSave (like `SharedCubeMesh`, which the player no longer
  uses). ACCEPTED: N profiles â†’ â‰¤ N meshes ever exist; no per-part allocations beyond transforms.
  Main thread only (model builds are already main-thread).
- Mesh selection: `PlayerPartMesher.BuildEllipsoid(profileId)` returns the shared instance; each part
  GameObject is just a MeshFilter/MeshRenderer + Transform. This mirrors how `SharedCubeMesh` +
  `MakeBlock` worked, so `MakePart` is a drop-in sibling with an extra profile arg and no collider.

### Step 4 â€” verification / dead ends
- Grep: no `MakeBlock` remains in `MapBuilder.PlayerModels.cs` (all player parts â†’ `MakePart`);
  part names/pivots/sizes/rotations preserved verbatim (PlayerAnimator shoulder/elbow/hip/knee
  chains, WeaponRigBuilder hand lookup, WeaponAnimator.FindOwnerShoulder, layer 6/7 culling,
  ApplyRaceRatioRecurse name matching Head/Neck/Eye/Hair/Ponytail and Shoulder/Hip) â€” all still
  resolve. Race ratios remain transform-only; nothing bakes a size-derived radius.
- Dead end walked: considering a `Dent` as a reusable struct array with per-profile static fields was
  fine but Dictionary-of-arrays is terser and grep-checkable; no GC impact (static init once).
- Left OPEN for Unity play-test: proportion tuning (dent strengths/radii are hand-tuned, one pass),
  the eye-dent alignment on non-Human `BodyHead` ratios (head counter-scale scales dents too â€” expect
  them to stay proportional), and overlap/Z-fighting where hair, skirt and hands sit on bodies.
- Verdict: SHIPPED in `1dw`. No CLI build (rule 3) â€” verified by grep + reread; the user compiles in
  Unity.

---

## 1dv â€” chunk mesh pooling: one Mesh per chunk + capped freed-mesh pool (SHIPPED in `1dv`)

Second half of the 1dq deferral ("deferred to 1dt (noise memo) + 1dv (mesh pooling)"). Scope B per
user choice (persistent mesh + reload pool; NOT reusing the merged CPU arrays too).

### Step 1 â€” where the allocation churn actually is
- `ApplyMerged` was the single mesh constructor: `new Mesh` + `UploadMeshData(false)`, then
  `Destroy(previous)`. Callers: boot sync chunk, `CreateChunkGameObject` (every chunk fill/reload),
  `FullRebuildChunk` (whole-chunk rebuilds: slab chunks, seam/border reconciles, deform fallback
  when a chunk holds flat tiles or a patch nearly covers it).
- The deform FAST path (`PatchRegion`) already mutates `_mf.sharedMesh` + the retained `_merged` CPU
  arrays in place â€” it never allocated. So the honest churn is FullRebuildChunk rebuilds + chunk
  unloadâ†’reload (walk far). Volume is modest: this is a GC/GPU-fragment micro-opt, not a lag fix
  (1di/1dq already took the real physics cost).
- H1 â€” size-keyed pool: REJECTED. Every chunk mesh is the same shape (~961 verts / ~1800 tris; slab
  walls may add verts), so ONE unordered pool suffices â€” no buckets.
- H2 â€” per-chunk persistent Mesh + return to a capped pool on Release. ACCEPTED: covers rebuild churn
  (same instance re-uploaded, no Destroy) AND reload churn (freed mesh reused by the next chunk). Cap
  48 bounds retained GPU memory; overflow destroys.

### Step 2 â€” pitfalls found while writing it
- **Overwrite-only pooling is safe** because every upload re-specifies ALL arrays (UploadMerged runs
  the full setter sequence) â€” a reused buffer is never left with stale index/vertex counts. A slab
  wall chunk uploads more verts â†’ buffer grows; a later plain chunk uploads fewer â†’ array length
  truncates; Unity retains the larger GPU allocation (memory retention, no corruption).
  - **PARTIALLY REJECTED by runtime evidence (1dv follow-up fix):** Unity's typed channel setters do
    NOT let the VERTEX COUNT shrink via re-specification alone â€” the mesh retains its previous larger
    vertex count, so a pooled mesh that held more verts (slab walls) than the incoming upload threw
    `SetNormals/SetUVs/SetColors "is out of bounds"` on the first smaller re-upload. The fix keeps
    overwrite-only semantics but adds `if (mesh.vertexCount != md.Vertices.Length) mesh.Clear();` at
    the top of `UploadMerged` â€” Clear resets every channel buffer to zero so the setters grow them
    fresh to md's size; the same-count hot path stays fully allocation-free. Memory-retention note is
    still true (Clear may drop/rebuild the GPU allocation on downsize â€” the old transient double-buffer
    cost, but only when counts change, not on the common identical rebuild).
- **COLLIDER RE-COOK TRAP (the important one):** old code swapped `sharedMesh` to a NEW instance each
  apply, implicitly re-cooking the MeshCollider. With a persistent shared instance the reference never
  changes, and a MeshCollider does NOT republish its baked physics mesh on vertex mutation â†’ a
  FullRebuildChunk on a collider-active chunk would update visuals but keep stale physics (player
  falls through a rebuilt wall). Fixed with the explicit nullâ†’assign used by `PatchRegion`
  (`_mc.sharedMesh = null; _mc.sharedMesh = _mesh;`).
- **Threading:** the pool is main-thread only by construction (ApplyMerged + Release both run on the
  main thread); documented, no lock.
- Verdict: H1 rejected, H2 confirmed (with the vertex-count shrink correction above). Shipped in `1dv`
  plus a follow-up fix commit. No behavior change expected â€” play-test that rebuilt collider terrain
  matches visuals (the catch above), a slab-walled chunk deforming flat handles the downsize, and
  F12/reset loops stream cleanly.

## 1du â€” prop-ring keep-alive + shared dent-debris cube (SHIPPED in `1du`)

Follow-up micro-opt from the 1di/1dq/1dt sweep. Two independent findings, two one-file fixes.

### H1 â€” prop ring destroy/respawn churn at ring equilibrium
- Walking the ring edge toggles chunks in/out constantly. Old behavior: leave â†’ `ReleaseProps` destroyed
  the spawned GOs; re-enter â†’ `BeginProps` re-rolled the full deterministic stream (~2 destroy + a
  900-tile re-roll per chunk toggle). That churn was the last per-frame hiccup left at the edge.
- CONFIRMED + fixed: keep-alive. `ReleaseProps` deactivates (`SetActive(false)`), `BeginProps` reactivates
  the same GameObjects; the RNG/tiles/cursor survive so no re-roll. `_propActive` becomes the single
  "props visible" flag, `PropsOn` mirrors it, `PropsPending` additionally requires a live stream, and
  `Release()` (chunk unload) still destroys outright.
- Behavior delta vs the `1di` doc: a chopped prop is a destroyed GO leaving a null `_props` slot; the
  preserved stream cursor is already past that tile, so re-entry does NOT respawn it (1di: documented
  deterministic respawn). Kept â€” "the world stays as I left it" is the friendlier read. Play-test item.
- Caller audit (grep): the five members are touched only by `SyncPropRing`/`StepChunkProps`
  (WorldStreamer.Props.cs) and `ChunkObject` itself. No wider surface to keep old semantics for.

### H2 â€” CreatePrimitive dent debris per piece
- `SpawnCraterDebris` called `GameObject.CreatePrimitive(PrimitiveType.Cube)` 3â€“5Ã— per dig/cast.
  Fact-check for the draft comment: CreatePrimitive does NOT allocate a fresh Mesh (Unity's built-in
  cube mesh is shared), so the original "allocates a fresh cube Mesh per piece" claim was WRONG. The real
  saving is skipping per-piece GO + MeshFilter/MeshRenderer initialization â€” modest, harmless.
- CONFIRMED + fixed: one shared inactive template (`SharedDebrisCube`) + `Instantiate`.
- REGRESSION caught in review: the template strips the BoxCollider, so clones (Rigidbody, no collider)
  **tunnel through the terrain** instead of landing in the crater. OLD debris had per-piece BoxColliders
  and thud-crumbled in the pit (the 1de "explode like pickaxe" look). User chose "collider-less is fine":
  accepted â€” brief up-burst, sink below-ground, `Destroy` at 2.5 s. Documented honestly in PROGRESS.
- Verdict: H1 + H2 confirmed; both shipped in `1du`. H2 carries an accepted visual trade-off.

Follow-up to 1dq (which left background mesh/noise work untouched by design). Second pass over the
costliest CPU: `BuildMergedMeshData`.

- Count: chunk = 900 tiles, 4 verts each â†’ **3,600 `TerrainBandColor` calls per chunk**, each
  re-running `GetHeight` (5 octaves â†’ ~18,000 `PerlinNoise`), plus pass1/pass3 `EdgeHeights` noise
  fallbacks for out-of-chunk borders, plus separate 961 corner-grid samples upstream in the chunk
  build.
- Observation: colors key on the **pristine noise surface** at world corner coords; those corners are
  pure functions of (seed, x, z). A chunk owns at most 31Ã—31 = 961 distinct corners. => memoize.
- H1 â€” reuse `tile.Data.Heights` (corner grid) instead of re-sampling noise: REJECTED. Heights on a
  deformed tile are the SAVED height (== vertexY), so colors would read depth â‰ˆ 0 everywhere â†’ all
  grass. The code deliberately compares against the pristine surface so a dug tile shows strata.
  Getting this wrong would silently kill strata colors on all carved terrain.
- H2 â€” thread a per-build dictionary through the band + border paths. CONFIRMED. Cost: one
  Dictionary with â‰¤961 entries per chunk build, thread-local, GC-pausing nothing (allocated once per
  build). Determinism preserved (same seed/coords â†’ same value; memo never changes the outcome).
- H3 â€” share the memo with the corner-grid sampling in WorldStreamer.ChunkBuild: possible but couples
  two call paths; skipped for now (961 samples Ã— 5 octaves stays, the band sampling collapses).
- Verdict: 3,600 â†’ â‰¤961 GetHeight/chunk (~3.7Ã—), byte-identical colors, no public-signature break
  (PatchRegion/terrain-aim use the kept 4-arg overload).

## 1dr â€” per-frame component lookups (SHIPPED in `1dr`)

Hotspot scan prompted by "need to optimize the game even more": profile-by-reading the per-frame Update
loops (no profiler on this machine; rule 3 = read + grep).

- **PlayerController** was the champion: 14 raw `GetComponent` calls per frame across Movement/Combat/Stamina
  (MaxHP, MaxStamina, HandleMovement, HandleStamina, TakeDamage, aim paths). All target permanent
  components on the player root. Decided: lazy accessors whose null case re-scans (safe vs.
  CombatController added late by WeaponRigBuilder), because caching a null forever would silently break
  late-rigged combat.
- H1 â€” cache once in Start: REJECTED for combat-specific refs (WeaponRigBuilder adds CombatController
  after Start on first rig). Lazy accessor chosen instead.
- H2 â€” cache CombatController in Awake/Start era: same flaw. [decide: lazy]
- **Wheel/skill-bar**: per-frame `GetComponent<SpellCaster>`/`GetComponent<CombatController>`/
  `GetComponent<SkillBindings>` â€” same fix pattern (cache against player swap, MagicWheelUI already
  had that idiom for `_caster`).
- **PlayerBarsHUD status strip**: `getComponent`Ã—4 + `SetActive`Ã—10 every frame. SetActive guard fixes
  the pointless native toggles; a 3-frame poll is invisible (values are whole-second countdowns) and
  cuts the scan by 2/3. Rejected gate-by-status-change because statuses are added/removed by other
  systems with no notification channel.
- **AudioManager**: `Camera.main` per frame â†’ cached field; `Crossfade` wrote both volumes every frame
  even at full settle â†’ settle guards `> 0.0001f`.
- **ChunkLodManager**: unguarded `gameObject.SetActive(cond)` per chunk per pass â†’ activeSelf guard.
- Left alone (each already gated or event-driven): EnemyHealthBarHUD (0.5 s scan), InteractionPrompt
  (1/3-frame raycast), SittableSeat.FindNearest (bounded list), menu/modal builders (on-open).
- Verdict path: each change is semantics-preserving (null-fallback identical); the remaining greps
  confirm the one-time/event-driven frames.

## 1dq â€” "load only those with neccessity" â†’ collider-on-demand (SHIPPED in `1dq`)

User: "currently the entire everything in 30 radius is loading at the same time, so would there be a
way to load only those with neccessity without reduceing the range"; clarification: "only load the
collider of those near the player and and magic".

- **Frame of the ask:** the WHOLE radius-30 square visibly streamed in at once (3,721 chunks: 900-tile
  meshes + merged colliders + props). The user explicitly does NOT want the render range cut. So the
  "necessity" axis must be *what physics needs*, not what renders.
- H0 â€” only mesh-upload nearby, keep all 900-tile per-tile NO builds at full radius: equivalent cost to
  today at boot; H0 rejected as not satisfying "optimize".
- H1 â€” shrink the streamed square but keep radius 30: rejected by the user (would reduce the render
  range they can see / walk to).
- H2 â€” collider on demand only (1dq): stream everything, gate the merged MeshCollider. Evidence it's
  the right axis: chunk creation passes a `buildCollider` flag to the SAME path that already renders
  meshes; collider cooks (~7,200 tri/chunk) + broadphase bodies are exactly the expensive per-chunk
  physics objects, and every gameplay raycast probe (player, SpellCaster â‰¤40 m, NavGrid, Tornado,
  ToolManager, Fishing) uses ranges â‰ª 240 m â€” so radius 8 covers all of it.
- **Component risk â€” long-range spells:** a firebolt's ground probe needs the terrain beneath it to
  NOT fly clean through the far world. => SpellEffect registers the chunk it currently flies over in
  a static registry; reconcile expands by 1 chunk. Design constraint: only track ONE chunk per bolt
  (release old on each boundary crossing) so the registry can't accumulate stale coords.
- **Component risk â€” ring jump (death/new game/F12):** interior chunks of a teleported ring could sit
  collider-less => the player falls through before the ring re-promotes them. Full-map reconcile each
  poll with the `HasCollider != want` guard closes this: toggles are zero while idle, and a jump just
  flips a square of colliders once (each cooked once).
- **Component risk â€” rebuilds:** deformation/arena-lane code path re-cooks colliders via
  `FullRebuildChunk`/`PatchRegion`; both must respect the flag or far chunks get incinerated colliders
  back. => `ApplyMerged` stores the collider intent (one authoritative `_colliderActive`), rebuild paths
  read it (`buildCollider: obj.HasCollider`, `&& _colliderActive`), `Release()` clears it.
- **Boot risk:** the placeholder spawn needs a collider BEFORE the first reconcile poll. =>
  `GenerateChunkSync` keeps `buildCollider:true` (single boot chunk, one cook).
- Chosen scope: 1dq = collider gating only. Mesh uploads/noise still cost per chunk at radius 30 â€”
  deferred to 1dt (noise memo) + 1dv (mesh pooling); props already limited by `PropRingRadius` (1di).
  NOTE these are separate knobs; a future per-chunk draw budget is out of scope (rendering is GPU-bound,
  the collider change targets CPU physics).

## 1dp â€” "some magic keep switch between big and small continuously" (SHIPPED in `1dp`)

User: "some magic keep switch between big and small continuously which really fuck up the visual".

- **Symptom:** on the magic-model bench, parts of the grid visibly breathe/scale up and down â€” not
  all models, and not at a constant speed (some flicker fast, some slow-pulse).
- Evidence scan: the bench uses `SpellCaster.CreateProjectileDisplay`, which routes through the SAME
  `AttachDefaultProjectileVisual` builders as live casts. Those builders attach `OrbFx` on every
  shape (Orb/Shard/Debris/Bolt/Lance/Spear/Blade/Splash/Comet/Missile/Dart).
- H1 â€” models carry a `SpellEffect` scaling them? REJECTED: the display explicitly adds none; no
  collider/launch.
- H2 â€” pedestal/timing hack animates them? REJECTED: nothing else in NewWorldTestGround touches the
  bench models post-build (they're plain children of a labelled group).
- H3 â€” `OrbFx` is the animator. CONFIRMED: `OrbFx.Update()` recomputes `localScale = base * pulse`
  every frame with per-mode amplitudes; `Bolt` (Â±0.22, ~24 Hz Ã— 7 Hz beat) is the worst "big/small"
  offender, `Ember` (Â±0.22) solid second; `Plain`/`Shard` amplitudes (0.04â€“0.06) are the subtle ones
  the user wouldn't complain about. Spins (Shard/Swirl/Tumble) add wobble.
- Options: (a) thread an `animate=false` param through 12+ private builders to skip `AddComponent<OrbFx>`
  â€” cleanest signature-wise but a large mechanical diff touching every shape; (b) strip the components
  inside `CreateProjectileDisplay` after building (`GetComponentsInChildren<OrbFx>` + Destroy). Chose
  **(b)**: one 3-line change fully inside the display path, live casts untouched (they never call
  CreateProjectileDisplay â€” single caller is the bench). OrbFx is a private nested class, so the strip
  must stay inside SpellCaster â€” it does.
- Kept the comet-exhaust ParticleSystems on pedestal models (they don't rescale the body). If the user
  later wants a dead-calm gallery, gate `AttachProjectileParticles` too.
- Open: none. Verified by reread + grep (single `CreateProjectileDisplay` caller; `OrbFx` added only
  in `SpellCaster.Projectiles.cs`, destroyed for the display before any child Update runs).

---

## 1do â€” "there is no enemy in enemy folders, split each enemy race into a folder of each own" (SHIPPED in `1do`)

User: "there is no enemy in enemy folders, split each enemy race into a folder of each own, that
folder would contain the script of enemy from that race".

- **Symptom:** zero enemy folders exist anywhere in Assets. Grep found NO `EnemyType` enum and NO
  `enemy race` concept: every race was a bare string `EnemyId` driving ONE shared `EnemyController`
  FSM + `EnemyModelBuilder.BuildEnemy(id)` for looks. Identical default stats (50 HP / 10 dmg /
  2.5 speed) on ALL 21 races. So "the script of that race" had to be CREATED per race, not found.
- H1 â€” just MOVE the shared `EnemyController` into each race folder? REJECTED: one shared brain
  cannot also be "the script of each race" without duplication or lying folder contents; subclasses
  are the honest mapping (each folder owns a real, editable script that governs that race).
- H2 â€” keep spawners on the generic `AddComponent<EnemyController>` and ship the folders
  reference-only? User explicitly chose "Wire spawners to per-race" â€” otherwise the per-race
  scripts would be dead code. CONFIRMED wiring via new `EnemyCatalog`.
- H3 â€” where does model-building go? Kept centralized in `EnemyModelBuilder` (each race's script
  sets id; base builds model in Awake). Full per-race model split would duplicate `MakeBlock`
  plumbing and explode the diff; not what "the script of enemy from that race" needs. DEFERRED.
- Stat presets: user chose "Distinct stat presets now". Table designed along archetype lines
  (slime tanky/no-flee, golem armor/DR, dragon/demon elite, mimic stationary ambush with tiny
  aggro, bat fastest + wide perception, skeleton = pristine baseline). Numbers tunable; recorded as
  the authoritative table in game-design Â§7.1.0.
- Stat application timing: base `Awake` is `private` on the base â€” subclasses can't override it
  safely, so added `protected virtual ApplyRaceConfig()` called at the TOP of `Awake` AND in
  `ApplyEnemyId`. Field-initializer stats were an alternative (no hook) but serialized Inspector
  defaults on the base would fight them; runtime stamping is unambiguous given the project has no
  enemy prefabs/enemy scene instances (all runtime `AddComponent`).
- `EnemySpawner` trap: existing `if (EnemyPrefab == null) return null;` would have silently killed
  ALL world spawns once the brain moved to the catalog (no prefab assigned anywhere). Removed;
  prefab is now only an optional visual template with its stale brain destroyed.
- File moves preserve Unity GUIDs via `git mv` of `.meta` (EnemyController + BossController); the
  new per-race files get fresh GUIDs (no existing asset references). `Assembly-CSharp.csproj` is
  git-ignored (Unity regenerates) â€” its stale `Assets\Scripts\EnemyController.cs` entries are
  legacy leftovers, not tracked.
- Dummy regression risk: `SpawnDummy` used to override 9 fields; `DummyEnemy` now carries all of
  them, and the armored variant still needs only `DamageReduction` + aggro ranges â€” kept identical
  values (cross-checked line-by-line).

## 1dn â€” "change the player spawn point to be on the test ground" (SHIPPED in `1dn`)

User: "change the player spawn point to be on the test ground".

- H1 â€” flip `AutoTeleportPlayerOnStart` to default on. The pull-onto-pad in `RunBenchSpawn` runs one
  frame in (coroutines started in Awake don't tick before the first frame), so the player would flash
  on the boot chunk for a frame then teleport. It also self-gates on `IsArenaReady`, and more
  importantly it's a teleport, not "the spawn point". PARTIAL â€” used as belt-and-braces, not the main
  mechanism.
- H2 â€” move the actual spawn: `GameBootstrap` creates the test ground at the END of boot (after the
  player teleport), so I reordered it BEFORE the player placement and spawn on `GetSpawnPoint()` when
  `CreatePlatform && IsArenaReady`. Is the deck safe to stand on at that instant? YES â€” the platform
  mesh/collider is built synchronously in `BuildTestGround` (Awake), no streaming gate like terrain.
  Boot-chunk placement stays as fallback (platform off). CONFIRMED as the core change.
- H3 â€” `ResetPlayer` (new game + death respawn) only re-homed to the pad if the player had already
  REACHED it (`IsOnOrNearArena`: XZ within 0.6Ã—platform, Y within 6 m). With the pad as default spawn,
  a brand-new game would immediately bounce back to the boot chunk â€” inconsistent. Dropped the gate:
  teleport to `GetSpawnPoint()` whenever `IsArenaReady`, boot chunk otherwise; deleted
  `IsOnOrNearArena` (only caller was the gate). CONFIRMED.
- Spawn rotation: `ResetPlayer` sets `Quaternion.identity` (facing +Z); `GetSpawnPoint` is at
  `PlatformCenter.z + 0.45Â·PlatformSize` (south edge), so the player faces AWAY from the deck centre
  at spawn. Accepted â€” mouse turns the camera anyway; not changing rotation (matches old pad-spawn
  behaviour).
- Double-teleport check: with the toggle on, `RunBenchSpawn.PlacePlayerOnArena` fires on frame 1 to
  the SAME coordinate GameBootstrap already placed â€” idempotent, no snap-back (records safe pos via
  `TeleportTo`). Fallback guard at the end of `RunBenchSpawn` also still fires only when
  `!IsArenaReady`. No conflict.

## 1dm â€” "reduce tree and stone spawn ratio to 1/5" (SHIPPED in `1dm`)

User: "reduce tree and stone spawn ratio to 1/5".

- H1 â€” the ratio is in `ChunkObject.StepProps` (streamed world): each tile rolls `_propRng.Next(200)`
  once for a tree and once for a rock â†’ 1/200 each. "reduce â€¦ to 1/5" reads as "Ã— 1/5" (an absolute
  "1/5" would be 40Ã— DENSER than today, so that can't be the intent) â†’ 1/200 Ã· 5 = **1/1000**.
- H2 â€” did the legacy `WorldBuilder.SpawnTrees(150)/SpawnRocks(75)` also need the cut? REJECTED: that
  path only runs under `EnableLegacyGeneration` (default false â€” the comment "streaming world: all
  blueprints usable" and Start/GenerateWorld gates confirm legacy generation is off). Only the streamed
  path drives what the player sees.
- Implementation: single const `ChunkObject.PropSpawnOdds = 1000`, both rolls use it (deterministic
  per-chunk RNG unchanged, so chunk-to-chunk placement is still deterministic â€” just sparser).
- Expected counts (chunk = 30Ã—30 = 900 tiles, `TerrainChunkCoord.ChunkSize`): old â‰ˆ 900Ã—(1/200Ã—2) =
  ~9 props/chunk; new â‰ˆ 900Ã—(1/1000Ã—2) = **~1.8 props/chunk** (poisson, most chunks 0-2, rare tail to
  ~5). A full ring-4 (81 chunks) holds ~150 props instead of ~730.
  WORTHY of a play-test check: at ring 4 the world may now read fairly empty in spots.

### Verdict
- CONFIRMED H1 (1-in-1000), H2 rejected. Implemented as `1dm`. OPEN until the user play-tests the new
  density.


## 1dl â€” stop spawning enemies, keep the code (SHIPPED in `1dl`)

User: "stop spawning enemy for now but keep the enemy code, i'll test them later".

- H1 â€” comment out the `SpawnEnemies` lane. REJECTED â€” the toggles are THE documented off-switch for
  lanes; deleting/commenting invites the code to rot and blocks a simple re-enable.
- H2 â€” flip `EnableEnemies` to `false`. ACCEPTED. Checked for scene instances first: the only
  serialized copy is `Assets\_Recovery\0 (10).unity` (a backup, `EnableEnemies: 1`); the live game
  adds the component at runtime (`GameBootstrap` â†’ `root.AddComponent<NewWorldTestGround>()`), so the
  C# default governs and enemies stop immediately. The `RunBenchSpawn` guard already keeps the whole
  `SpawnEnemies*` code path intact for later.

### Verdict
- CONFIRMED H2 (default flip is sufficient; no scene edit needed). Implemented as `1dl`.


## 1dk â€” "place down every magic on the test ground" (SHIPPED in `1dk`)

User: "place down every magic on the test ground so i can look and edit the magic model".

### Step 1 â€” what IS "the magic model"
- The game has no `MagicModelBuilder`; every magic spell's visual is either a projectile body built at
  runtime by `SpellCaster.Projectiles.cs` (`BuildProjectileBody` switch: Orb-Sphere, Bolt, Shard,
  Lance, Spear, Blade, Splash, Comet, Missile, Dart, Debris + the summonFallingRock boulder) or an
  animated delivery object (SpellZone ring/column, SpellBeam line, SpellStorm strikes, SpellTornado
  funnel, SpellSummon totem/effigy, transient SkillFx flashes).
- User picked scope C: **one display per castable magic spell** (the 90+ MagicTestMatrix roster) + a
  world-TMP label above each. So I re-read the cast path (`SpellCaster.Cast.cs`) to ground how each
  delivery looks before deciding what to display.

### Step 2 â€” how to display a spell statically
- Projectile spells: reuse the exact live-cast visuals by making the (instance-but-stateless)
  `AttachDefaultProjectileVisual` **static** and adding `public static CreateProjectileDisplay(type,
  shape, rockBody)` â€” same body + comet-exhaust particles, no `SpellEffect`/collider/launch.
- Non-projectile deliveries (Zone/Vortex/Beam/Storm/Summon/Instant) have **no static projectile** â€”
  their visuals are runtime-animated and would need live casts. Decision: show their school-colored
  default icon body + the spell name label, so the bench still lists every magic spell with a
  recognizable id to edit. Flagged to the user before shipping; folding in animated delivery models
  is a possible follow-up.

### Step 3 â€” where on the platform (spatial reasoning)
- Layout constraints read from `NewWorldTestGround.cs`: tool kit at x +0.42Â·PlatformSize (z âˆ’26..+22),
  weapon rack at x âˆ’0.42Â·PlatformSize, enemy rows z âˆ’24..âˆ’40 (x Â±27), dummies z âˆ’18 (x Â±4), NPC row
  z âˆ’0.18Â·PlatformSize (x +6..+22), farm/livestock/buildings z â‰¥ +18. Player spawns at z +0.45Â·PlatformSize.
- Free middle band: x Â±0.40Â·PlatformSize, z âˆ’14..+10. Placed the grid there (default 120 platform:
  32 columns Ã— 3 spacer-3u rows â‰ˆ 93u wide Ã— 6u deep). 3u pitch keeps labels apart and rows walkable;
  pedestals/bodies get **no collider** so the grid never blocks movement or E-interaction.

### Step 4 â€” labels
- World TMP pattern copied from the legacy building signs (`MapBuilder.Nightclub` neon label:
  text/fontSize/alignment/color/outline/sizeDelta, no explicit font assignment) â€” consistent with the
  codebase; relies on the project's configured TMP default font like the signs do.

### Verdict
- CONFIRMED: the reusable body builders (Step 2) are all static â†’ the static-visual approach needs
  no live caster. Implemented as `1dk`. OPEN until the user play-tests (walk the grid: every magic
  spell visible + labeled; each shape matches the cast; grid clear of other lanes; big icons not busy)
  or asks for the animated delivery models as a follow-up.


## 1dj â€” enemy HP bar "does not stay on their head" (SHIPPED in `1dj`)

User: "the hp bar of enemy does not stay on their head, fix".

### Step 1 â€” what anchors the bar, and why does it float?
- H1: bar lags behind a moving enemy. REJECTED â€” the HUD repositions every frame from the enemy root
  transform (`WorldToScreenPoint` in Update), so it tracks position exactly.
- H2: enemies bob/hover so the root height wobbles. REJECTED â€” grep of `Combat/AI` shows no
  hover/bob/sin code; enemy models are static cube children of the root built at local zero.
- H3 (CONFIRMED): the bar height is a FIXED 2.2u above the root, but models top out at very different
  heights (model top: slime ~0.43u, wolf ~0.8u, bandit 0.95u, golem ~1.2u, dummy ~1.9u). So bars
  float far above small enemies â€” reads as "not on their head".

### Step 2 â€” fix options
- A. Add a dedicated `Head`/top anchor transform to all ~20 model builders and anchor the HUD to it.
  REJECTED â€” large surface area for no gain: models are static, so a model's top never moves relative
  to the root; a measured bounds top is equivalent at attach time.
- B. Measure the model's renderer-bounds top once per enemy (on pool rebind) and cache the offset;
  keep following the root every frame. ACCEPTED â€” one-file change; `GetComponentsInChildren` only on
  target-change, never per-frame; fallback 2.2u if the model/renderers are missing.
- Margin `0.25` so the bar sits just above the head rather than inside it. Dragon/demon do have wide
  wings/tail, but the head/horns are still the highest point on those builds here, so the absolute-top
  rule is good enough without special-casing.

### Verdict
- CONFIRMED H3; implemented as B in `1dj` (`EnemyHealthBarHUD.ComputeHeadOffset`). OPEN until the user
  play-tests (damage a spread of enemy sizes and check the bars hug each head).


## 1di â€” "game too lag" at radius 30: root-cause + prop ring + burst fix (SHIPPED in `1di`)

User reported lag after `1dg` (radius 30). The `1dh` file refactor went first per their choice; this
task is the actual perf work.

### Step 1 â€” where does the lag actually come from at radius 30?
- H1: draw calls / overdraw from terrain meshes. WEAK â€” each chunk is ONE merged mesh (~900 quads,
  11k tris), so 3,721 chunks â‰ˆ 3.7-4k draw calls worst case. URP handles that; not the "lag" feel.
- H2: memory allocation / GC from streaming. PARTLY â€” chunk finalize allocates a mesh + merges arrays
  per chunk, but that's incremental and bounded per tick.
- H3 (STRONG, CONFIRMED): **prop GameObject + BoxCollider count at full load.** Each tagged tile spawns
  a tree AND/OR rock prop as separate cube-based GameObjects with colliders (~450k BoxColliders across
  the ring), because (a) `MapBuilder.MakeBlock` defaults `removeCollider=false` and (b) tree trunks/
  branches are created with colliders. Props are never LOD'd or culled (`ChunkLodManager` explicitly
  skips props; chunk-root culling by distance never engages while inside the render radius). 33k props Ã—
  (Transform sync + collider) + broadphase cost = sustained drag and spawn burst hitches.
- H4: the per-tick **finalize burst** hitching frames during the initial fill (12-16 chunk mesh uploads +
  MeshCollider cooking inside one poll tick at 20 ticks/s). SEPARATE from H3 (happens during load, H3
  during play), but reinforces the "lag" complaint right after boot.

### Step 2 â€” user scoping (questions answered)
- Chose: **prop ring** + **only the burst smoothing**. Explicitly NOT chosen: full collider band
  (~120-180 m), prop mesh-merge + instancing.
- Requires: **player + targets stay hit-able**; goal **steady 60 fps on default settings**.

### Step 3 â€” design reasoning
- Prop ring reuses the existing streaming pipeline (deterministic per-chunk RNG) instead of a new LOD/
  instancing system. Ring must be Chebyshev (matching `StreamAround` square) so "inside/outside" is
  unambiguous at chunk boundaries â€” props pop at the square edge, exactly like terrain streams in.
- Prop cleanup must NOT touch the chunk mesh/collider (`ChunkObject.ReleaseProps` keeps them), else the
  terrain would flicker out whenever the player turns slightly â€” the mesh is authoritative for physics.
- Who owns BeginProps? Moved OUT of `CreateChunkGameObject` into `SyncPropRing`, otherwise far chunks
  would queue 900 pending tiles even though nothing ever steps them (StepChunkProps would stream them
  because it only checked PropsPending, not position). `PropsPending` now requires `PropsOn`.
- Spawn chunk safety: the player's own chunk is always within the ring (ring is measured from exactly
  that focus), so boot ground keeps its trees immediately â€” no "no props at spawn".
- Burst smoothing: instead of hand-tuning constants again (1dg already raised them), made the budget
  ADAPTIVE to the previous frame (`AdaptiveBudgetMs` = base 6 ms Ã— clamp(target/delta, 0.35, 1.2)). A
  hitch, once, self-shrinks the next tick's chunk finalize work; a smooth frame spends the full budget,
  so average load speed is preserved while the per-frame spike is gone. Same pattern (wall-clock 3 ms)
  on prop spawning.
- Rejected: pooling the prop GameObjects for the pop-in/pop-out churn â€” adds a pool + lifecycle state
  for ~800 props at ring 4; the pop cost (~a few dozen destroy/create) is inside the 3 ms prop budget
  and churn only happens while the player actively walks the ring edge. Cheap enough not to justify it.
  REOPEN if ring is raised a lot or the user wants the collider band.

### Verdict
- H3 + H4 confirmed; H1 weak; H2 partial. Fix shipped in `1di`: prop ring (default 4) + adaptive
  finalize/prop budgets. Deferred by user choice: collider band, mesh merge/instancing.

---


## 1dh â€” "game too lag, first optimize the files structure" (SHIPPED in `1dh`)

User reported lag right after `1dg` (radius 30). Clarified via question: "optimize the files structure"
= reorganize the C# code files FIRST (their answer also included the game-object structure fix, but as
the explicit "Refactor first, perf after" ordering â€” so `1dh` is refactor, `1di` is the lag fix).

### Step 1 â€” what "structure" really meant here
- The folder tree was already sane (`Combat/*`, `World/*`, `NPCs`, `UI/NewWorld`, ...). The real
  structural debt is 25 `.cs` files over ~600 lines (god files: CharacterInfoUI 3321, MapBuilder 2030,
  WorldBuilder 1973, PlayerController 1779, UIManager 1609, SpellCaster 1165, WorldStreamer 1096,
  ToolManager 1076). The repo already has the right convention: `public partial class X` split by
  domain (WorldBuilder.*, MapBuilder.*). So "file structure" = extend that to the gods.
- H1: rename/refolder files â†’ would break Unity MonoBehaviourt GUID/meta references. REJECTED â€” a
  pure rename invalidates script references. Strategy: KEEP original filenames as the partial cores,
  ADD new `X.Group.cs` files (Unity auto-meta). Verified no duplicate meta GUIDs.

### Step 2 â€” the split rules that made it mechanical + safe
- Fields NEVER move (serialized layout untouched â€” Unity serializes by name regardless of file).
- Methods/properties/whole #regions move verbatim; base-class/interfaces/attributes stay on the core
  part; `sealed`/`static` repeated on every part (C# requires matching modifiers);
  `public static partial class MapBuilder` parts â€” static keyword required on ALL parts.
- New parts copy the original's FULL using block (superset) â€” unused usings are warnings, never
  compile breaks; this dodges "missing using" entirely.
- Trailing helper classes (WorldBuilder.Fields/Building/ThrownItem..., ToolManager.InventorySlot...)
  stay put.
- Partial classes: a MonoBehaviour's asset reference follows the file whose name matches the class â€”
  the cores keep those names, so scene/prefab references survive the split.

### Step 3 â€” verification reality-check (elsewhere I caught a fabrication risk)
- `git diff -U0` on every original: all hunks are pure deletions (+ `partial` keyword). Small "+"
  counts that appeared got scrutinized against `git show HEAD:<file>`:
  - CharacterInfoUI "+50": TreePan pan/zoom class, RaceNode colors, BuildRaceTree helpers â€” all
    PRESENT at HEAD, so the "+" is diff repositioning after big deletions, not invented code.
  - PlayerController "+3": IgnoreInput property present at HEAD (it was genuinely un-indented at
    line 23 originally â€” agent kept it verbatim, causing a repositioning hunk).
  - Per-family line totals are preserved (delta â‰¤ 64 = new-file header comments + using supersets).
- Grep: `partial class X` file counts match the created files exactly; moved member names unique
  across Assets/Scripts (the few "dupes" â€” ApplyHit/BuildCafe/ResetTerrainSaves/TakeDamage â€” were
  same-named members on DIFFERENT classes, verified per-hit).

### Verdict
- H1 rejected; the mechanical partial-split convention is confirmed as the low-risk path. Shipped in
  `1dh`. No compile (rule 3); user play-tests. Lag fix pending as `1di` (collider band + prop ring +
  prop mesh merge/instancing to cut ~450k colliders / ~33k renderers at radius 30).

---

## 1dg â€” "increase terrain render range and need to increase the loading speed even more" (SHIPPED in `1dg`)

User asked for (a) more render range and (b) faster loading. Clarified via questions: radius **30**, an
**aggressive** burst-load profile, keep the distance fog.

### Step 1 â€” where does loading speed actually sit?
- H1: mesh generation on threads is too slow. PARTLY â€” each chunk builds 900 tiles + a merged mesh on
  a ThreadPool thread (~961 noise calls), but that work was already backgrounded; the pipeline was not
  throttled by raw CPU but by pacing.
- H2 (CONFIRMED): the pacing constants cap throughput. `PollInterval = 0.1` s means only ~10 ticks/s;
  `FinalizeChunks` caps at `min(ChunksPerFrame, 8)` chunks AND a 6 ms wall clock â€” â‰ˆ 80 chunks/s max;
  `MaxInFlight = 8` limits how far ahead the background runs. At radius 20 (1,681 chunks) the full fill
  is ~21 s, so the `GameBootstrap` comment "fills the render radius over ~1.5 s" was stale â€” it predated
  the bigger radius and nothing accelerated the pipeline with it.
- Evidence for the pacing being the binding constraint: main-thread apply per chunk is tiny (a
  961-vertex/1800-tri flag upload + single collider cook) vs the 6 ms budget and 8-chunk cap. So the
  cheap work was artificially rationed.

### Step 2 â€” the speed levers (all in WorldStreamer.cs)
- PollInterval 0.1 â†’ 0.05 (2x ticks/frame budget). Each tick also re-runs StreamAround (unload sweep +
  ring enqueue) and DispatchPending (distance sort) â€” cheap at ~4k chunks.
- FinalizeChunks: hard cap `8 â†’ 16`, wall-clock `6 â†’ 12 ms`. Kept the cap so a pathological frame can't
  overrun; kept the wall clock so slow cooks (collider bake) don't blow the frame.
- MaxInFlight `8 â†’ 24`: more background backlog so the faster finalize never waits on emptiness.
- PropTilesPerTick `40 â†’ 120`: props exist to dress the ground; at the old 40 tiles/tick the ~1/200
  tree + ~1/200 rock density would take minutes to dribble in across 3,721 chunks' 3.3 M tiles.
- Net: â‰ˆ 320 chunks/s (4x today) â†’ radius-30 fill ~10â€“15 s.

### Step 3 â€” range knobs checked for side effects
- `ChunkLodManager.EffectiveCullDistance()` = `max(CullDistance, (Radius+1)*ChunkSize)` â€” auto-scales,
  no edit needed. At radius 30 that is 930 m; camera far plane default â‰ˆ1000 m just covers the axis
  (diagonal spurs may flirt with the clip â†’ play-test item, see PROGRESS).
- Memory/objects grow quadratically (1,681 â†’ 3,721 chunks); each chunk = 1 GameObject (mesh + collider)
  + ~9 props (~150 cubes) on average. Verified nothing else hard-codes the old radius/pacing
  (grep `Radius = 20`, `PollInterval`, `MaxInFlight`, `ChunksPerFrame`, `PropTilesPerTick`).

### Verdict
- H1 rejected, H2 confirmed. Changes shipped in `1dg`; play-test checklist in PROGRESS `1dg-status`.

User: "the animals dont have damage interaction so they basicly invincible right now". Clarified via
questions: at 0 HP â†’ "explode and part flung everywhere" (the old-game voxel burst); club stays
non-lethal so cage capture keeps working.

### Step 1 â€” why were they invincible?
- H1: no collider â†’ hits never connect. REJECTED â€” `Livestock.Awake` adds a root `SphereCollider`
  (radius 0.4) and the club's raycast hits it fine.
- H2: no health system. Mostly NOT the issue â€” `TakeDamage` already exists (red flash, flee/fight,
  knockout â†’ capture, 15 s recover), reachable today via the club.
- H3 (CONFIRMED): the combat pipeline only routes damage to `IDamageable` â€” every sink greps to
  `target.TryGetComponent<IDamageable>` (HitboxSystem, RangedProjectile, SpellCaster, SpellZone,
  SpellStorm, SpellBeam, SpellTornado, SpellDoT, IEffect/ClassEffect/RaceEffect, SummonedAlly).
  `Livestock` (or its root) never implements it, so every non-club attack no-ops. Played weird: the
  spell pipeline computes full damage numbers and pops them above the animal even though nothing is
  applied â€” hits visibly "connect" while HP never drops, exactly the reported "basically invincible".

### Step 2 â€” 0-HP behavior (design fork, user answered)
- Existing knockout â†’ capture was the only outcome; user wanted the old-game explosion. Kept BOTH by
  splitting the sources: lethal damage (any combat hit reaching 0 HP) â†’ `ExplodeAnimal()` (mirrors
  `EnemyController.ExplodeModel`, 1dd â€” detach parts, BoxCollider + Rigidbody mass 0.3,
  `AddForce(dir*8+up*6)` torque, cleaned after 5 s) then destroy the animal; the club gets a new
  non-lethal `KnockDown()` (stun, no HP loss) so cages/capture still function.
- Return type: `IDamageable.TakeDamage` is `int` (remaining health). Club callers previously ignored
  the return; changed signature to match â€” verified the only other `Livestock.TakeDamage` caller is
  the club (grep) and it now calls `KnockDown()`.
- Statuses: burning/chill DoTs attach now that animals are damageable; knocked-out animals early-return
  from `TakeDamage`, so a DoT can't kill a downed animal mid-capture. Knockback shoves the animal
  rigidbody â€” acceptable flavor of "damage interaction".

### Step 3 â€” latent debris-cleanup bug caught during build (fixed here)
- Both the shipped `EnemyController.ExplodeModel` (1dd) and my first `ExplodeAnimal` draft scheduled
  cleanup with `StartCoroutine(DestroyDebris)` on the controller, which `Destroy(gameObject, 0.2f)`
  destroys right after â€” Unity kills coroutines when their component dies, so the ~5 s cleanup never
  ran and debris would accumulate forever (the 1dd play-test item never confirmed this).
  Fixed by scheduling `Destroy(part, DebrisLifetime)` on EACH part (live objects), dropping the
  coroutine + list. Also trimmed `EnemyController`'s now-unused `System.Collections*` usings (its
  `_targets` field is fully-qualified `System.Collections.Generic.List`, so removal is safe).
- Noted for future bursts: never put a cleanup coroutine on an object that is being destroyed in the
  same breath.

### Step 4 â€” no-loot decision
- User only asked for the explosion; deaths grant nothing. Exploded animals are removed and the
  spawner (`LivestockSpawner`, 45â€“80 s trickle, Max 20) replaces them. If hunting loot is wanted it
  is a separate task.

---

## 1de â€” "dirt explode or rock debris when creating a dent, depends on the layer" (SHIPPED in `1de`)

User: "when creating dent, make a dirtblock explode as well or rock debris depends on the layer,
the explosion would much like when stone got destroyed by pickaxe".

### Step 1 â€” what IS a "dent", and where are they created?
- "Dent" = the crater excavation (`TerrainShape.Crater`), the ground disturbance shared by the tools
  (shovel/pickaxe via `TerrainDeformer.Dig`) and Earth magic (zone casts, Storm strikes, the Stone
  Shard projectile impact). Hypothesized the right hook is `WorldStreamer.DeformAt`, the single
  funnel every crater goes through.
- **Confirmed by grep**: every crater call site routes through `TerrainDeformer.Apply` â†’
  `WorldStreamer.DeformAt` â€” `SpellCaster.ResolveZone`, `SpellCaster` charged casts,
  `SpellStorm.DeformGround`, `SpellEffect.ResolveProjectileImpact`, `ToolManager` shovel/pickaxe
  `Dig`, and the QA strata lane on `NewWorldTestGround`. One hook covers all of them.

### Step 2 â€” "depends on the layer": where does the layer color come from?
- The strata system from `1cs` already paints the pit walls: `ChunkMeshGenerator.TerrainBandColor
  (seed, x, z, vertexY)` returns grass green â‰¤ 0.35 m depth, dirt-brown to ~2.3 m, stone-grey â‰¥ 2.7 m
  (`StoneBandEnd`). Bonus: `vertexY` can be the floor height at the crater center
  (`WorldStreamer.CurrentHeightOf`), so the debris exactly matches the stratum the dig just reached.
- Rejected alternatives (dead ends): (a) hard-coding `DirtBrown` for tools / `StoneGray` for spells â€”
  spells and the pickaxe both cross bands, so a fixed tint would lie half the time; (b) spawning from
  `ToolManager` only â€” leaves spell dents (the biggest craters) bare. Verdict: single `DeformAt` hook
  + `TerrainBandColor`.

### Step 3 â€” the "like stone destroyed by pickaxe" look
- `WorldBuilder.SpawnRockDebris` (WorldBuilder.RockMining.cs:178): 3â€“6 cubes sized from volume,
  `Color.Lerp(gray, black, rand*0.5)`, mass = volumeÂ·1000, up-bias velocity (4â€“8 up), spin. Its
  pieces persist forever (by design â€” they're smashable again). For dents that would litter across
  repeated digs, so the new `DentDebris` copies the physics but `Destroy`s after 2.5 s (the
  prior-impact-burst timeout).
- **Hypothesis H6 (from `1cx` re-checked):** do freshly-carved debris chunks fall through the chunk
  collider while it rebuilds mid-frame? The previous investigation concluded the crater floor is
  solid walkable terrain and chunks fall freely on it â€” same reasoning holds; debris spawns at floor
  + 0.08 m. No special handling.
- **Double-burst risk (confirmed, fixed):** `SpellEffect` already threw its own grey
  `SpawnImpactDebris` (3â€“5 grey cubes) on every Earth projectile impact. With the DeformAt hook that
  would fire twice at the same point. Decision (user-backed): delete `SpawnImpactDebris`/its call,
  let DeformAt own ALL crater debris. Grep after removal: the only remaining`SpawnImpactDebris`
  symbol is `RandomEventManager`'s unrelated `(Vector3, Transform)` method â€” untouched.
- `SpawnDigPuff` (the tools' quick 1 s shard poof) deliberately kept â€” it is the stroke accent, the
  new debris is the excavation chunk ("explode ... as well").

### Step 4 â€” edge cases
- Unloaded terrain: `DeformAt` early-returns when `newHeights.Count == 0`, so no debris without an
  actual edit. Raised shapes guarded out (`shape == TerrainShape.Crater` check).
- Massive spell radii: debris count/size fixed small (3â€“5 cubes, 0.08â€“0.16) regardless of radius â€”
  authored look, no per-cast allocation spike.

---


## 1dd â€” "enemies explode like in the old game" + "add more enemy" (SHIPPED in `1dd`)

User: "add more enemy and every time the enemy die they explode like in the old game". Clarified via
questions: test platform only; both new types + more numbers; visual-only explosion.

### Step 1 â€” what was "the old game" explosion?
- Hypothesis A: the project already had a death-explosion somewhere live â€” rejected; grep for
  Explosion/explode/burst across `Assets\Scripts` found only spell/skill FX, no enemy death burst.
- Hypothesis B: the archived legacy runtime had it â€” **confirmed**. `_Archived\Enemies\
  EnemyController.cs:980 Die()` calls `ExplodeModel()` (line 1057): every model block detaches,
  gets `BoxCollider` + `Rigidbody` (mass 0.3), `AddForce(dir*8 + up*6, Impulse)` with
  `dir = (blockPos - center).normalized; dir.y += 0.5`, random torque, destroyed after 5 s. That is
  the exact "old game" feel.

### Step 2 â€” mirror it into the new EnemyController (live) vs. new FX file
- Considered a new `EnemyDeathFx` particle/chunk helper. Decided against inventing a new look â€” the
  user asked for "like the old game", so replicate `ExplodeModel` directly inside
  `EnemyController.Die()` (prefixed same method names). Verdict: mirror, keep the 0.2 s destroy tail.
  Added `using System.Collections` + `System.Collections.Generic` for the coroutine/list (the file
  previously fully-qualified `System.Collections.Generic.List` â€” the new using is additive, no clash).
- Perf sanity: a few blocks per enemy, 5 s lifetime, bounded by world budget (`EnemySpawner
  MaxLiveEnemies = 60`) and the platform's ~26 enemies. No pooling needed (matches old game, spell
  debris does the same).

### Step 3 â€” "more enemy" scope
- `NewWorldTestGround.SpawnEnemies` had 5 types. Roster (EnemyModelBuilder) has 20. Laid out all 20
  in two rows of 10 (6-unit spacing from `x = center-27`) + a 6-copy horde row for density.
- Geometry check (platform 120Â², center (0,50,0), half 60): rows span x âˆ’27..+27, z âˆ’40/âˆ’32/âˆ’24 â€”
  inside bounds. Dummies moved from `z=center-36` (would collide with new rows) to `z=center-18`;
  boss at `z=center-50.4` clear of row A (âˆ’40). Confirmed.

### Step 4 â€” doc references
- `game-design.md` Â§3.7 is "Damage & Status Types", NOT loot â€” avoided a wrong Â§3.7 citation for the
  loot-before-burst note (draft had it, removed).

---


## 1dc â€” "Church + taoist roofs upside down" (SHIPPED in `1dc`)

User: "you kinda got the roofs of church and taoist upsidedown". The gable/hip panels of the four
new roof blocks (church nave gable, church spire pitch, shrine hip, shrine tier-2 hip) read as an
inverted V â€” ridge low, eaves high.

### Step 1 â€” establish the ground truth (which roof was correct?)
- `Pagoda_Roof1` (Blueprints.cs:1144-1147) had been play-tested and looked right: the roof forms a
  **peak** â€” panels slope DOWN from the ridge (top center) toward each eave. So it is the reference
  for the sign convention.

### Step 2 â€” derive the convention from the reference
- Z-axis panel at **+z** (`Euler(+14,0,0)`): R_x(Î¸) moves a point's height by `sinÎ¸ * zOffset` â€” a
  panel centered ahead of the ridge needs +Î¸ so its far (+z) edge dips LOW (eave) and its near edge
  stays up (ridge). Z-axis panel at **âˆ’z** mirrors it (`Euler(âˆ’14,0,0)`).
- X-axis panel at **+x** (`Euler(0,0,âˆ’14)`): R_z(Î¸) moves height by `sinÎ¸ * xOffset`: +x panel needs
  âˆ’Î¸ so its far (+x) edge dips low. X-axis panel at **âˆ’x** mirrors (`Euler(0,0,+14)`).
- So: `z=+ â†’ Euler(+Î¸)`, `z=âˆ’ â†’ Euler(âˆ’Î¸)`, `x=+ â†’ Euler(0,0,âˆ’Î¸)`, `x=âˆ’ â†’ Euler(0,0,+Î¸)`.

### Step 3 â€” compare each rewritten block against the convention
- `Church_Roof` (1500-1501): both Z panels inverted (z=+ had âˆ’24, z=âˆ’ had +24). **CONFIRMED** â†’ swap.
- `Church_SpireRoof` (1554-1557): Z pitch panels inverted (z=âˆ’ had +38, z=+ had âˆ’38) â€” swap; the X
  panels (1556-1557, `x=âˆ’ â†’ +38`, `x=+ â†’ âˆ’38`) already matched. **CONFIRMED** â†’ swap Z only.
- `Shrine_Roof` (1707-1710): Z panels already matched (z=âˆ’14/z=+14); X panels inverted (x=âˆ’ had âˆ’14,
  x=+ had +14) â€” swap X only. **CONFIRMED**.
- `Shrine_Roof2` (1750-1753): same pattern â€” Z fine, X swapped (Â±18) â€” swap X only. **CONFIRMED**.

Total: 8 sign flips across the four blocks.

### Hypotheses considered
- **H1 â€” only the church is wrong.** REJECTED: user said "church and taoist", and both shrine hip
  roofs (tiers 1+2) had the same inverted X-panel signs.
- **H2 â€” all four panels per roof are wrong.** PARTIAL: each roof mixed one correct axis with one
  inverted axis; only the offending axis per block was flipped (Z for church nave+gable, X for
  shrine hips; spire Z). This is why per-axis comparison against the reference was the right call.
- **H3 â€” geometries changed as well.** REJECTED: only the `Quaternion.Euler` rotation arguments
  changed; positions/sizes untouched (save-compatible since part names/offsets are unchanged).

### Open/risk notes
- Rotation signs are baked into blueprint visuals; building-damage/state persistence only stores
  part transforms, not Euler args â€” no save/load impact expected.
- Other roof blocks (pagoda 1144-1147, well/house hips 1220-1223, 1284-1287, 1358-1361) were all
  re-checked against the convention and are correct â€” no collateral fixes.

## 1db â€” "Raise the tab button in tab menu height" (SHIPPED in `1db`)

Ask clarified to the Character Info top tab strip at ~1.3x.

### Why two size writes had to change (the trap)
- `BuildTopButtons` writes `sizeDelta.y = 64f * S` at build time (CharacterInfoUI.cs:365), but the
  aspect-fit pass `OnLayoutFitted` (line 478) re-writes it to `56f * S` â€” and that pass runs during
  `BuildPanels` via `RegisterFit â†’ ApplyFits`, i.e. AFTER the buttons are built.
- Hypothesis: changing only the 64 in the builder would be enough. REJECTED on reread: the first
  RegisterFit immediately overwrites the height to 56, and every window resize after that re-applies
  56 too. To be effective at runtime, BOTH writes must match. Confirmed by grepping both call sites
  to the same `84f * S`.
- Height target: 64 Ã— 1.3 â‰ˆ 84, chosen over a round 80 so the "1.3x" ask is met honestly.

### Why buttons grow UP, not down
- `BodyRow` top edge sits 60 u below the panel top (`offsetMax.y = -60`, MenuPanelBase.cs:142);
  tabs (children of `PanelRect`, top-anchored) must not cover it. Tab bottom is currently at âˆ’50
  (`y=6, h=56`) / âˆ’48 (`y=16, h=64`), leaving ~10-12px clearance.
- Naive height bump (keep y) would push the bottom down to âˆ’78 â†’ overlap content by ~18px.
  Solution: raise `anchoredPosition.y` by the same delta as the height (+20 / +28) so the bar grows
  UPWARD and the bottom edge stays at its old spot. Verified position arithmetic on reread.

### Open/risk notes
- Label uses anchorMin/Max 0..1 with Â±8f offsets â†’ stretches with the taller button. No other code
  reads the tab bar geometry (`_tabButtonRects` is only used inside these two methods).
- On a very short window the fit clamps scale â‰¥ 0.25 but the tab bar derives its width from
  `availW * 0.8f` and keeps 84 height design units â€” pre-existing behavior, unchanged.

## 1da â€” "Ground AoE can't be placed far away" (SHIPPED in `1da`)

User report (paraphrased): the outdoor AoE spells can only be placed within their short spell
`Range`, so you can't drop a zone/summon/storm far across the world â€” the open-world game wants
long-distance placement.

### Root-cause trace (what actually capped the ground target)
- `SpellCaster.Execute` (SpellCaster.cs:264) builds the aim direction: `aim = cam.position +
  cam.forward * Mathf.Max(spell.Range, 5f)` â€” so the aim RAY was pinned to a point only
  `spell.Range` out. Ground deliveries then received `spell.Range * mods.RangeMult` as their delivery
  `range` (old lines 281/283/287/289 â†’ now 296/298/302/304).
- The ground-target resolvers honor that `range` as the probe length: `ResolveZone` â†’
  `TerrainDeformer.ResolveGroundTarget(pos, fwd, range)` (line 48), `ResolveSummon`/`ResolveStorm` â†’
  `GroundTarget(pos, fwd, range)` (line 328), `SpawnVortex` â†’ its own raycast (line 394). So even if
  the player points far away, the landing point was clamped to the spell's short range.
- The preview mirrored the same: `PlayerController.TryAoeTarget` (PlayerController.cs:1711) projected
  the landing ring at `Mathf.Max(spell.Range, 5f)` and raycast `Mathf.Max(spell.Range, 0.1f)` â€”
  preview and landing agreed, both short. That agreement is why the bug read as "can't aim far"
  rather than "preview lies".

### Hypotheses & decision
- **H1 â€” keep the aim pinned to spell.Range but scale it up.** REJECTED: spell `Range` is a delivery
  property (â‰ˆ12-30 on the earth/sky zones); multiplying it is a hack with no principled value.
- **H2 â€” unbounded (= float.MaxValue) probe.** REJECTED for the fallback branch: `aimHit.point`
  misses â†’ `pos + fwd * range` would hand positions at literal float infinity to the resolvers /
  preview ring. `float.MaxValue` math invites NaN/inf edge cases in squares/distance checks later.
- **H3 â€” a large fixed practical cap (1200) for GROUND deliveries only. [ADOPTED]** One constant
  `SpellCaster.GroundAimMax = 1200f`, used for the aim direction AND the four ground resolvers'
  range; preview mirrors the constant. Finite, safe in squared-distance math, and > any visible
  world distance (map ~20-chunk render â‰ˆ 600 m). Projectile/instant/beam deliveries keep
  `Mathf.Max(spell.Range, 5f)` and `spell.Range * mods.RangeMult` untouched.
- Scope guard: only the four GROUND deliveries (Zone/Vortex/Summon/Storm) â€” `FireProjectile`,
  `ResolveDirect` (instant), `ResolveBeam` and `SpellBeam.Length` still use spell.Range, and
  `SpellEffect` flight caps are untouched. Projectile spells keep their normal shot range; only AoE
  ground placement is opened up.

### Open/risk notes
- `TerrainDeformer.ResolveGroundTarget` walks up to 4 skip iterations over RAISED terrain while
  consuming `remaining`; with a 1200 probe the rest of the algorithm is unchanged (same skip loop,
  now just more ray budget). No new failure mode observed on reread.
- The ground-down probe in all paths stays fixed at 30 u (`Vector3.up * 0.1f` then down 30 u); a
  target on a very steep far face could land slightly below the hit surface, but that pre-existed and
  is unchanged.
- Verification is grep + reread only (rule 3 â€” no build). Confirm by searching the two call sites.

### Play-test checklist (user verifies in Unity)
Try aiming a Zone/Storm/Summon/Vortex well past the old spell Range; the preview ring should land far
away and the delivery should resolve there; confirm projectiles/instants/beams still behave exactly
as before.

## 1cz â€” "make the church and shrine as big and detailed as the pagoda" (SHIPPED in `1cz`)

### Context
User asked for the church and taoist shrine to match the pagoda in size and detail. Measured gap
before the change: pagoda = 15 parts, 14Ã—14, ~21 tall, ~430-line builder; church = 7 parts, 13Ã—9,
~9.6 tall, ~65-line builder; shrine = 6 parts, 10Ã—8.5, ~6 tall, ~65-line builder.

### Scope decisions (asked user; answers recorded)
1. Match pagoda scale (~14-16 wide, ~20 tall)? â†’ CHOSE match-pagoda scale.
2. Steeple style â†’ CHOSE a single tall front steeple (gothic, not a twin-tower or a separate topknot).
3. Shrine design â†’ CHOSE a two-tier pagoda-style hall (main tiled roof + an upper tier roof) topped
   by a jewelled gold spire.

### Design hypotheses
- **H1 - keep part names vs rename.** CHOSE keep every existing part name and add new ones. Save/load
  restores `structure_part_*` by name generically (`LoadBuildingsFromSave`); no gameplay code matches
  part names except the two builders' switch. Renaming would break old saves for no gain.
- **H2 - part granularity.** CHOSE 13 church / 12 shrine parts. Enough to separate the vertical stack
  (foundation â†’ interior â†’ roof â†’ steeple) so each cube-stack reads clearly, while staying in the
  same order of magnitude as the pagoda's 15 so the structure loop cost doesn't blow up.
- **H3 - NPC anchor orientation.** CONFIRMED by rotation math: `BuildChurch` rotates every part +90Â°
  about Y, so the new church's local -Z front (steps/tower door) faces world **-X (west)**; shrine
  rotation is 0, so its front faces world **-Z (south)**. The priest therefore moved from south of the
  church to **west** in front of the steps (rot 90â†’facing +X), and the taoist moved further south
  (-40,-33.4) in front of the widened steps and now faces the shrine (rot 0). Follow-up correction
  while landing `1cz`: after shifting the church's foundation from a 9-deep to a 13-deep slab and
  adding a full-width front porch + 4 descending steps (apron z -9.8..-6.6, landing to zâ‰ˆ-12), the
  priest anchor moved out to (27.5,-30) so he stands at the foot of the steps, not on the apron cube
  (the original 31.6 point landed inside the new porch volume). Test platform mirrored: priest
  `(cx+17.5, cz-35)` at the foot of the west steps (church center cx+30).
- **H4 - window/roof idiom.** Reused the pagoda's rotated-cube idiom (`CreatePartCubeRotated`) for
  eaves; used the same rotation trick at Â±35Â° to build the gothic pointed arch heads (chevron), which
  is the one non-pagoda motif the design needed.
- **H5 - pruning radius.** `ChurchExcludeHalf`/`ShrineExcludeHalf` raised to 15/14 so the tree/rock
  pruning box clears the grown footprints (16Ã—13 and 14Ã—12 â†’ half-extents ~8 and ~7-7.2).

### Evidence / verification (grep, no build â€” rule 3)
- Every new part name present in BOTH `_churchSubBuildings`/`_shrineSubBuildings` AND its switch case
  (25/25 via grep; see PROGRESS.md `1cz`). No legacy name dropped, so save-restore stays intact.
- New part names are only referenced in WorldBuilder.cs (array) + WorldBuilder.Blueprints.cs (switch);
  no other consumer strings (CharacterInfoUI, save/restore) refer to them.
- `IsFloorType` only matches `wood_floor`/`stone_floor`, so the new props can't pollute floor spawns.

### Dead ends / decisions NOT taken
- Did NOT add a side "village clock tower" or twin towers to the church (user picked single steeple).
- Did NOT make the shrine a literal 2nd pagoda â€” kept the deity statue + altar + censer interior so it
  stays a worship hall rather than a clone.
- Did NOT touch the pagoda or the legacy village layout.

### Open items
- Play-test: visual overlap of newly widened steps vs NPC box-collider walks; confirm pruning radius
  covers the grown footprints (esp. church east flank at x=48 now inside the 15 box) and the west
  priest placement reads as "in front of the door".

### Context
Player read the sky-spell descriptions ("a burning meteor falls from the sky", "a colossal mass of
burning rock that levels everything") and noticed the cast only flashed a ring on the ground â€” no rock
was ever summoned. Wanted the effect to match the tooltip.

### Scope decision (asked user)
- Asked: which spells â†’ "All sky/rock spells (Recommended)": Fire Meteor, Asteroid, Earth Meteor
  (Zone); Comet (Projectile); Meteor Rain + Rockfall (Storm).
- Asked: damage timing â†’ "Delay damage until landing": the rock should visibly fall and the burst
  resolves on impact, not instantly (a cast-time-driven beam of drama for the meteor family).

### Design hypotheses
- **H1 - data-driven flag vs hard-coded skill ids.** CHOSE the flag (`SpellData.SummonFallingRock`).
  The catalog already builds the whole spell via the `Spell(...)` factory; a new optional
  `summonFallingRock:` arg is the established pattern (`terrainShape:`/`projectileShape:`) and avoids
  scattering skill-id strings through damage paths. Scorch explicitly NOT flagged (jet of light, not a
  rock) even though it shares the Comet projectile shape.
- **H2 - defer via coroutine in SpellCaster vs a self-driving FX component.** CHOSE the component.
  `ResolveZone` is a pure method called from `Execute` (synchronous); making it start a coroutine
  breaks the return-value contract. Instead: `SkillFx.FallRock` owns a `RockDrop` MonoBehaviour that
  animates the fall and fires the `onImpact` callback. `ResolveZone` extracted the burst body into
  `ResolveZoneImpact` which the normal path calls synchronously and the sky path calls from the
  landing callback (guarded by `if (this == null) return`). Return-value check: `Execute`'s result
  only feeds `OnCastComplete`, which has **zero subscribers** â€” returning an early DamageResult for
  sky zones is harmless (verified by grep).
- **H3 - storm cadence change risk.** `SpellStorm.StrikeDelayed` already delays each strike by a
  random 0-0.35 s; adding the rock's fall only shifts the flash/damage/deform to landing time. Rocks
  are spawned per strike (~2/tick Ã— ticks), each short-lived and self-destroying, so no leak like the
  old lightning-bolt leak (which the comment explicitly warns about). No pooled allocation needed at
  this scale (a storm spawns single-digit rocks per tick; `StrikeFlash` is already pooled).
- **H4 - rock must never have a collider.** CONFIRMED constraint: the whole point of 1cx was that
  collider-grazing + knockback teleported the shared Terrain root. `FallRock` cubes strip their
  collider (`CubeChild`), like every other FX primitive in the codebase. Damage/knockback/deform are
  ONLY produced by the spell's own overlap/raycast pipeline inside the landing callback.

### Verified read-only before committing (per rule 3)
- `OnCastComplete` â€” only the declaration (SpellCaster.cs:62) + invoke (246); **no subscribers**.
- `Execute` switch routes Zone â†’ `ResolveZone` (line 281); Projectile â†’ `FireProjectile` (279) which
  calls `AttachDefaultProjectileVisual(go, type, shape)` (484) â€” threaded `summonFallingRock` through.
- `SpellStorm.StrikeDelayed` (SpellStorm.cs:78) â†’ `SpawnStrikeFx`/`ResolveStrike`/`DeformGround`;
  `RandomStrikePoint` returns world points (enemy pos or Â±radius random), so `FallRock` lands there.
- `Comet()` builder + `BuildProjectileBody` switch on shape â€” added `rockBody` without touching
  Scorch/Burn/Frost Bite (they keep the light core).
- SkillFx already has `RingFader`/`SlashFader` self-contained faders; `FallRock` mirrors them.

### Play-test checklist (user verifies in Unity)
Cast all six flagged spells; confirm the rock drops and the burst lands with it, Scorch still streaks
as light, and no terrain moves (rock has no collider).


## 1cx â€” The "whole terrain moves" bug was NEVER the deformer: knockback teleports the shared Terrain root (FIXED in `1cx`)

Player (Earth Wall report, verbatim): "when using earth wall it cause the same error, investigate the
root cause of this". Clarified symptom: "when im using skill, the scene camera litteraly see the
entire terrain move". This forced a rethink â€” the 1cv/1cw deform-width theory did NOT fit.

### Hypotheses & evidence
- **H1 - the deformer width was still leaking (1cv/1cw follow-up).** REJECTED as THE cause. Three
  facts broke the width theory: (a) the width is now capped for every shape (SpellCaster.cs:999-1001),
  (b) the very FIRST report was **Asteroid** (`magic_fireball_meteor_astroid`, SkillCatalog.Magic.cs:
  277), which carries **no `terrainShape:` arg** so `TerrainDeformer.Apply` no-ops â€” it cannot have
  moved terrain, yet the user saw it move, and (c) 1cv/1cw shipped and the user still saw the bug with
  Earth Wall. The width fixes corrected a genuine independent bug (charged shapes repaint a huge dish)
  but were a red herring for THIS symptom.
- **H2 - the camera does the moving (shake/FOV/dolly).** INVESTIGATED, REJECTED. `ScreenShake`
  (MaxAmplitude 0.15, child "CameraShakeRig" targeting), `CombatFeedback` (0.5 shake, HitStop),
  `ThirdPersonCamera`/`CameraModeSwitch`/`CameraFollow` (smooth damp footsteps, no cast-time snap).
  None can translate the whole visible world by meters. The shake note even documents that follow
  cameras overwrite the root â€” so shake is a tiny child-rig jitter, not terrain motion.
- **H3 - the CHAR cast pushes the player (depenetration from re-cooked chunk colliders / overlap).
  PARTIAL. `PlayerController.EnforcePhysicsSanity` only reverts launches â‰¥150 m, so a moderate
  physics depenetration could yank the player (and therefore the pivoted camera). BUT the symptom
  ALSO showed for Asteroid with zero chunk recook, and the camera pivot rides the player â€” a player
  launch alone doesn't make "the scene camera see the terrain move". Kept as an open edge case, not
  the driver.
- **H4 - ApplyKnockback teleports the shared Terrain root [THE WINNER].** CONFIRMED. `ResolveZone`
  (SpellCaster.cs:1017) queries `Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer)` with
  the default mask and calls `ApplyHit` on each collider. `ApplyHit` feeds `ApplyKnockback`
  (SpellCaster.cs:1205-1213): `Transform root = target.transform.root; root.position += dir.normalized
  * spell.Knockback`. Every streamed terrain chunk is parented under ONE `"Terrain"` root
  (`Terrain` â†’ `Chunks` â†’ `TerrainChunk_x_z`, WorldStreamer.cs:447-471), so ALL chunks share that root.
  A zone spell overlapping chunk MeshColliders moves the entire world's root by `Knockback` per chunk
  caught (charge-scaled radius â†’ several chunks â†’ big one-frame lurch) â€” the exact "entire terrain
  moved" read from a player-pivoted camera. Explains Every report: Asteroid (knockback 3, no deform),
  Earth Wall (knockback 3.5), Meteor (knockback 4). Real victims are `IDamageable` (EnemyController /
  BossController / SummonedAlly); terrain/props are not.
- **H5 - the fix could live in ApplyKnockback (guard root).** Option DESIGNED, merged into H4 fix:
  guard earlier in `ApplyHit` so ALL spell damage machinery (DamageNumber, status, knockback) skips
  non-`IDamageable` targets â€” a single point covering ResolveZone, `ResolveDirect`, and every
  `ResolveHitAt` caller. The other delivery components already pre-filter (`SpellZone`/`SpellStorm`/
  `SpellTornado`/`SpellBeam` loop-check `IDamageable`; `SpellEffect` uses `HitLayers`), so nothing
  legitimate is blocked â€” grep confirmed.

### Verdict
CONFIRMED (H4 + H1-rejected). Fix (`SpellCaster.cs:1076-1081`): at the top of `ApplyHit`, return an
empty `DamageResult` unless `target` or `target.transform.root` implements `IDamageable`. Overcharge
size/radius mechanic untouched (player wants no caps). Same-pass: PROGRESS.md `1cx`. Verified by
grep+reread only (rule 3); Unity play-test should confirm Asteroid/Earth Wall/Meteor no longer lurch
the world while enemies still take knockback. Legacy 1cv/1cw marks this trail's earlier theory as
rejected (regarding THIS symptom) â€” that history stays in the `1cv`/`1cw` sections.

---

## 1cv  - Crater dishes stay a LOCAL bowl: width bounded, depth UNBOUNDED (SHIPPED in `1cv`)

Player (play-test): "when i use asteroid skill the entire chunk moving for some reason, or could be
the entire terrain." Follow-up (verbatim): "i want no limit on my game / no limit."

### Hypotheses & evidence
- **H1 - Earth Meteor's crater fed the FULL blast splash straight into the deformer, so one cast
  lowered every corner inside the whole 30-tile ChunkSize dish at the same time.** CONFIRMED.
  `SpellCaster.ResolveZone` built `radius = spell.Radius * sizeScale` (Earth Meteor `Radius` 9 m,
  up to ~13+ tiles with charge `sizeScale`) and handed that whole radius to
  `TerrainDeformer.Apply(center, radius, Crater, fwd)` (SpellCaster.cs:986). `TerrainDeformer`
  lowers **every** corner inside `reach = radius + feather`, so a radius of 9-13+ tiles dropped the
  whole chunk at once - read as "the entire chunk / the entire terrain moved." This is the driver.
- **H2 - the whole chunk streams/reloads or the chunk teleports.** REJECTED. Each tile's height
  lives in the mutable terrain stream state (WorldStreamer) and is only addressed by fixed chunk
  coords; the heights themselves are the thing deformed. Nothing relocates a chunk.
- **H3 - the player wanted a depth/global cap (games usually clamp).** REJECTED - the player's "i
  want no limit on my game / no limit" is explicit, so DEPTH must stay unbounded (`CraterStep`
  ratchets ~1.1 m deeper per cast with NO floor, per 1cs) and the fix must live on WIDTH.

### Verdict
CONFIRMED (H1). Fix (`SpellCaster.cs:995-998`): bound the crater **WIDTH** to the spell's small
local delivery dish (`DeliveryRadius * 0.5`, ~2 m for Earth Meteor, floor-clamped to 0.5 m) instead
of the blast splash; **DEPTH stays unbounded** - each cast still ratchets `CraterStep` deeper with
no floor cap (per the player's "no limit"). Same-pass: game-design.md 3.8 (width bounded / depth
keeps no floor) + PROGRESS.md `1cv` + THINKING.md. No new QA lane (existing crater deform lane on
NewWorldTestGround covers the shape).

---

## 1cw â€” AoE *still* reshaped the whole chunk: raised shapes + projectiles fed the charge-scaled blast radius (SHIPPED in `1cw`)

Player (follow-up, verbatim): "still that bug where if player using an aoe skill it would move the
entire terrain." This arrived right after `1cv` fixed the Meteor crater, so the same whole-chunk
symptom survived on a DIFFERENT path.

### Hypotheses & evidence
- **H1 - `1cv` only bounded `Crater`; every RAISED shape still fed `radius = spell.Radius * sizeScale`
  into the deformer, and `sizeScale` is unbounded (overcharge).** CONFIRMED. In
  `SpellCaster.ResolveZone` the fix was `deformRadius = shape == Crater ? dish*0.5 : radius`, so
  Tremor (Ring), Spire Field (Spikes), Earth Wall (Wall), Landslide (Wall), Stone Pillars (Pillar)
  still passed the charge-scaled blast radius (SpellCaster.cs:997). `SpellChargeLevel` has no upper
  clamp (PlayerController.cs:1501 returns `(t-0.15)/(1.2-0.15)` unbounded), so a player who holds
  RMB can drive `sizeScale = 1 + charge*1.2` to 3.4+ before FP drains - a 3 m-delivery raise becomes
  a ~10 m+ raise spanning a whole chunk. Death-march match: any AoE skill (not just crater).
- **H2 - the projectile dent also scaled by charge.** CONFIRMED. `SpellEffect.ResolveProjectileImpact`
  did `dentRadius * _radiusMult` where `_radiusMult = sizeScale`; charged Stone Shard (Earth, Crater
  dent `max(1.2, Radius)` = 1.2 m) carves `1.2*3.4 â‰ˆ 4 m+`. Same width-leak family.
- **H3 - Storm/Summon were also leaking.** REJECTED. `SpellStorm.DeformGround` uses
  `max(Radius*0.55, 1.2)` (no sizeScale) and Summon uses `min(Radius*0.4, 2.5)` â€” both already
  dish-capped. Grep confirmed no other `TerrainDeformer.Apply` call sites (ToolManager Dig, test
  lanes fixed radii).

### Verdict
CONFIRMED (H1+H2). Fix: **width-capped for EVERY shape**, charge still scales the damage splash
(`radius`/`zoneRadius` untouched) but never the ground edit.
- `SpellCaster.cs:999-1001`: `dish = max(Radius>0 ? Radius : 1.6f, 0.5f)`;
  `deformRadius = Crater ? dish*0.5f : dish`.
- `SpellEffect.cs:262-270`: dent `max(1.2, Radius)` (Earth) / 1.4 m (others), `_radiusMult` dropped â€”
  also restores the doc's "fixed ~1.4 m non-Earth dent" to literal truth.
Crater depth remains unbounded (player's no-limit rule). Same-pass: game-design.md Â§3.8 (width bounded
for every shape incl. raised, `1cw`) + PROGRESS.md `1cw` + THINKING.md. Verify by grep+reread only
(rule 3); Unity play-test should confirm a **charged** raise/AoE stays a local dish.

---

## 1cu â€” Player HUD status strip under the bars (SHIPPED in `1cu`)

User asked: "add a show status function that show player the status effect that they're having under
their bars." Raw trail:

### Hypotheses & evidence
- **H1 â€” there is a dedicated player status tracker/manager to hook into.** REJECTED. Exploring
  `Assets/Scripts/Combat/Effects/` showed statuses are **stapled to the target's root**:
  `SpellDoT` (Burn/Poison/Rot/Bleed DoT), `WetStatus`, `BlindStatus`, `ChillStatus` (build gauge).
  No registry, no manager â€” each is a root `MonoBehaviour` with its own timer. So the HUD must
  poll `player.transform.root` for the components; no event bus exists or is needed.
- **H2 â€” remaining-time is uniformly readable.** MOSTLY REJECTED: only `SpellDoT.Remaining` was
  public. `WetStatus`/`BlindStatus` hide `_expiresAt` behind a private field and `Update()` destroys
  on expiry. Decision: add a tiny additive `Remaining` property to each (reads `_expiresAt -
  Time.time`), keeping behaviour identical. `ChillStatus` has NO timer at all â€” it's a decaying
  integer gauge decaying 1/sec that either converts to a Frost slow or self-destroys; a full freeze
  (`IsFrozen`) flips `_cold=0; Destroy(this)` on the next `Update`, so "FROZEN" literally cannot be
  displayed from this component. Display the gauge (`CHILL n/5`) instead.
- **H3 â€” the food/drink stamina buff is displayable from existing fields.** The user picked
  "combat statuses + food/drink buff". `ToolManager` calls
  `PlayerController.ApplyStaminaRegenModifier(0.5f | 1.2f, 120f)`; the expiry was already stored as
  `_staminaRegenModifierUntil` (used in `HandleStamina`). Added read-only `StaminaBuffRemaining` +
  `HasStaminaBuff` â€” pure exposure, no gameplay change. Sign/pct derived from the public
  `StaminaRegenModifier` field.
- **H4 â€” one bulletproof rendering style.** User chose "colored square + text" over text-only chips.
  No icon assets exist anywhere, so a plain `Image` colored per status + centered `TextMeshProUGUI`
  is the honest minimum; a `GridLayoutGroup` (4 fixed columns, 82Ã—24 cells) wraps to a second row and
  the panel height is resized to fit the row count. Pool of 10 chips is enough (max ~6 concurrent
  statuses today); chips update only when color/text actually change (mirrors the bars' label
  caching), so the strip never allocates per frame in steady state.
- **H5 â€” QA lane ordering matters.** `WetStatus.Apply` actively *douses* an active Burn
  (`SpellDoT.RemoveType(Fire)`), so the demo applies Wet FIRST, then Burn â€” otherwise the DoT is
  cancelled instantly. Chill twice while wet gives +2+2=4 stacks (below the 5-threshold the "freeze"
  path would destroy the component); a third stack would throw it into the invisible frozen/self-
  destroy state, so the demo stays at 4/5 to keep the gauge visible while it decays. Placement is
  direct-to-player (no `PlatformTopY` geometry, so the rule-4 lane convention is trivially obeyed).
- **Verdict:** H1 rejected (no tracker â€” poll components), H2 fix (add `Remaining`), H3 confirmed
  (buff already timed), H4 style as above, H5 ordering confirmed. Feature shipped in `1cu`;
  verification by source review (rule 3), play-test listed in `PROGRESS.md ## 1cu-status`.

---

## 1ct â€” Play-test fixes: pink terrain (1cs shader) + casual fighting pose (1cr revert) (SHIPPED in `1ct`)

### VERDICT
Two play-test bugs, two root causes, both confirmed by source review (no build, rule 3):

1. **Pink terrain** = `TerrainLayered.shader` got **rejected entirely**: its ForwardLit vertex called
   `GetVertexNormalInputs(input.normalOS, input.normalOS)` â€” URP 17.5 has no `(float3, float3)`
   overload, only `(float3)` and `(float3, float4 tangentOS)` (`ShaderVariablesFunctions.hlsl:22,31`)
   â†’ HLSL compile error â†’ subshader fails â†’ Unity SRP shows magenta (the shader's URP-Lit `FallBack`
   is **not** used under URP). Trees/rocks looked fine because they keep their own working URP Lit
   materials â€” only the ground material was broken. Fix: single-arg `GetVertexNormalInputs(input.normalOS)`.
2. **Fighting pose in normal mode** = `1cr`'s `WeaponsDrawn => FightingMode || firstPerson`
   (`PlayerController.cs:1861`) kept the weapon drawn at port arms in every first-person frame,
   casual included. Fix (confirmed with user: "draw only while fighting"): `WeaponsDrawn => FightingMode`.

### Hypotheses & evidence
- **H1 â€” pink terrain is a shader *name/path* problem (e.g. material lost the shader on reload).**
  REJECTED. `GameBootstrap` assigns the layered shader to the ground material by name at boot; a
  missing/dropped shader reference would pink EVERYTHING on the ground renderer, which it did â€” but
  the giveaway was that trees/rocks (plain URP Lit) were unaffected, so the ground's **own material**
  was at fault, i.e. its shader failing to compile (not a shared/global shader that all materials
  reference). Compile failure under SRP => magenta.
- **H2 â€” the pink is a missing *keyword* (fog / shadows) tricking URP into an error branch.**
  REJECTED after reading `Core.hlsl`/`Lighting.hlsl` include chain: keywords select variants, they
  don't produce magenta. Magenta requires the shader itself to be uncompiled/rejected.
- **H3 â€” the exact HLSL error is the `GetVertexNormalInputs(f3,f3)` call.** CONFIRMED (compile error
  in the strict sense â€” no such overload). Audited every other API call against the 17.5 package:
  `UniversalFragmentPBR(InputData, SurfaceData)` exists exactly at `Lighting.hlsl:302` with matching
  field names; `TransformWorldToShadowCoord` lives in `Shadows.hlsl:356` (reachable via
  `Lightingâ†’RealtimeLightsâ†’Shadows` and `Lightingâ†’GlobalIlluminationâ†’SphericalHarmonicsâ†’Shadows`);
  `MixFog`/`ComputeFogFactor`/`GetVertexPositionInputs` fine; ShadowCaster's `_LightDirection`,
  `_LightPosition`, `ApplyShadowBias`, `_CASTING_PUNCTUAL_LIGHT_SHADOW`, `UNITY_REVERSED_Z` all match
  the URP 17.5 `ShadowCasterPass.hlsl`. Only the overload was wrong. The tangent overload needed a
  `float4` tangent â€” the terrain has no tangent stream and no normal map, so the single-`float3`
  overload is exactly right.
  - Side-check: shader references `half4 _Color; float _UseVertexColor;` at file scope without a
    `CBUFFER UnityPerMaterial` â€” legal (plain uniforms), just not SRP-batcher friendly; not the bug.
- **H4 â€” the fighting pose is a `/pose` console command or an animation state leak.**
  REJECTED. Grep found no pose command; the pose is entirely driven by `WeaponsDrawn` â†’
  `ReApplyWeaponPose` â†’ `WeaponRigBuilder.ApplyPose(gameObject, draw)`. `ApplyPose` (`WeaponRigBuilder.cs:363`)
  is binary draw/stow (`WeaponStowAnimator.Snap/SetPose`) â€” there is NO neutral-carry pose, so the
  only ways to kill a port-arm look in casual are (a) stow (what the user chose) or (b) build a whole
  new "carry" pose API (rejected: not what the user asked for).
- **H5 â€” boot/test-ground drew the weapon regardless of `WeaponsDrawn`.**
  PARTLY TRUE: `NewWorldTestGround.SpawnAllWeapons` called `pc.ReApplyWeaponPose(instant: true)` at
  spawn with a comment claiming first-person visibility â€” under the 1cr rule that DREW the weapon at
  boot. With the revert, the same call now STOWS at boot (single code path) and the stale comment was
  rewritten. No extra toggle exists.
- **H6 â€” `WeaponsDrawn` revert is safe for all *hand-visibility* consumers.** CONFIRMED by grep:
  `CharacterInfoUI` (2613, 3612) only uses it to pose the preview after equip/cycle (stowed while
  casual is correct); `CameraModeSwitch.SetMode`'s `ReApplyWeaponPose` becomes a harmless no-op when
  casual; `LoadPlayerModel` re-rig + respawn re-pose flow through the same property. The arms/hands
  themselves stay visible in first person (that's `CameraModeSwitch` layer logic, untouched) â€” the
  player still sees their hands, just not a raised weapon.

### Dead ends & gotchas
- **FallBack is a red herring for SRP:** `FallBack "Universal Render Pipeline/Lit"` in the subshader
  is ignored under URP â€” don't rely on it to save a broken pass. The only fix path is a valid
  subshader.
- **`VertexNormalInputs` vs `TransformObjectToWorldNormal`:** I considered dropping the struct and
  just normalizing `TransformObjectToWorldNormal(input.normalOS)` â€” equivalent for a no-tangent
  surface, but the struct form is the URP-idiomatic one and shares the code path the working */
  /* URP-content objects use, so I kept `GetVertexNormalInputs(f3)`.
- **Doc drift:** `PROGRESS`'s `1cr` entry and `THINKING`'s 1cr verdict describe keep-drawn-in-first-
  person as shipped behavior â€” 1ct explicitly **reverses** that decision per play-test feedback;
  `game-design.md` Â§3.6 and Â§5.5 were rewritten in the same pass so no doc still claims the old rule.

### Follow-up: ShadowCaster `_LightDirection` undeclared (fixed in 1ct second commit)
- **New evidence after the first 1ct fix:** ForwardLit now compiles, but the ShadowCaster pass failed
  with `undeclared identifier '_LightDirection' at TerrainLayered.shader(145)`. My 1ct H3 audit was
  wrong in one detail: I confirmed the *names* `_LightDirection`/`_LightPosition`/`ApplyShadowBias`
  exist in URP 17.5's `Shaders/ShadowCasterPass.hlsl` â€” but those two `float3` globals are declared
  **inside that pass-utility file** (`ShadowCasterPass.hlsl:13-14`), NOT in `Shadows.hlsl` (which only
  defines `ApplyShadowBias`/`ApplyNormalBias` functions and shadow-matrix helpers). Since the custom
  caster includes only `Core.hlsl` + `Shadows.hlsl`, the variables were never in scope. **CONFIRMED:
  URP declares them itself only in its own inclue-able shadow-caster file `ShadowCasterPass.hlsl`.
  Fix: declare `float3 _LightDirection; float3 _LightPosition;` in the caster's `HLSLPROGRAM` block,
  byte-for-byte matching URP's own declaration** (verified again by reading `ShadowCasterPass.hlsl:13-14`
  right before the fix). Lesson: when a URP pass-support file declares its uniforms in the body of
  that file (not in a `ShaderLibrary/*.hlsl`), a custom shader must duplicate those declarations.

### Follow-up: still black after the GetShadowCoord fix â†’ shadow-free explicit Lambert (fixed in 1ct fourth commit)
- **Symptom:** terrain AND test-ground platform both black; trees/rocks/player fine; the
  GetShadowCoord change changed nothing.
- **H1 â€” screen-space shadows.** REJECTED (again, harder): read `Assets/Settings/PC_Renderer.asset`
  â€” the Forward renderer has no screen-space-shadow feature (only SSAO, `m_ShadowTransparentReceive`),
  so `_MAIN_LIGHT_SHADOWS_SCREEN` can't be the active variant. The whole 1ct-third-commit theory
  was chasing the wrong keyword set. Also read `MainLightRealtimeShadow` (17.5 Shadows.hlsl): when
  `MAIN_LIGHT_CALCULATE_SHADOWS` is not defined it returns `half(1.0)` â€” full light â€” so a missing/
  garbage shadow coord CANNOT zero the direct term in this pipeline. Direct light was never the bug.
- **H2 â€” the platform is black because of vertex colors.** CONFIRMED: `NewWorldTestGround.BuildTestGround`
  builds the top as `GameObject.CreatePrimitive(PrimitiveType.Cube)` and assigns `streamer.GroundMaterial`
  â€” the layered shader with `_UseVertexColor = 1`. A stock cube has no COLOR channel â†’ vertexColor
  reads (0,0,0,0) â†’ `albedo = _Color * black = black` â†’ black cube (URP Lit grass earlier worked
  because Lit ignores vertex colors). Fix: `PlatformMaterial` returns a plain URP Lit `GrassGreen`
  material instead (a platform has no strata bands anyway).
- **H3 â€” the TERRAIN is black from the same missing-color path.** REJECTED: `BuildMergedMeshData`
  fills `Colors` (ChunkMeshGenerator.cs:470) and `CreateMeshFromMerged` uploads via
  `mesh.SetColors` (line 596-597); chunk load/rebuild paths both verified to include colors. Terrain
  albedo is genuinely green on the GPU â€” yet output still black.
- **H4 â€” `UniversalFragmentPBR`/`InputData`/`SurfaceData`/BRDF plumbing outputs ~0 for this
  hand-rolled input regardless of light.** ACCEPTED as the working theory (with H2 kept for the
  platform): after three build rounds the remaining moving part is exactly that machinery, and it
  cannot be audited to zero-guarantee from the package in reasonable time. **Decision (user's call:
  "fuck the shadow"): delete it.** The ForwardLit now shades with three source-verified functions
  that are individually trivially correct â€” `GetMainLight()` (RealtimeLights.hlsl:89,
  shadowAttenuation = 1, no shadow data needed), `LightingLambert(half3,half3,half3)` (Lighting.hlsl:32,
  `color * saturate(dot)`), `SampleSHVertex(half3)` (GlobalIllumination.hlsl:45, sky ambient) â€” plus
  `ComputeFogFactor`/`MixFog`. `direct = albedo*LightingLambert(sun...);` is positive whenever the
  scene's main directional sun exists (it does: SampleScene.unity "Directional Light", intensity 2),
  so a fully black terrain becomes impossible. Cost: the terrain no longer samples realtime shadow
  maps (still casts via ShadowCaster). Re-add shadows later on a known-good base if needed.
- **Confirmed verdict:** H4 + H2. Fixed in 1ct fourth commit; `PROGRESS` notes it. Play-test
  checklist: terrain shows lit green strata (day), sun side brighter than sky side, fog at distance,
  digging exposes dirt/stone bands; platform top + legs render grass/brown respectively.

---

## 1cs â€” Infinite digging depth + terrain strata (grass â†’ dirt â†’ stone) (SHIPPED in `1cs`)

### VERDICT
Digging now goes infinitely deep (bounded only by the Â±200 m mesh-sanity band) and the terrain is
vertex-colored at mesh-build time into discrete strata bands (grass â†’ dirt â†’ stone) derived from
`pristine noise at the corner âˆ’ current vertex Y`. Craters ratchet a fixed `CraterStep` down per
cast/swing (deliberately NOT idempotent anymore), while the raised shapes keep their idempotent
`Max`-cap. No save-format or hash change â€” the colors are derived, never stored.

### Hypotheses & evidence
- **H1 â€” store a per-corner "layer" value in the save.** REJECTED. Would change `ChunkTileMod`'s
  4-float payload, the `tc_*.dat` format version/hash, `AntiCheat`/`ChunkSync` hashing and every
  save-format doc/const. Too wide a blast radius for a cosmetic read. The save already stores each
  corner's CURRENT height; depth below the *pristine* surface is the only extra datum needed, and it
  is recomputable from the seed (deterministic).
- **H2 â€” derive band from height alone (absolute Y).** REJECTED. Terrain rolls over ~Â±60 m; banding by
  absolute Y would paint whole mountains dirt/stone and river-beds grass regardless of excavation.
  Depth below the local noise surface is the only self-consistent measure ("how far am I below where
  this corner was born").
- **H3 â€” depth from a saved "original height" copy in memory only.** REJECTED after closer look: on
  first load after an edit, pristine height doesn't exist in memory for a loaded chunk (only the
  saved deformed corner + the noise formula). Since the noise is pure-deterministic function of the
  seed + corner coords, sampling `GetHeight(seed, cx, cz)` directly is both simpler and always
  correct, for pristine, deformed, and raised corners alike. **CONFIRMED as the implementation.**
- **H4 â€” idempotent "grind" is fine (revert the 1cm crater clamp, keep everything else).**
  **CONFIRMED by user.** The user explicitly wants digging to be able to go arbitrarily deep, which
  is the exact inverse of the `1cm` "crater floors clamp at noise âˆ’ 1.8" idempotency. Decided to keep
  the raised shapes idempotent AND make craters compounding again (the pre-1cm behavior) â€” a clean
  split, and the `1cm`/`1cj` fixes that matter (no whole-chunk rise, smooth per-corner profiles,
  mesh atomicity, no flat slabs) are untouched. The old cap code (`CraterMaxDepth`) and its XML
  claims were fully removed; stale prose in `DeformAt`'s doc and `game-design.md`/`magic-skills.md`
  was rewritten in the same pass so no doc still claims craters clamp.
- **H5 â€” band boundaries as soft blends vs hard cuts.** User asked for "discrete strata bands" â€” small
  blends (0.3 m) keep the band transitions readable as layers without hard machÃ© seams, per-corner at
  the SAME coords used for the height, so the color field is watertight across tile edges
  (neighbours share corners â†’ identical colors â€” same contract that keeps the mesh gapless).
  **CONFIRMED.**

### Dead ends & gotchas
- **Rename drift:** the first implementation named the band constants `*BlendStart/BlendEnd`, then the
  public rename to `*BandStart/BandEnd` left `TerrainBandColor`'s body referencing the old names (a
  compile error caught by grep before doc pass). Fixed + grep-verified no `BlendStart`/`CraterMaxDepth`
  remain in `Assets/Scripts`.
- **Wall-pass color origin:** wall vertices are chunk-LOCAL `(ex, ez)`; their world corner is
  `tiles[0].Coord` (the chunk's min-tile world coord) + `(ex, ez)`. The defensive `tiles` fill in
  `BuildMergedMeshData` guarantees `tiles[0]` non-null before the color pass, so no NRE. Both
  `BuildMergedMeshData` call sites build the 900-array in the same (lx, lz) local order â€” cross-checked
  the two call sites in `WorldStreamer.cs:407` and `:879`.
- **`PatchRegion` must pass the seed too:** the newly-added seed parameter demanded a call-site update
  that the project can't compiler-check â€” grep-verified exactly 2 `PatchRegion` refs (definition +
  `WorldStreamer.cs:1120`), both 6-arg after the edit.
- **Crater rim "digs sideways":** repeating a cast at the same center ratchets the rim corners down a
  hair too (small `s`), slowly WIDENING the pit as it deepens. Accepted â€” reads as a natural bowl and
  matches "repeat casts keep digging"; flagged in the play-test checklist, not a bug.
- **Shader is the unverifiable risk:** no build is run (rule 3); `TerrainLayered.shader` is a hand-
  written URP ForwardLit (PBR + fog) + ShadowCaster + DepthOnly. If it fails to compile under URP 17.5
  the terrain goes magenta/pink in play-test and the fix is a shader compile pass, NOT the C# code.
  Fallback chain (`Shader.Find` â†’ URP Lit â†’ white base) is intentionally boring.
- **Tool gating uses the same math:** `shovel` gate compares `DigDepthAt(hit.point)` against
  `StoneBandEnd` (2.7); the pit floor at the hit point is interpolated across its corner heights and
  noise, and the floored integer corner sample is a faithful proxy for the band the tool is digging
  in. `Dig` shares the Crater shape so tool pits and spell craters stay one code path.

---

## 1cr â€” Weapon not visible on hand (held in inventory slots) (RESOLVED â€” shipped in `1cr`)

### VERDICT
Equipped weapons were stowed onto body anchors (`StowBack`/`StowWaist`) whenever NOT fighting; in
first person those anchors are behind the head-mounted camera, so the weapon could never appear on
screen. Fixed by making "visually drawn" = `FightingMode || firstPerson`, re-applied on combat
toggles, camera switches (F5), model rebuilds, and equip. Only third-person casual sheathes.

### Hypotheses & evidence
- **H1 â€” a weapon id is missing a `WeaponModelBuilder.Build` case â†’ null visual.** REJECTED. Checked
  every catalog id vs the dispatch; all 18 weapons (incl. the 3 shields) have builders.
- **H2 â€” weapon renderers land on a culled layer in first person.** REJECTED. `CameraModeSwitch`
  culling only clears the body bit (layer 6); rigs stay on Default (layer 0), visible in both modes.
- **H3 â€” attach/hand-bone lookup fails.** REJECTED. `FindHand` resolves `PlayerModel/Torso/ShoulderX/
  â€¦/HandX` for the standing model and `ReparentToHands` re-seats park-fallback rigs after rebuilds.
- **H4 â€” the weapon IS stowed, but the stow anchors are positioned behind the first-person camera.**
  **CONFIRMED.** `ToggleCombatMode` casual â†’ `ApplyPose(draw:false)` â†’ `WeaponStowAnimator` moves the
  rig to `StowBack`/`StowWaist` under Torso; the first-person camera looks forward from the head pivot
  and never sees behind the body. Fix: draw whenever first person.
- **H5 â€” stow/draw could stay per-combat-only if the user prefers.** User chose "Show in hand when
  first person" over "always in hand / drop stow" and "keep stow everywhere" â€” so sheathing only in
  third-person casual was the accepted target.

### Implementation notes
- Central source of truth: `PlayerController.WeaponsDrawn` + `ReApplyWeaponPose`.
- Combat toggles pass `instant:false` to keep the existing draw/stow *animation*; model rebuild /
  camera-switch equip-snap use `instant:true`. `CameraModeSwitch.SetMode` invoking the player's
  re-pose is safe during Awake because `ReApplyWeaponPose` no-ops without a `CombatController`.

---

## 1cq â€” Translucent Wind/Ice + denser Fire projectiles (RESOLVED â€” shipped in `1cq`)

### VERDICT
Wind/Ice bodies made translucent by passing a lower alpha through the existing `"Sprites/Default"`
material; Fire made "hotter" by switching the sphere to `OrbFx.Ember` flicker and roughly doubling
the ember exhaust (`EmissionRate`/`MaxParticles` up, `StartSize` up). No new shaders or assets.

### Hypotheses & evidence
- **H1 â€” translucency needs a shader change.** REJECTED. `Sprites/Default` (the projectile body
  shader) already blends with `SrcAlpha/OneMinusSrcAlpha`, proven by existing semi-transparent
  visuals: `CCZone` (alpha 0.4 disc), `CastingCircle`, `AoeAimPreview`, `ProjectilePathPreview` â€”
  all just set `material.color.a`. So the fix is purely data: give the color alpha.
- **H2 â€” set alpha in `DamageNumber.ColorFor`.** REJECTED â€” it drives ~30 call sites (bodies,
  particles, UI, skill FX across many files). Overriding alpha inside the two builders keeps Wind/
  Ice translucent without tinting damage popups or other effects.
- **H3 â€” the `Unlit/Color` fallback would kill transparency.** Confirmed finding, not a fix here:
  `Materialize` picks `Sprites/Default` first and only falls back to the opaque `Unlit/Color` if the
  former is missing (it never is in practice). Recorded as a doc caveat, matching how the rest of
  the codebase (RingFader, CastingCircle) already relies on this.
- **H4 â€” Ice/Lance share geometry?** Ice's *Lance* shape (Ice Lance/Frost Pierce, explicit `Lance`
  builder) is a separate, solid spike â€” kept opaque. Only the Auto `Shard` frost chip becomes
  glassy, which matches "ice magic should be transparent" for the generic chip while named lances
  keep their heft.
- **H5 â€” fire = bump just one number.** REJECTED â€” a single bump reads as a minor density change.
  Combined three levers (rate Ã—1.67, cap Ã—1.75 for sustained flight, size Ã—1.33) + a flickery body
  gives the obvious "more fiery" read. Kept the existing additive `Particles/Additive` exhaust and
  the gradient fade (rates seen as alpha/brightness of the glow).
- **H6 â€” Comet/Scorch/Burn also need the boost.** Already Ember-flickered AND have their own dense
  `Comet(...)` body; exhaust boost applies automatically (per-element tuning is shared by all fire
  delivery visuals). No per-shape change needed.

### Known limits (noted, not fixed)
- Translucency is Z-write-off sprite blending: two projectiles crossing can overdraw, but projectiles
  are transient and fast â€” acceptable, same as friendly/enemy cast circles today.

---

## 1cp â€” Earth magic projectile as rock debris (RESOLVED â€” shipped in `1cp`)

### VERDICT (read this first)
Confirmed approach: a **new `ProjectileShape.Debris`** (tumbling grey rock-clump, dressed like
`WorldBuilder.SpawnRockDebris`) wired to the Earth school + an **impact debris burst** out of the
crater. Trail below records the dead ends checked.

### Hypotheses & evidence
- **H1 â€” swap the existing `Shard` builder to rock chunks.** REJECTED. `Shard` is Ice's `Auto` shape
  too (`SpellCaster.AutoShapeFor(Ice) â†’ Shard`), so editing it silently reskins frost chips. Instead a
  new enum value keeps Ice's diamond and gives Earth its own builder.
- **H2 â€” change only `AutoShapeFor(Earth)`.** INSUFFICIENT on its own. The Earth school's only
  projectile spell sets `projectileShape: ProjectileShape.Shard` **explicitly** on its `Spell(...)`
  line, so it bypasses Auto resolution. Confirmed via `SpellCastEffect Spell(...)` helper: the
  `projectileShape` param lands straight on `spell.Shape`. Fix = flip that one line AND the Auto
  default (for summoned-turret earth shots `SpellSummon.DecorateProjectile`, which pass `Auto`).
- **H3 â€” reuse the world debris pieces themselves.** The world chunks have `Rigidbody`/colliders and
  are interactable by the pickaxe/pickup system (`SmashDebris`, name `RockDebris`); reusing them as
  renderer-only projectile chunks risks the flight raycast self-hitting or pickups grabbing a
  fleeting chunk. The projectile keeps **renderer-only** primitives (matching the other shapes'
  "no collider" contract) but borrows the exact color formula.
- **H4 â€” tumbling = single Y spin (existing OrbFx path).** REJECTED for the chunks: a shared Y-spin
  looks like a drill, not debris. Added `Mode.Tumble` with a per-object `Random.onUnitSphere` axis
  captured in `Start()`; existing modes keep the old `Rotate(0, spin, 0)` branch untouched.
- **H5 â€” impact burst reuses the in-flight cluster builder.** REJECTED. The flight body is a static
  cluster parented to the projectile; impact needs independent physics chunks that fall out of the
  crater. Small throwaway `SpawnImpactDebris` (3-5 cubes, up-bias scatter, `Destroy` after 2.5 s),
  mirroring SpawnRockDebris' mass/velocity values.
- **H6 â€” could the burst chunks collide with the just-carved crater collider?** Terrain collider
  rebuilds once per deformer apply (1ck made the swap atomic) â€” the physics chunks spawn above the
  impact point and fall freely; the crater floor is solid walkable terrain. No special handling.

### Known limitation (noted, not fixed)
The StarEffigy/Golem **summon** spells (Stone Effigy/Sentinel/Guardian/Colossus) fire rocks via
`SpellSummon` with `Auto` shape â†’ they now read as the same Debris cluster. Consistent, intended;
not a regression.

---

## 1co â€” Race look on the block player model (RESOLVED â€” shipped in `1co`)

### VERDICT (read this first)
Confirmed approach: **palette + body ratios on the shared block model**, race resolved from the
model's parent at build time, uniform RigScale raised for the giants, and a model rebuild on race
change. Walking list of hypotheses below is the trail.

### Hypotheses & evidence
- **H1 â€” tint the block model via `RaceRig.RigTint`.** REJECTED. One flat tint hits every renderer:
  eye whites go dark, hair/clothes/pants/shoes can't differ, and RigTint is inherently uniform per
  race. `RaceRig.ApplyRace` therefore keeps *only* the uniform scale and the prefab-branch tint.
- **H2 â€” swap a per-race prefab at build.** REJECTED for now. Needs authored `.asset` bodies; nothing
  exists yet. `RigPrefab` stays as the documented later path (drop-in, no code change).
- **H3 â€” per-race palette + ratio data on `RaceData`.** CONFIRMED (chosen). 6 colors + 6 knobs with
  Human defaults = original colors / 1, so Human output is a strict no-op (checked `ApplyRaceLook`
  exits when all ratios == 1 and the colors fall back to the same constexprs).
- **H4 â€” rebuild the model on race change.** CONFIRMED as a gap: `OnActiveRaceChanged`
  (RaceChangeManager.cs:125) had **zero subscribers**; `LoadPlayerModel` runs on Awake/ApplyGender
  only. Fix: idempotent subscription in `LoadPlayerModel` guarded by `_raceSubscribed`. Dead end I
  avoided: subscribing in Awake only â€” respawn/reparent paths that reload the model may drop the
  subscription; guarding inside `LoadPlayerModel` covers every reload.
- **H5 â€” `ActiveRace` allocates 22 ScriptableObjects per get.** CONFIRMED (calls `BuildDefaultRoster`).
  Fix: static cached `RaceDatabase.DefaultRoster`. (RebuildIndex still runs the serialized `Races` on
  OnEnable â€” unchanged.)
- **H6 â€” cutscene models.** CONFIRMED via call-site trace: `BuildSeatedPlayerModel` parents the model
  to the **car**, which has no `RaceChangeManager` â†’ always Human. `null`-parent fallback also covers
  any other host; the seated model built off the player root (`PlayerSitController`) *is* race-aware.
- **H7 â€” head counter-scale.** The standing model's Hair/Eyes/Neck live under the **Torso pivot**, not
  the root, so a root-only scale would leave them embedded after Height/Bulk/RigScale â†’ must recurse
  and counter-scale names Head/Neck/Eye*/Hair*/Ponytail* by `(Head/Bulk, Head/Height, Head/Bulk)`.
  Dead end checked: relying on RigScale only would look correct until the first Height/Bulk knob â€” so
  ratios are authored on top of the uniform scale, ratios decide the *look*.
- **H8 â€” foot calibration.** Standing leg chain: Hip at -0.25, Thigh/Shin/Shoe bottom â‰ˆ -0.62 â†’ foot
  bottom = `root.y âˆ’ 0.25 âˆ’ 0.62`; after scaling root by h and legs by leg the bottom sits at
  `root.y âˆ’ 0.25*h âˆ’ 0.62*leg`; setting `root.y = h*(0.25 + 0.62*leg)` re-lands it at 0 (Human â‰ˆ 0.87
  vs 0.86 â€” 1 cm, absorbed by boots). Seated/sit models skip calibration (their pose isn't ground
  planted; a bottom-heavier sit for short-legged races is acceptable).
- **H9 â€” raise vs lengthen giants.** User chose **raise** (global `RigScale`, also enlarges the
  hitbox for "feel"). Goblin/Gnome scale unchanged (15% smaller hitbox is a real contract in Â§3.5).

### Known limitation (noted, not fixed)
`SaveManager` has no race field (grep of save/restore paths found none) â€” a saved game loads as Human
until the player re-picks a race. Pre-existing behavior; remains out of scope.

---

## 1cm â€” Earth Wall repeat cast "makes the entire chunk moving" (RESOLVED â€” fix shipped in `1cm`)

### VERDICT (read this first â€” the trail below is the *before* picture)
Confirmed root cause: **`DeformAt` was additive, not idempotent.** Raise was `current + s*lift`
(capped at `noise+lift`) and crater `max(current âˆ’ s*1.8, noiseâˆ’1.8)`. Because `current` already held
the previous cast's raise, a repeat cast added the raise **again**, lifting the whole influence
footprint toward the cap on each of the first several casts (the low-influence flanks included), so
the ground rose across a wide swath of the chunk on cast #2+ â†’ "the entire chunk moving". Craters
compounded identically (deeper each cast).

**My "provable no-op" claim below (H1) was WRONG.** The cap bounds the *final* height but does not
make the operation idempotent; the additive form still compounds up to that cap. The tell I missed:
`DeformAt`'s own comment admitted "repeat casts (which stack the ridge on the previous height)".
`1bo` bounded the height, not the compounding.

Fix shipped (`1cm`):
- `DeformAt` uses absolute per-corner targets â€” raise `Mathf.Max(current, noise + s*lift)`, crater
  `Mathf.Min(current, noise âˆ’ s*depth)` â†’ idempotent.
- H7 addressed: `TerrainDeformer.ResolveGroundTarget` skips a forward hit that is the chunk's OWN
  `ChunkObject` terrain collider **and** sits above pristine noise + 0.25 m (a reared wall), so a
  repeat cast targets the intended ground rather than the wall face. (NOTE: the wall is the terrain
  `MeshCollider` itself â€” there is no separate wall collider/layer, which is why the "ignore a layer"
  idea below was unworkable.)
- H8 addressed: `ChunkObject.ApplyMerged` re-points filter + collider at the new mesh before
  destroying the old one. (`PatchRegion`'s `null â†’ assign` is a single synchronous call; physics
  never observes the null, so it was left as-is with a comment.)

The hypotheses below are kept as the raw trail; see each for its final status.

### The report
> "when cast earthwall it only work the first time and the next time it make the entire chunk moving"

Repro facts gathered from the user (3 clarifying questions):
- **Deterministic** â€” happens on **every** cast after the first, never intermittent.
- **Same spot, standing close** â€” the second cast targets roughly the same spot as the first, and the
  player is standing near where the first wall came up.
- **"Moving" is not pinned down** â€” the user can't say for sure whether it's the player being
  shoved/sinking, the chunk's surface re-meshing, or the chunk relocating. So the investigation has
  to be robust to all three readings.

### Prior related history (from PROGRESS.md)
This is the third report in the same symptom family, so start from what earlier fixes did:
- `1bn` â€” Earth Wall fix: taller blocking wall + no more player "teleport" on repeat casts (added the
  caster keep-out ring, `keepOutR â‰ˆ 0.9`, so raised shapes can't grow directly under the capsule).
- `1bo` â€” Earth Wall repeat casts no longer "shrink the world": raised shapes are **height-capped**
  (`newHeights = current + s*lift` then clamp), so they can't stack unbounded.
- `1cl` â€” per-corner caps (no flat plateaus) + tops-first merged mesh.

So the naive "it stacks taller each cast and launches you" cause was already fixed by `1bo`, and the
"flat plateau re-arms legacy walls" cause by `1cl`. This new report survives both, which is a strong
hint the remaining cause is NOT in the height math at all.

### Mental model of the cast pipeline (walked end-to-end)
1. `SpellCaster.ResolveZone` (`SpellCaster.cs:922-961`): ray from caster along aim â†’ `aimHit`;
   then a down-probe from `aimHit.point` â†’ `groundHit` â†’ `center = groundHit.point`.
2. `TerrainDeformer.Apply(center, radius, Wall, fwd)` â†’ `WorldStreamer.DeformAt`.
3. `DeformAt` (`WorldStreamer.cs:704-833`): builds the Wall ridge influence over an AABB of world
   corner coords, computes `newHeights[corner]`, calls `ApplyHeightEdits`.
4. `ApplyHeightEdits` (`1028-1066`): writes the 4 corners into every loaded tile in the AABB, bumps
   `Version`, `MarkDirty`, then per touched chunk `RebuildChunkRegion` + `FlushDirtyChunk`.
5. `RebuildChunkRegion` (`1075-1115`): if `ChunkContainsFlatTile` OR region â‰¥ 75% area â†’ full rebuild;
   else build just the region and `PatchRegion` the existing merged mesh.
6. `ChunkObject.PatchRegion` (`76-135`) / `ApplyMerged` (`51-75`): `SetVertices`/`SetNormals`/UV,
   `mesh.bounds`, then recook the `MeshCollider` (`_mc.sharedMesh = null; _mc.sharedMesh = mesh`).

### Hypotheses and verdicts

**H1 â€” heights stack/grind on repeat (dirty heights).** **WRONG â€” this turned out to BE the bug.**
(Originally mislabeled "REJECTED".) `DeformAt` computed `current + s*lift` (capped at `noise + lift`)
for raises and `max(current âˆ’ s*1.8, noise âˆ’ 1.8)` for craters. The clamp bounds the *final* height but
does **not** make the operation idempotent: `current` already includes cast #1's raise, so each repeat
cast adds it again and compounds toward the cap â€” lifting the whole influence footprint (low-influence
flanks included) and reading as the entire chunk's ground rising. Craters compounded identically.
`DeformAt`'s own comment admitted "stack the ridge on the previous height"; I read past it. Fixed by
switching to absolute per-corner targets with `Mathf.Max`/`Mathf.Min`.

**H2 â€” coordinate/frame mismatch (corners vs tiles, chunk origin).** REJECTED.
Checked `ChunkData.Size = 1f`, `TerrainChunkCoord.ChunkSize = 30`, `FromTile` `FloorToInt`, chunk
origin `(tc.X*30, 0, tc.Z*30)`, and `DeformAt` corner ints. All agree; negative coords handled. A
mismatch here could have shifted a whole region, so it was worth ruling out, but it's clean.

**H3 â€” `PatchRegion` writes into wall-band vertex slots (interleaved layout).** REJECTED (fixed by 1cl).
That was exactly `1cl` root cause B. `BuildMergedMeshData` is now tops-first (all 4Ã—900 top verts
contiguous, walls last), so `PatchRegion`'s fixed `(lz*cs + lx)*4` offsets are always valid. Confirmed
by reread (`ChunkMeshGenerator.cs:285-314` pass 2, `316-403` pass 3) and no other consumer of
`_merged.Vertices` exists.

**H4 â€” something literally translates the chunk transform.** REJECTED.
Grepped for `transform.position`/`SetParent` in `Assets/Scripts/World`; `CreateChunkGameObject` sets
the chunk position once at `(tc.X*cs, 0, tc.Z*cs)` and nothing moves/reparents a `ChunkObject` after.
`ChunkObject.Release` only destroys props + mesh. So "the chunk moved" cannot be a literal transform
move.

**H5 â€” the wall crest reads as a flat tile, flipping `ChunkContainsFlatTile` â†’ full rebuild.**
REJECTED. `IsFlatTile` needs all 4 corners within `0.001f`. A ridge tile's corners are
`noise(corner) + 2.6` with genuine per-corner noise slope, so they are not flat. (This was the
leading theory for a while and it's attractive because it would explain "every second+ cast" â€” cast
#1 creates the state that flips #2 â€” but the math doesn't support it.)

**H6 â€” the repeat cast's region crosses the 75%-of-chunk threshold â†’ full rebuild.** REJECTED for the
plain case. Default Earth Wall radius 3.6; charged max `sizeScale â‰ˆ 1.8` â†’ radius â‰ˆ 6.5, reach
â‰ˆ 7 â†’ AABB â‰ˆ 13Ã—13 = 169 tiles, far below `0.75 Ã— 900 = 675`. Would only matter if heavily charged
AND near a chunk edge; user says same spot every time, so not the default explanation.

**H7 â€” the second cast's aim ray hits the first wall's collider. ADDRESSED (real, but a secondary
contributor).** `ResolveZone` did a plain `Physics.Raycast(pos, fwd, range)` with no layer filtering,
so after cast #1 a repeat cast's `aimHit` could land on the 2.6 m ridge and leave `center` on/near the
wall instead of the intended ground â€” moving the deform + collider recook. There is no separate wall
collider/layer to filter (the wall IS the chunk's terrain `MeshCollider`), so the fix is geometric:
`TerrainDeformer.ResolveGroundTarget` now skips a forward hit only when it is the chunk's OWN
`ChunkObject` collider **and** sits above pristine noise + 0.25 m (a reared shape), then re-probes;
craters/ground/entities/props are never skipped. `ResolveZone` uses it.

**H8 â€” null-collider physics frame during recook. ADDRESSED (defensive).**
`ApplyMerged` used to do `_mc.sharedMesh = null; â€¦ _mc.sharedMesh = mesh` (and `PatchRegion` still
does). The corrupt-order path is fixed: `ApplyMerged` now assigns the new mesh to the filter/collider
**before** destroying the old mesh. `PatchRegion`'s nullâ†’assign is one synchronous call the physics
step never observes, so it was left as-is with a clarifying comment.

**H9 â€” full-chunk re-mesh re-emits side bands around the smooth ridge. REJECTED / not needed.**
H5/H6 already argued the default repeat Wall doesn't flip to `FullRebuildChunk`, and once H1 (additive
compounding) explained the chunk-wide rise, no full-rebuild re-mesh was required to account for the
symptom. Not investigated further.

**H10 â€” background-generate race. REJECTED.**
`BackgroundGenerateChunk` does run `BuildOrLoadChunk` off-thread, but the report is fully
deterministic (every second+ cast), which a race can't explain. Superseded by H1.

### Dead ends worth remembering
- Time was spent chasing a "fill origin" bug in `BuildMergedMeshData`'s defensive null-tile fill
  (`fillOrigin = tiles[i].Coord` of the first non-null tile, then used as the origin for later null
  tiles). It IS a latent correctness smell if the first non-null tile isn't at local (0,0), but the
  clean path (`BuildOrLoadChunk` on `anyMissing`) means the fill rarely fires, and it doesn't explain
  a repeat-cast symptom. Don't re-chase it for 1cm.
- Considered "player walks up the wall then the next cast embeds them" â€” but the `1bn` keep-out ring
  (`keepOutR â‰ˆ 0.9` around `casterFeet`) prevents a raise within a capsule radius of the feet, so the
  capsule can't be newly embedded by the cast itself.

### Facts to carry forward (verified by reread)
- **CORRECTED:** repeat-cast height math was **NOT** a no-op â€” the additive form compounded up to the
  cap (H1). Now genuinely idempotent via absolute `Max`/`Min` targets.
- Coordinates/origins are consistent (H2).
- Merged mesh is tops-first; `PatchRegion` offsets are safe (H3).
- Nothing translates a `ChunkObject` at runtime (H4).
- `IsFlatTile` and the 75% threshold do NOT flip for a plain repeat Wall (H5/H6).

### Next session plan (EXECUTED â€” outcome in VERDICT)
1. Instrument cast #1 vs #2 (temporary logging, or careful read): log `center`, which collider the
   aim ray hits, the route chosen (`PatchRegion` vs `FullRebuildChunk`), and the region `w,h`. This
   single step should collapse H7 vs H9.
2. Read `ChunkMeshGenerator.EdgeIsRaised` / `EdgeHeights` + collapse threshold; confirm whether a
   smooth 2.6 m ridge edge emits side bands on a full rebuild (H9).
3. Then decide the fix from evidence. Likely one or more of: (a) exclude the deformed terrain
   collider from the **aim** ray only, (b) make the collider recook atomic (no null frame), (c) keep
   repeat casts on the region-patch route.
4. Play-test: cast Earth Wall 3-4Ã— at the same spot standing on the chunk â†’ every cast must look and
   behave like the first (ridge â‰ˆ 2.6 m, no whole-chunk re-mesh, no jerk); walk away and back â†’ wall
   persists identical.

### Open questions I still wanted answered (resolved)
**Answered:** the compounding is per-cast and independent of charge/radius (the flanks compound up to
the cap regardless); the "move" is the terrain re-mesh at cast time, not a delayed physics pop (H8 is
not the cause); a Crater repeat also compounded (ground got deeper each cast), consistent with H1
rather than H7 alone.

- Was the first cast the DEFAULT radius or heavily charged? (changes H6/H9 exposure)
- Does the "move" happen at the instant of the cast, or a frame or two later (physics step â†’ H8)?
- Does a non-Wall repeat cast (Crater at the same spot) also do it? If no, H7 (aim hitting the raised
  collider) is a much stronger candidate than the generic recook/rebuild paths (H8/H9).

## 1im — magic weapon drifts a little further every cast (SHIPPED; awaiting play-test)

**Report:** the magic weapon rotates a little every time it is used, and after enough uses in a row it
stays permanently rotated. Not "the swing looks odd" — the *rest* pose is wrong, and it compounds.

### H56-a — "the stow/draw lerp is writing the weapon's local rotation and drifting" — REJECTED

First instinct, and the wrong one. `WeaponStowAnimator` does write `e.Rig.localRotation`, but it
slerps from the rig's CURRENT world pose (converted into the target parent's frame) to an **authored**
end pose from `DrawPoseFor`/`StowPoseFor`, and `Apply` is called with `k = 1f`, which is a set rather
than an accumulate. `SetPose` also early-outs on `drawn == _drawn`, so it cannot run repeatedly against
the same target. A lerp that ends on an authored value is self-correcting; this one was exonerated
without needing to run anything.

### H56-b — "the casting circle is rotating the weapon" — REJECTED

Plausible, because the circle is the other magic-only thing that takes the hand transform and runs
every aim frame. Read it: `CastingCircle` writes only **its own** `transform.rotation`
(`Quaternion.FromToRotation(Vector3.up, anchor.up)`) and parents its disc/runes/arcs under itself. It
never writes the hand or the rig. Ruled out by reading, not by guessing.

### H56-c — "`CaptureRest` samples the live transform, so the accent folds into the rest" — CONFIRMED

The mechanism. Three facts compose into a silent integrator:

1. Every animated frame is written as `Quaternion.Euler(_baseEuler + aEuler)` (`UpdateAttack`,
   `UpdateCharge`, and the sway's lit-focus pulse).
2. `CaptureRest` began each phase with `_baseEuler = transform.localRotation.eulerAngles` — a read of
   whatever is on the transform *right now*.
3. `End`, `StopSway` and `AbandonSway` all restore **to** `_baseEuler`.

So a phase that starts while the previous phase's pose is still applied captures that offset as its
own rest, and the restores then faithfully re-apply the polluted value forever. There is no unwind path
in the design, which is exactly why this presents as permanent rather than as a one-frame glitch.

**How the trigger reaches that window.** `PlayerController.Interactions.cs:389` calls `PlayCharge()`
on *every* LMB press, gated only by `WeaponTransitionBusy()` — which checks the draw/stow *lerp*, not a
charge phase in flight. The magic release tail is 0.38–0.66 s per weapon, so click faster than that
and the next capture lands mid-tail. I deliberately did **not** add input gating for this (asked, and
"mechanism fix only" was the answer): the drift is wrong regardless of how fast you click, and gating
the input would change feel while leaving the real defect in place.

### The step that named the mechanism in one look — a data asymmetry, not a magnitude

I nearly went looking for timing. The thing that actually settled it was noticing **only magic** drifted.
Reading `ApplyAccent`'s switch: `K_Staff` 14° roll, `K_Book` 16° yaw, `K_Wand` 10° roll + 26 % scale,
`K_Orb` 30° roll, `K_Lute` **scale only** — and *every* melee/ranged/shield def is `K_None` or
`K_Dual`, which has no case in the switch at all and returns zeros. So `K_Lute` cannot drift either, and
the claim "all magic weapons are affected" would have been **wrong** on the lute. Only the defs with a
non-zero `aEuler` can fold an offset, and that set is exactly the magic set minus the lute.

This is rule 13's "before deduplicating a value, ask what would catch the bug if that value were wrong"
turned on animation data: the useful signal was not how big the number was but which rows could produce
it at all. Corollary for the next time: when a per-instance bug lands on one category only, diff that
category's rows against the others **before** theorising about timing or frame order.

### The fix, and why it is two named methods

`SyncRestFromIdle` (write — only while `!_active && !_charging && !_guarding && !_swayActive`) and
`RestoreAuthoredRest` (read — restore, or no-op if the rest is not known yet). The guard condition is
load-bearing rather than cautious: the original capture existed so that **re-parenting** onto a hand
(`ReparentToHands`) would be harmless, and that intent is preserved exactly, because a re-parent
rewrites the local pose and an idle rig is by definition sitting on that new pose. Restricting the
write to the idle state is what keeps that fix while removing the integrator — the two requirements
looked contradictory until they were split by *when the pose is trustworthy*.

`StartSway` had the identical live capture and was routed through the same helper, so there is one
writer and one reader of the rest. `OnDisable` clears `_restValid` because a rig can be re-parented or
re-posed while disabled.

One nice property, not the reason for the design: a session that has *already* drifted self-heals on
the next cast, because `RestoreAuthoredRest` snaps back to the authored rest. No restart needed.

### Verification status

`tools\StaticChecks.ps1` does **not** cover `WeaponAnimator.cs` (it covers `WorldBuilder*` and
`NewWorldTestGround`, rule 3), so verification here is grep + reread: all 8 `transform.localRotation`
writers were enumerated and classified (7 read `_baseEuler` or `_baseEuler + offset`, 1 is the new
`RestoreAuthoredRest`), and the only writers of `_rest*` are `SyncRestFromIdle` and the `OnDisable`
invalidation. Balance 99/99 braces, 666/666 parens; the sole depth-0 member is the class declaration
itself. **No Unity compile or play-test has been run** (rule 3).
