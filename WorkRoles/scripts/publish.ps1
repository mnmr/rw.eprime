[CmdletBinding()] param([string]$ChangeNote, [switch]$AllowRepublish, [string]$ApproveVersion, [string]$RimWorldMods, [string]$SteamCmd, [string]$Username)
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\..\..\Shared\tools\deploy\publish-mod.ps1" -ModRoot (Split-Path -Parent $PSScriptRoot) -DeployName WorkRoles @PSBoundParameters
exit $LASTEXITCODE
