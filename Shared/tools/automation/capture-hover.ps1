[CmdletBinding()]
param([Parameter(Mandatory)][int]$X, [Parameter(Mandatory)][int]$Y, [Parameter(Mandatory)][string]$OutName, [int]$WaitMilliseconds = 750, [string]$RunId = $env:RIMWORLD_AUTOMATION_RUN_ID)
. (Join-Path $PSScriptRoot 'automation-common.ps1') -RunId $RunId
Invoke-SharedGameCommand @{command = 'hover'; x = $X; y = $Y} | Out-Null
Start-Sleep -Milliseconds $WaitMilliseconds
Save-SharedGameCapture $OutName | Out-Null
