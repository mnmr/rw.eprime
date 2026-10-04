[CmdletBinding()] param([string]$ChangeNote, [string]$RimWorldMods, [string]$SteamCmd, [string]$Username)
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\..\..\Shared\tools\deploy\publish-mod.ps1" -ModRoot (Split-Path -Parent $PSScriptRoot) -DeployName EPrimeReadouts @PSBoundParameters
exit $LASTEXITCODE
