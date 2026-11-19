[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'run-common.ps1')
. (Join-Path $PSScriptRoot 'game-sync-common.ps1')
$mutex = Enter-GamePreparation
try { Update-LocalGame17 }
finally { $mutex.ReleaseMutex(); $mutex.Dispose() }
