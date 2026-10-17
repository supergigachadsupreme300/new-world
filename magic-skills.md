# Magic Skills - full tree list

Source: `Assets/Scripts/Combat/Skills/SkillCatalog.cs` (base skills) + `SkillCatalog.Magic.cs` (L1/L2 branch tables). Generated 2026-09-15. All costs are Focus unless noted; cast time 0.5s default. Projectile spells show their effective `shape:` out front from the summary above. `A("id")` = authoring reference to a base skill; `S("id", ...)` = skill node defined in the branch table.

**Statuses are automatic (§3.7):** every magic attack applies its element's signature status on hit — Fire→**Burn**, Ice→**Chill** (light slow), Lightning→**Stagger** (stun), Dark→**Blind** (black fog), Water→**Wet**, Arcane→**none** (pure force). A skill that lists its own status below overrides the default (e.g. Freeze / Deep Freeze use the heavier **Frost**). Wind = knockback, Earth = terrain reshape, Holy = heals.

## SpellDeliveries - what each delivery does

| Delivery | Behavior (resolved in `SpellCaster.Execute`) |
|---|---|
| **Instant** | No travel. If `SelfBuff` is set -> applies a timed buff to the caster (e.g. Wind Walk flight for `Duration`s). If `Heals` is set -> instant holy-touch heal on the caster. Otherwise a straight hitscan raycast up to `Range`, damaging the first target hit. |
| **Projectile** | Casts a bolt that flies along the aim at `ProjectileSpeed` up to `Range`; explodes/damages within `Radius` (explosion or direct hit). Applies status/knockback on contact. **Every projectile dents the terrain where it strikes** (`SpellEffect.ResolveProjectileImpact`): Earth craters (TerrainShape) are spell-scaled, every other magic bolt leaves a small ~1.4 m impact dent. **1l109: the dent AND the contact hitbox now grow with charge** — the dent scales by the same `sizeScale` (1.4 m → ~3.1 m at full charge, clamped to `MaxDentRadius` 5 m; a wider bowl, never a deeper one — depth stays `CraterStep`), and the contact detector is now a **charge-scaled swept capsule** (`OverlapCapsuleNonAlloc`, `ContactRadiusBase` 0.3 m × `sizeScale`) instead of a zero-width ray, so an overcharged bolt no longer slips past an off-centre enemy. **1l113: a projectile can additionally carry an in-flight ZAP AURA** (`ZapsInFlight`, Arc Volley): every `TickInterval` while it flies it damages each enemy or livestock animal within the charge-scaled `Radius` for **40%** of the cast's power and draws a jagged Lightning arc from the sphere to it. This is an ADDITIONAL effect — the bolt still detonates on contact/ground/expiry exactly as any projectile — which is why `Radius` is the scan reach here while the impact burst stays `BoltSplashRadius`. |
| **Chain** | 1l76 (Chain Lightning, testing.md #7): one instant strike on the aimed foe — or the nearest combatant within `Range` if the reticle is between two — then up to `ChainMaxJumps` (2) leaps to the nearest un-hit combatant within `ChainJumpRadius` (5 m) of the last victim. Every victim takes **full** power; each leg is a jagged `SpellBoltModelBuilder` bolt (caster→primary, then victim→victim), so the arc reads as one stroke. No aim preview (like Instant). |
| **Zone** | Ground-targeted AoE at aim point. Instant burst if `Duration`=0, else a persistent `SpellZone` that ticks damage every `TickInterval` for `Duration`. Earth school applies its `TerrainShape` (Crater / Ring / Spikes / Wall / Pillar, §3.8) first. `Heals` is set -> also mends allies inside. **Sky spells** (`summonFallingRock`, e.g. Fire Meteor / Earth Meteor) defer the burst ~0.6-0.8s: a rock formation summons high above, falls, and damage/knockback/terrain resolve when it lands (which formation is the spell's SkyRock axis). 1ir: four spells in this family — the meteor-line Asteroid left it, becoming a `CasterAnchored` following familiar instead. **1l70**: the redundant ice freeze-fields left this family — Deep Freeze became a Storm and Tundra became the Snow Drift familiar (see those rows). |
| **Wave** | 1l87: a ground-hugging wave front (`SpellWave`, spawned by `SpellCaster.ResolveWave`). Flattens the aim to horizontal, rides the terrain at a small random lift, and sweeps a damage band forward: every foe the band passes over takes **one** hit + one knockback (a per-cast `_hit` set enforces "once", never a per-frame re-resolve) as it travels `Range` at `ProjectileSpeed`, then dies. `Radius` is the band's half-width, charge-scaled. Used by **Fire Wave** (Fire's rolling surge, `shape:Wave`) and **Frozen Touch** (Ice's chilling roll), and — 1l111 — **Storm Lash** (`shape:StormWave`, a crest of ground-running lightning). The drawn floor is built by the same `MagicProjectileModelBuilder` path a projectile body uses; only the per-frame `WaveRipple` lives in the `SpellWave` component (rule 19). **1l116** adds its radial sibling **NovaWave** (next row). |
| **NovaWave** | 1l116: the radial sibling of **Wave** — the SAME `SpellWave` component in its radial mode (`SpellCaster.ResolveNova`), not a second sweep class (rule 8/13). There is no travel direction: the band is a CIRCLE growing from the caster outward — centred on the ground under the caster (the same `GroundUnder` snap a `CenterOnCaster` zone used), the front growing to `Range` at `ProjectileSpeed`, with the drawn `NovaRing` body scaled to `front / Range` so the ring travels with the front (1l116 follow-up: the body is not a flat disc but the Fire-Wave voxel floor bent into a ring — a band of cubes lifting into a crest at the leading edge, rolled outward by a radial `WaveRipple`, so it reads as a rolling wave). The hit test is the **annulus** `front ± Radius` around the fixed centre (XZ-only — the ring hugs the ground), and the same `_hit` set gives each foe exactly one hit as the front passes. `Radius` = band half-width (charge-scaled); `Duration` is **unused** (lifetime = `Range / ProjectileSpeed`). Used by **Static Nova** (`magic_lightning_fury_beam`), which keeps `centerOnCaster: true` for its under-feet aim preview only. |
| **Vortex** | Ground-targeted funnel. The Great Tornado (`magic_tornado`) = old environmental Tornado behavior (tall drifting funnel, physics drag/pull). All other Vortex spells = persistent `SpellZone` that ticks damage and drags enemies toward the center. Lifetime = `Duration` (or 5s). **1kc**: Conflagration (below) draws the firestorm body instead of the generic pull funnel — a ground circle over which **spinning ember rings spawn continuously** until the spell ends (1l6u) (`SpellLook.ZoneBody.VortexCircle`). |
| **Beam** | Channeled beam while the sustain input is held and focus upkeep (`ChannelDrainPerSecond`) is affordable. Ticks damage along the line; charge widens it and boosts tick power. Fades on release or when focus runs dry. **`BeamHalfAngle` > 0 (1ir) makes it a CONE, not a line**: the sweep becomes `ConeRays` (7) rays across `±BeamHalfAngle` and `ConeSegments` (2) capsule segments each, opening outward from a mouth at 0.35× the tip radius to the full `Width`; one shared impact flash per tick, and each target root is damaged once per tick however many segments touch it. **0 (the default) is the original single-capsule beam, untouched** — the axis is opt-in, so all ten earlier beams are byte-identical. The cone's drawn funnel (discs + debris + ring) is re-aimed with the live aim every frame like the tip orb, so a channeled cone sweeps with the player (1jz). **1kb: the drawn BODY holds its authored size** (testing.md task 5) — the shrink-and-enlarge breathing is gone: the line cylinder and the funnel discs keep the scale the builder wrote, and only the debris orbit + tip-orb glow throb continue, so a beam reads as held rather than breathing. **1l75: a Lightning beam's drawn body is a jagged re-jittering `BoltStrip`** (12 segments re-shuffled every 0.07 s) instead of the line cylinder — the school's identity, keyed on `DamageType == Lightning` (not a per-spell flag), with width/jitter derived from the beam width by `SpellBeam.BoltWidthFor`/`BoltJitterFor`. |
| **Summon** | Spawns a persistent object at the ground target. Damage summons = turret firing (projectile) at nearest foe; `Heals` summons = persistent heal aura. **`CasterAnchored` (1ir) spawns it on the caster instead** and makes it follow: each frame it re-samples the **nearest** ground beneath the caster (skipping their own colliders, so it does not snap to their head) and carries a persistent body behind it while it lives. It tracks XZ outright — no wall avoidance, so it clips through walls with you. A caster-anchored summon shows **no** ground AoE aim preview (1l6s): it stations behind the caster, so there is no aim point to mark. **1l6p:** the follower's body is a **fixed vertical translucent ring** of launch orbs behind the caster — NOT a circle sized by the targeting `Radius` (which is display-only for a forward-spraying familiar) — and each bolt leaves from a randomly chosen rim orb. **1l110** adds an authored *drawn* axis for the non-following case: `look: summonBody:` resolves to `Default` (the component's own totem/familiar choice, which is what every other summon draws) unless a profile grants `TeslaCoil` — Static Coil's stationary spire (base disc, tapering coil turns, one emitter orb). |
| **Follower upkeep** | 1ir: a **caster-anchored** summon lives for `Duration`, which scales on the charge ladder (`SpellCaster.DurationScale`, which is `SizeScale`). **1l6p:** the familiar's *drawn* body is a **fixed** ring, so charging no longer enlarges it — a full charge still buys a longer stream (`17.6s`) and higher bolt damage (`2.6×`), and the spawn flash still grows with `Radius`, but the persistent ring and its bolts keep their authored size. Continuous Fireball at full charge ≈44 bolts at 0.4s each. Ground-targeted summons are **not** affected: Ember Effigy keeps its 6 s however you charge. **1l70** adds the first Ice follower: Snow Drift (was Tundra) is caster-anchored, sprays chilling shards down the aim line, 8 s. |
| **Storm** | Persistent storm over the ground target: repeated element-styled strikes inside `Radius` for `Duration`, ticking every `TickInterval`. **Sky storms** (`summonFallingRock`, e.g. Meteor Rain / Rockfall) drop a small rock to each strike point; the strike's flash/damage/deform fire when that rock lands. **1l70**: a Storm may mount a persistent body through `SpellLook.StormBody` — Deep Freeze draws a frozen field (`FrozenField`, the 1l6z spikes) under its repeated freeze strikes; **1l111** adds `Rain` (Downpour's low rain shelf, a static lobed cloud hung with a curtain of streaks a `RainFall` component scrolls forever) and `Thunderhead` (a stacked cumulonimbus that REPLACES the generic puff for that spell); **1l112** adds `Maelstrom` (Devastation's larger four-band cumulonimbus at 22 m, also replacing that spell's legacy puff). **1l77**: a **storm-cloud** storm (`StormCloud`) mounts a dark cloud (`MapBuilder.BuildCloud`) 45 m above the area and draws each strike as a long multi-segment lightning bolt from that cloud down to the strike point, instead of the crossed-bar flash — the strike bolt falls from `SpellStorm._cloudY`, which is 45 m for a plain `StormCloud` storm but the body's own altitude for a `Thunderhead` (16 m) or `Maelstrom` (22 m) storm, so the cloud the player sees is always the cloud the bolts leave. |

Delivery fields: **Range** = max reach/travel; **Radius** = zone/explosion size; **ProjectileSpeed** = bolt speed (default 20); **Duration** = persistent-zone lifetime (0 = instant); **TickInterval** = seconds between ticks (default 0.5); **ChannelDrainPerSecond** = focus upkeep for Beams (0 = none); **BeamHalfAngle** = swept-cone half-angle in degrees for Beams (0 = legacy single-capsule line); **CasterAnchored** = Summon spawns at the caster and follows it; **BoltSplashRadius** = a follower's own burst radius; **ChainJumpRadius**/**ChainMaxJumps** (`SpellCaster` consts) = a Chain delivery's leap distance and leap count; **StormCloud** = a Storm mounts a dark sky cloud and strikes down long bolts from it; **SelfTeleport** = an Instant delivery teleports the CASTER up to `Range` along the flat look and hits foes at the landing, drawing a bolt between the two positions (Leap Bolt); **CenterOnCaster** = a Zone/Storm bursts under the CASTER rather than the aim point (1l116: for **Static Nova**, now a **NovaWave**, it no longer picks the delivery — it only keeps the armed AoE preview disc under the feet); **ZapsInFlight** = a Projectile zaps nearby foes every `TickInterval` while flying (`Radius` = scan reach, 40% power each + a drawn arc; Arc Volley).

`SizeScale`/`DurationScale` (`SpellCaster`) are **instance** methods over the inspector-tunable
`ChargeSizeBonus` field — do not make them `static`, which is CS0120.

Every terrain shape renders as **smooth feathered terrain**, written as continuous per-corner
heights and smoothstep-blended at the rim — never flat slabs. Raised caps are sampled **per-corner**
(each corner keeps its own natural slope), so Raised shapes (Ring/Spikes/
Wall/Pillar) cap at (noise + lift) and are **idempotent**: a repeat cast at the same spot
reproduces the exact same profile and never stacks higher (`1cm`). **Craters are the inverse
(1cs):** each cast excavates one `CraterStep` (~1.1 m at full influence) below the current floor,
so repeated craters dig progressively deeper — bounded only by 1l108's struck-surface floor (a
corner already more than a full step below the surface the crater struck is left alone, so a bolt
into a raised ridge does not dent the ground at its base) and by the ±200 m mesh-sanity band —
exposing the terrain's **strata bands**: grass → dirt (~0.65–2.3 m down)
→ stone (≥ 2.7 m down), painted per-vertex as small-blend bands (§3.8; the shovel stops at stone,
the pickaxe digs on). Flat tiles
saved by older builds (legacy whole-metre slabs OR fractional carve plateaus) are re-smoothed
toward their noise when loaded (§3.8, 1cl), deterministically, so the map never "re-randomizes"
on reopen; current shapes are never flat, so they are never re-smoothed.

## Projectile Shapes - what each projectile looks like

Every spell that uses the **Projectile** delivery has a `projectileShape:` visual. Each effect line ends with `shape:<Name>` (the *effective* shape, Auto resolved per school).

| Shape | Built as | Used by name |
|---|---|---|
| **Bolt** | Jagged segmented bolt along the flight axis, crackling afterimages (same technique as the thunder-event lightning, `SpawnJaggedBolt`) | every spell with "Bolt" in the name, e.g. Frost Bolt, Chain Lightning, Fork/Leap/Doom Bolt (1l110: Volt now wears `VoltBolt`, Volt Bolt `GreatBolt`; 1l112: Fury Bolt `FuryLance`) |
| **Sphere** | Classic orb on a fast warm flicker (`Ember` mode) with the densest ember exhaust in the game — fireball / plain orbs when the name gives no better shape |
| **Ball** | 1l71: a round hot voxel orb — renamed from the old diamond **Shard** so the fireball look is a ball | fireballs and generic orbs (Fire's chip slot) |
| **Shard** | 1l71: a thin long flat triangle, tapering to a point along the flight line — the frost/stone chip reprofiled from a spinning diamond to a flat sliver | frost & stone chips (Ice, Lightning, Holy, Dark, Earth, Water, Arcane) |
| **VoltBolt** | 1l110: short chunky jagged streak — 6 segments over 0.9 m tapering 0.20→0.09, the front half ramping toward white-hot so the tip reads as the zap's point | Volt |
| **SparkSwarm** | 1l110: nine tiny bright motes jittered in a loose cloud around the travel axis (a third lerped toward white) — a crackle of static rather than a dart volley (display-only body; the spell keeps `Missile` for its homing) | Volt Spark |
| **GreatBolt** | 1l110: heavy bolt — 10 segments over 1.7 m tapering 0.26→0.10 around a white-hot core rod, shedding 3 trailing flecks | Volt Bolt |
| **StormWave** | 1l111: the Lightning wave floor — the `Wave` lattice (rolling crest over a jagged spike column per floor column) re-tinted to the spell's colour, its crest carrying a `WaveRipple` so the ground-running crest shimmers as it sweeps | Storm Lash |
| **FuryLance** | 1l112: the heavy **forked** bolt — 11 segments over 2.0 m tapering 0.28→0.11 around a 0.08 white-hot core rod, two short white-tinted branches forking off the shaft near the head (longer and branching vs `GreatBolt`) | Fury Bolt |
| **Debris** | Tumbling cluster of grey rock chunks (mixed sizes, random spins, one leader) - the Earth school's "Stone Shard", dressed like world rock-debris; a short debris burst kicks up from the crater at impact | Stone Shard |
| **Lance** | Long thin spear of ice, heavier and faster than a bolt | Ice Lance, Frost Pierce, Glacial Impale |
| **Spear** | Thick dark spear, the heaviest of the linear shapes | Shadow Spear |
| **Blade** | Flat translucent cross-blade (alpha ~0.4) that spins in-plane with a shimmer envelope | Wind Blade, Razor Blade, Wind Scissor, Laceration |
| **Splash** | Rolling surge with a splash envelope that soaks on contact | Tidal Surge |
| **Comet** | Streaking fire with a trailing ember tail | Scorch, Burn, Frost Bite |
| **Missile** | Small dart with a soft halo; **homing** - re-evaluates its trajectory every frame and prioritizes the target that ends up on the flight path, bending to chase it | Arcane Missiles |
| **Soul** | Spectral wisp - a bright cold core in a soft translucent halo with a long tapering frost tail and two stray motes, so a homing shot reads as a spirit that hunts (display-only body; the spell keeps `Missile` for its homing) | Chill Soul |
| **Hand** | Spectral reaching hand - a flat translucent palm with four fingers leading into the foe, a thumb, a bright cold core and two ghostly flecks trailing (display-only body) | Chill Touch |
| **Dart** | Sleek single dart, thin and fast | physical shots (Archer Wind Shot, Taoist Talisman) |

Shapes are set per spell in the skill tables (`projectileShape:`), or left `Auto` to resolve by school: Fire=Sphere, Ice=Shard, Lightning=Bolt, Wind=Blade, Water=Splash, Earth=Debris, anything else=Sphere. `Auto` can never resolve to `Ember Streak` (1f7): it is authored-only, exactly like `Missile`, because the look layer must not grant a body a spell never asked for.

### Falling formations (1f7)

A `summonFallingRock` spell's rock comes from the spell's own resolved look, not from the delivery:

| Style | Body | Spells |
| --- | --- | --- |
| `Boulder` | one ragged rock (core + 4 ridges) | Fire Meteor, Meteor Rain, Earth Meteor, Rockfall |
| `Swarm` | 1f7: a smaller lead rock on the aim point + a flat fan of six around it | **removed in 1ir** (its last user, Fire Asteroid, became Continuous Fireball) |

The style is authored through `look: skyRock:`. Its `scale` is always the spell's **blast radius** — the style only decides how that radius is spent. The fan is flat (X/Z only) because the whole formation lands at one ground height, so vertical scatter would leave outer rocks floating on a slope. Every shipped spell resolves this axis to `Boulder` again as of 1ir.

### Beam widths (1ir)

`BeamHalfAngle` is the half-angle of the swept wedge in degrees, so the full angle the player sees is **twice** it:

| Spell | `BeamHalfAngle` | Full angle | Why |
| --- | --- | --- | --- |
| Flamethrower | 22 | 44° | the broad jet — opens from 0.84 m at the mouth to 2.4 m at the tip |
| Searing Ray | 5.625 | 11.25° | the narrow line of fire, deliberately four times tighter than the Flamethrower so two Fire beams are distinguishable by shape alone |

All ten earlier beams leave `BeamHalfAngle` at 0 and keep the original single-capsule line.

## Base skills (roots)

| Skill id | Name | Passive? | Kind | Effect | Desc |
|---|---|---|---|---|---|
| magic_focus | Focal Mind | true | Arcane | + 3 Intelligence (passive) | Permanent +3 Intelligence (max FP). |
| magic_arcane | Arcane Study | true | Arcane | + 3 Wisdom (passive) | Permanent +3 Wisdom (spell power). |
| magic_manaflow | Mana Flow | true | Arcane | + 2 Intelligence (passive) | Permanent +2 Intelligence (regen/FP, requires Arcane Study). |
| magic_fireball | Fireball | false | Fire | Active (Fire) - power 25, FP 15, cd 4s, Burn, shape:Sphere | Launch a fireball that burns the target. |
| magic_frostbolt | Frost Bolt | false | Ice | Active (Ice) - power 22, FP 13, cd 4s, Chill, shape:Bolt | Launch a freezing bolt that chills the target. |
| magic_lightning | Storm Focus | true | Lightning | + 3 Intelligence (passive) | Permanent +3 Intelligence (max FP), enfolding the storm. |
| magic_chain | Chain Lightning | false | Lightning | Active (Lightning) - power 28, FP 20, cd 5s, range 10, Stagger, chain | An electric strike that staggers the first foe it hits, then leaps to nearby enemies (requires Storm Focus). |
| magic_heal | Lesser Heal | false | Holy | Active (Holy) - power 15, FP 10, heals | Restore health with a holy miracle (requires Focal Mind). |
| magic_ward | Arcane Ward | false | Arcane | Active (Arcane) - power 14, FP 12, cd 3s, range 8, radius 2, knockback 1.5 | A protective arcane wave that shoves foes back. |
| magic_dark | Dark Bolt | false | Dark | Active (Dark) - power 24, FP 14, cd 4s, Blind, shape:Bolt | Fire a shadow bolt that blinds the target. |
| magic_blizzard | Blizzard | false | Ice | Active (Ice) - power 22, FP 28, cd 6s, range 8, radius 3.2, dur 2.5s, Chill | A frozen storm that repeatedly chills all inside with light frost. |
| magic_gust | Wind Gust | false | Wind | Active (Wind) - power 14, FP 12, cd 3s, radius 2.5, knockback 2.5 | A blast of wind that scatters nearby foes. |
| magic_windblade | Wind Blade | false | Wind | Active (Wind) - power 18, FP 15, cd 4s, radius 1.2, knockback 1, shape:Blade | Hurl a razor-sharp blade of wind (requires Wind Gust). |
| magic_gale | Gale Force | false | Wind | Active (Wind) - power 24, FP 22, cd 6s, radius 3.4, knockback 2 | Summon a towering storm of razor wind that drives foes back (requires Wind Blade). |
| magic_tornado | Tornado | false | Wind | Active (Wind) - power 16, FP 28, cd 10s, range 12, radius 3 | Summon a ravenous tornado that pulls foes in and shreds them (requires Gale Force). |
| magic_flight | Wind Walk | false | Wind | Active (Wind) - power 0, FP 18, cd 25s, dur 10s, self-buff | Ride the wind and take flight for 10 seconds (requires Gale Force). |
| magic_water | Water Bolt | false | Water | Active (Water) - power 22, FP 13, cd 4s, Wet, shape:Bolt | Launch a splash that soaks and slows the target. |
| magic_earth | Stone Shard | false | Earth | Active (Earth) - power 26, FP 15, cd 4s, shape:Debris, terrain:Crater (impact + debris burst) | Hurl a fistful of living rock that carves a crater where it strikes. |

## Tree branches (L1 + L2)

### Focal Mind (magic_focus)

- **authoring ref** `magic_heal` (links to a base skill above)
- **Clarity** (`magic_focus_clarity`) - + 3 Intelligence (passive) | Permanent +3 Intelligence.
  - **Tranquil Mind** (`magic_focus_clarity_tranquil`) - + 5 Intelligence (passive) | Permanent +5 Intelligence.
  - **Deep Wisdom** (`magic_focus_clarity_wisdom`) - + 5 Wisdom (passive) | Permanent +5 Wisdom.
  - **Patience** (`magic_focus_clarity_patience`) - + 5 Endurance (passive) | Permanent +5 Endurance.
  - **Serenity** (`magic_focus_clarity_serenity`) - + 5 Health (passive) | Permanent +5 Health.
  - **Inner Light** (`magic_focus_clarity_faith`) - + 5 Faith (passive) | Permanent +5 Faith.
- **Holy Light** (`magic_focus_holylight`) - Active (Holy) - power 24, FP 14, cd 4s, radius 2.4, heals | A radiant burst of holy light that sears enemies and mends allies.
  - **Radiance** (`magic_focus_holylight_radiance`) - Active (Holy) - power 28, FP 18, cd 5s, radius 2.8, heals | A blinding burst of holy radiance that sears foes and mends allies.
  - **Beacon** (`magic_focus_holylight_beacon`) - Active (Holy) - power 26, FP 16, cd 4s, range 12, radius 1.5, channels 7 FP/s, heals | A tower of holy light — hold it over enemies to sear them and over allies to mend them.
  - **Glory** (`magic_focus_holylight_glory`) - + 5 Faith (passive) | Permanent +5 Faith.
  - **Sunburst** (`magic_focus_holylight_sunburst`) - Active (Holy) - power 30, FP 20, cd 6s, radius 3, heals | A sun-bright explosion that wounds the wicked and heals the faithful.
  - **Illumination** (`magic_focus_holylight_illum`) - + 5 Wisdom (passive) | Permanent +5 Wisdom.
- **Inspire** (`magic_focus_inspire`) - + 3 Wisdom (passive) | Permanent +3 Wisdom.
  - **Ancient Wisdom** (`magic_focus_inspire_wisdom`) - + 5 Wisdom (passive) | Permanent +5 Wisdom.
  - **Unshaken Faith** (`magic_focus_inspire_faith`) - + 5 Faith (passive) | Permanent +5 Faith.
  - **Grand Intellect** (`magic_focus_inspire_intellect`) - + 5 Intelligence (passive) | Permanent +5 Intelligence.
  - **Enduring Spirit** (`magic_focus_inspire_endurance`) - + 5 Endurance (passive) | Permanent +5 Endurance.
  - **Fortune's Favor** (`magic_focus_inspire_luck`) - + 5 Luck (passive) | Permanent +5 Luck.
- **Renew** (`magic_focus_renew`) - Active (Holy) - power 18, FP 12, heals | A gentle holy glow that restores the body.
  - **Regrowth** (`magic_focus_renew_regrowth`) - Active (Holy) - power 22, FP 14, heals | A renewing light that mends swiftly.
  - **Vigor** (`magic_focus_renew_vigor`) - + 5 Health (passive) | Permanent +5 Health.
  - **Restore** (`magic_focus_renew_restore`) - Active (Holy) - power 30, FP 22, heals | A major restoration of the body.
  - **Peak Health** (`magic_focus_renew_health`) - + 5 Health (passive) | Permanent +5 Health.
  - **Bloom** (`magic_focus_renew_bloom`) - Active (Holy) - power 24, FP 16, cd 4s, radius 2.2, heals | A bloom of revitalizing light.

### Arcane Study (magic_arcane)

- **authoring ref** `magic_manaflow` (links to a base skill above)
- **authoring ref** `magic_ward` (links to a base skill above)
- **Arcane Bolt** (`magic_arcane_bolt`) - Active (Arcane) - power 26, FP 14, cd 3s, shape:Bolt | A bolt of raw arcane energy.
  - **Force Bolt** (`magic_arcane_bolt_force`) - Active (Arcane) - power 30, FP 18, cd 4s, shape:Bolt | A heavy bolt of arcane force.
  - **Prism Bolt** (`magic_arcane_bolt_prism`) - Active (Arcane) - power 32, FP 20, cd 5s, shape:Bolt | A bolt that splits into a prism of colors.
  - **Arcane Insight** (`magic_arcane_bolt_insight`) - + 5 Wisdom (passive) | Permanent +5 Wisdom.
  - **Rupture** (`magic_arcane_bolt_rupture`) - Active (Arcane) - power 34, FP 22, cd 5s, radius 2.2 | A rupturing blast of arcane power.
  - **Arcane Missiles** (`magic_arcane_bolt_missile`) - Active (Arcane) - power 28, FP 16, cd 4s, shape:Missile | A stream of small arcane missiles.
- **Arcane Surge** (`magic_arcane_surge`) - + 3 Wisdom (passive) | Permanent +3 Wisdom.
  - **Arcane Power** (`magic_arcane_surge_power`) - + 5 Wisdom (passive) | Permanent +5 Wisdom.
  - **Arcane Intellect** (`magic_arcane_surge_intellect`) - + 5 Intelligence (passive) | Permanent +5 Intelligence.
  - **Focused Energy** (`magic_arcane_surge_focus`) - + 4 Intelligence (passive) | Permanent +4 Intelligence.
  - **Arcane Spirit** (`magic_arcane_surge_spirit`) - + 5 Faith (passive) | Permanent +5 Faith.
  - **Arcane Mastery** (`magic_arcane_surge_mastery`) - + 6 Wisdom (passive) | Permanent +6 Wisdom.
- **Arcane Bind** (`magic_arcane_bind`) - Active (Arcane) - power 24, FP 12, cd 5s, radius 2 | A binding wave of pure arcane force.
  - **Shackles** (`magic_arcane_bind_shackle`) - Active (Arcane) - power 28, FP 16, cd 4s, radius 2.2 | Arcane chains that bind and crush.
  - **Hold** (`magic_arcane_bind_hold`) - Active (Arcane) - power 26, FP 14, cd 4s, radius 2 | A solid arcane grip that crushes foes in place.
  - **Vine Cage** (`magic_arcane_bind_vine`) - Active (Arcane) - power 24, FP 12, cd 4s, radius 2.4 | Arcanum vines that ensnare.
  - **Bound Endurance** (`magic_arcane_bind_endurance`) - + 5 Endurance (passive) | Permanent +5 Endurance.
  - **Warding Bind** (`magic_arcane_bind_warding`) - + 5 Defense (passive) | Permanent +5 Defense.

### Mana Flow (magic_manaflow)

- (no branch table)

### Fireball (magic_fireball)

- **Meteor** (`magic_fireball_meteor`) - Active (Fire) - power 30, FP 22, cd 6s, radius 3, knockback 2, falls-a-boulder, craters the ground | A burning meteor falls from the sky, scattering the blast.
  - **Meteor Rain** (`magic_fireball_meteor_rain`) - Active (Fire) - power 36, FP 26, cd 8s, range 10, radius 3.6, dur 3.5s, rocks-per-strike, falls-a-boulder, craters the ground | A storm of falling meteors that bombards the area.
  - **Flamethrower** (`magic_fireball_meteor_flamethrower`) - Active (Fire) - power 36, FP 26, cd 6s, range 11, tip radius 2.4, channel, Burn, knockback 1.5, drains 11 FP/s | A sustained jet of fire that washes over everything in a widening cone.
  - **Impact** (`magic_fireball_meteor_impact`) - + 5 Intelligence (passive) | Permanent +5 Intelligence.
  - **Continuous Fireball** (`magic_fireball_meteor_continuous`) - Active (Fire) - power 36, FP 28, cd 8s, radius 5, follows caster, dur 8s, Burn, bolt splash 1.6 | A bound flame that follows you, spitting molten bolts straight down your aim line.
  - **Ember Effigy** (`magic_fireball_meteor_ember`) - Active (Fire) - power 30, FP 20, cd 6s, range 8, radius 6, dur 6s, Burn | Summon a burning effigy that hurls embers at nearby foes.
- **Inferno** (`magic_fireball_inferno`) - Active (Fire) - power 32, FP 24, cd 7s, radius 3.4, dur 3.5s, Burn | An expanding ring of fire that lingers, scorching all it touches.
  - **Conflagration** (`magic_fireball_inferno_conflagration`) - Active (Fire) - power 38, FP 28, cd 8s, range 8, radius 2.6, dur 3s, Burn | A firestorm that rises off a burning circle of flame, dragging foes in and burning them alive. (**1kc**: draws this firestorm model — ground circle + rising particles — instead of the generic pull funnel. **1l6u**: spinning ember rings spawn continuously from the ground over the circle until the spell ends.)
  - **Firestorm** (`magic_fireball_inferno_firestorm`) - Active (Fire) - power 36, FP 26, cd 7s, range 10, radius 3.8, dur 3.5s, Burn | A storm of whirling flame that rains burning embers over the area.
  - **Infernal Rage** (`magic_fireball_inferno_rage`) - + 5 Strength (passive) | Permanent +5 Strength.
  - **Inferno Peak** (`magic_fireball_inferno_peak`) - Active (Fire) - power 40, FP 30, cd 9s, radius 4.2, dur 3.5s, Burn, knockback 1 | A towering eruption of flame that leaves the ground burning for seconds.
  - **Fire Wave** (`magic_fireball_inferno_wave`) - Active (Fire) - power 32, FP 22, cd 6s, range 20, radius 3.4, knockback 2.5, shape:Wave | A rolling ground-hugging wave of fire that sweeps forward, scorching every foe it rolls over.
- **Embermind** (`magic_fireball_ember`) - + 3 Intelligence (passive) | Permanent +3 Intelligence.
  - **Ember Intellect** (`magic_fireball_ember_intellect`) - + 5 Intelligence (passive) | Permanent +5 Intelligence.
  - **Ember Wisdom** (`magic_fireball_ember_wisdom`) - + 5 Wisdom (passive) | Permanent +5 Wisdom.
  - **Ember Faith** (`magic_fireball_ember_faith`) - + 5 Faith (passive) | Permanent +5 Faith.
  - **Luck of the Flame** (`magic_fireball_ember_luck`) - + 5 Luck (passive) | Permanent +5 Luck.
  - **Flame Reflex** (`magic_fireball_ember_speed`) - + 5 Speed (passive) | Permanent +5 Speed.
- **Scorch** (`magic_fireball_scorch`) - Active (Fire) - power 26, FP 16, cd 4s, Burn, shape:Comet | A narrow, searing jet of fire that leaves nothing unburnt.
  - **Burn** (`magic_fireball_scorch_burn`) - Active (Fire) - power 30, FP 18, cd 4s, Burn, shape:Comet | A searing burn that lingers long after impact.
  - **Ignite** (`magic_fireball_scorch_ignite`) - + 5 Intelligence (passive) | Permanent +5 Intelligence.
  - **Flash Fire** (`magic_fireball_scorch_flash`) - Active (Fire) - power 32, FP 20, cd 5s, Burn | A swift flash of scorching fire that strikes whatever you look at.
  - **Searing Ray** (`magic_fireball_scorch_searing`) - Active (Fire) - power 34, FP 22, cd 5s, range 13, radius 1.3, channels 9 FP/s, Burn | A narrow beam of searing heat — hold it over foes, burning the whole line.
  - **Radiant Heat** (`magic_fireball_scorch_heat`) - + 5 Health (passive) | Permanent +5 Health.

### Frost Bolt (magic_frostbolt)

- **Ice Lance** (`magic_frostbolt_icelance`) - Active (Ice) - power 26, FP 16, cd 4s, shape:Lance | A long spear of solid ice.
  - **Frost Pierce** (`magic_frostbolt_icelance_pierce`) - Active (Ice) - power 30, FP 18, cd 4s, Chill, **instant** | An armor-piercing lance of ice that strikes whatever you look at. (**1l70**: was a lance *projectile* duplicating its parent — now an undodgeable hitscan.)
  - **Ice Havoc** (`magic_frostbolt_icelance_havoc`) - + 5 Intelligence (passive) | Permanent +5 Intelligence.
  - **Glacial Impale** (`magic_frostbolt_icelance_impale`) - Active (Ice) - power 36, FP 22, cd 5s, Frost, **instant** | A massive spike of ice that impales one foe and freezes them solid. (**1l70**: was a third lance projectile.)
  - **Hail Lance** (`magic_frostbolt_icelance_hail`) - Active (Ice) - power 30, FP 20, cd 5s, range 10, radius 2.6, dur 3.5s, Chill | A storm of hail lances that batters the whole area.
  - **Frost Bite** (`magic_frostbolt_icelance_bite`) - Active (Ice) - power 28, FP 16, cd 4s, Chill, shape:Comet | A biting cold that flashes in and chills to the bone.
- **Freeze** (`magic_frostbolt_freeze`) - Active (Ice) - power 28, FP 18, cd 5s, radius 2.4, dur 3s, Frost | A wave of freezing air that clings, leaving a field of frost.
  - **Deep Freeze** (`magic_frostbolt_freeze_deep`) - Active (Ice) - power 36, FP 24, cd 8s, range 10, radius 2.8, dur 3.5s, Frost, **storm** | A storm that deep-freezes the area, striking again and again. (**1l6z** drew a frozen field — a frost disc ringed by ice spikes; **1l70** moved the spell from Zone to Storm and mounts that field as `StormBody.FrozenField` under the repeated freeze strikes.)
  - **Cold Snap** (`magic_frostbolt_freeze_snap`) - Active (Ice) - power 32, FP 20, cd 5s, Frost | A sudden snap-freeze of whatever you look at.
  - **Frozen Will** (`magic_frostbolt_freeze_chill`) - + 5 Wisdom (passive) | Permanent +5 Wisdom.
  - **Snow Drift** (`magic_frostbolt_freeze_tundra`, was *Tundra*) - Active (Ice) - power 32, FP 22, cd 8s, dur 8s, Chill, **caster-anchored familiar** | A drifting snow wisp that takes up station behind you and pours chilling shards down your aim line. (**1l70**: reworked from a slow-field zone — the only Ice follower.)
  - **Frozen Touch** (`magic_frostbolt_freeze_touch`) - Active (Ice) - power 28, FP 18, cd 6s, range 10, radius 3.2, knockback 1.5, Chill, shape:Wave | A rolling wall of frost that sweeps foes across the field. (**1l70**: was an ice vortex duplicating Chill Touch/Frost Curse — now a ground-hugging wave, the 2nd Wave spell in the game.)
- **Crystal Mind** (`magic_frostbolt_crystal`) - + 3 Intelligence (passive) | Permanent +3 Intelligence.
  - **Crystal Intellect** (`magic_frostbolt_crystal_intellect`) - + 5 Intelligence (passive) | Permanent +5 Intelligence.
  - **Crystal Wisdom** (`magic_frostbolt_crystal_wisdom`) - + 5 Wisdom (passive) | Permanent +5 Wisdom.
  - **Crystal Endurance** (`magic_frostbolt_crystal_endurance`) - + 5 Endurance (passive) | Permanent +5 Endurance.
  - **Crystal Armor** (`magic_frostbolt_crystal_defense`) - + 5 Defense (passive) | Permanent +5 Defense.
  - **Crystal Luck** (`magic_frostbolt_crystal_luck`) - + 5 Luck (passive) | Permanent +5 Luck.
- **Glacier** (`magic_frostbolt_glacier`) - Active (Ice) - power 30, FP 20, cd 6s, radius 2.6, knockback 2 | A massive wall of glacial ice that crushes and shoves foes.
  - **Frost Obelisk** (`magic_frostbolt_glacier_wall`) - Active (Ice) - power 36, FP 26, cd 8s, range 8, radius 6, dur 6s, Chill | Summon a frozen obelisk that hurls frost bolts at nearby foes.
  - **Glacial Surge** (`magic_frostbolt_glacier_surge`) - Active (Ice) - power 40, FP 30, cd 9s, radius 3.8, dur 4s, Frost | A suffocating surge of cold that swallows the area.
  - **Glacial Weight** (`magic_frostbolt_glacier_weight`) - + 5 Strength (passive) | Permanent +5 Strength.
  - **Avalanche** (`magic_frostbolt_glacier_avalanche`) - Active (Ice) - power 38, FP 28, cd 8s, range 10, radius 3.6, dur 3.5s, Frost | A cascading avalanche that crushes the area under falling ice.
  - **Eternal Cold** (`magic_frostbolt_glacier_eternal`) - + 6 Wisdom (passive) | Permanent +6 Wisdom.
- **Chill Touch** (`magic_frostbolt_chill`) - Active (Ice) - power 24, FP 14, cd 3s, Chill, body:Hand | A numbing cold that slows the foe.
  - **Chill Soul** (`magic_frostbolt_chill_soul`) - Active (Ice) - power 30, FP 18, cd 4s, Chill, shape:Missile, body:Soul | A homing cold that hunts its prey and seeps into the soul.
  - **Frost Curse** (`magic_frostbolt_chill_curse`) - Active (Ice) - power 28, FP 16, cd 4s, range 8, radius 2, dur 3s, Chill | A creeping cold that spirals, cursing all it touches.
  - **Frost Will** (`magic_frostbolt_chill_will`) - + 5 Faith (passive) | Permanent +5 Faith.
  - **Cold Stare** (`magic_frostbolt_chill_stare`) - Active (Ice) - power 26, FP 14, cd 3s, range 11, radius 1, channels 7 FP/s, Chill | A gaze of ice that freezes the heart — hold it to chill and slow.
  - **Witching Chill** (`magic_frostbolt_chill_hour`) - Active (Ice) - power 32, FP 20, cd 5s, range 8, radius 2.6, dur 3s, Frost, **vortex** | An unnatural hour of cold that drags foes in and freezes them. (**1l70**: was a lingering-cold zone duplicating the other freeze fields — now a pulling Frost vortex, distinct from Frost Curse's Chill.)

### Storm Focus (magic_lightning)

- **authoring ref** `magic_chain` (links to a base skill above)
- **Volt** (`magic_lightning_volt`) - Active (Lightning) - power 16, FP 8, cd 1.6s, range 11, Stagger, shape:VoltBolt | A cheap, rapid jolt — a fast bolt that staggers on impact.  
  - **Arc Volley** (`magic_lightning_volt_arc`) - Active (Lightning) - power 30, FP 18, cd 4s, range 12, radius 5, speed 7, tick 0.35s, splash 2, Stagger, shape:Sphere + **zaps in flight** (1l113) | A slow sphere of ball lightning that drifts forward, arcing to nearby foes as it passes. 1l114: livestock animals are zapped too.
- **Volt Spark** (`magic_lightning_volt_spark`) - Active (Lightning) - power 22, FP 13, cd 3s, range 12, Stagger, shape:Missile + look:SparkSwarm | A swarm of seeking sparks that home in and stagger.  
  - **High Potential** (`magic_lightning_volt_potential`) - + 5 Intelligence (passive) | Permanent +5 Intelligence.
- **Volt Bolt** (`magic_lightning_volt_bolt`) - Active (Lightning) - power 40, FP 24, cd 5s, range 15, knockback 3.5, Stagger, shape:GreatBolt | A heavy charged great-bolt that knocks foes back and staggers.  
- **Static Coil** (`magic_lightning_volt_charge`) - Active (Lightning) - power 26, FP 18, cd 9s, Summon, range 8, radius 5.5, dur 6s, tick 0.55s, splash 1.6, Stagger, look:SummonBody.TeslaCoil, deployed reach ring grows with charge (1l118) | Deploy a static coil that zaps nearby foes — a stationary turret that staggers.  
- **Thunderhead** (`magic_lightning_storm`) - Active (Lightning) - power 46, FP 32, cd 11s, range 10, radius 3.6, dur 5s, Stagger, storm cloud + thunderhead body (1l111) | Raise a thunderhead that hurls sky-bolts at everything beneath it, staggering them.
  - **Downpour** (`magic_lightning_storm_rain`) - Active (Lightning) - power 42, FP 30, cd 14s, range 10, radius 4.4, dur 9s, Stagger, rain-shelf body (1l111) | Open a long downpour that drenches the whole area in staggering strikes.
  - **Storm Sight** (`magic_lightning_storm_eye`) - + 5 Wisdom (passive) | Permanent +5 Wisdom.
  - **Storm Fan** (`magic_lightning_storm_pulse`) - Active (Lightning) - power 34, FP 24, cd 8s, range 13, radius 1.8, half-angle 26°, channels 11 FP/s, Stagger, bolt-fan cone (1l111) | Sweep a flat fan of crackling bolts that staggers everything it crosses.
  - **Storm Lash** (`magic_lightning_storm_wrack`) - Active (Lightning) - power 40, FP 28, cd 8s, range 11, radius 2.6, speed 13, Stagger, shape:StormWave (Wave) | Lash a wave of ground-running lightning forward, staggering everything it rolls over.
  - **Storm Will** (`magic_lightning_storm_will`) - + 5 Intelligence (passive) | Permanent +5 Intelligence.
- **Deep Charge** (`magic_lightning_charge`) - + 3 Intelligence (passive) | Permanent +3 Intelligence.
  - **Amp** (`magic_lightning_charge_amp`) - + 5 Intelligence (passive) | Permanent +5 Intelligence.
  - **Volt Mind** (`magic_lightning_charge_volt`) - + 5 Intelligence (passive) | Permanent +5 Intelligence.
  - **Energy** (`magic_lightning_charge_energy`) - + 5 Wisdom (passive) | Permanent +5 Wisdom.
  - **True Conduit** (`magic_lightning_charge_conduit`) - + 5 Wisdom (passive) | Permanent +5 Wisdom.
  - **Storm Heart** (`magic_lightning_charge_heart`) - + 5 Faith (passive) | Permanent +5 Faith.
- **Lightning Strike** (`magic_lightning_fury`) - Active (Lightning) - power 40, FP 28, cd 9s, range 13, radius 2.5, instant, sky-strike, Stagger (1l112) | Call a single jagged lightning bolt down from the sky onto the target.
  - **Fury Bolt** (`magic_lightning_fury_bolt`) - Active (Lightning) - power 38, FP 24, cd 8s, range 16, speed 18, knockback 2.5, Stagger, shape:FuryLance (1l112) | Hurl a forked lance of lightning that punches through and staggers on impact.
  - **Static Nova** (`magic_lightning_fury_beam`) - Active (Lightning) - power 22, FP 26, cd 11s, range 10, radius 3.4, speed 16, nova-wave, centered-on-caster, Stagger, nova-ring body (1l112; radial wave 1l116) | Erupt a ring of static that rolls outward from you, searing and staggering everything it sweeps over.
  - **Stormsurge** (`magic_lightning_fury_rage`) - + 4 AttackSpeed (passive) | Permanent +4 Attack Speed.
  - **Devastation** (`magic_lightning_fury_devastation`) - Active (Lightning) - power 52, FP 36, cd 16s, range 11, radius 4.2, dur 6s, Stagger, maelstrom body + sky bolts (1l112) | Tear open a maelstrom sky-fury that batters the whole area with relentless bolts.
  - **Thunder Vortex** (`magic_lightning_fury_tempest`) - Active (Lightning) - power 30, FP 24, cd 9s, range 9, radius 3.2, dur 5s (Vortex floors lifetime at 5s), vortex (pulls), Stagger, lightning-cyclone body (1l112) | Spin up a howling vortex of lightning that drags foes in and tears at everything it holds.

### Chain Lightning (magic_chain)

- `SpellDelivery.Chain` (1l76): strikes the aimed foe (or nearest combatant within `Range` 10 m), then
  leaps up to 2 times to the nearest un-hit combatant within 5 m of the last victim — three victims
  max, each at full power (28), Stagger on every hit. Each leg is a `SpellBoltModelBuilder` jagged bolt
  (caster→primary, then victim→victim). No branch table.

### Lesser Heal (magic_heal)

- (no branch table)

### Arcane Ward (magic_ward)

- (no branch table)

### Dark Bolt (magic_dark)

- **Shadow Bolt** (`magic_dark_shadowbolt`) - Active (Dark) - power 28, FP 16, cd 4s, shape:Bolt | A bolt of concentrated shadow.
  - **Doom Bolt** (`magic_dark_shadowbolt_doom`) - Active (Dark) - power 34, FP 22, cd 5s, shape:Bolt | A bolt of impending doom.
  - **Gloom** (`magic_dark_shadowbolt_gloom`) - + 5 Faith (passive) | Permanent +5 Faith.
  - **Shadow Totem** (`magic_dark_shadowbolt_pool`) - Active (Dark) - power 32, FP 20, cd 5s, range 8, radius 6, dur 6s, Blind | Summon a totem of writhing shadow that hunts foes with blinding bolts.
  - **Shadow Spear** (`magic_dark_shadowbolt_spear`) - Active (Dark) - power 36, FP 24, cd 6s, shape:Spear | A spear of condensed darkness.
  - **Tendrils** (`magic_dark_shadowbolt_tendrils`) - Active (Dark) - power 30, FP 18, cd 4s, radius 2.4 | Shadow tentacles lash all around.
- **Void Rend** (`magic_dark_voidrend`) - Active (Dark) - power 30, FP 18, cd 5s, radius 2.2, knockback 1, Blind | Darkness tears through the area, blinding and shoving.
  - **Void Rip** (`magic_dark_voidrend_rip`) - Active (Dark) - power 36, FP 24, cd 6s, radius 2.6 | A tear in reality that rends the target.
  - **Tear** (`magic_dark_voidrend_tear`) - + 5 Faith (passive) | Permanent +5 Faith.
  - **Abyss** (`magic_dark_voidrend_abyss`) - Active (Dark) - power 38, FP 26, cd 7s, radius 2.8 | Darkness opens into a void abyss.
  - **Laceration** (`magic_dark_voidrend_laceration`) - Active (Dark) - power 32, FP 20, cd 5s, shape:Blade | A void that lacerates on contact.
  - **Void Rend** (`magic_dark_voidrend_rend`) - Active (Dark) - power 34, FP 22, cd 5s, shape:Bolt | A rending projectile of void nothing.
- **Dark Pact** (`magic_dark_curse`) - + 3 Faith (passive) | Permanent +3 Faith.
  - **Pact of Dark** (`magic_dark_curse_pact`) - + 5 Faith (passive) | Permanent +5 Faith.
  - **Dark Might** (`magic_dark_curse_might`) - + 5 Strength (passive) | Permanent +5 Strength.
  - **Unholy Wisdom** (`magic_dark_curse_wisdom`) - + 5 Wisdom (passive) | Permanent +5 Wisdom.
  - **Dark Intellect** (`magic_dark_curse_intellect`) - + 5 Intelligence (passive) | Permanent +5 Intelligence.
  - **Dark Endurance** (`magic_dark_curse_endurance`) - + 5 Endurance (passive) | Permanent +5 Endurance.
- **Devour** (`magic_dark_devour`) - Active (Dark) - power 34, FP 22, cd 6s, radius 2, Blind | Void mouths snap at all nearby foes, blinding them.
  - **Consume** (`magic_dark_devour_consume`) - Active (Dark) - power 38, FP 26, cd 7s, radius 2.4, Blind | Maws of darkness consume and blind all they touch.
  - **Swallow** (`magic_dark_devour_swallow`) - + 5 Health (passive) | Permanent +5 Health.
  - **Void Gullet** (`magic_dark_devour_gullet`) - Active (Dark) - power 40, FP 30, cd 8s, radius 3 | A gaping void that swallows foes.
  - **Dark Feast** (`magic_dark_devour_feast`) - Active (Dark) - power 34, FP 22, cd 6s, radius 2.6 | Darkness that feasts on the enemy.
  - **Hunger** (`magic_dark_devour_hunger`) - Active (Dark) - power 30, FP 18, cd 4s, range 11, radius 1.1, channels 8 FP/s, Blind | A hungry beam of darkness — hold it over foes, blinding the whole line.
- **Nightfall** (`magic_dark_nightfall`) - Active (Dark) - power 30, FP 20, cd 6s, radius 3 | A plane of unnatural darkness descends.
  - **Eclipse** (`magic_dark_nightfall_eclipse`) - Active (Dark) - power 38, FP 28, cd 8s, range 9, radius 3.4, dur 3s, Blind | The sky darkens — shadow strikes blind the whole area.
  - **Midnight** (`magic_dark_nightfall_midnight`) - + 6 Faith (passive) | Permanent +6 Faith.
  - **Enshroud** (`magic_dark_nightfall_enshroud`) - Active (Dark) - power 34, FP 24, cd 6s, radius 3 | Darkness enshrouds the area.
  - **Veil of Night** (`magic_dark_nightfall_veil`) - Active (Dark) - power 30, FP 20, cd 5s, radius 2.6 | A veil of impenetrable night.
  - **Permanent Dusk** (`magic_dark_nightfall_dark`) - + 5 Speed (passive) | Permanent +5 Speed.

### Blizzard (magic_blizzard)

- (no branch table)

### Wind Gust (magic_gust)

- **authoring ref** `magic_windblade` (links to a base skill above)
- **Storm Breath** (`magic_gust_stormbreath`) - Active (Wind) - power 26, FP 16, cd 4s, range 10, radius 1.6, knockback 2, channels 8 FP/s | A howling breath of storm wind — hold it to drive foes back.
  - **Gale Breath** (`magic_gust_stormbreath_gale`) - Active (Wind) - power 30, FP 18, cd 5s, radius 3 | A strengthened gale of breath.
  - **Howl** (`magic_gust_stormbreath_howl`) - + 5 Speed (passive) | Permanent +5 Speed.
  - **Storm Blast** (`magic_gust_stormbreath_blast`) - Active (Wind) - power 32, FP 20, cd 6s, radius 3.2 | A pressurized blast of storm air.
  - **Wind Shriek** (`magic_gust_stormbreath_shriek`) - Active (Wind) - power 28, FP 16, cd 4s, radius 2.8 | A piercing shriek of wind.
  - **Zephyr** (`magic_gust_stormbreath_zephyr`) - Active (Wind) - power 26, FP 14, cd 4s, radius 2.6 | A gentle but cutting breeze.
- **Cyclone** (`magic_gust_cyclone`) - Active (Wind) - power 30, FP 20, cd 6s, radius 3, knockback 2.5 | A spinning cyclone that scatters foes.
  - **Mini Tornado** (`magic_gust_cyclone_tornado`) - Active (Wind) - power 34, FP 22, cd 7s, range 8, radius 3, dur 4s | A compact but violent tornado that drags foes in.
  - **Whirlwind** (`magic_gust_cyclone_whirl`) - Active (Wind) - power 32, FP 20, cd 6s, radius 2.8 | A chaotic whirlwind of force.
  - **Storm Eye** (`magic_gust_cyclone_stormeye`) - + 5 Wisdom (passive) | Permanent +5 Wisdom.
  - **Windstorm** (`magic_gust_cyclone_windstorm`) - Active (Wind) - power 36, FP 24, cd 7s, radius 3.4 | A full-scale windstorm.
  - **Spin** (`magic_gust_cyclone_spin`) - Active (Wind) - power 28, FP 16, cd 4s, radius 2.4 | A rapid spinning vortex.
- **Air Mastery** (`magic_gust_airmastery`) - + 3 AttackSpeed (passive) | Permanent +3 Attack Speed.
  - **Sky Speed** (`magic_gust_airmastery_speed`) - + 5 Speed (passive) | Permanent +5 Speed.
  - **Wind Hands** (`magic_gust_airmastery_attackspeed`) - + 5 AttackSpeed (passive) | Permanent +5 Attack Speed.
  - **Feather Reflex** (`magic_gust_airmastery_dex`) - + 5 Dexterity (passive) | Permanent +5 Dexterity.
  - **Air Agility** (`magic_gust_airmastery_agility`) - + 4 Dexterity (passive) | Permanent +4 Dexterity.
  - **Wind Grace** (`magic_gust_airmastery_grace`) - + 4 Speed (passive) | Permanent +4 Speed.
- **Gust Totem** (`magic_gust_airburst`) - Active (Wind) - power 24, FP 14, cd 3s, range 8, radius 5.5, dur 6s | Summon a wind totem that blasts nearby foes with gales.
  - **Air Surge** (`magic_gust_airburst_surge`) - Active (Wind) - power 30, FP 18, cd 5s, radius 2.6 | A surge of compressed air.
  - **Pop** (`magic_gust_airburst_pop`) - + 5 AttackSpeed (passive) | Permanent +5 Attack Speed.
  - **Crack** (`magic_gust_airburst_crack`) - Active (Wind) - power 28, FP 16, cd 4s, radius 2.4, knockback 2 | A sonic crack of bursting air.
  - **Pressure** (`magic_gust_airburst_pressure`) - Active (Wind) - power 30, FP 18, cd 5s, radius 2.6, knockback 2.2 | Crushing air pressure.
  - **Shockwave** (`magic_gust_airburst_shock`) - Active (Wind) - power 32, FP 20, cd 6s, radius 3, knockback 2.5 | A wide shockwave of air that scatters everything.

### Wind Blade (magic_windblade)

- (no branch table)

### Gale Force (magic_gale)

- (no branch table)

### Tornado (magic_tornado)

- (no branch table)

### Wind Walk (magic_flight)

- (no branch table)

### Water Bolt (magic_water)

- **Tidal Stream** (`magic_water_stream`) - Active (Water) - power 24, FP 16, cd 4s, range 11, radius 1.4, channels 8 FP/s, Wet | Hold a surging line of water that soaks everything it crosses.
  - **Tidal Surge** (`magic_water_stream_surge`) - Active (Water) - power 28, FP 18, cd 4s, Wet, shape:Splash | A heavy surge of water.
  - **Rapids** (`magic_water_stream_rapids`) - + 5 AttackSpeed (passive) | Permanent +5 Attack Speed.
  - **Flood** (`magic_water_stream_flood`) - Active (Water) - power 30, FP 20, cd 6s, radius 2.6, dur 3s, Wet | Rising water that floods the area.
  - **Tide** (`magic_water_stream_tide`) - Active (Water) - power 32, FP 22, cd 5s, range 13, radius 1.5, channels 9 FP/s, Wet | Hold a rolling tide that soaks the whole line.
  - **Tsunami** (`magic_water_stream_tsunami`) - Active (Water) - power 34, FP 26, cd 7s, range 9, radius 3.2, dur 3s, Wet | A towering wave crashes down on the area.
- **Whirlpool** (`magic_water_whirlpool`) - Active (Water) - power 24, FP 20, cd 6s, range 8, radius 2.6, dur 4s, Wet | A sucking vortex that drags foes in and drowns them.
  - **Suction** (`magic_water_whirlpool_suck`) - Active (Water) - power 26, FP 18, cd 5s, range 8, radius 2.8, dur 3s, Wet | A vortex that drags foes toward its maw.
  - **Drown** (`magic_water_whirlpool_drown`) - Active (Water) - power 30, FP 20, cd 5s, radius 2.4, Wet | A drowning pull that suffocates foes.
  - **Abyssal Well** (`magic_water_whirlpool_abyss`) - + 5 Intelligence (passive) | Permanent +5 Intelligence.
  - **Maelstrom** (`magic_water_whirlpool_maelstrom`) - Active (Water) - power 34, FP 24, cd 7s, range 9, radius 3, dur 4s, Wet | A sea-wide whirlpool that pulls and shreds.
  - **Water Vortex** (`magic_water_whirlpool_vortex`) - Active (Water) - power 36, FP 26, cd 8s, range 9, radius 3.4, dur 3s, Wet | Waterspouts rake the area, soaking everything.
- **Mist Veil** (`magic_water_veil`) - Active (Water) - power 22, FP 16, cd 5s, radius 2.8, dur 4s, Wet | A clinging mist that soaks and slows all inside.
  - **Drizzle** (`magic_water_veil_drizzle`) - Active (Water) - power 26, FP 18, cd 4s, radius 2.4, Wet | A soft but relentless drizzle.
  - **Soaked** (`magic_water_veil_soaked`) - + 5 Wisdom (passive) | Permanent +5 Wisdom.
  - **Raincall** (`magic_water_veil_rain`) - Active (Water) - power 28, FP 20, cd 5s, radius 3, dur 3s, Wet | Rain hammers the whole area.
  - **Mist** (`magic_water_veil_mist`) - Active (Water) - power 24, FP 16, cd 4s, radius 2.6, Wet | A creeping mist that clings and slows.
  - **Downpour** (`magic_water_veil_downpour`) - Active (Water) - power 32, FP 24, cd 7s, range 10, radius 3.4, dur 3.5s, Wet | A drenching downpour over the target.
- **Deep Mind** (`magic_water_deep`) - + 3 Intelligence (passive) | Permanent +3 Intelligence.
  - **Deep Intellect** (`magic_water_deep_intellect`) - + 5 Intelligence (passive) | Permanent +5 Intelligence.
  - **Deep Wisdom** (`magic_water_deep_wisdom`) - + 5 Wisdom (passive) | Permanent +5 Wisdom.
  - **Deep Endurance** (`magic_water_deep_endurance`) - + 5 Endurance (passive) | Permanent +5 Endurance.
  - **Water Shield** (`magic_water_deep_defense`) - + 5 Defense (passive) | Permanent +5 Defense.
  - **Currents of Fate** (`magic_water_deep_luck`) - + 5 Luck (passive) | Permanent +5 Luck.
- **Healing Spring** (`magic_water_spring`) - Active (Water) - power 20, FP 12, heals | Living water restores the body.
  - **Living Well** (`magic_water_spring_well`) - Active (Water) - power 26, FP 18, heals | A deep well of living water.
  - **Spring Vigor** (`magic_water_spring_vigor`) - + 5 Health (passive) | Permanent +5 Health.
  - **Healing Tide** (`magic_water_spring_tide`) - Active (Water) - power 24, FP 16, cd 4s, radius 2.4, dur 3s, heals | A restorative tide that mends allies inside it.
  - **Renewal** (`magic_water_spring_renew`) - Active (Water) - power 28, FP 20, heals | A surge of cleansing water.
  - **Mist of Life** (`magic_water_spring_mist`) - Active (Water) - power 22, FP 14, cd 3s, radius 2.6, heals | A revitalising mist that mends allies within.

### Stone Shard (magic_earth)

- **Boulder Crash** (`magic_earth_boulder`) - Active (Earth) - power 28, FP 18, cd 5s, radius 3, knockback 2.5, terrain:Crater | A tumbling boulder that flattens and shoves foes, carving a dent where it lands.
  - **Crash** (`magic_earth_boulder_crash`) - Active (Earth) - power 32, FP 22, cd 6s, radius 3.2, knockback 3, terrain:Crater | A colossal boulder that crashes into the enemy, denting the ground.
  - **Boulderweight** (`magic_earth_boulder_weight`) - + 5 Strength (passive) | Permanent +5 Strength.
  - **Landslide** (`magic_earth_boulder_landslide`) - Active (Earth) - power 34, FP 24, cd 7s, radius 3.6, knockback 3, terrain:Wall | An earth wall rears up across the cast and crashes onto foes.
  - **Rockfall** (`magic_earth_boulder_fall`) - Active (Earth) - power 30, FP 20, cd 6s, range 9, radius 3.2, dur 3s, terrain:Crater, rocks-per-strike, falls-a-boulder | Boulders rain down over the area, pitting the ground with craters.
  - **Tectonic** (`magic_earth_boulder_tectonic`) - Active (Earth) - power 36, FP 26, cd 8s, radius 3.8, knockback 3.5, terrain:Crater | A tectonic blow that shatters the ground, carving a wide crater.
  - **Meteor** (`magic_earth_meteor`, deep, requires Boulder Crash) - Active (Earth) - power 40, FP 28, cd 9s, range 12, radius 4, knockback 4, terrain:Crater, falls-a-boulder | A meteor plunges from the sky, carving a crater into the ground.
  - **Earth Wall** (`magic_earth_wall`, deep, requires Landslide) - Active (Earth) - power 36, FP 26, cd 8s, range 10, radius 3.6, knockback 3.5, terrain:Wall | A towering wall of stone rears up across the cast and crashes down on foes.
- **Tremor** (`magic_earth_quake`) - Active (Earth) - power 26, FP 20, cd 6s, radius 2.8, terrain:Ring | The ground ripples — a stone ring rears up around the impact.
  - **Faultline** (`magic_earth_quake_faultline`) - Active (Earth) - power 30, FP 22, cd 6s, radius 3, terrain:Ring | A second stone ring rears up around the impact.
  - **Stable Ground** (`magic_earth_quake_stable`) - + 5 Defense (passive) | Permanent +5 Defense.
  - **Epicenter** (`magic_earth_quake_epicenter`) - Active (Earth) - power 34, FP 24, cd 7s, radius 3.4, terrain:Ring | The ground heaves in a rising ring.
  - **Aftershock** (`magic_earth_quake_aftermath`) - Active (Earth) - power 26, FP 18, cd 4s, radius 2.8, knockback 2, terrain:Ring | A second tremor that tosses foes, rearing a ring of stone.
  - **Seismic Ring** (`magic_earth_quake_seismic`) - Active (Earth) - power 32, FP 24, cd 7s, radius 3.2, terrain:Ring | A towering stone circle that closes in on foes.
- **Spire Field** (`magic_earth_spires`) - Active (Earth) - power 24, FP 18, cd 5s, radius 3, terrain:Spikes | Stone spires erupt from beneath the target area.
  - **Spike Burst** (`magic_earth_spires_spike`) - Active (Earth) - power 30, FP 20, cd 5s, radius 3, terrain:Spikes | Spikes erupt in a violent burst.
  - **Bedrock** (`magic_earth_spires_bedrock`) - + 5 Defense (passive) | Permanent +5 Defense.
  - **Needle Field** (`magic_earth_spires_needles`) - Active (Earth) - power 28, FP 18, cd 5s, radius 3.4, terrain:Spikes | A field of needle-thin stone spikes.
  - **Stone Pillars** (`magic_earth_spires_pillar`) - Active (Earth) - power 32, FP 22, cd 6s, radius 3.2, terrain:Pillar | Massive pillars thrust up out of the ground.
  - **Crystal Field** (`magic_earth_spires_crystal`) - Active (Earth) - power 30, FP 20, cd 6s, radius 3, terrain:Spikes | Jagged crystal shards tear up the ground.
- **Earth Bulwark** (`magic_earth_bulwark`) - + 3 Defense (passive) | Permanent +3 Defense.
  - **Granite** (`magic_earth_bulwark_granite`) - + 5 Defense (passive) | Permanent +5 Defense.
  - **Rock Armor** (`magic_earth_bulwark_armor`) - + 5 Endurance (passive) | Permanent +5 Endurance.
  - **Stone Heart** (`magic_earth_bulwark_stone`) - + 5 Strength (passive) | Permanent +5 Strength.
  - **Earth Ward** (`magic_earth_bulwark_ward`) - + 6 Defense (passive) | Permanent +6 Defense.
  - **Ironhide** (`magic_earth_bulwark_iron`) - + 5 Health (passive) | Permanent +5 Health.
- **Stone Effigy** (`magic_earth_golem`) - Active (Earth) - power 28, FP 20, cd 5s, range 8, radius 6, dur 6s, terrain:Spikes | Summon a stone effigy that erupts from the ground and flings rocks at nearby foes.
  - **Stone Sentinel** (`magic_earth_golem_sentinel`) - Active (Earth) - power 32, FP 22, cd 6s, range 8, radius 6, dur 6s, terrain:Spikes | Summon a vigilant stone sentinel that erupts from the ground.
  - **Mason's Craft** (`magic_earth_golem_mason`) - + 5 Defense (passive) | Permanent +5 Defense.
  - **Stone Guardian** (`magic_earth_golem_guardian`) - Active (Earth) - power 34, FP 24, cd 7s, range 8, radius 6, dur 7s, terrain:Spikes | Summon a hulking stone guardian that tears out of the earth.
  - **Effigy Core** (`magic_earth_golem_core`) - + 5 Health (passive) | Permanent +5 Health.
  - **Colossus** (`magic_earth_golem_colossus`) - Active (Earth) - power 36, FP 26, cd 8s, range 8, radius 6, dur 8s, terrain:Spikes | Summon a towering colossus of living rock that heaves out of the ground.

