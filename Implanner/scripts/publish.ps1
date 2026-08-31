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
$content = Join-Path $RimWorldMods "Implanner"
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

# Preview: sent on first publish or when the deployed Preview.png differs from
# the image currently on the Workshop (fetched via the item's preview_url and
# compared by hash; Steam serves the uploaded bytes verbatim). If the current
# preview cannot be fetched, it is sent — re-sending an identical image is
# harmless.
$previewFile = Join-Path $content "About\Preview.png"
$repoPreview = Join-Path $repo "mod\About\Preview.png"
$previewHash = (Get-FileHash -LiteralPath $previewFile -Algorithm SHA256).Hash
if ($previewHash -ne (Get-FileHash -LiteralPath $repoPreview -Algorithm SHA256).Hash) {
    throw "Preview.png in the game's Mods folder ($content) differs from the repo's; the upload sends the deployed folder, so run scripts/deploy.ps1 first"
}
if ((Get-Item -LiteralPath $previewFile).Length -gt 1MB) {
    throw "Preview.png is $((Get-Item -LiteralPath $previewFile).Length) bytes; Steam caps workshop previews at 1MB"
}
$sendPreview = $firstPublish
if (-not $firstPublish) {
    try {
        $details = Invoke-RestMethod -Method Post `
            -Uri "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/" `
            -Body @{ itemcount = "1"; "publishedfileids[0]" = $publishedFileId }
        $previewUrl = $details.response.publishedfiledetails[0].preview_url
        if (-not $previewUrl) { throw "no preview_url in API response" }
        $remotePreview = Join-Path ([System.IO.Path]::GetTempPath()) "workshop-preview-$publishedFileId.png"
        Invoke-WebRequest -Uri $previewUrl -OutFile $remotePreview | Out-Null
        $sendPreview = (Get-FileHash -LiteralPath $remotePreview -Algorithm SHA256).Hash -ne $previewHash
        Remove-Item -LiteralPath $remotePreview
    }
    catch {
        Write-Warning "Could not fetch the current Workshop preview ($_); sending Preview.png"
        $sendPreview = $true
    }
}

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

# Omitted keys (title, changenote when none is given, previewfile when
# unchanged) are left untouched by Steam for updates. A first publish sets
# title + visibility so the new item is complete; updates never touch them
# (managed on the web page). The preview is sent whenever $sendPreview says
# it differs from the image currently on the Workshop.
$contentPath = (Resolve-Path -LiteralPath $content).Path
$noteLine = if ($ChangeNote) { "`n    `"changenote`"       `"$ChangeNote`"" } else { "" }
$previewLine = if ($sendPreview) {
    "`n    `"previewfile`"      `"$((Resolve-Path -LiteralPath $previewFile).Path)`""
} else { "" }
$firstLines = ""
if ($firstPublish) {
    $firstLines = "`n    `"title`"            `"EPrime's Implanner`"" +
                  "`n    `"visibility`"       `"0`""
}
$vdf = @"
"workshopitem"
{
    "appid"            "294100"
    "publishedfileid"  "$publishedFileId"
    "contentfolder"    "$contentPath"
    "description"      "$description"$firstLines$previewLine$noteLine
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
    $previewMsg = if ($sendPreview) { "preview updated" } else { "preview unchanged, not sent" }
    Write-Host "Upload complete. Description updated ($descriptionBytes bytes); $previewMsg; title not modified."
}
exit 0
