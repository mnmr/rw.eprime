[CmdletBinding()]
param(
    [string]$RimWorldMods = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repo "mod"
if (-not (Test-Path -LiteralPath $source -PathType Container)) {
    throw "Mod source directory does not exist: $source"
}
if (-not (Test-Path -LiteralPath $RimWorldMods -PathType Container)) {
    throw "RimWorld Mods directory does not exist: $RimWorldMods"
}

$modsRoot = (Resolve-Path -LiteralPath $RimWorldMods).Path
$destination = Join-Path $modsRoot "WorkRoles"

# Mirror removes stale mod files. Excluded files are neither copied nor purged,
# so Steam's destination-owned PublishedFileId.txt survives the deployment.
robocopy $source $destination /MIR /XF PublishedFileId.txt *.pdb /R:2 /W:1 | Out-Null
$robocopyExitCode = $LASTEXITCODE
if ($robocopyExitCode -ge 8) {
    throw "robocopy failed with exit code $robocopyExitCode"
}

# PDBs excluded from the mirror may already exist from an older deployment.
if (Test-Path -LiteralPath $destination -PathType Container) {
    Get-ChildItem -LiteralPath $destination -Filter "*.pdb" -File -Recurse |
        Remove-Item -Force
}

Write-Host "Deployed to $destination"
exit 0
