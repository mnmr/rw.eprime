[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunId)
. (Join-Path $PSScriptRoot 'run-common.ps1')
$path = Assert-RunPath $RunId
# This final owner record lives outside the tree being deleted. It is published
# only after Windows/native leases and exact process checks have established
# the tombstone; retries therefore need no potentially deleted snapshot helper.
$intentPath = Join-Path $script:AutomationRunsRoot ('.removing-' + $RunId + '.json')
$intent = Read-RunJson $intentPath
if ($intent -and ($intent.runId -cne $RunId -or $intent.profile -ine $path -or $intent.owner -cne (Get-AutomationOwner))) {
    throw 'Only the recorded owner may resume managed run removal.'
}
if (-not (Test-Path -LiteralPath $path)) {
    if ($intent) { Remove-Item -LiteralPath $intentPath }
    return
}
$control = Open-RunLock (Join-Path $path 'control.lock')
$lease = $null
try {
    $record = Read-RunJson (Join-Path $path 'run.json')
    if (-not $record) { $record = Read-RunJson (Join-Path $path 'preparing.json') }
    if (-not $record) { $record = $intent }
    if (-not $record -or $record.runId -cne $RunId -or $record.owner -cne (Get-AutomationOwner)) {
        throw 'Only the recorded owner may remove a managed run.'
    }
    $lease = Open-RunLock (Join-Path $path 'run.lock')
    $processes = @(Get-AllRimWorldProcessInfo | Where-Object { Test-RunCommandLine $_.CommandLine $path ('rimworld-shared-' + $RunId) })
    if ($processes.Count -ne 0) { throw 'Stop the run before removing its files.' }
    if (-not $intent -and $record.backend -ceq 'wsl') { Invoke-WslRunControl $record 'remove' | Out-Null }
    # The durable tombstone blocks a new launch after these locks are closed.
    $intent = @{utc=[DateTime]::UtcNow.ToString('o');owner=$record.owner;runId=$RunId;profile=$path}
    Write-RunJson $intentPath $intent
    Write-RunJson (Join-Path $path 'removing.json') $intent
} finally { if ($lease) { $lease.Dispose() }; $control.Dispose() }
Remove-RunTree $RunId
Remove-Item -LiteralPath $intentPath
Write-Host "removed run $RunId"
