[CmdletBinding()]
param([Parameter(Mandatory)][int]$X, [Parameter(Mandatory)][int]$Y, [Parameter(Mandatory)][string]$OutName, [int]$WaitMilliseconds = 750, [switch]$Right, [string]$RunId = $env:RIMWORLD_AUTOMATION_RUN_ID)
& (Join-Path $PSScriptRoot 'click.ps1') -X $X -Y $Y -WaitMilliseconds $WaitMilliseconds -Right:$Right -RunId $RunId
. (Join-Path $PSScriptRoot 'automation-common.ps1') -RunId $RunId
Save-SharedGameCapture $OutName | Out-Null
