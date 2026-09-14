[CmdletBinding()]
param([switch]$Json, [switch]$Detailed)
. (Join-Path $PSScriptRoot 'run-common.ps1')
$processes = @(Get-AllRimWorldProcessInfo)
$results = @(
    if (Test-Path -LiteralPath $script:AutomationRunsRoot) {
        foreach ($directory in Get-ChildItem -LiteralPath $script:AutomationRunsRoot -Directory) {
            if ($directory.Name -cnotmatch '^[a-f0-9]{32}$') { continue }
            try {
                $path = Assert-RunPath $directory.Name
                $record = Read-RunJson (Join-Path $path 'run.json')
                if (-not $record) { $record = Read-RunJson (Join-Path $path 'preparing.json') }
                $heartbeat = Read-RunJson (Join-Path $path 'host.json')
                $activity = Read-RunJson (Join-Path $path 'activity.json')
                $launch = Read-RunJson (Join-Path $path 'launch.json')
                $games = @($processes | Where-Object { Test-RunCommandLine $_.CommandLine $path ('rimworld-shared-' + $directory.Name) })
                $locked = Test-RunLocked (Join-Path $path 'run.lock')
                $state = if ($games.Count -gt 0) { 'running' }
                    elseif ($locked -and $heartbeat) { 'starting-or-stopping' }
                    elseif ($locked) { 'preparing' }
                    elseif (Test-Path -LiteralPath (Join-Path $path 'removing.json')) { 'removing' }
                    elseif (Test-Path -LiteralPath (Join-Path $path 'preparation-failed.json')) { 'preparation-failed' }
                    elseif ($heartbeat.state -in 'stopped','failed') { $heartbeat.state }
                    elseif (Test-Path -LiteralPath (Join-Path $path 'started.json')) { 'exited-without-status' }
                    elseif (Test-Path -LiteralPath (Join-Path $path 'run.json')) { 'prepared' }
                    else { 'abandoned-preparation' }
                $item = [ordered]@{
                    runId=$directory.Name; owner=$record.owner; purpose=$record.purpose; state=$state
                    inUse=($locked -or $games.Count -gt 0); gameProcessIds=@($games | ForEach-Object { $_.ProcessId })
                    lastCommand=$activity.activity; lastCommandUtc=$activity.utc
                    heartbeatUtc=$heartbeat.heartbeatUtc; profile=$path
                    modSet=$record.configuration.modSet; sourceSave=$record.configuration.sourceSave
                    saveSha256=$record.configuration.sha256; modSourceRoot=$record.modSourceRoot
                    createdUtc=$record.createdUtc
                }
                if ($Detailed) { $item.configuration=$record.configuration; $item.snapshots=$record.snapshots; $item.launch=$launch; $item.host=$heartbeat }
                [pscustomobject]$item
            } catch {
                [pscustomobject]@{runId=$directory.Name;state='unreadable';inUse=$true;error=$_.ToString()}
            }
        }
    }
)
if ($Json) { ConvertTo-Json -InputObject $results -Depth 14 }
else { $results }
