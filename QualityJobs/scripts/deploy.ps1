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
$destination = Join-Path $modsRoot "QualityJobs"

# images/ is the authoritative source for mod textures: sync any PNGs into
# mod/Textures/QualityJobs before mirroring, so updated art always ships.
$imagesSource = Join-Path $repo "images"
if (Test-Path -LiteralPath $imagesSource -PathType Container) {
    $texturesDest = Join-Path $source "Textures\QualityJobs"
    if (-not (Test-Path -LiteralPath $texturesDest -PathType Container)) {
        New-Item -ItemType Directory -Path $texturesDest -Force | Out-Null
    }
    Copy-Item -Path (Join-Path $imagesSource "*.png") -Destination $texturesDest -Force
}

robocopy $source $destination /MIR /XF PublishedFileId.txt *.pdb /R:2 /W:1 | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }

if (Test-Path -LiteralPath $destination -PathType Container) {
    Get-ChildItem -LiteralPath $destination -Filter "*.pdb" -File -Recurse | Remove-Item -Force
}

Write-Host "Deployed to $destination"
exit 0
