[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunId)
. (Join-Path $PSScriptRoot 'run-common.ps1')
$path = Assert-RunPath $RunId
if (-not (Test-Path -LiteralPath $path)) { return }
$control = Open-RunLock (Join-Path $path 'control.lock')
$lease = $null
try {
    $record = Read-RunJson (Join-Path $path 'run.json')
    if (-not $record) { $record = Read-RunJson (Join-Path $path 'preparing.json') }
    if (-not $record -or $record.runId -cne $RunId -or $record.owner -cne (Get-AutomationOwner)) {
        throw 'Only the recorded owner may remove a managed run.'
    }
    $lease = Open-RunLock (Join-Path $path 'run.lock')
    $processes = @(Get-AllRimWorldProcessInfo | Where-Object { Test-RunCommandLine $_.CommandLine $path ('rimworld-shared-' + $RunId) })
    if ($processes.Count -ne 0) { throw 'Stop the run before removing its files.' }
    # The durable tombstone blocks a new launch after these locks are closed.
    Write-RunJson (Join-Path $path 'removing.json') @{utc=[DateTime]::UtcNow.ToString('o');owner=$record.owner}
} finally { if ($lease) { $lease.Dispose() }; $control.Dispose() }
Remove-RunTree $RunId
Write-Host "removed run $RunId"
