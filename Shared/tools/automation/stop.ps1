[CmdletBinding()]
param([int]$ForceAfterSeconds = 10)
. (Join-Path $PSScriptRoot 'automation-common.ps1')
$matches = @(Get-SharedRimWorldProcessInfo)
if ($matches.Count -eq 0) { Write-Host 'Shared RimWorld process is not running'; exit 0 }
if ($matches.Count -ne 1) { throw "Refusing to stop $($matches.Count) shared-profile processes." }
$process = Get-Process -Id $matches[0].ProcessId -ErrorAction SilentlyContinue
if ($null -eq $process) { exit 0 }
$processId = $process.Id
try { Invoke-SharedGameCommand @{command = 'quit'} -ConnectTimeoutMilliseconds 500 | Out-Null }
catch { Write-Verbose "Runtime unavailable during shutdown: $_" }
if (-not $process.WaitForExit($ForceAfterSeconds * 1000)) {
    $confirmed = @(Get-SharedRimWorldProcessInfo)
    if ($confirmed.Count -ne 1 -or $confirmed[0].ProcessId -ne $processId) { throw 'Shared process identity changed during shutdown.' }
    Stop-Process -Id $processId -Force
    $process.WaitForExit(10000) | Out-Null
}
$process.Dispose()
Write-Host "stopped shared RimWorld pid=$processId"
