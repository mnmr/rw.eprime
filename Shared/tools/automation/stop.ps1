[CmdletBinding()]
param([int]$ForceAfterSeconds = 10)

. (Join-Path $PSScriptRoot 'automation-common.ps1')

$matches = @(Get-SharedRimWorldProcessInfo)
if ($matches.Count -eq 0) {
    Write-Host 'shared RimWorld process is not running'
    exit 0
}
if ($matches.Count -ne 1) {
    throw "refusing to stop $($matches.Count) processes for the shared profile"
}
$process = Get-Process -Id $matches[0].ProcessId
$processId = $process.Id
$process.CloseMainWindow() | Out-Null
if (-not $process.WaitForExit($ForceAfterSeconds * 1000)) {
    $confirmed = @(Get-SharedRimWorldProcessInfo)
    if ($confirmed.Count -ne 1 -or $confirmed[0].ProcessId -ne $processId) {
        throw "shared process identity changed while stopping pid $processId"
    }
    Stop-Process -Id $processId -Force
    $process.WaitForExit(10000) | Out-Null
}
Write-Host "stopped shared RimWorld pid=$processId"

