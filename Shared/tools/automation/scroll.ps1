[CmdletBinding()]
param([Parameter(Mandatory)][int]$X, [Parameter(Mandatory)][int]$Y, [Parameter(Mandatory)][int]$Notches, [int]$WaitMilliseconds = 750)
. (Join-Path $PSScriptRoot 'automation-common.ps1')
Invoke-SharedGameCommand @{ command = 'scroll'; x = $X; y = $Y; notches = $Notches } | Out-Null
Start-Sleep -Milliseconds $WaitMilliseconds
