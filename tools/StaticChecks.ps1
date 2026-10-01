<#
    StaticChecks.ps1 - the compile-risk sweep for this project.

    WHY THIS EXISTS (AGENTS.md rule 3): no CLI/Unity build is run here, so Unity's
    console is the only real compiler. Two errors shipped through review in 1hm-1hp
    (CS0029 a `void` helper returned as a GameObject, CS0165 a `switch`-case local
    read from a sibling case). Both are invisible to grep: grep proves a symbol
    exists, not that two signatures agree, and a case-local is in scope for the
    whole switch so a cross-case read LOOKS legal. This script mechanises the checks
    that would have caught them, so a session does not re-derive them by hand.

    It is NOT a compiler. It cannot type-check, resolve overloads by argument type,
    or prove definite assignment on every path - it reports CANDIDATES for a human
    to confirm. Run it from the repo root:

    Check 1 counts braces and parens over code with comments, string literals and
    char literals STRIPPED. 1ic added a readout whose summary line ends
    `.Append("), ")` - a ')' inside a string - and the raw character count reported
    it as parens 840/841, i.e. one candidate on the very first file it was run
    against. A check that cries wolf on its first file has a silence nobody can read
    any more, so the count is literal-aware (AGENTS.md rule 7). Verified both ways:
    an extra '(' injected into WorldStreamer.Deform.cs:38 fires the check, and the
    restored file goes quiet.

        powershell -ExecutionPolicy Bypass -File tools\StaticChecks.ps1

    Lives outside Assets/ on purpose: a script under Assets/ is compiled by Unity
    and can itself break the build.
#>

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

$blueprints = 'Assets\Scripts\World\WorldBuilder.Blueprints.cs'
$persistence = 'Assets\Scripts\World\WorldBuilder.Persistence.cs'
$npcs       = 'Assets\Scripts\World\WorldBuilder.NPCs.cs'
$world      = 'Assets\Scripts\World\WorldBuilder.cs'
$testground = 'Assets\Scripts\Opt\NewWorldTestGround.cs'
$corneraudit = 'Assets\Scripts\World\Streaming\WorldStreamer.CornerAudit.cs'
$chunkbuild  = 'Assets\Scripts\World\Streaming\WorldStreamer.ChunkBuild.cs'
$farshell    = 'Assets\Scripts\World\Streaming\WorldStreamer.FarShell.cs'
$deform      = 'Assets\Scripts\World\Streaming\WorldStreamer.Deform.cs'
$crateraudit = 'Assets\Scripts\World\Streaming\WorldStreamer.CraterAudit.cs'

# AGENTS.md rule 3: any WorldBuilder*.cs edited here belongs in $files, or checks 1, 4
# and 5 silently stop covering it. 1hz added Persistence + NPCs. 1hy added the
# WorldStreamer corner/void audit (checks 1 and 4 apply to it; 2/3/5/6 are builder-specific).
# 1i4 added ChunkBuild + FarShell - the seam fix and the far-cell source fix - and
# check 7 below, which exists because of ChunkBuild.
# 1i9 added Deform - the facet-visibility skirt, three new locals read across a loop
# and a new static helper, so it needs the same coverage rather than shipping unchecked.
# 1in added CraterAudit - the same partial-class shape as CornerAudit, with an `out`
# parameter set read across four helper calls (check 4's CS0165 surface) and a private
# TryGet-style helper (check 2's void-return surface).
$files = @($blueprints, $persistence, $npcs, $world, $testground, $corneraudit,
           $chunkbuild, $farshell, $deform, $crateraudit)

# Types a structure-part helper can be declared with, plus local declarations.
$retAlt  = '(?:static\s+)?(?:GameObject|void|int|float|bool|string|Vector3|Color|Vector2|Quaternion|Transform)'
$typeAlt = '(?:const\s+)?(?:float|int|bool|string|Vector3|Color|Vector2|Quaternion|GameObject|Transform)'

$problems = 0
function Section($t) { Write-Output ''; Write-Output "=== $t ===" }
function Bad($t) { Write-Output "  !! $t"; $script:problems++ }
function Ok($t)  { Write-Output "  ok  $t" }

# Index of the paren matching the one at $open; -1 if it is not on this string.
function MatchClose([string]$s, [int]$open) {
    $d = 0
    for ($i = $open; $i -lt $s.Length; $i++) {
        if ($s[$i] -eq '(') { $d++ }
        elseif ($s[$i] -eq ')') { $d--; if ($d -eq 0) { return $i } }
    }
    return -1
}

# Argument count of the text between an already-matched paren pair, ignoring commas
# nested inside a call such as `new Vector3(0f, 1f, 2f)`.
function CountArgs([string]$inner) {
    if ($inner.Trim() -eq '') { return 0 }
    $n = 1; $d = 0
    for ($i = 0; $i -lt $inner.Length; $i++) {
        $c = $inner[$i]
        if ($c -eq '(') { $d++ }
        elseif ($c -eq ')') { $d-- }
        elseif ($c -eq ',' -and $d -eq 0) { $n++ }
    }
    return $n
}

foreach ($f in $files) {
    if (-not (Test-Path $f)) { Write-Output "MISSING: $f"; $problems++ }
}

# Strip everything the C# lexer would not count as a delimiter, so a '{' or ')' inside a
# string, a char literal or a comment cannot manufacture a candidate. Character-level,
# in one pass, because a regex cannot tell an escaped quote from a closing one.
function StripNonCode([string]$s) {
    $sb = New-Object System.Text.StringBuilder $s.Length
    $i = 0; $n = $s.Length
    while ($i -lt $n) {
        $c = $s[$i]
        if ($c -eq '/' -and ($i + 1) -lt $n) {
            $next = $s[$i + 1]
            if ($next -eq '/') {                       # line comment
                while ($i -lt $n -and $s[$i] -ne "`n") { $i++ }
                continue
            }
            if ($next -eq '*') {                       # block comment
                $i += 2
                while ($i -lt $n -and -not ($s[$i] -eq '*' -and ($i + 1) -lt $n -and $s[$i + 1] -eq '/')) { $i++ }
                $i += 2
                continue
            }
        }
        if ($c -eq '@' -and ($i + 1) -lt $n -and $s[$i + 1] -eq '"') {   # verbatim @"..."
            $i += 2
            while ($i -lt $n) {
                if ($s[$i] -eq '"') {
                    if (($i + 1) -lt $n -and $s[$i + 1] -eq '"') { $i += 2; continue }
                    $i++; break
                }
                $i++
            }
            [void]$sb.Append(' ')
            continue
        }
        if ($c -eq '"' -or $c -eq "'") {                # regular string / char literal
            $q = $c
            $i++
            while ($i -lt $n) {
                if ($s[$i] -eq '\') { $i += 2; continue }
                if ($s[$i] -eq $q) { $i++; break }
                if ($s[$i] -eq "`n") { break }         # unterminated: bail at EOL
                $i++
            }
            [void]$sb.Append(' ')
            continue
        }
        [void]$sb.Append($c)
        $i++
    }
    return $sb.ToString()
}

# ---------------------------------------------------------------- 1. balance
Section '1. brace / paren balance'
foreach ($f in $files) {
    $t = StripNonCode ([System.IO.File]::ReadAllText((Resolve-Path $f)))
    $chars = $t.ToCharArray()
    $ob = 0; $cb = 0; $op = 0; $cp = 0
    foreach ($c in $chars) {
        if ($c -eq '{') { $ob++ } elseif ($c -eq '}') { $cb++ }
        elseif ($c -eq '(') { $op++ } elseif ($c -eq ')') { $cp++ }
    }
    if ($ob -ne $cb -or $op -ne $cp) { Bad "$f braces $ob/$cb parens $op/$cp" }
    else { Ok ("{0} braces {1}/{1} parens {2}/{2}" -f $f.Split('\')[-1], $ob, $op) }
}

# ------------------------------------------- 2. declared vs called arity
Section '2. helper arity: every call must match SOME declared overload'
$lines = [System.IO.File]::ReadAllLines((Resolve-Path $blueprints))
$decl = @{}
for ($i = 0; $i -lt $lines.Count; $i++) {
    $m = [regex]::Match($lines[$i], "(?:private|public|internal)\s+$retAlt\s+(\w+)\s*\(")
    if (-not $m.Success) { continue }
    $name = $m.Groups[1].Value
    $open = $m.Index + $m.Length - 1
    $sig = $lines[$i]
    $close = MatchClose $sig $open
    if ($close -lt 0) {                       # signature wrapped across lines
        $j = $i; $acc = $lines[$i]
        while ($close -lt 0 -and $j -lt $lines.Count) { $j++; $acc += ' ' + $lines[$j].Trim(); $close = MatchClose $acc $open }
        $sig = $acc
    }
    if (-not $decl.ContainsKey($name)) { $decl[$name] = @() }
    $decl[$name] += (CountArgs $sig.Substring($open + 1, $close - $open - 1))
}
$helpers = @('CreatePartBoxOn', 'CreatePartPanelBetween', 'CreatePartGableSteps',
             'CreatePartCubeRotated', 'CreatePartCube', 'CreateShrineSanqingFigure')
foreach ($h in $helpers) {
    if (-not $decl.ContainsKey($h)) { Bad "$h is not declared in $(Split-Path $blueprints -Leaf)"; continue }
    Ok ("{0,-26} declared ({1})" -f $h, ($decl[$h] -join ', '))
}
for ($i = 0; $i -lt $lines.Count; $i++) {
    $l = $lines[$i].Trim()
    if ($l -match '^(?:private|public|internal)\s') { continue }
    foreach ($h in $helpers) {
        $m = [regex]::Match($l, "(?<![\w.])$h\s*\(")
        if (-not $m.Success) { continue }
        $open = $m.Index + $m.Length - 1
        $call = $l
        $close = MatchClose $call $open
        if ($close -lt 0) {                     # call wrapped across lines
            $j = $i; $acc = $l
            while ($close -lt 0 -and $j -lt $lines.Count) { $j++; $acc += ' ' + $lines[$j].Trim(); $close = MatchClose $acc $open }
            $call = $acc
        }
        $a = CountArgs $call.Substring($open + 1, $close - $open - 1)
        if ($decl[$h] -notcontains $a) { Bad "line $($i+1): $h called with $a, declared ($($decl[$h] -join ','))" }
    }
}
Ok "every call site matches a declared arity"

# ------------------------------------------- 3. a void helper that is returned
Section '3. `return <void helper>(...)` (CS0029)'
$voids = @{}
for ($i = 0; $i -lt $lines.Count; $i++) {
    $m = [regex]::Match($lines[$i], '(?:private|public|internal)\s+void\s+(\w+)\s*\(')
    if ($m.Success) { $voids[$m.Groups[1].Value] = $i + 1 }
}
foreach ($v in $voids.Keys) {
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "return\s+$v\s*\(") { Bad "line $($i+1) returns void helper $v (declared line $($voids[$v]))" }
    }
}
Ok "no void helper is returned"

# ------------------------------------------- 4. bare local declarations
Section '4. locals declared with no initializer (CS0165 candidates - confirm each is assigned before use)'
# An `out` PARAMETER is assigned by the callee's contract, and a bare local that is only ever
# written through an `out` CALL ARGUMENT (the TryGetValue / TryParse pattern) is assigned too.
# Neither is a CS0165 candidate. 1hy added a file whose every method reports through out params,
# and a check that flags all of them trains its reader to ignore the section — so both shapes are
# recognised here instead. The trailing separator accepts `,` as well as `;` because a wrapped
# signature puts the final out param on a line ending in `)`.
$declPat = '^\s*(out\s+)?' + $typeAlt + '\s+(\w+)\s*[,;]\s*$'
foreach ($f in $files) {
    $ls = [System.IO.File]::ReadAllLines((Resolve-Path $f))
    for ($i = 0; $i -lt $ls.Count; $i++) {
        $m = [regex]::Match($ls[$i], $declPat)
        if (-not $m.Success) { continue }
        $isOutParam = $m.Groups[1].Success
        $n = $m.Groups[2].Value
        $assigned = $isOutParam
        for ($k = $i; $k -lt $ls.Count -and -not $assigned; $k++) {
            if ($ls[$k] -match ('\b' + [regex]::Escape($n) + '\s*=[^=]')) { $assigned = $true; break }
            if ($ls[$k] -match ('\bout\s+' + [regex]::Escape($n) + '\b')) { $assigned = $true; break }
            if ($ls[$k] -match 'return\b|throw\b') { break }
        }
        $label = '{0}:{1} {2}' -f $f.Split('\')[-1], ($i + 1), $n
        if ($isOutParam) { Ok "$label (out parameter - assigned by the callee's contract)" }
        elseif ($assigned) { Ok "$label (assigned later in the same block)" }
        else { Bad "$label never assigned" }
    }
}

# ------------------------------------------- 5. cross-case local reads
Section '5. switch cases: a local declared in one case and read in another (CS0165), or declared twice'
foreach ($f in $files) {
    $ls = [System.IO.File]::ReadAllLines((Resolve-Path $f))
    $depth = 0; $inSwitch = $false; $swDepth = 0
    $d = @{}; $cur = '<switch-head>'
    for ($i = 0; $i -lt $ls.Count; $i++) {
        $l = $ls[$i]
        $isComment = $l.TrimStart().StartsWith('//')
        if ($l -match 'switch\s*\(') { $inSwitch = $true; $swDepth = $depth; $d = @{}; $cur = '<switch-head>' }
        if ($inSwitch -and $l -match 'case\s+"([^"]+)"') { $cur = $matches[1] }
        if ($inSwitch -and -not $isComment) {
            $dm = [regex]::Match($l, '^\s{12,}' + $typeAlt + '\s+(\w+)\s*(?:=|;)')
            if ($dm.Success) {
                $n = $dm.Groups[1].Value
                if ($d.ContainsKey($n)) { Bad "$($f.Split('\')[-1]) '$n' declared in both case '$($d[$n].Case)' and '$cur' (line $($i+1))" }
                $d[$n] = @{ Case = $cur; Line = ($i + 1) }
            }
            foreach ($n in @($d.Keys)) {
                if ($d[$n].Line -ne ($i + 1) -and $d[$n].Case -ne $cur -and
                    $l -match ('(?<![\w.])' + [regex]::Escape($n) + '(?![\w])')) {
                    Bad "$($f.Split('\')[-1]) line $($i+1) case '$cur' reads '$n' declared in case '$($d[$n].Case)' (line $($d[$n].Line)) - hoist it to method scope"
                }
            }
        }
        $step = 0
        foreach ($c in $l.ToCharArray()) { if ($c -eq '{') { $step++ } elseif ($c -eq '}') { $step-- } }
        $depth += $step
        if ($inSwitch -and $depth -le $swDepth) { $inSwitch = $false }
    }
}
Ok "no cross-case reads, no duplicate case-scope names"

# ------------------------------------------- 6. part key <-> builder case parity
Section '6. save/load part keys must each have a builder case (a renamed key builds NOTHING, silently)'
$wbText = [System.IO.File]::ReadAllText((Resolve-Path $world))
$bpText = [System.IO.File]::ReadAllLines((Resolve-Path $blueprints)) -join "`n"
foreach ($p in @('Shrine', 'Church', 'Pagoda')) {
    $keys = [regex]::Matches($wbText, '"(' + $p + '_\w+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
    $cases = [regex]::Matches($bpText, 'case "(' + $p + '_\w+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
    $noCase = @($keys | Where-Object { $cases -notcontains $_ })
    $noTable = @($cases | Where-Object { $keys -notcontains $_ })
    if ($noCase.Count -gt 0) { Bad "$p keys with no builder case: $($noCase -join ', ')" }
    if ($noTable.Count -gt 0) { Bad "$p cases not in any table (dead geometry): $($noTable -join ', ')" }
    if ($noCase.Count -eq 0 -and $noTable.Count -eq 0) { Ok "$p : $($keys.Count) keys == $($cases.Count) cases" }
}

Section '7. a member must be INSIDE a class body (CS0106 / CS1519)'
# 1i4 shipped a helper method declared above `public partial class WorldStreamer`, between
# the usings and the class. Grep found the symbol, brace/paren counts balanced (the method
# and the class simply each balance), and the review read straight past it. Only Unity's
# parser objected, with CS0106 'the modifier private is not valid for this item' - a line
# that does not even name the class. A member at brace depth 0 is illegal in C# full stop,
# so this check has no false-positive shape: there is no valid C# that trips it.
$declRe = '^\s*(?:private|public|internal|protected)\s+(?:static\s+|sealed\s+|override\s+|virtual\s+|async\s+|extern\s+|unsafe\s+|partial\s+)*[A-Za-z_][\w<>\[\],\.\?]*\s+[A-Za-z_]\w*\s*\('
$outside = 0
foreach ($f in $files) {
    $lines = [System.IO.File]::ReadAllLines((Resolve-Path $f))
    $d = 0
    $inBlock = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $ln = $lines[$i]
        $t = $ln.Trim()
        if ($inBlock) {
            if ($t -match '\*/') { $inBlock = $false }
            continue
        }
        if ($t.StartsWith('/*')) { if ($t -notmatch '\*/') { $inBlock = $true }; continue }
        if ($t.StartsWith('//')) { continue }
        if ($d -eq 0 -and $t -match $declRe) {
            # depth 0 + an access modifier + a call-shaped signature: there is nowhere in C#
            # this can legally sit, so report the class declaration that follows it.
            $cls = ($lines | Select-String -Pattern '^\s*(?:public|internal|private)?\s*(?:partial\s+)?(?:class|struct)\s' | Select-Object -First 1)
            Bad "$f L$($i + 1): member declared OUTSIDE the class body (depth 0) - $($t.Substring(0, [Math]::Min(60, $t.Length)))"
            $outside++
        }
        foreach ($c in $ln.ToCharArray()) {
            if ($c -eq '{') { $d++ } elseif ($c -eq '}') { $d-- }
        }
    }
}
if ($outside -eq 0) { Ok "every member sits inside a class body" }

Section 'summary'
if ($problems -eq 0) { Write-Output '  0 candidates. Still not a compile: Unity is the compiler.' }
else { Write-Output "  $problems candidate(s) above - fix or explain each before committing." }

Pop-Location
