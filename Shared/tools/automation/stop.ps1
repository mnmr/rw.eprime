[CmdletBinding()]
param([string]$RunId = $env:RIMWORLD_AUTOMATION_RUN_ID, [int]$ForceAfterSeconds = 10)
. (Join-Path $PSScriptRoot 'automation-common.ps1') -RunId $RunId
$record = Get-SelectedRun
$control = Open-RunLock (Join-Path $record.profile 'control.lock')
try {
    $games = @(Get-SharedRimWorldProcessInfo)
    if ($games.Count -gt 1) { throw 'Multiple games claimed this run; refusing ambiguous shutdown.' }
    $hostState = Read-RunJson (Join-Path $record.profile 'host.json')
    $hostInfo = if ($hostState) { Get-CimInstance Win32_Process -Filter "ProcessId = $($hostState.hostProcessId)" } else { $null }
    $hostProcess = $null
    if ($hostInfo -and $hostInfo.ExecutablePath -ieq (Join-Path $record.profile 'Host\RimWorld.Automation.Host.exe') -and
        $hostInfo.CommandLine.Contains('rimworld-shared-' + $record.runId)) {
        $hostProcess = Get-Process -Id $hostInfo.ProcessId -ErrorAction SilentlyContinue
        if ($hostProcess) {
            $null = $hostProcess.Handle
            $confirmedHost = Get-CimInstance Win32_Process -Filter "ProcessId = $($hostProcess.Id)"
            if (-not $confirmedHost -or $confirmedHost.CreationDate -ne $hostInfo.CreationDate -or
                $confirmedHost.ExecutablePath -ine $hostInfo.ExecutablePath -or $confirmedHost.CommandLine -cne $hostInfo.CommandLine) {
                $hostProcess.Dispose()
                throw 'Host identity changed during shutdown.'
            }
        }
    }
    try {
        if ($games.Count -eq 1) {
            $process = Get-Process -Id $games[0].ProcessId -ErrorAction SilentlyContinue
            if ($process) {
                try {
                    $null = $process.Handle
                    # Reconfirm exact command line after acquiring the handle.
                    $confirmed = @(Get-SharedRimWorldProcessInfo)
                    if ($confirmed.Count -ne 1 -or $confirmed[0].ProcessId -ne $process.Id) { throw 'Run identity changed during shutdown.' }
                    try { Invoke-SharedGameCommand @{command='quit'} -ConnectTimeoutMilliseconds 500 | Out-Null }
                    catch { Write-Verbose "Runtime unavailable during shutdown: $_" }
                    if (-not $process.WaitForExit($ForceAfterSeconds * 1000)) { $process.Kill(); $process.WaitForExit(10000) | Out-Null }
                } finally { $process.Dispose() }
            }
        }
        if ($hostProcess -and -not $hostProcess.WaitForExit(10000)) { $hostProcess.Kill(); $hostProcess.WaitForExit(10000) | Out-Null }
        if (@(Get-SharedRimWorldProcessInfo).Count -ne 0 -or (Test-RunLocked (Join-Path $record.profile 'run.lock'))) { throw 'Run still owns live resources.' }
        Update-RunActivity 'stopped'
        Write-Host "stopped run $($record.runId)"
    } finally { if ($hostProcess) { $hostProcess.Dispose() } }
} finally { $control.Dispose() }
