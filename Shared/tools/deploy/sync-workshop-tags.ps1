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
. "$PSScriptRoot\steam-workshop.ps1"

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
    [pscustomobject]@{ PublishedFileId = $itemId; Tags = [string[]]@(Get-WorkshopTags -About $about -Source $aboutPath) }
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

Invoke-SteamWorkshopSession -RimWorldDir $RimWorldDir -Action {
    foreach ($update in $approved) {
        Assert-WorkshopConnected
        $item = [Steamworks.PublishedFileId_t]::new($update.PublishedFileId)
        $handle = [Steamworks.SteamUGC]::StartItemUpdate([Steamworks.AppId_t]::new(294100), $item)
        if (-not [Steamworks.SteamUGC]::SetItemTags($handle, [string[]]$update.Tags)) {
            throw "Steam rejected tags for item $item"
        }
        # No content, title, description, preview, visibility or changenote is set.
        $call = [Steamworks.SteamUGC]::SubmitItemUpdate($handle, $null)
        $result = Wait-WorkshopResult -Call $call -ResultType ([Steamworks.SubmitItemUpdateResult_t]) -Operation "tag update for item $item"
        if ($result.m_bUserNeedsToAcceptWorkshopLegalAgreement) { throw "Accept the Steam Workshop agreement for item $item before it can be published." }
        Write-Host "Workshop item ${item}: $($update.Tags -join ', ')"
        $update
    }
}
