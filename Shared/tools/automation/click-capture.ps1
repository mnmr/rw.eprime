[CmdletBinding()]
param([Parameter(Mandatory)][int]$X, [Parameter(Mandatory)][int]$Y, [Parameter(Mandatory)][string]$OutName, [int]$WaitMilliseconds = 750, [switch]$Right)
& (Join-Path $PSScriptRoot 'click.ps1') -X $X -Y $Y -WaitMilliseconds $WaitMilliseconds -Right:$Right
. (Join-Path $PSScriptRoot 'automation-common.ps1')
Save-SharedGameCapture $OutName | Out-Null
