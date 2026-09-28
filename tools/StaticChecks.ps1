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
# AGENTS.md rule 3: any WorldBuilder*.cs edited here belongs in $files, or checks 1, 4
# and 5 silently stop covering it. 1hz added Persistence + NPCs.
$files = @($blueprints, $persistence, $npcs, $world, $testground)

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

# ---------------------------------------------------------------- 1. balance
Section '1. brace / paren balance'
foreach ($f in $files) {
    $t = [System.IO.File]::ReadAllText((Resolve-Path $f))
    $ob = ($t.ToCharArray() | Where-Object { $_ -eq '{' }).Count
    $cb = ($t.ToCharArray() | Where-Object { $_ -eq '}' }).Count
    $op = ($t.ToCharArray() | Where-Object { $_ -eq '(' }).Count
    $cp = ($t.ToCharArray() | Where-Object { $_ -eq ')' }).Count
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
foreach ($f in $files) {
    $ls = [System.IO.File]::ReadAllLines((Resolve-Path $f))
    for ($i = 0; $i -lt $ls.Count; $i++) {
        $m = [regex]::Match($ls[$i], '^\s*' + $typeAlt + '\s+(\w+)\s*;\s*$')
        if (-not $m.Success) { continue }
        $n = $m.Groups[1].Value
        $assigned = $false
        for ($k = $i; $k -lt $ls.Count; $k++) {
            if ($ls[$k] -match ('\b' + [regex]::Escape($n) + '\s*=[^=]')) { $assigned = $true; break }
            if ($ls[$k] -match 'return\b|throw\b') { break }
        }
        $label = '{0}:{1} {2}' -f $f.Split('\')[-1], ($i + 1), $n
        if ($assigned) { Ok "$label (assigned later in the same block)" } else { Bad "$label never assigned" }
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

Section 'summary'
if ($problems -eq 0) { Write-Output '  0 candidates. Still not a compile: Unity is the compiler.' }
else { Write-Output "  $problems candidate(s) above - fix or explain each before committing." }

Pop-Location
