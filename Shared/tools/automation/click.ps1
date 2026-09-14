[CmdletBinding()]
param([Parameter(Mandatory)][int]$X, [Parameter(Mandatory)][int]$Y, [int]$WaitMilliseconds = 750, [switch]$Right, [string]$RunId = $env:RIMWORLD_AUTOMATION_RUN_ID)
. (Join-Path $PSScriptRoot 'automation-common.ps1') -RunId $RunId
Invoke-SharedGameCommand @{ command = 'click'; x = $X; y = $Y; button = [int][bool]$Right } | Out-Null
Start-Sleep -Milliseconds $WaitMilliseconds
