[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutName, [switch]$WaitForRenderedMap, [switch]$KeepCursor, [string]$RunId = $env:RIMWORLD_AUTOMATION_RUN_ID)
. (Join-Path $PSScriptRoot 'automation-common.ps1') -RunId $RunId
if ($WaitForRenderedMap) {
    $deadline = (Get-Date).AddSeconds(30)
    do {
        $state = Invoke-SharedGameCommand @{command = 'status'}
        if ($state.ready) { break }
        Start-Sleep -Milliseconds 200
    } while ((Get-Date) -lt $deadline)
    if (-not $state.ready) { throw 'The shared game has no ready map.' }
}
if (-not $KeepCursor) { Invoke-SharedGameCommand @{command = 'hover'; x = 1919; y = 0} | Out-Null }
Save-SharedGameCapture $OutName | Out-Null
