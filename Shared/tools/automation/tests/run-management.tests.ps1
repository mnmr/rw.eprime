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
    foreach ($index in 1..5) {
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
    foreach ($prefix in @('', '23:07:00 ERROR ')) {
        Check (Test-RunStartupFailure "$token`n${prefix}SaveableFromNode exception: broken save" $token) 'Both Unity and Waylith fatal startup logs must fail promptly.'
    }
    Check (-not (Test-RunStartupFailure "another-token`nSaveableFromNode exception: stale log" $token)) 'A foreign log must not decide this run startup.'
    Check (-not (Test-RunStartupFailure "$token`n23:07:00 INFO [SharedAutomation] ready for background commands" $token)) 'The readiness marker is not a failure.'
    $removedAnalyzer = "$token`nSaveableFromNode exception: System.ArgumentException: Can't load abstract class Verse.GameComponent`n[Ref 2FE8A16F]`n  at Verse.ScribeExtractor.SaveableFromNode[T] (System.Xml.XmlNode node) [0x00143] in <fixture>:0`nSubnode:`n<li Class=`"Analyzer.GameComponent_Analyzer`" />`n"
    Check (Test-RunStartupFailure $removedAnalyzer $token) 'Missing saved components remain failures by default.'
    Check (-not (Test-RunStartupFailure $removedAnalyzer $token -AllowRecoverableSaveErrors)) 'An explicit compatibility run may continue past caught save-node errors to prove actual runtime readiness.'
    foreach ($fatal in @('Exception while loading game: broken map', 'Crash!!!', 'Could not find game XML node.', 'Could not execute post-long-event action. Exception: failed')) {
        Check (Test-RunStartupFailure ($removedAnalyzer + $fatal + "`n") $token -AllowRecoverableSaveErrors) 'The opt-in must never hide crashes or fatal loading errors.'
    }
    Check (-not (Test-RunStartupFailure ($removedAnalyzer.Replace('Analyzer.GameComponent_Analyzer','Other.GameComponent')) $token -AllowRecoverableSaveErrors)) 'Compatibility tolerance is a recorded launch choice, not a growing list of mod classes.'
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
    # Interruption can occur after the snapshot helper and run-local metadata
    # are gone. The externally retained, owner-checked removal claim must allow
    # only that owner to finish, including the final directory-delete boundary.
    $interruptedPath = Assert-RunPath $runs[4]
    $interruptedRecord = Get-RunRecord $runs[4]
    $intentPath = Join-Path $script:AutomationRunsRoot ('.removing-' + $runs[4] + '.json')
    try {
        Write-RunJson $intentPath @{runId=$runs[4];owner=(Get-AutomationOwner);profile=$interruptedPath}
        Remove-Item -LiteralPath (Join-Path $interruptedPath 'run.json')
        $env:RIMWORLD_AUTOMATION_OWNER = 'different-task'
        Rejects { & (Join-Path $toolsRoot 'remove-run.ps1') -RunId $runs[4] } 'A durable cleanup claim must not grant another owner access.'
        $env:RIMWORLD_AUTOMATION_OWNER = 'run-management-regression'
        & (Join-Path $toolsRoot 'remove-run.ps1') -RunId $runs[4]
        Check (-not (Test-Path -LiteralPath $interruptedPath)) 'Interrupted cleanup must resume without deleted helper/metadata files.'
        Check (-not (Test-Path -LiteralPath $intentPath)) 'Completed cleanup must release its durable claim.'
        Write-RunJson $intentPath @{runId=$runs[4];owner=(Get-AutomationOwner);profile=$interruptedPath}
        & (Join-Path $toolsRoot 'remove-run.ps1') -RunId $runs[4]
        Check (-not (Test-Path -LiteralPath $intentPath)) 'Retry after directory removal must release a remaining owned claim.'
    } finally {
        $env:RIMWORLD_AUTOMATION_OWNER = 'run-management-regression'
        if (Test-Path -LiteralPath $interruptedPath) { Write-RunJson (Join-Path $interruptedPath 'run.json') $interruptedRecord }
        if (Test-Path -LiteralPath $intentPath) { Remove-Item -LiteralPath $intentPath }
    }
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
        $script:InstalledGameRoot = Join-Path $fixture 'waylith-installation'
        foreach ($folder in @('Mods','Data','Rendering')) {
            New-Item -ItemType Directory -Path (Join-Path $script:InstalledGameRoot $folder) -Force | Out-Null
        }
        foreach ($file in @('RimWorld.exe','RimWorld.dll','UnityStubs.dll')) {
            [IO.File]::WriteAllText((Join-Path $script:InstalledGameRoot $file), 'Waylith fixture; never launched')
        }
        New-Item -ItemType Directory -Path (Join-Path $worktree 'Shared\Automation\mod\1.7\Assemblies') -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $worktree 'Shared\Automation\mod\1.7\Assemblies\RimWorld.Automation.dll'), 'Waylith runtime fixture')
        $destination = Assert-RunPath $runs[2]
        $copies = New-RunGameSnapshot $destination $worktree
        Check ($copies.executable -ceq 'RimWorld.exe' -and $copies.gameVersion -ceq '1.7') 'Waylith snapshot must identify its actual executable and runtime version.'
        Check (Test-Path -LiteralPath (Join-Path $destination 'Game\Rendering')) 'Waylith renderer assets must be available in the isolated game.'
        Check (-not (Test-Path -LiteralPath (Join-Path $destination 'Game\RimWorldWin64_Data'))) 'Waylith must not require missing Unity asset folders.'
        $waylithRecord = Get-RunRecord $runs[2]
        $waylithRecord | Add-Member -NotePropertyName snapshots -NotePropertyValue $copies
        Write-RunJson (Join-Path $destination 'run.json') $waylithRecord
        Set-AutomationRun $runs[2]
        Check ($script:RimWorldExecutable -ieq (Join-Path $destination 'Game\RimWorld.exe')) 'Run selection must use its copied executable, regardless of the current installation.'
        $waylithRecord.snapshots.executable = '..\RimWorld.exe'
        Rejects { Get-RunExecutable $waylithRecord } 'Executable metadata cannot escape the isolated Game folder.'

        $waylithInstallation = $script:InstalledGameRoot
        $script:InstalledGameRoot = Join-Path $fixture 'linux-installation'
        foreach ($folder in @('Mods','Data','RimWorldLinux_Data')) {
            New-Item -ItemType Directory -Path (Join-Path $script:InstalledGameRoot $folder) -Force | Out-Null
        }
        [IO.File]::WriteAllText((Join-Path $script:InstalledGameRoot 'RimWorldLinux'), 'Linux fixture; never launched')
        $workshopRoot = $script:InstalledWorkshopRoot
        $script:InstalledWorkshopRoot = Join-Path $fixture 'workshop'
        foreach ($entry in @(@('local','mnmr.eprimereadouts'), @('external','regression.external'))) {
            $workshopAbout = Join-Path $script:InstalledWorkshopRoot ($entry[0] + '\About')
            New-Item -ItemType Directory -Path $workshopAbout -Force | Out-Null
            [IO.File]::WriteAllText((Join-Path $workshopAbout 'About.xml'), ('<ModMetaData><packageId>' + $entry[1] + '</packageId></ModMetaData>'))
        }
        $localAbout = Join-Path $worktree 'Readouts\mod\About'
        New-Item -ItemType Directory -Path $localAbout -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $localAbout 'About.xml'), '<ModMetaData><packageId>mnmr.eprimereadouts</packageId></ModMetaData>')
        $destination = Assert-RunPath $runs[3]
        New-Item -ItemType Directory -Path (Join-Path $destination 'Config') -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $destination 'Config\ModsConfig.xml'), '<ModsConfigData><activeMods><li>mnmr.eprimereadouts</li><li>regression.external</li></activeMods></ModsConfigData>')
        $copies = New-RunGameSnapshot $destination $worktree
        $copiedReadouts = @(Get-ChildItem -LiteralPath (Join-Path $destination 'Game\Mods') -Directory | Where-Object {
            $copiedAbout = Join-Path $_.FullName 'About\About.xml'
            (Test-Path -LiteralPath $copiedAbout) -and ([xml](Get-Content -LiteralPath $copiedAbout -Raw)).ModMetaData.packageId -ieq 'mnmr.eprimereadouts'
        })
        Check ($copiedReadouts.Count -eq 1 -and $copiedReadouts[0].Name -ceq 'EPrimeReadouts') 'Selected source mods must win over duplicate Workshop packages.'
        Check (Test-Path -LiteralPath (Join-Path $destination 'Game\Mods\Workshop-external\About\About.xml')) 'Active external Workshop dependencies must remain available.'
        $script:InstalledWorkshopRoot = $workshopRoot
        Check ($copies.executable -ceq 'RimWorldLinux' -and $copies.gameVersion -ceq '1.6') 'Linux snapshot must select Unity 1.6 and its exact executable.'
        Check (Test-Path -LiteralPath (Join-Path $destination 'Game\RimWorldLinux_Data')) 'Linux assets must be available in the isolated game.'
        Check (-not (Test-Path -LiteralPath (Join-Path $destination 'Game\RimWorldWin64_Data'))) 'Linux must not use the Windows Unity assets.'
        $linuxRecord = Get-RunRecord $runs[3]
        $linuxRecord | Add-Member -NotePropertyName snapshots -NotePropertyValue $copies
        Check ((Get-RunExecutable $linuxRecord) -ceq (Join-Path $destination 'Game\RimWorldLinux')) 'Linux selection must retain the copied executable.'
        $script:InstalledGameRoot = $waylithInstallation

        # Exercise profile preparation using the real baseline and fake save,
        # isolated even from the repository's canonical save destination.
        [IO.File]::WriteAllText((Join-Path $script:InstalledGameRoot 'Version.txt'), '1.7.4976 rev1043')
        [IO.File]::WriteAllText((Join-Path $about 'About.xml'), '<ModMetaData><packageId>eprime.sharedautomation</packageId></ModMetaData>')
        [xml]$baseline = Get-Content (Join-Path $toolsRoot 'profile-template\Config\ModsConfig.xml') -Raw
        $saveDirectory = Join-Path $fixture 'saves'
        New-Item -ItemType Directory -Path $saveDirectory | Out-Null
        [IO.File]::WriteAllText((Join-Path $saveDirectory 'Fisso-NAM-fixture.rws'), ('<savegame><meta><modIds>' + (($baseline.ModsConfigData.activeMods.li | ForEach-Object { '<li>' + $_ + '</li>' }) -join '') + '</modIds></meta></savegame>'))
        $repositoryRoot = $script:AutomationRepositoryRoot
        try {
            $script:AutomationRepositoryRoot = $fixture
            $prepared = Initialize-RunProfile $destination $saveDirectory 'Fisso-NAM*.rws' 'own-mods-waylith' $worktree
            [xml]$preparedConfig = Get-Content (Join-Path $destination 'Config\ModsConfig.xml') -Raw
            Check ($preparedConfig.ModsConfigData.version -ceq '1.7.4976 rev1043') 'The game must not reset the prepared mod list due to a stale template version.'
            Check (($preparedConfig.ModsConfigData.activeMods.li -join ',') -ceq ($prepared.activeMods -join ',')) 'Version adaptation must preserve the declared active-mod order.'
            $legacyBaselinePath = Join-Path $toolsRoot 'profile-template\Config\ModsConfig-1.6.xml'
            [xml]$legacyBaseline = Get-Content -LiteralPath $legacyBaselinePath -Raw
            [IO.File]::WriteAllText((Join-Path $saveDirectory 'Fisso-NAM-fixture.rws'), ('<savegame><meta><modIds>' + (($legacyBaseline.ModsConfigData.activeMods.li | ForEach-Object { '<li>' + $_ + '</li>' }) -join '') + '</modIds></meta></savegame>'))
            Rejects { Initialize-RunProfile $destination $saveDirectory 'Fisso-NAM*.rws' 'own-mods-waylith' $worktree } 'The ordinary baseline must still reject a differently ordered source save.'
            $legacy = Initialize-RunProfile $destination $saveDirectory 'Fisso-NAM*.rws' 'own-mods-waylith' $worktree $legacyBaselinePath
            Check (($legacy.activeMods -join ',') -ceq ($prepared.activeMods -join ',')) 'An explicit matching legacy baseline must layer the same clean mod set without weakening default verification.'
        } finally { $script:AutomationRepositoryRoot = $repositoryRoot }
    } finally { $script:InstalledGameRoot = $installedRoot }
    Write-Output 'PASS run selection, ownership, lease liveness, immutable snapshots, junction-safe removal, independent neighbors.'
} finally {
    $env:RIMWORLD_AUTOMATION_OWNER = 'run-management-regression'
    foreach ($id in $runs) { & (Join-Path $toolsRoot 'remove-run.ps1') -RunId $id }
    $env:RIMWORLD_AUTOMATION_OWNER = $savedOwner
    $env:RIMWORLD_AUTOMATION_RUN_ID = $savedSelection
}
