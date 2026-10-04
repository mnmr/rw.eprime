[CmdletBinding()] param([string]$RimWorldMods)
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\..\..\Shared\tools\deploy\deploy-mod.ps1" -ModRoot (Split-Path -Parent $PSScriptRoot) -DeployName QualityJobs -SyncImagesTo QualityJobs @PSBoundParameters
exit $LASTEXITCODE
