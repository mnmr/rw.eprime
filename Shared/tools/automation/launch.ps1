[CmdletBinding()]
param([string]$RunId = $env:RIMWORLD_AUTOMATION_RUN_ID, [int]$TimeoutMinutes = 12)
. (Join-Path $PSScriptRoot 'automation-common.ps1') -RunId $RunId
$record = Get-SelectedRun
$control = Open-RunLock (Join-Path $record.profile 'control.lock')
$hostProcess = $null
$runToken = 'rimworld-shared-' + $record.runId
try {
    if (Test-Path -LiteralPath (Join-Path $record.profile 'started.json')) { throw 'Runs are single-use; create a new run to restart.' }
    Assert-NoSharedRimWorldProcess
    # Verify copied mod/host bytes immediately before launch. Tests with custom
    # builds must prepare another run, rather than mutate an existing snapshot.
    foreach ($mod in $record.snapshots.mods.PSObject.Properties) {
        foreach ($file in $mod.Value.files) {
            $path = Join-Path $record.profile ('Game\Mods\' + $mod.Name + '\' + $file.path)
            if ((Get-FileHash -LiteralPath $path).Hash -cne $file.sha256) { throw "Run snapshot changed: $path" }
        }
    }
    foreach ($file in $record.snapshots.host.files) {
        $path = Join-Path $record.profile ('Host\' + $file.path)
        if ((Get-FileHash -LiteralPath $path).Hash -cne $file.sha256) { throw "Run host changed: $path" }
    }
    [xml]$prefs = Get-Content -LiteralPath (Join-Path $record.profile 'Config\Prefs.xml') -Raw
    if ($prefs.PrefsData.volumeMaster -ne '0') { throw 'Automation must start muted.' }
    $configFiles = @(Get-ChildItem -LiteralPath (Join-Path $record.profile 'Config') -File -Recurse | ForEach-Object {
        [ordered]@{path=[IO.Path]::GetRelativePath($record.profile,$_.FullName); sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
    })
    [xml]$mods = Get-Content -LiteralPath (Join-Path $record.profile 'Config\ModsConfig.xml') -Raw
    if (@($mods.ModsConfigData.activeMods.li) -notcontains 'eprime.sharedautomation') { throw 'The run must include its automation runtime.' }
    $saveHash = (Get-FileHash -LiteralPath (Join-Path $record.profile 'Saves\Autostart.rws')).Hash
    if ($saveHash -cne $record.configuration.sha256) { throw 'Run save changed; create a run with the intended source save.' }
    Write-RunJson (Join-Path $record.profile 'launch.json') ([ordered]@{
        utc=[DateTime]::UtcNow.ToString('o'); owner=$record.owner; configFiles=$configFiles; saveSha256=$saveHash
        activeMods=@($mods.ModsConfigData.activeMods.li); width=[int]$prefs.PrefsData.screenWidth
        height=[int]$prefs.PrefsData.screenHeight; uiScale=[double]$prefs.PrefsData.uiScale
    })
    Update-RunActivity 'launch'
    $hostExecutable = Join-Path $record.profile 'Host\RimWorld.Automation.Host.exe'
    $arguments = '"' + $script:RimWorldExecutable + '" "' + $script:AutomationProfilePath + '" ' + $runToken + ' "' + $script:RimWorldPlayerLog + '"'
    $hostProcess = Start-Process -FilePath $hostExecutable -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    do {
        if ($hostProcess.HasExited) { throw "Run host exited; inspect $script:RimWorldPlayerLog.host-error.txt" }
        $processes = @(Get-SharedRimWorldProcessInfo)
        if ($processes.Count -gt 1) { throw 'Multiple games claimed the same run.' }
        if ($processes.Count -eq 1) {
            $content = if (Test-Path -LiteralPath $script:RimWorldPlayerLog) { Read-TextFileWhileOpen $script:RimWorldPlayerLog } else { '' }
            if ($content.Contains($runToken) -and $content -match '(?m)^SaveableFromNode exception:|^Exception while loading|SharedAutomation.*Exception|^Crash!!!|Could not execute post-long-event action\. Exception:') {
                throw 'The run logged an automation or save-load failure; inspect its Player.log.'
            }
            if ($content.Contains($runToken) -and $content.Contains('[SharedAutomation] ready for background commands')) {
                $state = Invoke-SharedGameCommand @{command='status'}
                if ($state.ready) {
                    Write-RunJson (Join-Path $record.profile 'ready.json') $state
                    if ($state.audioVolume -ne 0) { throw 'Run audio listener is not muted.' }
                    # Prepatcher loads assemblies from bytes, so Assembly.Location
                    # can be empty. Validate the actual game's ModContentPack root.
                    $expectedRoot = Join-Path $record.profile 'Game\Mods\SharedAutomation'
                    if ([IO.Path]::GetFullPath($state.runtimeModRoot) -ine $expectedRoot) { throw "Game runtime mod root '$($state.runtimeModRoot)' differs from '$expectedRoot'." }
                    if ($state.width -ne [int]$prefs.PrefsData.screenWidth -or $state.height -ne [int]$prefs.PrefsData.screenHeight) { throw 'Game frame differs from run preferences.' }
                    Save-SharedGameCapture 'launch-ready.png' | Out-Null
                    Write-Host "run $($record.runId) ready in background pid=$($state.processId)"
                    return
                }
            }
        }
        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Run did not become ready before the deadline.'
} catch {
    # The host owns only this run's child tree. Its retained process handle
    # prevents a stale PID from terminating another session.
    if ($hostProcess -and -not $hostProcess.HasExited) { $hostProcess.Kill(); $hostProcess.WaitForExit(10000) | Out-Null }
    throw
} finally { if ($hostProcess) { $hostProcess.Dispose() }; $control.Dispose() }
