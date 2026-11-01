# Uploads the deployed Mods\<DeployName> folder to the Steam Workshop. Each
# mod's scripts/publish.ps1 calls this with its own names; Scripts/publish-all.cmd
# calls those. Content and tags use one desktop Steam session; SteamCMD would
# replace that session's server login.
# The change note is the CHANGELOG.md section for About.xml's modVersion, as
# BBCode. The plan (version, change note, tag/description/preview changes) is
# shown and must be approved with y/yes. A successful upload is recorded in the
# mod's RELEASES.md; an already-recorded version is skipped (-AllowRepublish).
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ModRoot,
    [Parameter(Mandatory)][string]$DeployName,
    # Overrides the change note generated from CHANGELOG.md.
    [string]$ChangeNote = "",
    [switch]$AllowRepublish,
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
. "$PSScriptRoot\publish-content.ps1"
. "$PSScriptRoot\publish-release.ps1"
$version = [string]([xml]$deployedAbout).ModMetaData.modVersion
if ([string]::IsNullOrWhiteSpace($version)) { throw 'About.xml has no modVersion; set the version being released.' }
$version = $version.Trim()
$released = Get-ReleaseRecord -ModRoot $repo -Version $version
if ($released -and -not $AllowRepublish) {
    Write-Host "$DeployName $version was already published ($($released.Published)); nothing to publish. Bump modVersion and add its changelog section to release changes."
    exit 0
}
Assert-WorkshopDeploymentContent -Source (Join-Path $repo 'mod') -Deployed $content
$tags = [string[]]@(Get-WorkshopTags -About ([xml]$deployedAbout) -Source (Join-Path $content 'About\About.xml'))
if (-not $ChangeNote) {
    $ChangeNote = ConvertTo-WorkshopChangeNote -Version $version -Entries (Get-ChangelogRelease -Path (Join-Path $repo 'CHANGELOG.md') -Version $version)
}

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
# No local record: the Workshop's latest change note shows whether this
# version is already live (published by hand or before RELEASES.md existed).
if (-not $firstPublish -and -not $released -and -not $AllowRepublish) {
    $latestNote = Get-WorkshopLatestChangeNote $publishedFileId
    if ($null -eq $latestNote) {
        Write-Warning "Could not read the Workshop changelog for item $publishedFileId; cannot tell whether $version is already live."
    } elseif (Test-ChangeNoteVersion $latestNote.Text $version) {
        Add-ReleaseRecord -ModRoot $repo -Line (Format-ReleaseRecordLine $version $latestNote.PublishedUtc $publishedFileId)
        Write-Host "$DeployName $version is already the latest Workshop update ($($latestNote.PublishedUtc.ToString('yyyy-MM-dd HH:mm')) UTC); recorded it in RELEASES.md, nothing to publish."
        exit 0
    }
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
# The listing as Steam currently has it, for the preview, tag and description
# comparisons shown before approval (null when unavailable).
$remoteTags = $null
$remoteDescription = $null
if (-not $initializeListing) {
    try {
        $details = Invoke-RestMethod -Method Post `
            -Uri "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/" `
            -Body @{ itemcount = "1"; "publishedfileids[0]" = $publishedFileId }
        $remote = $details.response.publishedfiledetails[0]
        function Get-RemoteField($Name) {
            if ($remote -is [Collections.IDictionary]) { return $remote[$Name] }
            $property = $remote.PSObject.Properties[$Name]
            if ($property) { return $property.Value } else { return $null }
        }
        $remoteTagList = Get-RemoteField 'tags'
        if ($null -ne $remoteTagList) { $remoteTags = [string[]]@($remoteTagList | ForEach-Object { if ($_ -is [string]) { $_ } else { $_.tag } }) }
        $remoteDescription = Get-RemoteField 'file_description'
        $previewUrl = $remote.preview_url
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

# Change notes are write-once on Steam: show the complete plan, then confirm.
$ChangeNote = $ChangeNote.Replace('"', "'")
$tagPlan = if ($null -eq $remoteTags) {
    if ($initializeListing) { "set to $($tags -join ', ')" } else { "set to $($tags -join ', ') (current Workshop tags unavailable)" }
} else {
    $added = @($tags | Where-Object { $remoteTags -notcontains $_ })
    $removed = @($remoteTags | Where-Object { $tags -notcontains $_ })
    if ($added.Count -eq 0 -and $removed.Count -eq 0) { "unchanged ($($tags -join ', '))" }
    else {
        $changes = (@($added | ForEach-Object { "+$_" }) + @($removed | ForEach-Object { "-$_" })) -join ' '
        "$changes (result: $($tags -join ', '))"
    }
}
$descriptionPlan = if ($null -eq $remoteDescription) { "set from workshop-description.bbcode ($descriptionBytes bytes)" }
    elseif ($remoteDescription.Replace("`r`n", "`n").TrimEnd() -ceq $description) { 'unchanged' }
    else { "updated from workshop-description.bbcode ($descriptionBytes bytes)" }
$recordLine = Format-ReleaseRecordLine $version ([DateTime]::UtcNow) $(if ($firstPublish) { '<assigned item id>' } else { $publishedFileId })
Write-Host ""
Write-Host "==== Publish $DeployName $version to the Steam Workshop ===="
Write-Host "Item:        $(if ($firstPublish) { 'new Workshop item (title: ' + $title + ')' } else { "$publishedFileId (https://steamcommunity.com/sharedfiles/filedetails/?id=$publishedFileId)" })"
if ($released) { Write-Host "Republish:   $version was already published ($($released.Published))" }
Write-Host "Content:     $content"
Write-Host "Tags:        $tagPlan"
Write-Host "Description: $descriptionPlan"
Write-Host "Preview:     $(if ($sendPreview) { 'upload Preview.png' } else { 'unchanged, not sent' })"
Write-Host "Record:      append to $(Join-Path $repo 'RELEASES.md'): $recordLine"
Write-Host "---- Change note (published immediately, can never be edited) ----"
if ($ChangeNote) { Write-Host $ChangeNote } else { Write-Host "(none: the changelog entry will be blank)" }
Write-Host "-------------------------------------------------------------------"
$answer = Read-Host "Publish $DeployName $version now? Type y or yes to confirm"
if ($answer -notin @('y', 'yes')) {
    Write-Host "Aborted, nothing uploaded."
    exit 2
}

$contentPath = (Resolve-Path -LiteralPath $content).Path
$published = Invoke-SteamWorkshopProcess -RimWorldDir (Split-Path -Parent $RimWorldMods) -Parameters @{
    itemId = $itemId; firstPublish = $firstPublish; initializeListing = $initializeListing
    pendingFirstPublishPath = $pendingFirstPublishPath; idFile = $idFile; repo = $repo
    contentPath = $contentPath; description = $description; tags = $tags; title = $title
    sendPreview = $sendPreview; previewFile = $previewFile; content = $content; ChangeNote = $ChangeNote
} -Action {
    param($itemId, $firstPublish, $initializeListing, $pendingFirstPublishPath, $idFile, $repo,
          $contentPath, $description, [string[]]$tags, $title, $sendPreview, $previewFile, $content, $ChangeNote)
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

Add-ReleaseRecord -ModRoot $repo -Line (Format-ReleaseRecordLine $version ([DateTime]::UtcNow) ([string]$published))
Write-Host "Recorded $DeployName $version in $(Join-Path $repo 'RELEASES.md')."
if ($initializeListing) {
    Write-Host "First publish complete: item $published; Workshop tags synchronized."
} else {
    $previewMsg = if ($sendPreview) { "preview updated" } else { "preview unchanged, not sent" }
    Write-Host "Upload complete. Description updated ($descriptionBytes bytes); $previewMsg; title not modified; Workshop tags synchronized."
}
exit 0
