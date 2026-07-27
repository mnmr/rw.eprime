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
$content = Join-Path $RimWorldMods "EPrimeReadouts"
if (-not (Test-Path -LiteralPath $content -PathType Container)) {
    throw "Deployed mod not found: $content (run scripts/deploy.ps1 first)"
}

# Guard against uploading a stale deployment.
$repoAbout = Get-Content -LiteralPath (Join-Path $repo "mod\About\About.xml") -Raw
$deployedAbout = Get-Content -LiteralPath (Join-Path $content "About\About.xml") -Raw
if ($repoAbout -ne $deployedAbout) {
    throw "About.xml in the game's Mods folder ($content) is older than the repo's; the upload sends the deployed folder, so run dotnet build + scripts/deploy.ps1 first"
}

# Existing item -> update; missing id file -> first publish (Steam assigns the
# id, which is written back to both the deployed and repo About folders).
$idFile = Join-Path $content "About\PublishedFileId.txt"
$firstPublish = -not (Test-Path -LiteralPath $idFile -PathType Leaf)
$publishedFileId = if ($firstPublish) { "0" } else { (Get-Content -LiteralPath $idFile -Raw).Trim() }

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
if ($description.Contains('"')) {
    throw "workshop-description.bbcode contains a double quote; steamcmd VDF cannot represent it"
}

# Change notes are write-once on Steam: collect deliberately, then confirm.
if (-not $ChangeNote) {
    Write-Host "Enter the change note (BBCode allowed, no double quotes)."
    Write-Host "Finish by pressing Enter three times in a row (two blank lines)."
    $lines = @()
    $blanks = 0
    while ($true) {
        $line = Read-Host
        if ($line -eq "") {
            $blanks++
            if ($blanks -ge 2) { break }
        }
        else {
            $blanks = 0
        }
        $lines += $line
    }
    $ChangeNote = ($lines -join "`n").TrimEnd()
}
if ($ChangeNote.Contains('"')) {
    throw "Change note contains a double quote; steamcmd VDF cannot represent it"
}

Write-Host ""
Write-Host "---- Change note (published immediately, can never be edited) ----"
if ($ChangeNote) { Write-Host $ChangeNote } else { Write-Host "(none: the changelog entry will be blank)" }
Write-Host "-------------------------------------------------------------------"
$answer = Read-Host "Upload to the Steam Workshop now? Type yes to confirm"
if ($answer -notin @('y', 'yes')) {
    Write-Host "Aborted, nothing uploaded."
    exit 1
}

# Omitted keys (title, and changenote when none is given) are left untouched by
# Steam for updates. A first publish sets title + preview so the new item is
# complete; updates never touch them (managed on the web page).
$contentPath = (Resolve-Path -LiteralPath $content).Path
$noteLine = if ($ChangeNote) { "`n    `"changenote`"       `"$ChangeNote`"" } else { "" }
$firstLines = ""
if ($firstPublish) {
    $previewPath = (Resolve-Path -LiteralPath (Join-Path $content "About\Preview.png")).Path
    $firstLines = "`n    `"title`"            `"EPrime's Readouts`"" +
                  "`n    `"previewfile`"      `"$previewPath`"" +
                  "`n    `"visibility`"       `"0`""
}
$vdf = @"
"workshopitem"
{
    "appid"            "294100"
    "publishedfileid"  "$publishedFileId"
    "contentfolder"    "$contentPath"
    "description"      "$description"$firstLines$noteLine
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

if ($firstPublish) {
    # steamcmd rewrites the vdf with the assigned publishedfileid.
    $assigned = ([regex]::Match((Get-Content -LiteralPath $vdfPath -Raw),
        '"publishedfileid"\s+"(\d+)"')).Groups[1].Value
    if (-not $assigned -or $assigned -eq "0") {
        throw "Upload reported success but no publishedfileid was assigned; check steamcmd output"
    }
    Set-Content -LiteralPath $idFile -Value $assigned -NoNewline
    Set-Content -LiteralPath (Join-Path $repo "mod\About\PublishedFileId.txt") -Value $assigned -NoNewline
    Write-Host "First publish complete: item $assigned (id written to deployed + repo About)."
} else {
    Write-Host "Upload complete. Description updated ($descriptionBytes bytes); title/preview not modified."
}
exit 0
