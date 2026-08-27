# Consolidates the individual mod repos into a single monorepo rooted at D:\Code\RimWorld,
# preserving each repo's history under its subdirectory, with origin https://github.com/mnmr/rw.eprime.git.
#
# What it does, in order:
#   1. Preflight: git + git-filter-repo available, no repo at the workspace root yet,
#      every mod repo clean (no uncommitted changes).
#   2. For each mod repo: fresh clone into temp\monorepo-build, strip docs/superpowers/**
#      and the mod's own LICENSE from all of history (the root LICENSE from GitHub becomes
#      the single repo license), then rewrite paths under the mod's subdirectory and prefix
#      its tags with "<Mod>-".
#   3. Build the monorepo in temp\monorepo-build\monorepo: start from origin/main (the
#      GitHub repo's initial commit with LICENSE), add .gitignore, then merge each
#      rewritten history (--allow-unrelated-histories). Requires network access.
#   4. Map it onto the workspace: move the monorepo's .git to D:\Code\RimWorld\.git and
#      delete each mod's old .git (restore from your folder backup if needed).
#   5. Commit Shared/, root AGENTS.md/CLAUDE.md and scripts/ as new content.
#   6. Rewrite github.com/mnmr/rw.workroles links in READMEs and workshop descriptions to
#      the monorepo equivalents, and commit.
#   7. With -Push: push main + tags to origin.
#
# Prereqs: pwsh 7+, git, git-filter-repo (pip install git-filter-repo).
# Rollback: restore the workspace from your folder backup.

#Requires -Version 7
[CmdletBinding()]
param(
    [switch]$Push
)

$ErrorActionPreference = 'Stop'

$Root      = Split-Path -Parent $PSScriptRoot
$Mods      = @('Readouts', 'QualityJobs', 'WorkRoles')
$ModPrefix = @{ Readouts = 'ER'; QualityJobs = 'QJ'; WorkRoles = 'WR' }   # historical commit-message prefixes
$RemoteUrl = 'https://github.com/mnmr/rw.eprime.git'
$BuildDir  = Join-Path $Root 'temp\monorepo-build'
$SpecPath  = 'docs/superpowers'   # stripped from all history

function Invoke-Git {
    param(
        [Parameter(Mandatory)][string]$WorkDir,
        [Parameter(Mandatory, ValueFromRemainingArguments)][string[]]$GitArgs
    )
    & git -C $WorkDir @GitArgs
    if ($LASTEXITCODE -ne 0) {
        throw "git $($GitArgs -join ' ') failed in $WorkDir (exit $LASTEXITCODE)"
    }
}

Write-Host '== Preflight =='

if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'git not found on PATH.' }
& git filter-repo --version *> $null
if ($LASTEXITCODE -ne 0) { throw 'git-filter-repo not found. Install with: pip install git-filter-repo' }

if (Test-Path (Join-Path $Root '.git')) {
    throw "$Root already contains a .git — the workspace root must not be a repo before consolidation."
}

$modBranches = @{}
foreach ($mod in $Mods) {
    $modPath = Join-Path $Root $mod
    if (-not (Test-Path (Join-Path $modPath '.git'))) { throw "$modPath is not a git repository." }
    $dirty = Invoke-Git $modPath status --porcelain
    if ($dirty) {
        throw "$mod has uncommitted changes — commit or stash them first:`n$($dirty -join "`n")"
    }
    $modBranches[$mod] = (Invoke-Git $modPath symbolic-ref --short HEAD)
    Write-Host "  $mod is clean (branch $($modBranches[$mod]))"
}

if (Test-Path $BuildDir) { Remove-Item -Recurse -Force $BuildDir }
New-Item -ItemType Directory -Path $BuildDir | Out-Null

Write-Host '== Rewriting mod histories =='

foreach ($mod in $Mods) {
    $clone = Join-Path $BuildDir "rw-$mod"
    Write-Host "  $mod -> $clone"
    & git clone --quiet --no-local (Join-Path $Root $mod) $clone
    if ($LASTEXITCODE -ne 0) { throw "clone of $mod failed" }

    # Strip specs/plans and the per-mod LICENSE from every commit, then move everything
    # under the mod's directory, prefix tags, and tag every historical commit message
    # with the mod's short code.
    Invoke-Git $clone filter-repo --quiet --invert-paths --path $SpecPath --path LICENSE
    $callback = "return b'[$($ModPrefix[$mod])] ' + message"
    Invoke-Git $clone filter-repo --quiet --force --to-subdirectory-filter $mod --tag-rename ":$mod-" --message-callback $callback
}

Write-Host '== Building monorepo =='

$mono = Join-Path $BuildDir 'monorepo'
New-Item -ItemType Directory -Path $mono | Out-Null
Invoke-Git $mono init --quiet --initial-branch=main

# Match the mod repos' line-ending behavior so the relocated worktree compares clean.
$autocrlf = & git -C (Join-Path $Root $Mods[0]) config --get core.autocrlf
if ($autocrlf) { Invoke-Git $mono config core.autocrlf $autocrlf }

# Start from the remote's initial commit (the GitHub repo was created with a LICENSE),
# so the final push is a plain fast-forward.
Invoke-Git $mono remote add origin $RemoteUrl
Invoke-Git $mono fetch --quiet origin main
Invoke-Git $mono reset --quiet --hard origin/main

$gitignore = @'
# Game source/artwork, local profiles and saves must never be committed
/Game/
/Saves/
/AutomationProfiles/

# Specs and plans are intentionally untracked
docs/superpowers/

# Build output and scratch space
bin/
obj/
temp/
.vs/
*.user
'@
Set-Content -Path (Join-Path $mono '.gitignore') -Value $gitignore -NoNewline
Invoke-Git $mono add .gitignore
Invoke-Git $mono commit --quiet -m 'Add monorepo .gitignore'

foreach ($mod in $Mods) {
    $clone = Join-Path $BuildDir "rw-$mod"
    Write-Host "  merging $mod history"
    Invoke-Git $mono fetch --quiet --tags $clone $modBranches[$mod]
    Invoke-Git $mono merge --quiet --allow-unrelated-histories -m "Merge $mod history into monorepo" FETCH_HEAD
}

Write-Host '== Mapping monorepo onto the workspace =='

Move-Item -Path (Join-Path $mono '.git') -Destination (Join-Path $Root '.git')

foreach ($mod in $Mods) {
    Remove-Item -Recurse -Force (Join-Path $Root "$mod\.git")
    Write-Host "  removed $mod\.git"
    $modLicense = Join-Path $Root "$mod\LICENSE"
    if (Test-Path $modLicense) {
        Remove-Item -Force $modLicense
        Write-Host "  removed $mod\LICENSE (root LICENSE governs)"
    }
}

# Materialize tracked files that don't exist on disk yet (.gitignore, LICENSE, and
# anything else from the remote's initial commit).
$missing = Invoke-Git $Root ls-files --deleted
foreach ($file in $missing) {
    Invoke-Git $Root checkout --quiet -- $file
}

$drift = Invoke-Git $Root status --porcelain --untracked-files=no
if ($drift) {
    Write-Warning "Tracked files differ from the rewritten history (likely line endings) — review before committing:`n$($drift -join "`n")"
}

Write-Host '== Adding shared library and workspace files =='

Invoke-Git $Root add Shared AGENTS.md CLAUDE.md scripts
Invoke-Git $Root commit --quiet -m 'Add shared library and workspace root files'

Write-Host '== Rewriting GitHub links =='

$replacements = [ordered]@{
    'raw.githubusercontent.com/mnmr/rw.workroles/main/' = 'raw.githubusercontent.com/mnmr/rw.eprime/main/WorkRoles/'
    'github.com/mnmr/rw.workroles#readme'               = 'github.com/mnmr/rw.eprime/tree/main/WorkRoles#readme'
    'github.com/mnmr/rw.workroles'                      = 'github.com/mnmr/rw.eprime'
    '[LICENSE](LICENSE)'                                = '[LICENSE](../LICENSE)'
}

$changed = @()
foreach ($mod in $Mods) {
    foreach ($name in @('README.md', 'workshop-description.bbcode')) {
        $file = Join-Path $Root "$mod\$name"
        if (-not (Test-Path $file)) { continue }
        $text = [IO.File]::ReadAllText($file)
        $updated = $text
        foreach ($entry in $replacements.GetEnumerator()) {
            $updated = $updated.Replace($entry.Key, $entry.Value)
        }
        if ($updated -ne $text) {
            [IO.File]::WriteAllText($file, $updated)
            Invoke-Git $Root add "$mod/$name"
            $changed += "$mod/$name"
        }
    }
}
if ($changed) {
    Invoke-Git $Root commit --quiet -m 'Point GitHub and license links at the monorepo'
    Write-Host "  updated: $($changed -join ', ')"
} else {
    Write-Host '  no link changes needed'
}

# Surface any old-repo references the rewrite did not cover.
$leftovers = Get-ChildItem -Path $Root -Recurse -Include 'README.md', 'workshop-description.bbcode' -Depth 2 |
    Where-Object { $_.FullName -notmatch '\\temp\\' } |
    Select-String -Pattern 'github(usercontent)?\.com/mnmr/rw\.(?!eprime)' |
    ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
if ($leftovers) {
    Write-Warning "Remaining links to old repos — review manually:`n$($leftovers -join "`n")"
}

if ($Push) {
    Write-Host '== Pushing to origin =='
    Invoke-Git $Root push -u origin main
    Invoke-Git $Root push origin --tags
} else {
    Write-Host 'Skipped push (run with -Push, or: git push -u origin main && git push origin --tags).'
}

Write-Host ''
Write-Host '== Done =='
Write-Host 'Follow-ups:'
Write-Host '  - Verify builds: dotnet build each mod solution (paths are unchanged).'
Write-Host '  - Paste the updated workshop descriptions into Steam (links now point at rw.eprime).'
Write-Host '  - Make rw.eprime public BEFORE updating the WorkRoles workshop page (AGPL source link).'
Write-Host '  - Archive github.com/mnmr/rw.workroles once the workshop page points at the monorepo.'
