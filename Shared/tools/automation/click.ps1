[CmdletBinding()]
param([Parameter(Mandatory)][int]$X, [Parameter(Mandatory)][int]$Y, [int]$WaitMilliseconds = 750, [switch]$Right)
. (Join-Path $PSScriptRoot 'automation-common.ps1')
Invoke-SharedGameCommand @{ command = 'click'; x = $X; y = $Y; button = [int][bool]$Right } | Out-Null
Start-Sleep -Milliseconds $WaitMilliseconds
