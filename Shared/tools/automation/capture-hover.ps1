[CmdletBinding()]
param([Parameter(Mandatory)][int]$X, [Parameter(Mandatory)][int]$Y, [Parameter(Mandatory)][string]$OutName, [int]$WaitMilliseconds = 750)
. (Join-Path $PSScriptRoot 'automation-common.ps1')
Invoke-SharedGameCommand @{command = 'hover'; x = $X; y = $Y} | Out-Null
Start-Sleep -Milliseconds $WaitMilliseconds
Save-SharedGameCapture $OutName | Out-Null
