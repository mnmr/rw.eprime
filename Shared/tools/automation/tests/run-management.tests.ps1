$ErrorActionPreference = 'Stop'
$toolsRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $toolsRoot 'run-common.ps1')
. (Join-Path $toolsRoot 'snapshot-common.ps1')
. (Join-Path $toolsRoot 'profile-common.ps1')
function Check([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Rejects([scriptblock]$Action, [string]$Message) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Check $rejected $Message
}
$savedOwner = $env:RIMWORLD_AUTOMATION_OWNER
$savedSelection = $env:RIMWORLD_AUTOMATION_RUN_ID
$env:RIMWORLD_AUTOMATION_OWNER = 'run-management-regression'
$env:RIMWORLD_AUTOMATION_RUN_ID = ''
$runs = @()
$fixture = Join-Path $script:AutomationRepositoryRoot ('Shared\Automation\temp\management-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
try {
    # Real manifests/files and OS locks, without launching a game. Ownership and
    # safe cleanup must be provable even before the runtime/pipe can exist.
    foreach ($index in 1..3) {
        $id = [Guid]::NewGuid().ToString('N')
        $path = Assert-RunPath $id
        New-Item -ItemType Directory -Path $path | Out-Null
        $runs += $id
        Write-RunJson (Join-Path $path 'run.json') @{runId=$id;owner=(Get-AutomationOwner);profile=$path;purpose="management fixture $index"}
    }
    . (Join-Path $toolsRoot 'automation-common.ps1')
    Rejects { Get-ExactlyOneSharedRimWorldProcessInfo } 'An omitted run must never select any game.'
    Set-AutomationRun $runs[0]
    Check ((Get-SelectedRun).runId -ceq $runs[0]) 'Explicit selection must target the intended run.'
    Set-AutomationRun $runs[2]
    Check ((Get-SelectedRun).runId -ceq $runs[2]) 'Selection must support more than two independent runs.'
    $path = Assert-RunPath $runs[0]
    $token = 'rimworld-shared-' + $runs[0]
    Check (Test-RunCommandLine "game -savedatafolder=`"$path`" -automationtoken=$token" $path $token) 'Exact process must match.'
    Check (-not (Test-RunCommandLine "game -savedatafolder=`"$path-other`" -automationtoken=$token" $path $token)) 'Profile prefixes must not match.'
    Check (-not (Test-RunCommandLine "game -savedatafolder=`"$path`" -automationtoken=$token-other" $path $token)) 'Token prefixes must not match.'
    Rejects { Assert-RunPath '../Shared' } 'Traversal must be rejected.'
    $env:RIMWORLD_AUTOMATION_OWNER = 'different-task'
    Rejects { Set-AutomationRun $runs[0] } 'A foreign run must reject control.'
    Rejects { & (Join-Path $toolsRoot 'remove-run.ps1') -RunId $runs[0] } 'A foreign run must reject removal.'
    $env:RIMWORLD_AUTOMATION_OWNER = 'run-management-regression'
    $lease = Open-RunLock (Join-Path $path 'run.lock')
    try {
        Rejects { & (Join-Path $toolsRoot 'remove-run.ps1') -RunId $runs[0] } 'An active OS lease must prevent removal.'
        $listed = @(& (Join-Path $toolsRoot 'list-runs.ps1')) | Where-Object runId -eq $runs[0]
        Check $listed.inUse 'Discovery must report a leased run as in use.'
    } finally { $lease.Dispose() }
    $source = Join-Path $fixture 'source'
    New-Item -ItemType Directory -Path $source | Out-Null
    [IO.File]::WriteAllText((Join-Path $source 'build.txt'), 'original build')
    $snapshot = Copy-RunSnapshot $source (Join-Path $path 'snapshot')
    [IO.File]::WriteAllText((Join-Path $source 'build.txt'), 'subsequent build')
    Check ((Get-Content (Join-Path $path 'snapshot\build.txt') -Raw) -ceq 'original build') 'Subsequent source builds must not mutate snapshots.'
    Check ($snapshot.files[0].sha256 -ceq (Get-FileHash (Join-Path $path 'snapshot\build.txt')).Hash) 'Manifest must describe copied bytes.'
    New-Item -ItemType Junction -Path (Join-Path $path 'shared-assets') -Target $source | Out-Null
    & (Join-Path $toolsRoot 'remove-run.ps1') -RunId $runs[0]
    Check (-not (Test-Path -LiteralPath $path)) 'Stopped owned run should be removed.'
    Check ((Get-Content (Join-Path $source 'build.txt') -Raw) -ceq 'subsequent build') 'Removal must not traverse asset junctions.'
    Check (Test-Path -LiteralPath (Assert-RunPath $runs[1])) 'Removing a run must preserve its neighbor.'
    $worktree = Join-Path $fixture 'worktree'
    $about = Join-Path $worktree 'Shared\Automation\mod\About'
    New-Item -ItemType Directory -Path $about -Force | Out-Null
    '<ModMetaData><packageId>Regression.OnlyInSource</packageId></ModMetaData>' | Set-Content (Join-Path $about 'About.xml')
    $modSet = Join-Path $fixture 'source-only.txt'
    @('after ludeon.rimworld','regression.onlyinsource') | Set-Content $modSet
    $resolved = @(Build-ModSetList -SaveModIds @('ludeon.rimworld') -SetPath $modSet -ModSourceRoot $worktree)
    Check ($resolved.Count -eq 2 -and $resolved[1] -ceq 'regression.onlyinsource') 'Source-only mods must resolve without deployment, ignoring package-ID case.'
    @('after ludeon.rimworld', 'remove Regression.Unwanted', 'regression.onlyinsource', 'remove regression.neverlisted') | Set-Content $modSet
    $resolved = @(Build-ModSetList -SaveModIds @('ludeon.rimworld', 'regression.unwanted', 'regression.kept') -SetPath $modSet -ModSourceRoot $worktree)
    Check (($resolved -join ',') -ceq 'ludeon.rimworld,regression.onlyinsource,regression.kept') 'A mod set must drop removed save mods (ignoring case and absent ids) and keep insertion order.'
    # Stall rules decide when a live run is killed; a game cannot be made to
    # stall on demand at each boundary, so the decision is checked on samples.
    . (Join-Path $toolsRoot 'guard-common.ps1')
    $t0 = [DateTime]::new(2026, 9, 27, 0, 0, 0, [DateTimeKind]::Utc)
    function Sample([double]$At, [int]$Ticks, [bool]$Paused = $true, [double]$InputAge = 0, [double]$CommandAge = 0, [bool]$Loading = $false, [double]$HeartbeatAge = 0.2, [double]$ActivityAge = 0, [switch]$Pickle) {
        $state = [pscustomobject]@{ ticks = $Ticks; paused = $Paused; forcePaused = $false; loading = $Loading; inputAgeSeconds = $InputAge
            commandAgeSeconds = $CommandAge; blockers = @('Verse.Dialog_Example'); focused = 'Verse.Dialog_Example' }
        Get-RunStallReason -Tracker $tracker -State $state -NowUtc $t0.AddSeconds($At) -HeartbeatUtc $t0.AddSeconds($At - $HeartbeatAge) -ActivityUtc $t0.AddSeconds($At - $ActivityAge) -Pickle:$Pickle
    }
    $tracker = New-RunStallTracker $t0
    Check ($null -eq (Sample 9.9 5 -InputAge 9.9 -CommandAge 1)) 'A paused game without input is not a stall before 10 s.'
    $reason = Sample 10 5 -InputAge 10 -CommandAge 1
    Check ($reason -match 'no progress' -and $reason -match 'paused=True' -and $reason -match 'Dialog_Example') 'A paused game without input for 10 s is a stall that names the pause and the blocking window.'
    $tracker = New-RunStallTracker $t0
    foreach ($second in 1..30) { Check ($null -eq (Sample $second (5 + $second) -Paused $false -InputAge $second -CommandAge $second -ActivityAge 0.5)) 'Advancing ticks with bridge polling is progress.' }
    $tracker = New-RunStallTracker $t0
    Check ($null -eq (Sample 25 5 -Loading $true -InputAge 25 -CommandAge 1)) 'Loading is progress.'
    $tracker = New-RunStallTracker $t0
    foreach ($second in 1..9) { Sample $second (5 + $second) -Paused $false -InputAge $second -CommandAge $second -ActivityAge $second | Out-Null }
    Check ((Sample 10 15 -Paused $false -InputAge 10 -CommandAge 10 -ActivityAge 10) -match 'idle') 'A driving script silent for 10 s is a stall even while the game ticks.'
    $tracker = New-RunStallTracker $t0
    foreach ($second in 1..9) { Sample $second (5 + $second) -Paused $false -InputAge $second -CommandAge $second -ActivityAge $second -Pickle | Out-Null }
    Check ($null -eq (Sample 10 15 -Paused $false -InputAge 10 -CommandAge 10 -ActivityAge 10 -Pickle)) 'Pickle drives itself; script silence is not a stall.'
    $tracker = New-RunStallTracker $t0
    Check ((Sample 3 5 -HeartbeatAge 5.5) -match 'not responding') 'A stale game heartbeat is a stall.'
    $tracker = New-RunStallTracker $t0
    foreach ($second in 1..59) { Sample $second (5 + $second) -Paused $false -ActivityAge 0.5 | Out-Null }
    Check ((Sample 60 65 -Paused $false -ActivityAge 0.5) -match 'cap') 'The hard cap stops a progressing run 60 s after ready.'
    # The bridge wire format is the GABP boundary; a live game cannot isolate a
    # framing error from a bridge fault, so the codec is checked on bytes.
    . (Join-Path $toolsRoot 'bridge-common.ps1')
    $wire = [pscustomobject]@{ Stream = [IO.MemoryStream]::new() }
    Send-GabpFrame $wire '{"v":"gabp/1","text":"Försök ✓"}'
    Send-GabpFrame $wire '{"n":2}'
    $bytes = $wire.Stream.ToArray()
    Check ([Text.Encoding]::ASCII.GetString($bytes, 0, 20) -ceq 'Content-Length: 36' + "`r`n") 'Content-Length must count UTF-8 bytes (36), not characters (32).'
    $wire.Stream.Position = 0
    Check ((Read-GabpFrame $wire) -ceq '{"v":"gabp/1","text":"Försök ✓"}') 'A framed UTF-8 body must round-trip exactly.'
    Check ((Read-GabpFrame $wire) -ceq '{"n":2}') 'Back-to-back frames must split on their declared lengths.'
    Rejects { Read-GabpFrame $wire } 'A closed stream must fail instead of returning a partial frame.'
    $installedRoot = $script:InstalledGameRoot
    try {
        $script:InstalledGameRoot = Join-Path $fixture 'game-installation'
        foreach ($folder in @('Mods','Data','RimWorldWin64_Data','MonoBleedingEdge')) {
            New-Item -ItemType Directory -Path (Join-Path $script:InstalledGameRoot $folder) -Force | Out-Null
        }
        [IO.File]::WriteAllText((Join-Path $script:InstalledGameRoot 'RimWorldWin64.exe'), 'fixture executable; never launched')
        foreach ($folder in @('Readouts','WorkRoles','QualityJobs','Implanner','Shared\Automation')) {
            New-Item -ItemType Directory -Path (Join-Path $worktree ($folder + '\mod\1.6\Assemblies')) -Force | Out-Null
        }
        [IO.File]::WriteAllText((Join-Path $worktree 'Shared\Automation\mod\1.6\Assemblies\RimWorld.Automation.dll'), 'worktree runtime fixture')
        $hostSource = Join-Path $worktree 'Shared\Automation\src\RimWorld.Automation.Host\bin\Release\net10.0-windows'
        New-Item -ItemType Directory -Path $hostSource -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $hostSource 'RimWorld.Automation.Host.exe'), 'worktree host fixture')
        $destination = Assert-RunPath $runs[1]
        $copies = New-RunGameSnapshot $destination $worktree
        Check ((Get-Content (Join-Path $destination 'Host\RimWorld.Automation.Host.exe') -Raw) -ceq 'worktree host fixture') 'Runtime and host must come from the same selected source root.'
    } finally { $script:InstalledGameRoot = $installedRoot }
    Write-Output 'PASS run selection, ownership, lease liveness, immutable snapshots, junction-safe removal, independent neighbors.'
} finally {
    $env:RIMWORLD_AUTOMATION_OWNER = 'run-management-regression'
    foreach ($id in $runs) { & (Join-Path $toolsRoot 'remove-run.ps1') -RunId $id }
    $env:RIMWORLD_AUTOMATION_OWNER = $savedOwner
    $env:RIMWORLD_AUTOMATION_RUN_ID = $savedSelection
}
