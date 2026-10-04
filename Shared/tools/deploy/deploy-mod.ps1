# Mirrors <ModRoot>\mod into the game's Mods\<DeployName>. Each mod's
# scripts/deploy.ps1 calls this with its own names.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ModRoot,
    [Parameter(Mandatory)][string]$DeployName,
    [string]$RimWorldMods = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods",
    # Textures subfolder that receives images\*.png before mirroring, for a mod
    # whose images/ is the authoritative source of its textures.
    [string]$SyncImagesTo = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$source = Join-Path $ModRoot "mod"
if (-not (Test-Path -LiteralPath $source -PathType Container)) {
    throw "Mod source directory does not exist: $source"
}
if (-not (Test-Path -LiteralPath $RimWorldMods -PathType Container)) {
    throw "RimWorld Mods directory does not exist: $RimWorldMods"
}

$modsRoot = (Resolve-Path -LiteralPath $RimWorldMods).Path
$destination = Join-Path $modsRoot $DeployName

# A running game holds the mod dll open; the mirror would fail mid-copy. Only
# this installation's executable counts: managed automation runs launch their
# own copies and load their own mod copies.
$gameExecutable = Join-Path (Split-Path $modsRoot -Parent) "RimWorldWin64.exe"
if (@(Get-CimInstance Win32_Process -Filter "name = 'RimWorldWin64.exe'" | Where-Object { $_.ExecutablePath -ieq $gameExecutable }).Count -ne 0) {
    throw "RimWorld is running from $gameExecutable. Close the game and rerun the deploy."
}

# Sync any PNGs into mod/Textures/<SyncImagesTo> before mirroring, so updated
# art always ships.
if ($SyncImagesTo) {
    $imagesSource = Join-Path $ModRoot "images"
    if (Test-Path -LiteralPath $imagesSource -PathType Container) {
        $texturesDest = Join-Path $source "Textures\$SyncImagesTo"
        if (-not (Test-Path -LiteralPath $texturesDest -PathType Container)) {
            New-Item -ItemType Directory -Path $texturesDest -Force | Out-Null
        }
        Copy-Item -Path (Join-Path $imagesSource "*.png") -Destination $texturesDest -Force
    }
}

# Mirror removes stale mod files. Excluded files are neither copied nor purged,
# so Steam's destination-owned PublishedFileId.txt survives the deployment.
# /R:2 /W:1 fails fast on locked files (robocopy's default is a million
# 30-second retries).
robocopy $source $destination /MIR /XF PublishedFileId.txt *.pdb /R:2 /W:1 | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }

# PDBs excluded from the mirror may already exist from an older deployment.
if (Test-Path -LiteralPath $destination -PathType Container) {
    Get-ChildItem -LiteralPath $destination -Filter "*.pdb" -File -Recurse | Remove-Item -Force
}

Write-Host "Deployed to $destination"
exit 0
