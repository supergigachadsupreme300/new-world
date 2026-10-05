# tools/Write-Tree.ps1 - regenerate TREE.md from git's index.
#
# TREE.md is GENERATED. Do not hand-edit it.
#
# Why generated rather than maintained: 1iu shipped Magic/README.md and
# Animation/README.md, and every symbol in them was wrong on the first pass.
# A navigation map is a copy of the codebase, and it is the one file nothing in
# this repo compiles, greps or otherwise checks. A hand-maintained tree of a
# 1000+ file repo therefore rots on the very next structural commit, silently.
# A generated one is at worst out of date, and it names the commit it saw.
#
# Usage (from anywhere):
#     powershell -ExecutionPolicy Bypass -File tools\Write-Tree.ps1
#
#     -OutFile      path to write          (default TREE.md at repo root)
#     -ExpandBelow  expand a directory when it holds at most this many
#                   non-.meta files; deeper ones collapse to "[N files]"
#     -AlwaysExpand directory prefixes expanded regardless of depth
#     -SelfTest     print the index counts and exit, without writing
#
# Source of truth is `git ls-files`, so untracked and .gitignore'd clutter
# (Library/, Temp/, Logs/, obj/, UserSettings/) cannot appear here.

param(
    [string]$OutFile = "TREE.md",
    [int]$ExpandBelow = 25,
    [string[]]$AlwaysExpand = @("Assets/Scripts"),
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath (Join-Path $repoRoot ".git"))) { $repoRoot = (Get-Location).Path }
Set-Location $repoRoot

# --- 1. read the index -------------------------------------------------
$tracked = @(git ls-files)
if ($tracked.Count -eq 0) { throw "git ls-files returned nothing - not inside a repository, or nothing is tracked." }

$metas  = @($tracked | Where-Object { $_ -match '\.meta$' })
$assets = @($tracked | Where-Object { $_ -notmatch '\.meta$' })

function Split-Path-Parent([string]$p) {
    $i = $p.LastIndexOf('/')
    if ($i -lt 0) { return "" }
    return $p.Substring(0, $i)
}
function Split-Path-Leaf([string]$p) {
    $i = $p.LastIndexOf('/')
    if ($i -lt 0) { return $p }
    return $p.Substring($i + 1)
}

# --- 2. index paths into parent -> children, and per-directory counts ---
$childFiles = @{}
$childDirs  = @{}
$dirCount   = @{}

foreach ($f in $assets) {
    $parent = Split-Path-Parent $f
    if (-not $childFiles.ContainsKey($parent)) { $childFiles[$parent] = New-Object System.Collections.ArrayList }
    [void]$childFiles[$parent].Add((Split-Path-Leaf $f))

    $parts = $f -split '/'
    for ($i = 1; $i -lt $parts.Count; $i++) {
        $d = ($parts[0..($i - 1)] -join '/')
        if ($dirCount.ContainsKey($d)) { $dirCount[$d] = [int]$dirCount[$d] + 1 } else { $dirCount[$d] = 1 }
    }
}

foreach ($d in $dirCount.Keys) {
    $parent = Split-Path-Parent $d
    if (-not $childDirs.ContainsKey($parent)) { $childDirs[$parent] = New-Object System.Collections.ArrayList }
    [void]$childDirs[$parent].Add((Split-Path-Leaf $d))
}

if ($SelfTest) {
    Write-Output ("tracked={0}  non-meta={1}  metas={2}  directories={3}" -f $tracked.Count, $assets.Count, $metas.Count, $dirCount.Count)
    foreach ($d in @("Assets", "Assets/Scripts", "Assets/Scripts/World", "Assets/Scripts/Models/MapBuilder", "_ArtSource")) {
        Write-Output ("  {0,-34} {1,4} files" -f $d, $dirCount[$d])
    }
    Write-Output ("childDirs['']={0}  childFiles['']={1}" -f $childDirs[""].Count, $childFiles[""].Count)
    return
}

# --- 3. render ---------------------------------------------------------
# Glyphs via [char] rather than `u escapes: those are PowerShell 6+, and this
# project runs 5.1, where `u{251C} emits the literal text "u{251C}".
$TEE   = [string][char]0x251C   # |-
$ELBOW = [string][char]0x2514   # |-
$BAR   = [string][char]0x2500   # -
$VLINE = [string][char]0x2502   # |

$out = New-Object System.Collections.ArrayList
function Emit([string]$s) { [void]$out.Add($s) }

function Should-Expand([string]$dir, [int]$limit, [string[]]$force) {
    if ([int]$dirCount[$dir] -le $limit) { return $true }
    foreach ($p in $force) {
        if ($dir -eq $p) { return $true }                                    # is a forced dir
        if ($dir.Length -gt $p.Length -and $dir.StartsWith($p + "/")) { return $true }   # inside one
        # Is an ANCESTOR of a forced dir. Without this, Assets/ (445 files)
        # collapses on the threshold and Assets/Scripts is never reached - the
        # threshold then silently hides exactly the part the force list exists
        # to protect. A size heuristic must not be able to hide a subtree.
        if ($p.Length -gt $dir.Length -and $p.StartsWith($dir + "/")) { return $true }
    }
    return $false
}

function Render-Tree([string]$dir, [string]$indent, [int]$limit, [string[]]$force) {
    $entries = New-Object System.Collections.ArrayList
    if ($childDirs.ContainsKey($dir))  { foreach ($d in $childDirs[$dir])  { [void]$entries.Add("D|" + $d) } }
    if ($childFiles.ContainsKey($dir)) { foreach ($f in $childFiles[$dir]) { [void]$entries.Add("F|" + $f) } }

    # directories before files, alphabetical within each group
    $sorted = @($entries | Sort-Object @{ Expression = { $_.Substring(0, 1) } }, @{ Expression = { $_.Substring(2) } })

    $n = $sorted.Count
    for ($i = 0; $i -lt $n; $i++) {
        $isLast = ($i -eq ($n - 1))
        $branch = $(if ($isLast) { $ELBOW + $BAR + $BAR + " " } else { $TEE + $BAR + $BAR + " " })
        $pad    = $(if ($isLast) { "    " } else { $VLINE + "   " })

        $entry = $sorted[$i]
        $kind  = $entry.Substring(0, 1)
        $name  = $entry.Substring(2)
        $full  = $(if ($dir -eq "") { $name } else { $dir + "/" + $name })

        if ($kind -eq "D") {
            if (Should-Expand $full $limit $force) {
                Emit ($indent + $branch + $name + "/")
                Render-Tree $full ($indent + $pad) $limit $force
            } else {
                Emit ($indent + $branch + $name + "/   [" + $dirCount[$full] + " files]")
            }
        } else {
            Emit ($indent + $branch + $name)
        }
    }
}

# --- 4. assemble the document -----------------------------------------
$sha   = (git rev-parse --short HEAD)
$stamp = (Get-Date).ToString("yyyy-MM-dd HH:mm")
$cs    = @($assets | Where-Object { $_ -match '\.cs$' }).Count

Emit "# Project tree"
Emit ""
Emit "**Generated file - do not hand-edit.** Regenerate after any structural change:"
Emit ""
Emit "``````powershell"
Emit "powershell -ExecutionPolicy Bypass -File tools\Write-Tree.ps1"
Emit "``````"
Emit ""
Emit "| | |"
Emit "|---|---|"
Emit "| Source of truth | ``git ls-files`` (untracked and ``.gitignore``d paths cannot appear) |"
Emit "| Generated at commit | ``$sha`` (HEAD when written - the commit *before* the one this file lands in) |"
Emit "| Generated on | $stamp |"
Emit "| Tracked files | $($tracked.Count) = $($assets.Count) non-``.meta`` + $($metas.Count) ``.meta`` |"
Emit "| C# files | $cs |"
Emit ""
Emit "Reading the tree:"
Emit ""
Emit "- ``.meta`` files are omitted everywhere. There are $($metas.Count) of them and none carries"
Emit "  information a reader needs. Every ``.cs`` has a paired ``.cs.meta`` and every folder under"
Emit "  ``Assets/`` has a folder meta - both currently hold - but **no check enforces them**:"
Emit "  ``tools\StaticChecks.ps1`` does not mention ``.meta`` at all. Verify by hand:"
Emit "  folder count vs folders missing a sibling ``.meta``, and ``*.cs`` vs ``*.cs.meta``."
Emit "  It held only by luck until 1ji: the four ``Legacy`` quarantine folders had no folder meta"
Emit "  and were committed by accident rather than by anything noticing they were absent."
Emit "- ``name/   [N files]`` means the directory was collapsed for length. Nothing inside it is"
Emit "  missing, only summarised. Raise ``-ExpandBelow`` to see more."
Emit "- ``Assets/Scripts`` is always expanded in full - it is the part people navigate."
Emit ""
Emit "Canonical docs: ``AGENTS.md`` (working rules), ``game-design.md`` (design),"
Emit "``PROGRESS.md`` (shipped work), ``THINKING.md`` (reasoning). This file deliberately asserts"
Emit "no design or process claim, only structure - structure being the one thing it can be"
Emit "regenerated to verify."
Emit ""
Emit "``````"
Render-Tree "" "" $ExpandBelow $AlwaysExpand
Emit "``````"

$target = Join-Path $repoRoot $OutFile
# BOM ON, deliberately. Every other .md in this repo has one, and Windows PowerShell 5.1
# reads a BOM-less UTF-8 file as ANSI - so a BOM-less TREE.md renders as mojibake in the
# shell this repo is actually maintained from. GitHub strips it, so it costs nothing there.
[System.IO.File]::WriteAllLines($target, $out, (New-Object System.Text.UTF8Encoding($true)))
Write-Output ("wrote {0} ({1} lines) from commit {2}" -f $OutFile, $out.Count, $sha)