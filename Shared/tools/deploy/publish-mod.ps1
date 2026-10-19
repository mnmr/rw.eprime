# Uploads the deployed Mods\<DeployName> folder to the Steam Workshop. Each
# mod's scripts/publish.ps1 calls this with its own names. Content and tags use
# one desktop Steam session; SteamCMD would replace that session's server login.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ModRoot,
    [Parameter(Mandatory)][string]$DeployName,
    [string]$ChangeNote = "",
    [string]$RimWorldMods = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods",
    # Retained for existing wrappers/callers; neither starts a second session.
    [string]$SteamCmd = "",
    [string]$Username = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# The Steamworks binding needs modern PowerShell. Reject unsupported
# hosts before any network access, prompts or irreversible Workshop upload.
if ($PSVersionTable.PSVersion -lt [version]'7.6') {
    throw "Workshop publishing requires PowerShell 7.6 or newer (current: $($PSVersionTable.PSVersion)). Use pwsh to run the publish script."
}
. "$PSScriptRoot\steam-workshop.ps1"
if ($SteamCmd -or $Username) {
    Write-Warning 'SteamCmd and Username are no longer used. Publishing uses the logged-in Steam desktop account.'
}

$repo = $ModRoot
$content = Join-Path $RimWorldMods $DeployName
if (-not (Test-Path -LiteralPath $content -PathType Container)) {
    throw "Deployed mod not found: $content (run scripts/deploy.ps1 first)"
}

# Guard against uploading a stale deployment.
$repoAbout = Get-Content -LiteralPath (Join-Path $repo "mod\About\About.xml") -Raw
$deployedAbout = Get-Content -LiteralPath (Join-Path $content "About\About.xml") -Raw
if ($repoAbout -ne $deployedAbout) {
    throw "About.xml in the game's Mods folder ($content) is older than the repo's; the upload sends the deployed folder, so run dotnet build + scripts/deploy.ps1 first"
}
$tags = [string[]]@(Get-WorkshopTags -About ([xml]$deployedAbout) -Source (Join-Path $content 'About\About.xml'))

# Existing item -> update; missing id file -> first publish (Steam assigns the
# id, which is written back to both the deployed and repo About folders).
$idFile = Join-Path $content "About\PublishedFileId.txt"
# Creation and content submission are separate Steam operations. The journal
# also recovers the ID if writing either PublishedFileId.txt was interrupted.
$pendingFirstPublishPath = Join-Path $repo 'temp\workshop-created-item.txt'
[ulong]$pendingItemId = 0
if (Test-Path -LiteralPath $pendingFirstPublishPath) {
    $pendingIdText = (Get-Content -LiteralPath $pendingFirstPublishPath -Raw).Trim()
    if (-not [ulong]::TryParse($pendingIdText, [ref]$pendingItemId) -or $pendingItemId -eq 0) { throw "Invalid Workshop recovery ID in $pendingFirstPublishPath" }
    foreach ($savedIdPath in @($idFile, (Join-Path $repo 'mod\About\PublishedFileId.txt'))) {
        if ((Test-Path -LiteralPath $savedIdPath) -and (Get-Content -LiteralPath $savedIdPath -Raw).Trim() -ne $pendingIdText) {
            throw "Workshop recovery ID $pendingItemId conflicts with $savedIdPath. Resolve the item identity before publishing."
        }
    }
}
$firstPublish = -not (Test-Path -LiteralPath $idFile -PathType Leaf) -and $pendingItemId -eq 0
$publishedFileId = if ($pendingItemId -ne 0) { $pendingItemId.ToString() } elseif ($firstPublish) { '0' } else { (Get-Content -LiteralPath $idFile -Raw).Trim() }
[ulong]$itemId = 0
if (-not $firstPublish -and (-not [ulong]::TryParse($publishedFileId, [ref]$itemId) -or $itemId -eq 0)) {
    throw "Invalid existing Workshop item ID in $idFile"
}
$initializeListing = $firstPublish -or $pendingItemId -ne 0
$title = ([xml]$deployedAbout).ModMetaData.name
if ($initializeListing -and [string]::IsNullOrWhiteSpace($title)) { throw 'A new Workshop item requires an About.xml name.' }

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
$sendPreview = $initializeListing
if (-not $initializeListing) {
    try {
        $details = Invoke-RestMethod -Method Post `
            -Uri "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/" `
            -Body @{ itemcount = "1"; "publishedfileids[0]" = $publishedFileId }
        $previewUrl = $details.response.publishedfiledetails[0].preview_url
        if (-not $previewUrl) { throw "no preview_url in API response" }
        $remotePreview = Join-Path ([System.IO.Path]::GetTempPath()) ("workshop-preview-" + [guid]::NewGuid().ToString('N') + '.png')
        try {
            Invoke-WebRequest -Uri $previewUrl -OutFile $remotePreview | Out-Null
            $sendPreview = (Get-FileHash -LiteralPath $remotePreview -Algorithm SHA256).Hash -ne $previewHash
        } finally {
            if (Test-Path -LiteralPath $remotePreview) { Remove-Item -LiteralPath $remotePreview }
        }
    }
    catch {
        Write-Warning "Could not fetch the current Workshop preview ($_); sending Preview.png"
        $sendPreview = $true
    }
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

# Change notes are write-once on Steam: collect deliberately, then confirm.
if (-not $ChangeNote) {
    Write-Host "Enter the change note (BBCode allowed)."
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
Write-Host ""
Write-Host "---- Change note (published immediately, can never be edited) ----"
if ($ChangeNote) { Write-Host $ChangeNote } else { Write-Host "(none: the changelog entry will be blank)" }
Write-Host "-------------------------------------------------------------------"
$answer = Read-Host "Upload to the Steam Workshop now? Type yes to confirm"
if ($answer -notin @('y', 'yes')) {
    Write-Host "Aborted, nothing uploaded."
    exit 1
}

$contentPath = (Resolve-Path -LiteralPath $content).Path
$published = Invoke-SteamWorkshopSession -RimWorldDir (Split-Path -Parent $RimWorldMods) -Action {
    $app = [Steamworks.AppId_t]::new(294100)
    $item = [Steamworks.PublishedFileId_t]::new($itemId)
    $needsAgreement = $false
    if ($firstPublish) {
        $call = [Steamworks.SteamUGC]::CreateItem($app, [Steamworks.EWorkshopFileType]::k_EWorkshopFileTypeCommunity)
        $created = Wait-WorkshopResult -Call $call -ResultType ([Steamworks.CreateItemResult_t]) -Operation 'item creation'
        $item = $created.m_nPublishedFileId
        if ($item.m_PublishedFileId -eq 0) { throw 'Steam created an item but returned no published file ID. Check the Workshop before retrying.' }
        Write-Host "Created Workshop item $item. Saving its ID before uploading content."
        New-Item -ItemType Directory -Path (Split-Path -Parent $pendingFirstPublishPath) -Force | Out-Null
        Set-Content -LiteralPath $pendingFirstPublishPath -Value $item.m_PublishedFileId -NoNewline
        $needsAgreement = $created.m_bUserNeedsToAcceptWorkshopLegalAgreement
    }
    if ($initializeListing) {
        Set-Content -LiteralPath $idFile -Value $item.m_PublishedFileId -NoNewline
        Set-Content -LiteralPath (Join-Path $repo 'mod\About\PublishedFileId.txt') -Value $item.m_PublishedFileId -NoNewline
        if ($needsAgreement) {
            throw "Accept the Steam Workshop agreement at https://steamcommunity.com/sharedfiles/filedetails/?id=$item then retry; the assigned ID has been saved."
        }
    }
    Assert-WorkshopConnected
    $handle = [Steamworks.SteamUGC]::StartItemUpdate($app, $item)
    if (-not [Steamworks.SteamUGC]::SetItemContent($handle, $contentPath)) { throw "Steam rejected content for item $item" }
    if (-not [Steamworks.SteamUGC]::SetItemDescription($handle, $description)) { throw "Steam rejected the description for item $item" }
    if (-not [Steamworks.SteamUGC]::SetItemTags($handle, $tags)) { throw "Steam rejected tags for item $item" }
    # Existing titles and visibility remain managed on the Workshop page.
    if ($initializeListing) {
        if (-not [Steamworks.SteamUGC]::SetItemTitle($handle, $title)) { throw "Steam rejected the title for item $item" }
        if (-not [Steamworks.SteamUGC]::SetItemVisibility($handle, [Steamworks.ERemoteStoragePublishedFileVisibility]::k_ERemoteStoragePublishedFileVisibilityPublic)) { throw "Steam rejected visibility for item $item" }
    }
    if ($sendPreview -and -not [Steamworks.SteamUGC]::SetItemPreview($handle, (Resolve-Path -LiteralPath $previewFile).Path)) { throw "Steam rejected the preview for item $item" }
    Write-Host "Uploading $content as item $item (Steam desktop session; tags: $($tags -join ', '))"
    $call = [Steamworks.SteamUGC]::SubmitItemUpdate($handle, $ChangeNote)
    $result = Wait-WorkshopResult -Call $call -ResultType ([Steamworks.SubmitItemUpdateResult_t]) -Operation "upload for item $item" -TimeoutSeconds 600
    if ($initializeListing -and (Test-Path -LiteralPath $pendingFirstPublishPath)) { Remove-Item -LiteralPath $pendingFirstPublishPath }
    if ($result.m_bUserNeedsToAcceptWorkshopLegalAgreement) { throw "Upload accepted. Accept the Steam Workshop agreement for item $item before it can be published; do not upload the same change note again." }
    $item.m_PublishedFileId
}

if ($initializeListing) {
    Write-Host "First publish complete: item $published; Workshop tags synchronized."
} else {
    $previewMsg = if ($sendPreview) { "preview updated" } else { "preview unchanged, not sent" }
    Write-Host "Upload complete. Description updated ($descriptionBytes bytes); $previewMsg; title not modified; Workshop tags synchronized."
}
exit 0
