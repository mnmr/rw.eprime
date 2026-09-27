[CmdletBinding()]
# -GameArguments passes Pickle flags (-pickle-*) and switches the run to Pickle
# mode: start at the main menu (autorun waits for it and loads its own
# fixtures; the verified save stays under its own name), load unpaused (the
# fixture settle waits for game ticks it never advances), and return the host's
# final state when Pickle quits the game instead of failing the launch.
# At game-ready a detached run-guard.ps1 takes over: it fails the run fast on a
# stall (10 s without input/ticks, a silent driving script, a frozen game) and
# stops it -HardCapSeconds (at most 60, the owner's cap) after ready. Load time
# before readiness is bounded by -TimeoutMinutes.
param([string]$RunId = $env:RIMWORLD_AUTOMATION_RUN_ID, [int]$TimeoutMinutes = 12, [string[]]$GameArguments = @(), [ValidateRange(1, 60)][int]$HardCapSeconds = 60)
$pickle = $GameArguments.Count -gt 0
. (Join-Path $PSScriptRoot 'automation-common.ps1') -RunId $RunId
$record = Get-SelectedRun
$control = Open-RunLock (Join-Path $record.profile 'control.lock')
$hostProcess = $null
$runToken = 'rimworld-shared-' + $record.runId
try {
    if (Test-Path -LiteralPath (Join-Path $record.profile 'started.json')) { throw 'Runs are single-use; create a new run to restart.' }
    # Early copy of RunIdentity.IsAllowedGameArgument; the host enforces it.
    foreach ($argument in $GameArguments) { if ($argument -cnotmatch '^-pickle-[a-z-]+(=[^\s"]*)?\z') { throw "Unsupported game argument: $argument" } }
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
    $prefsPath = Join-Path $record.profile 'Config\Prefs.xml'
    [xml]$prefs = Get-Content -LiteralPath $prefsPath -Raw
    if ($prefs.PrefsData.volumeMaster -ne '0') { throw 'Automation must start muted.' }
    if ($pickle) { $prefs.PrefsData.pauseOnLoad = 'False'; $prefs.Save($prefsPath) }
    $configFiles = @(Get-ChildItem -LiteralPath (Join-Path $record.profile 'Config') -File -Recurse | ForEach-Object {
        [ordered]@{path=[IO.Path]::GetRelativePath($record.profile,$_.FullName); sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
    })
    [xml]$mods = Get-Content -LiteralPath (Join-Path $record.profile 'Config\ModsConfig.xml') -Raw
    if (@($mods.ModsConfigData.activeMods.li) -notcontains 'eprime.sharedautomation') { throw 'The run must include its automation runtime.' }
    $saveHash = (Get-FileHash -LiteralPath (Join-Path $record.profile 'Saves\Autostart.rws')).Hash
    if ($saveHash -cne $record.configuration.sha256) { throw 'Run save changed; create a run with the intended source save.' }
    if ($pickle) { Move-Item -LiteralPath (Join-Path $record.profile 'Saves\Autostart.rws') -Destination (Join-Path $record.profile ('Saves\' + [IO.Path]::GetFileName($record.configuration.sourceSave))) }
    # RimBridgeServer (automation-runtime mod set) listens on a per-run loopback
    # endpoint taken from the game's environment; bridge.ps1 reads bridge.json.
    $bridgePort = $null
    if (@($mods.ModsConfigData.activeMods.li) -contains 'brrainz.rimbridgeserver') {
        # ponytail: the probed port is free now, not reserved; bridge.ps1 fails loudly if the game loses the race.
        $probe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0); $probe.Start()
        $bridgePort = $probe.LocalEndpoint.Port; $probe.Stop()
        Write-RunJson (Join-Path $record.profile 'bridge.json') ([ordered]@{ port=$bridgePort; token=[Guid]::NewGuid().ToString('N') })
    }
    Write-RunJson (Join-Path $record.profile 'launch.json') ([ordered]@{
        utc=[DateTime]::UtcNow.ToString('o'); owner=$record.owner; configFiles=$configFiles; saveSha256=$saveHash
        activeMods=@($mods.ModsConfigData.activeMods.li); width=[int]$prefs.PrefsData.screenWidth
        height=[int]$prefs.PrefsData.screenHeight; uiScale=[double]$prefs.PrefsData.uiScale
        gameArguments=$GameArguments; bridgePort=$bridgePort; pickle=$pickle; hardCapSeconds=$HardCapSeconds
    })
    Update-RunActivity 'launch'
    $hostExecutable = Join-Path $record.profile 'Host\RimWorld.Automation.Host.exe'
    $arguments = '"' + $script:RimWorldExecutable + '" "' + $script:AutomationProfilePath + '" ' + $runToken + ' "' + $script:RimWorldPlayerLog + '"'
    foreach ($argument in $GameArguments) { $arguments += ' ' + $argument }
    # The host passes its environment to the game; set it only for this start.
    $bridgeEnvironment = @{ GABP_SERVER_PORT=$null; GABP_TOKEN=$null; GABS_GAME_ID=$null }
    if ($bridgePort) {
        $endpoint = Get-Content -LiteralPath (Join-Path $record.profile 'bridge.json') -Raw | ConvertFrom-Json
        $bridgeEnvironment = @{ GABP_SERVER_PORT=[string]$endpoint.port; GABP_TOKEN=$endpoint.token; GABS_GAME_ID='rimworld' }
    }
    $savedEnvironment = @{}
    foreach ($name in $bridgeEnvironment.Keys) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name); [Environment]::SetEnvironmentVariable($name, $bridgeEnvironment[$name]) }
    try { $hostProcess = Start-Process -FilePath $hostExecutable -ArgumentList $arguments -WindowStyle Hidden -PassThru }
    finally { foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) } }
    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    do {
        if ($hostProcess.HasExited) {
            $final = Join-Path $record.profile 'host.json'
            if ($pickle -and (Test-Path -LiteralPath $final)) { return (Get-Content -LiteralPath $final -Raw | ConvertFrom-Json) }
            throw "Run host exited; inspect $script:RimWorldPlayerLog.host-error.txt"
        }
        $processes = @(Get-SharedRimWorldProcessInfo)
        if ($processes.Count -gt 1) { throw 'Multiple games claimed the same run.' }
        if ($processes.Count -eq 1) {
            $content = if (Test-Path -LiteralPath $script:RimWorldPlayerLog) { Read-TextFileWhileOpen $script:RimWorldPlayerLog } else { '' }
            if ($content.Contains($runToken) -and $content -match '(?m)^SaveableFromNode exception:|^Exception while loading|SharedAutomation.*Exception|^Crash!!!|Could not execute post-long-event action\. Exception:|^Could not find (?:game|world) XML node\.|^Called InitSaving\(\) but current mode is') {
                throw 'The run logged an automation or save-load failure; inspect its Player.log.'
            }
            if ($content.Contains($runToken) -and $content.Contains('[SharedAutomation] ready for background commands')) {
                # Pickle may quit the game at any moment; its exit is handled at the loop top.
                try { $state = Invoke-SharedGameCommand @{command='status'} }
                catch { if ($pickle -and $hostProcess.WaitForExit(10000)) { continue }; throw }
                if ($state.ready) {
                    Write-RunJson (Join-Path $record.profile 'ready.json') $state
                    $guardArguments = @('-NoProfile', '-File', ('"' + (Join-Path $PSScriptRoot 'run-guard.ps1') + '"'), '-RunId', $record.runId, '-CapSeconds', $HardCapSeconds)
                    if ($pickle) { $guardArguments += '-Pickle' }
                    Start-Process -FilePath (Get-Process -Id $PID).Path -WindowStyle Hidden -ArgumentList $guardArguments | Out-Null
                    if ($state.audioVolume -ne 0) { throw 'Run audio listener is not muted.' }
                    # Prepatcher loads assemblies from bytes, so Assembly.Location
                    # can be empty. Validate the actual game's ModContentPack root.
                    $expectedRoot = Join-Path $record.profile 'Game\Mods\SharedAutomation'
                    if ([IO.Path]::GetFullPath($state.runtimeModRoot) -ine $expectedRoot) { throw "Game runtime mod root '$($state.runtimeModRoot)' differs from '$expectedRoot'." }
                    if ($state.width -ne [int]$prefs.PrefsData.screenWidth -or $state.height -ne [int]$prefs.PrefsData.screenHeight) { throw 'Game frame differs from run preferences.' }
                    if (-not $pickle) { Save-SharedGameCapture 'launch-ready.png' | Out-Null }
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
