# Compare-MovedModel.ps1 - rule 17's instrument for a code MOVE.
#
# WHY THIS EXISTS
#   A move is the one edit whose diff proves nothing (AGENTS.md rule 17). `Destroy(col)` was correct
#   inside `SpellCaster : MonoBehaviour` and stops resolving inside a `static class`, so the moved copy
#   needs `Object.Destroy(col)` - and the line is TEXTUALLY IDENTICAL in the deletion hunk and the
#   new-file hunk, in two different files, so a reader diffing either sees nothing. With no compiler
#   (rule 3) this script IS the compiler.
#
# WHAT IT COMPARES
#   One moved body at a time, comment- and whitespace-normalised, after an explicit rename map for the
#   rewrites a move legitimately requires. Everything left over is printed for review.
#
#   The load-bearing signal is the LITERAL STREAM - every number and every string in build order. A
#   geometry move is a list of numbers; dropping one, reordering one or retyping one is the failure
#   mode, and it is invisible in a diff. `Destroy(` -> `Object.Destroy(` is normalised away because it
#   is a required rewrite, not a change of shape.
#
# USAGE
#   powershell -ExecutionPolicy Bypass -File tools\Compare-MovedModel.ps1
#   powershell -ExecutionPolicy Bypass -File tools\Compare-MovedModel.ps1 -Mutate   # prove it can fail
#
# RULE 7 APPLIES TO THIS SCRIPT
#   A green comparator nobody has seen fail is not a comparator. -Mutate deliberately changes one
#   literal in a new builder (0.18f -> 0.19f, the exact class of edit rule 17 names) and the run MUST
#   report it. If -Mutate prints clean, this script is broken and its silence means nothing.
#
# ENTRY SHAPES
#   OldM/NewM may be:
#     - a member name: its signature (or a nested `class` declaration - those have no parameter list,
#       so a `(` cannot be required) is located and brace-balanced from there;
#     - `@start|||end`  an inclusive LINE RANGE between two text markers, which is how a sub-block
#       inside a longer method is addressed. A plain `@fragment` brace-balances from the fragment.
#   Both ends must exist in BOTH files, which is the point: a range that only fits one side means the
#   two sides are not describing the same block, and the entry is wrong rather than the move.
#
#   `Carry` lists literals that legitimately moved OUT to the call site instead of into the builder
#   (1jd's CCZone colour is chosen by the component, not the model). Declared here rather than left as
#   an unexplained difference.

param([switch]$Mutate)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$moves = @(
    @{ Name='SummonModelBuilder.BuildTotem';         Old='Assets/Scripts/Magic/Cast/SpellSummon.cs'; OldM='BuildTotemVisual';       New='Assets/Scripts/Models/Magic/SummonModelBuilder.cs';       NewM='BuildTotem';          Map=@{'Radius'='radius'} }
    @{ Name='SummonModelBuilder.BuildFamiliarCircle'; Old='Assets/Scripts/Magic/Cast/SpellSummon.cs'; OldM='BuildCircleVisual';    New='Assets/Scripts/Models/Magic/SummonModelBuilder.cs';       NewM='BuildFamiliarCircle'; Map=@{'Radius'='radius'} }
    @{ Name='SpellBeamModelBuilder.BuildFunnel';      Old='Assets/Scripts/Magic/Cast/SpellBeam.cs';  OldM='BuildFunnelVisual';    New='Assets/Scripts/Models/Magic/SpellBeamModelBuilder.cs';    NewM='BuildFunnelVisual';   Map=@{'FunnelChunks'='SpellBeam.FunnelChunks';'FunnelDebris'='SpellBeam.FunnelDebris'} }
    @{ Name='SpellBeamModelBuilder.BuildTipOrb';      Old='Assets/Scripts/Magic/Cast/SpellBeam.cs';  OldM='@_endOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere)|||_orbBaseScale = _endOrb.localScale;'; New='Assets/Scripts/Models/Magic/SpellBeamModelBuilder.cs'; NewM='@Transform orb = GameObject.CreatePrimitive(PrimitiveType.Sphere)|||result.BaseScale = orb.localScale;' }
    # Start at the primitive, NOT at `Vector3 mid`: that line appears TWICE in the old file - once in
    # the per-frame `Animate()` and once in `BuildVisual` - and the first match is the animation, so
    # the range silently ran 86 lines and reported the pulse maths as a move difference.
    @{ Name='SpellBeamModelBuilder.BuildLineBody';    Old='Assets/Scripts/Magic/Cast/SpellBeam.cs';  OldM='@_body = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;|||_bodyBaseScale = _body.localScale;'; New='Assets/Scripts/Models/Magic/SpellBeamModelBuilder.cs'; NewM='@Transform body = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;|||result.BodyBaseScale = body.localScale;'; Map=@{'Width'='width';'Length'='length'} }
    @{ Name='SpellZoneModelBuilder.BuildFunnel';      Old='Assets/Scripts/Magic/Cast/SpellZone.cs';  OldM='@const float height = 4.8f|||blockR.sharedMaterial = sharedMat;'; New='Assets/Scripts/Models/Magic/SpellZoneModelBuilder.cs'; NewM='@const float height = 4.8f|||ApplyShared(block, sharedMat);'; Map=@{'Radius'='radius'} }
    @{ Name='SpellZoneModelBuilder.BuildGroundZone';  Old='Assets/Scripts/Magic/Cast/SpellZone.cs';  OldM='@var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder)|||haloR.sharedMaterial = sharedMat;'; New='Assets/Scripts/Models/Magic/SpellZoneModelBuilder.cs'; NewM='@var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder)|||ApplyShared(halo, sharedMat);'; Map=@{'Radius'='radius'} }
    @{ Name='SpellStormModelBuilder.BoltFader';       Old='Assets/Scripts/Magic/Cast/SpellStorm.cs'; OldM='BoltFader';             New='Assets/Scripts/Models/Magic/SpellStormModelBuilder.cs';  NewM='BoltFader';            Map=@{} }
    # Both bolts keep their explicit `localScale` (the fader would overwrite it next frame anyway,
    # but the spawn frame renders before that). The three numbers became the named `BoltScale`, so
    # SIX literals are declared here - one Carry per occurrence, because two bolts each set it.
    @{ Name='SpellStormModelBuilder.BuildLightningBolt'; Old='Assets/Scripts/Magic/Cast/SpellStorm.cs'; OldM='@GameObject bolt = GameObject.CreatePrimitive(PrimitiveType.Cube)|||AssembleBolt(boltB, sharedMat);'; New='Assets/Scripts/Models/Magic/SpellStormModelBuilder.cs'; NewM='@GameObject bolt = GameObject.CreatePrimitive(PrimitiveType.Cube)|||AssembleBolt(boltB, sharedMat);'; Map=@{}; Carry=@('0.1f','3.2f','0.1f','0.1f','3.2f','0.1f') }
    # The inline `new Vector3(0.1f, 3.2f, 0.1f)` became the named `BoltScale`, so the three numbers
    # are declared here: they are a hoist to a field, not a silently dropped value.
    @{ Name='SpellStormModelBuilder.AssembleBolt';  Old='Assets/Scripts/Magic/Cast/SpellStorm.cs'; OldM='AssembleBolt';          New='Assets/Scripts/Models/Magic/SpellStormModelBuilder.cs';  NewM='AssembleBolt';         Map=@{}; Carry=@('0.1f','3.2f','0.1f') }
    @{ Name='SkillFxModelBuilder.RingFader';          Old='Assets/Scripts/Magic/Fx/SkillFx.cs';      OldM='RingFader';             New='Assets/Scripts/Models/Magic/SkillFxModelBuilder.cs';    NewM='RingFader';            Map=@{} }
    @{ Name='SkillFxModelBuilder.SlashFader';         Old='Assets/Scripts/Magic/Fx/SkillFx.cs';      OldM='SlashFader';            New='Assets/Scripts/Models/Magic/SkillFxModelBuilder.cs';    NewM='SlashFader';           Map=@{} }
    @{ Name='SkillFxModelBuilder.BuildSlashFlash';    Old='Assets/Scripts/Magic/Fx/SkillFx.cs';      OldM='SlashFlash';            New='Assets/Scripts/Models/Magic/SkillFxModelBuilder.cs';    NewM='BuildSlashFlash';      Map=@{} }
    # A range, not the name `RingFlash`: there are TWO overloads and the first is the expression-bodied
    # forwarder, so member-addressing lands on `=> RingFlash(..., 1f);` - one literal, no braces. The
    # `radius *= Mathf.Max(0.2f, scaleMul)` clamp is deliberately outside the range: that clamp stayed
    # in the public API, so including it would report a literal the builder was never given.
    @{ Name='SkillFxModelBuilder.BuildRingFlash';     Old='Assets/Scripts/Magic/Fx/SkillFx.cs';      OldM='@GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder)|||ring.AddComponent<RingFader>().Init(radius, lifetime);'; New='Assets/Scripts/Models/Magic/SkillFxModelBuilder.cs'; NewM='@GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder)|||ring.AddComponent<RingFader>().Init(radius, lifetime);'; Map=@{} }
    @{ Name='WeaponProjectileModelBuilder.Arrow';     Old='Assets/Scripts/Combat/Weapons/RangedWeaponBehavior.cs'; OldM='BuildArrowVisual'; New='Assets/Scripts/Models/WeaponProjectileModelBuilder.cs'; NewM='BuildArrowVisual'; Map=@{} }
    @{ Name='WeaponProjectileModelBuilder.Hammer';    Old='Assets/Scripts/Combat/Weapons/RangedWeaponBehavior.cs'; OldM='BuildHammerVisual';New='Assets/Scripts/Models/WeaponProjectileModelBuilder.cs'; NewM='BuildHammerVisual';Map=@{} }
    @{ Name='WeaponProjectileModelBuilder.TumbleSpin';Old='Assets/Scripts/Combat/Weapons/RangedWeaponBehavior.cs'; OldM='TumbleSpin';       New='Assets/Scripts/Models/WeaponProjectileModelBuilder.cs'; NewM='TumbleSpin';       Map=@{} }
    @{ Name='AoeAimPreviewModelBuilder disc';         Old='Assets/Scripts/Combat/Weapons/AoeAimPreview.cs'; OldM='@Shader shader = Shader.Find("Sprites/Default")|||_discRenderer.enabled = false;'; New='Assets/Scripts/Models/Magic/AoeAimPreviewModelBuilder.cs'; NewM='@Shader shader = Shader.Find("Sprites/Default")|||result.Disc.Renderer.enabled = false;'; Map=@{'GroundRaise'='groundRaise'} }
    @{ Name='AoeAimPreviewModelBuilder ring';         Old='Assets/Scripts/Combat/Weapons/AoeAimPreview.cs'; OldM='@gameObject.AddComponent<LineRenderer>()|||_ring.material = _ringMat;'; New='Assets/Scripts/Models/Magic/AoeAimPreviewModelBuilder.cs'; NewM='@host.gameObject.AddComponent<LineRenderer>()|||ring.material = result.Ring.Material;' }
    @{ Name='AoeAimPreviewModelBuilder beacon';       Old='Assets/Scripts/Combat/Weapons/AoeAimPreview.cs'; OldM='@var beaconGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder)|||brenderer.enabled = false;'; New='Assets/Scripts/Models/Magic/AoeAimPreviewModelBuilder.cs'; NewM='@var beaconGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder)|||result.Beacon.Renderer.enabled = false;' }
    @{ Name='CastingCircleModelBuilder disc';         Old='Assets/Scripts/Magic/Fx/CastingCircle.cs'; OldM='@Shader shader = Shader.Find("Sprites/Default")|||_discRenderer.enabled = false;'; New='Assets/Scripts/Models/Magic/CastingCircleModelBuilder.cs'; NewM='@Shader shader = Shader.Find("Sprites/Default")|||result.Disc.Renderer.enabled = false;' }
    @{ Name='CastingCircleModelBuilder rings';        Old='Assets/Scripts/Magic/Fx/CastingCircle.cs'; OldM='@var outerGo = new GameObject("OuterRing")|||_waveC = NewRing("WaveC", WaveSegments, 0.025f, shader, out _waveMatC);'; New='Assets/Scripts/Models/Magic/CastingCircleModelBuilder.cs'; NewM='@result.OuterRing = NewRing(host, "OuterRing"|||result.WaveC = NewRing(host, "WaveC"'; Map=@{'OuterSegments'='outerSegments';'InnerSegments'='innerSegments';'HexSegments'='hexSegments';'ArcSegments'='arcSegments';'WaveSegments'='waveSegments';'RuneTicks'='runeTicks'} }
    @{ Name='CastingCircleModelBuilder rune';         Old='Assets/Scripts/Magic/Fx/CastingCircle.cs'; OldM='@_runeGroup = NewGroup("Rune")|||tr.localScale = new Vector3(0.02f, 0.02f, 0.12f);'; New='Assets/Scripts/Models/Magic/CastingCircleModelBuilder.cs'; NewM='@var runeGo = new GameObject("Rune")|||tr.localScale = new Vector3(0.02f, 0.02f, 0.12f);'; Map=@{'RuneTicks'='runeTicks'} }
    @{ Name='CcZoneFxModelBuilder.BuildCcZoneRing';   Old='Assets/Scripts/Combat/Status/CCZone.cs';   OldM='BuildVisual';           New='Assets/Scripts/Models/Magic/CcZoneFxModelBuilder.cs';   NewM='BuildCcZoneRing';      Map=@{}; Carry=@('0.38f','0.75f','0.96f','0.4f') }
)

function Get-Head([string]$path) {
    $raw = & git -C $root show "HEAD:$path" 2>$null
    if ($LASTEXITCODE -ne 0) { throw "cannot read HEAD:$path" }
    return ($raw -join "`n")
}

# Body of a member, of a `@fragment`, or of an inclusive `@start|||end` line range.
function Get-Body([string]$text, [string]$member) {
    $lines = $text -split "`r?`n"

    if ($member.Contains('|||')) {
        $parts = $member.Split(@('|||'), [System.StringSplitOptions]::None)
        $from = $parts[0].Substring(1); $to = $parts[1]
        $a = -1; $b = -1
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($a -lt 0 -and $lines[$i].Contains($from)) { $a = $i }
            if ($lines[$i].Contains($to)) { $b = $i; break }
        }
        if ($a -lt 0 -or $b -lt $a) { return $null }
        return (($lines[$a..$b]) -join "`n")
    }

    $start = -1
    if ($member.StartsWith('@')) {
        $frag = $member.Substring(1)
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i].Contains($frag)) { $start = $i; break }
        }
    } else {
        for ($i = 0; $i -lt $lines.Count; $i++) {
            # A signature OR a nested class declaration: the latter has no parameter list, so a `(`
            # cannot be required (this gap is why the first run reported 4 false 'not found').
            if ($lines[$i] -match ('\b' + [regex]::Escape($member) + '\b') -and
                ($lines[$i] -match '(private|public|internal|protected)') -and
                ($lines[$i] -match '(\(|\bclass\b)')) { $start = $i; break }
        }
    }
    if ($start -lt 0) { return $null }

    # From the signature/fragment line, run forward to the first '{', then balance.
    $depth = 0; $opened = $false; $out = New-Object System.Collections.Generic.List[string]
    for ($i = $start; $i -lt $lines.Count; $i++) {
        $out.Add($lines[$i])
        $depth += ([regex]::Matches($lines[$i], '\{')).Count
        $depth -= ([regex]::Matches($lines[$i], '\}')).Count
        if ($depth -gt 0) { $opened = $true }
        if ($opened -and $depth -eq 0) { break }
        if (-not $opened -and $depth -eq 0 -and $lines[$i] -match ';') { break }
    }
    return ($out -join "`n")
}

function Normalise([string]$body, [hashtable]$map) {
    $t = $body
    $t = ($t -split "`r?`n" | Where-Object { $_ -notmatch '^\s*///' -and $_ -notmatch '^\s*//' }) -join "`n"
    foreach ($k in $map.Keys) { $t = $t.Replace($k, $map[$k]) }
    $t = $t.Replace('Object.Destroy(', 'Destroy(')
    $t = [regex]::Replace($t, '\s+', ' ')
    return $t.Trim()
}

function Literal-Stream([string]$norm) {
    $parts = @(([regex]::Matches($norm, '"[^"]*"|\b\d+\.?\d*f?\b')) | ForEach-Object { $_.Value } | Where-Object { $_ -ne '' })
    return ($parts -join ',')
}

$fail = 0; $unknown = 0; $carried = 0
Write-Output "=== moved-body comparison (HEAD vs working tree) ==="
foreach ($m in $moves) {
    $old = Get-Head $m.Old
    $oldBody = Get-Body $old $m.OldM
    if (-not $oldBody) { Write-Output "  ?? $($m.Name): '$($m.OldM)' not found in HEAD"; $unknown++; continue }

    $newPath = Join-Path $root ($m.New -replace '/', '\')
    if (-not (Test-Path -LiteralPath $newPath)) { Write-Output "  ?? $($m.Name): new file missing"; $unknown++; continue }
    $new = [System.IO.File]::ReadAllText($newPath)
    if ($Mutate) {
        $raw = $new
        $new = $raw.Replace('0.45f, 0.32f, 0.18f, 1f', '0.45f, 0.32f, 0.19f, 1f')
        $new = $new.Replace('0.25f, 0.03f', '0.25f, 0.04f')
    }

    $newBody = Get-Body $new $m.NewM
    if (-not $newBody) { Write-Output "  ?? $($m.Name): '$($m.NewM)' not found in new file"; $unknown++; continue }

    $a = Literal-Stream (Normalise $oldBody $m.Map)
    $b = Literal-Stream (Normalise $newBody $m.Map)
    foreach ($c in @($m.Carry)) {
        if ($c -and $a.Contains($c) -and -not $b.Contains($c)) {
            $a = ($a.Split(',') | Where-Object { $_ -ne $c }) -join ','
            $carried++
        }
    }
    if ($a -eq $b) { Write-Output "  ==  $($m.Name)" }
    else {
        Write-Output "  !!  $($m.Name)  LITERALS DIFFER"
        Write-Output "        old: $a"
        Write-Output "        new: $b"
        $fail++
    }
}

Write-Output ''
Write-Output "=== totals ==="
Write-Output "  moves compared : $($moves.Count)"
Write-Output "  identical      : $($moves.Count - $fail - $unknown)"
Write-Output "  literal diffs  : $fail"
Write-Output "  declared carried to caller: $carried literal(s)"
Write-Output "  UNRESOLVED     : $unknown"
if ($Mutate) {
    if ($fail -gt 0) { Write-Output "MUTATION DETECTED in $fail move(s) - the comparator CAN fail. Good." }
    else { Write-Output "!! MUTATION NOT DETECTED - this comparator is not a check (rule 7)."; $fail++ }
}
exit ($fail + $unknown)
