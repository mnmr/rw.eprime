[CmdletBinding()]
param([Parameter(Mandatory)][int]$X, [Parameter(Mandatory)][int]$Y, [Parameter(Mandatory)][int]$Notches, [int]$WaitMilliseconds = 750, [string]$RunId = $env:RIMWORLD_AUTOMATION_RUN_ID)
. (Join-Path $PSScriptRoot 'automation-common.ps1') -RunId $RunId
Invoke-SharedGameCommand @{ command = 'scroll'; x = $X; y = $Y; notches = $Notches } | Out-Null
Start-Sleep -Milliseconds $WaitMilliseconds
