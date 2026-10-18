# Synchronizes only Workshop tags, using RimWorld's own Steamworks binding.
# SteamCMD does not set tags from About.xml (or from a VDF tags block).
# Requires the Steam desktop client to be running and logged in as the owner.
# Example: ./sync-workshop-tags.ps1 -ModPath Readouts/mod,WorkRoles/mod -WhatIf
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string[]]$ModPath,
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Validate the entire batch before making any remote changes. Match
# Verse.Steam.Workshop.SetWorkshopItemDataFrom: category + major.minor versions.
$updates = @(foreach ($path in $ModPath) {
    $aboutPath = Join-Path $path 'About\About.xml'
    [xml]$about = Get-Content -LiteralPath $aboutPath -Raw
    $idPath = Join-Path $path 'About\PublishedFileId.txt'
    $idText = (Get-Content -LiteralPath $idPath -Raw).Trim()
    [ulong]$itemId = 0
    if (-not [ulong]::TryParse($idText, [ref]$itemId) -or $itemId -eq 0) {
        throw "Invalid existing Workshop item ID in $idPath"
    }
    $tags = [Collections.Generic.List[string]]::new()
    $translation = $about.SelectSingleNode('/ModMetaData/translationMod')
    $tags.Add($(if ($translation -and [bool]::Parse($translation.InnerText)) { 'Translation' } else { 'Mod' }))
    $versions = $about.SelectNodes('/ModMetaData/supportedVersions/li')
    if ($versions.Count -eq 0) { throw "No supported versions in $aboutPath" }
    foreach ($node in $versions) {
        [version]$version = $null
        if (-not [version]::TryParse($node.InnerText.Trim(), [ref]$version) -or $version.Minor -lt 0) {
            throw "Invalid supported version '$($node.InnerText)' in $aboutPath"
        }
        $tag = '{0}.{1}' -f $version.Major, $version.Minor
        if (-not $tags.Contains($tag)) { $tags.Add($tag) }
    }
    [pscustomobject]@{ PublishedFileId = $itemId; Tags = $tags.ToArray() }
})

$approved = @(foreach ($update in $updates) {
    if ($PSCmdlet.ShouldProcess("Workshop item $($update.PublishedFileId)", "Set tags: $($update.Tags -join ', ')")) {
        $update
    }
})
if ($approved.Count -eq 0) {
    if ($WhatIfPreference) { $updates }
    return
}

$binding = Join-Path $RimWorldDir 'com.rlabrecque.steamworks.net.dll'
$nativePath = Join-Path $RimWorldDir 'steam_api64.dll'
# Windows resolves the binding's steam_api64 import to this loaded module.
$nativeHandle = [Runtime.InteropServices.NativeLibrary]::Load((Resolve-Path -LiteralPath $nativePath).Path)
$oldAppId = $env:SteamAppId
$oldGameId = $env:SteamGameId
$initialized = $false
try {
    $env:SteamAppId = '294100'
    $env:SteamGameId = '294100'
    [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $binding).Path) | Out-Null
    $initialized = [Steamworks.SteamAPI]::Init()
    if (-not $initialized) { throw 'Steam initialization failed. Start Steam and log in as the Workshop item owner.' }
    if ([Steamworks.SteamUtils]::GetAppID().m_AppId -ne 294100) { throw 'Steam initialized for the wrong app; no tags were submitted.' }
    foreach ($update in $approved) {
        $item = [Steamworks.PublishedFileId_t]::new($update.PublishedFileId)
        $handle = [Steamworks.SteamUGC]::StartItemUpdate([Steamworks.AppId_t]::new(294100), $item)
        if (-not [Steamworks.SteamUGC]::SetItemTags($handle, [string[]]$update.Tags)) {
            throw "Steam rejected tags for item $item"
        }
        # No content, title, description, preview, visibility or changenote is set.
        $call = [Steamworks.SteamUGC]::SubmitItemUpdate($handle, $null)
        if ($call.m_SteamAPICall -eq 0) { throw "Steam could not submit tags for item $item" }
        $timer = [Diagnostics.Stopwatch]::StartNew()
        $failed = $false
        while (-not [Steamworks.SteamUtils]::IsAPICallCompleted($call, [ref]$failed)) {
            if ($timer.Elapsed.TotalSeconds -ge 120) {
                throw "Timed out waiting for item $item. Check its live tags before retrying; the submission may still finish."
            }
            [Steamworks.SteamAPI]::RunCallbacks()
            Start-Sleep -Milliseconds 100
        }
        if ($failed) { throw "Steam transport failed while updating tags for item $item" }
        $size = [Runtime.InteropServices.Marshal]::SizeOf[Steamworks.SubmitItemUpdateResult_t]()
        $buffer = [Runtime.InteropServices.Marshal]::AllocHGlobal($size)
        try {
            $received = [Steamworks.SteamUtils]::GetAPICallResult($call, $buffer, $size, [Steamworks.SubmitItemUpdateResult_t]::k_iCallback, [ref]$failed)
            if (-not $received -or $failed) { throw "Could not read Steam's tag update result for item $item" }
            $result = [Runtime.InteropServices.Marshal]::PtrToStructure[Steamworks.SubmitItemUpdateResult_t]($buffer)
            if ($result.m_eResult -ne [Steamworks.EResult]::k_EResultOK) { throw "Steam tag update failed for item ${item}: $($result.m_eResult)" }
            if ($result.m_bUserNeedsToAcceptWorkshopLegalAgreement) { throw "Accept the Steam Workshop agreement for item $item before it can be published." }
        } finally {
            [Runtime.InteropServices.Marshal]::FreeHGlobal($buffer)
        }
        Write-Host "Workshop item ${item}: $($update.Tags -join ', ')"
        $update
    }
} finally {
    if ($initialized) { [Steamworks.SteamAPI]::Shutdown() }
    $env:SteamAppId = $oldAppId
    $env:SteamGameId = $oldGameId
    [Runtime.InteropServices.NativeLibrary]::Free($nativeHandle)
}
