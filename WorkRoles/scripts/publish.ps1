[CmdletBinding()]
param(
    [string]$ChangeNote = "",
    [string]$RimWorldMods = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods",
    [string]$SteamCmd = "",
    [string]$Username = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$content = Join-Path $RimWorldMods "WorkRoles"
if (-not (Test-Path -LiteralPath $content -PathType Container)) {
    throw "Deployed mod not found: $content (run scripts/deploy.ps1 first)"
}

# Guard against uploading a stale deployment.
$repoAbout = Get-Content -LiteralPath (Join-Path $repo "mod\About\About.xml") -Raw
$deployedAbout = Get-Content -LiteralPath (Join-Path $content "About\About.xml") -Raw
if ($repoAbout -ne $deployedAbout) {
    throw "About.xml in the game's Mods folder ($content) is older than the repo's; the upload sends the deployed folder, so run dotnet build + scripts/deploy.ps1 first"
}

$idFile = Join-Path $content "About\PublishedFileId.txt"
if (-not (Test-Path -LiteralPath $idFile -PathType Leaf)) {
    throw "PublishedFileId.txt not found: $idFile"
}
$publishedFileId = (Get-Content -LiteralPath $idFile -Raw).Trim()

if (-not $SteamCmd) {
    $cmd = Get-Command steamcmd -ErrorAction SilentlyContinue
    if ($cmd) { $SteamCmd = $cmd.Source }
    elseif (Test-Path "C:\steamcmd\steamcmd.exe") { $SteamCmd = "C:\steamcmd\steamcmd.exe" }
    else { throw "steamcmd not found; install from https://developer.valvesoftware.com/wiki/SteamCMD or pass -SteamCmd" }
}

if (-not $Username) {
    $Username = (Get-ItemProperty "HKCU:\Software\Valve\Steam" -ErrorAction SilentlyContinue).AutoLoginUser
    if (-not $Username) { throw "Could not determine Steam username; pass -Username" }
}

$bbcodePath = Join-Path $repo "workshop-description.bbcode"
if (-not (Test-Path -LiteralPath $bbcodePath -PathType Leaf)) {
    throw "Description source not found: $bbcodePath"
}
$description = (Get-Content -LiteralPath $bbcodePath -Raw).Replace("`r`n", "`n").TrimEnd()
$descriptionBytes = [System.Text.Encoding]::UTF8.GetByteCount($description)
if ($descriptionBytes -gt 8000) {
    throw "workshop-description.bbcode is $descriptionBytes bytes; Steam caps descriptions at ~8000"
}

# steamcmd's VDF parser has escape sequences off: values are written raw,
# newlines are fine inside quoted values, but a double quote is unrepresentable.
if ($ChangeNote.Contains('"')) {
    throw "ChangeNote contains a double quote; steamcmd VDF cannot represent it"
}
if ($description.Contains('"')) {
    throw "workshop-description.bbcode contains a double quote; steamcmd VDF cannot represent it"
}

# Omitted keys (title, previewfile, and changenote when none is given) are left
# untouched by Steam: the title is managed on the web page only, and a note-less
# upload posts a blank changelog entry instead of publishing throwaway text.
$contentPath = (Resolve-Path -LiteralPath $content).Path
$noteLine = if ($ChangeNote) { "`n    `"changenote`"       `"$ChangeNote`"" } else { "" }
$vdf = @"
"workshopitem"
{
    "appid"            "294100"
    "publishedfileid"  "$publishedFileId"
    "contentfolder"    "$contentPath"
    "description"      "$description"$noteLine
}
"@

$tempDir = Join-Path $repo "temp"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
$vdfPath = Join-Path $tempDir "workshop_item.vdf"
# BOM-less: steamcmd's VDF parser rejects a UTF-8 BOM.
[System.IO.File]::WriteAllText($vdfPath, $vdf, [System.Text.UTF8Encoding]::new($false))

Write-Host "Uploading $content as item $publishedFileId (user: $Username)"
& $SteamCmd +login $Username +workshop_build_item $vdfPath +quit
if ($LASTEXITCODE -ne 0) {
    throw "steamcmd failed with exit code $LASTEXITCODE"
}
Write-Host "Upload complete. Description updated ($descriptionBytes bytes); title/preview not modified."
exit 0
