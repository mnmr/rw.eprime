[CmdletBinding()]
param(
    [string]$SourceSaveDirectory =
        'C:\Users\morte\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves',
    [string]$SavePattern = 'Fisso-NAM*.rws',
    # Name of a mod set under modsets\<name>.txt: extra installed mods
    # spliced into the save's own ordered list at named anchors. The result
    # is an order-preserving superset, which the dev-mode autostart loader
    # accepts with a logged mismatch only (no dialog). Omit to restore the
    # exact save list.
    [string]$ModSet = ''
)

. (Join-Path $PSScriptRoot 'automation-common.ps1')

function Get-InstalledPackageIds {
    $ids = New-Object 'Collections.Generic.HashSet[string]' (
        [StringComparer]::OrdinalIgnoreCase)
    $roots = @(
        'C:\Program Files (x86)\Steam\steamapps\workshop\content\294100',
        'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods'
    )
    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
        Get-ChildItem -LiteralPath $root -Directory | ForEach-Object {
            $about = Join-Path $_.FullName 'About\About.xml'
            if (-not (Test-Path -LiteralPath $about -PathType Leaf)) { return }
            try {
                [xml]$meta = Get-Content -LiteralPath $about -Raw
                $id = [string]$meta.ModMetaData.packageId
                if ($id) { $ids.Add($id.Trim()) | Out-Null }
            }
            catch { }
        }
    }
    return $ids
}

# Builds the active mod list for a mod set: the save's ids in their saved
# order with each set group inserted at its anchor. Throws on an unknown
# anchor, a missing installed mod, or a line before the first anchor.
function Build-ModSetList {
    param(
        [Parameter(Mandatory)][string[]]$SaveModIds,
        [Parameter(Mandatory)][string]$SetPath
    )
    $installed = Get-InstalledPackageIds
    $result = New-Object 'Collections.Generic.List[string]'
    $result.AddRange([string[]]$SaveModIds)
    $insertAt = -1
    foreach ($raw in Get-Content -LiteralPath $SetPath) {
        $line = ($raw -split '#')[0].Trim()
        if (-not $line) { continue }
        if ($line -match '^(?i)(after|before)\s+(\S+)$') {
            $anchor = $Matches[2]
            $index = -1
            for ($i = 0; $i -lt $result.Count; $i++) {
                if ([string]::Equals($result[$i], $anchor,
                        [StringComparison]::OrdinalIgnoreCase)) { $index = $i; break }
            }
            if ($index -lt 0) { throw "mod set anchor '$anchor' is not in the save's mod list" }
            $insertAt = if ($Matches[1] -ieq 'after') { $index + 1 } else { $index }
            continue
        }
        if ($insertAt -lt 0) { throw "mod set line '$line' appears before any anchor" }
        if (-not $installed.Contains($line)) {
            throw "mod set package '$line' is not installed (workshop or Mods folder)"
        }
        $already = $false
        foreach ($existing in $result) {
            if ([string]::Equals($existing, $line, [StringComparison]::OrdinalIgnoreCase)) {
                $already = $true; break
            }
        }
        if ($already) { continue }
        # ModsConfig ids must be lower case: the game lower-cases the id it
        # is asked about (ModsConfig.IsActive) but not the stored list, so a
        # mixed-case entry activates the mod yet fails every other mod's
        # IfModActive load-folder condition that names it.
        $result.Insert($insertAt, $line.ToLowerInvariant())
        $insertAt++
    }
    return $result.ToArray()
}

function Copy-VerifiedFile {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )

    $destinationDirectory = Split-Path -Parent $Destination
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    $temporary = $Destination + '.tmp.' + [Guid]::NewGuid().ToString('N')
    try {
        Copy-Item -LiteralPath $Source -Destination $temporary
        $sourceHash = (Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash
        $temporaryHash = (Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash
        if ($sourceHash -ne $temporaryHash) {
            throw "copied file hash mismatch for $Destination"
        }
        Move-Item -LiteralPath $temporary -Destination $Destination -Force
        $destinationHash = (
            Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
        if ($sourceHash -ne $destinationHash) {
            throw "published file hash mismatch for $Destination"
        }
        return $sourceHash
    }
    finally {
        if (Test-Path -LiteralPath $temporary) {
            Remove-Item -LiteralPath $temporary -Force
        }
    }
}

function Get-SaveModIds {
    param([Parameter(Mandatory)][string]$SavePath)

    $settings = New-Object Xml.XmlReaderSettings
    $settings.IgnoreComments = $true
    $settings.IgnoreWhitespace = $true
    $reader = [Xml.XmlReader]::Create($SavePath, $settings)
    $ids = New-Object 'Collections.Generic.List[string]'
    try {
        while ($reader.Read()) {
            if ($reader.NodeType -eq [Xml.XmlNodeType]::Element -and
                    $reader.Name -eq 'modIds') {
                $depth = $reader.Depth
                while ($reader.Read()) {
                    if ($reader.NodeType -eq [Xml.XmlNodeType]::EndElement -and
                            $reader.Name -eq 'modIds' -and
                            $reader.Depth -eq $depth) {
                        return $ids.ToArray()
                    }
                    if ($reader.NodeType -eq [Xml.XmlNodeType]::Element -and
                            $reader.Name -eq 'li') {
                        $ids.Add($reader.ReadString())
                    }
                }
            }
        }
    }
    finally {
        $reader.Dispose()
    }
    throw "save contains no modIds list: $SavePath"
}

Assert-NoSharedRimWorldProcess
if (-not (Test-Path -LiteralPath $SourceSaveDirectory -PathType Container)) {
    throw "source save directory does not exist: $SourceSaveDirectory"
}
$sourceSave = Get-ChildItem -LiteralPath $SourceSaveDirectory -File |
    Where-Object { $_.Name -like $SavePattern } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if ($null -eq $sourceSave) {
    throw "no save matching '$SavePattern' exists in $SourceSaveDirectory"
}

$templateConfig = Join-Path $PSScriptRoot 'profile-template\Config'
$profileConfig = Join-Path $script:AutomationProfilePath 'Config'
$profileSaves = Join-Path $script:AutomationProfilePath 'Saves'
$profileCaptures = Join-Path $script:AutomationProfilePath 'Captures'
New-Item -ItemType Directory -Path $profileConfig, $profileSaves, `
    $profileCaptures -Force | Out-Null
Get-ChildItem -LiteralPath $templateConfig -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $profileConfig -Force
}

[xml]$prefs = Get-Content -LiteralPath (Join-Path $profileConfig 'Prefs.xml') -Raw
if ($prefs.PrefsData.adaptiveTrainingEnabled -ne 'False' -or
        $prefs.PrefsData.runInBackground -ne 'True' -or
        $prefs.PrefsData.pauseOnLoad -ne 'True' -or
        $prefs.PrefsData.screenWidth -ne '1920' -or
        $prefs.PrefsData.screenHeight -ne '1080' -or
        $prefs.PrefsData.uiScale -ne '1.25') {
    throw 'shared profile preferences do not match the deterministic automation baseline'
}

[xml]$modsConfig = Get-Content -LiteralPath `
    (Join-Path $profileConfig 'ModsConfig.xml') -Raw
$configuredModIds = @($modsConfig.ModsConfigData.activeMods.li | ForEach-Object {
    [string]$_
})
$saveModIds = @(Get-SaveModIds $sourceSave.FullName)
if ($configuredModIds.Count -ne $saveModIds.Count) {
    throw "mod-list count mismatch: config=$($configuredModIds.Count), save=$($saveModIds.Count)"
}
for ($index = 0; $index -lt $saveModIds.Count; $index++) {
    if ($configuredModIds[$index] -cne $saveModIds[$index]) {
        throw "mod-list mismatch at index ${index}: config='$($configuredModIds[$index])', save='$($saveModIds[$index])'"
    }
}

$canonicalSave = Join-Path $script:AutomationRepositoryRoot 'Saves\Fisso-NAM.rws'
$autostartSave = Join-Path $profileSaves 'Autostart.rws'
$canonicalHash = Copy-VerifiedFile $sourceSave.FullName $canonicalSave
$autostartHash = Copy-VerifiedFile $sourceSave.FullName $autostartSave
if ($canonicalHash -ne $autostartHash) {
    throw 'canonical and autostart save hashes differ'
}

# The template config (verified above to equal the save's list) is the
# baseline; a mod set rewrites only the profile's copy of ModsConfig.xml.
$activeMods = $saveModIds
if ($ModSet) {
    $setPath = Join-Path $PSScriptRoot ('modsets\' + $ModSet + '.txt')
    if (-not (Test-Path -LiteralPath $setPath -PathType Leaf)) {
        throw "mod set does not exist: $setPath"
    }
    $activeMods = @(Build-ModSetList -SaveModIds $saveModIds -SetPath $setPath)
    $profileModsConfigPath = Join-Path $profileConfig 'ModsConfig.xml'
    [xml]$profileModsConfig = Get-Content -LiteralPath $profileModsConfigPath -Raw
    $activeNode = $profileModsConfig.ModsConfigData.SelectSingleNode('activeMods')
    $activeNode.RemoveAll()
    foreach ($id in $activeMods) {
        $li = $profileModsConfig.CreateElement('li')
        $li.InnerText = $id
        $activeNode.AppendChild($li) | Out-Null
    }
    $profileModsConfig.Save($profileModsConfigPath)
    Write-Host "mod set '$ModSet' applied: $($activeMods.Count - $saveModIds.Count) extra mod(s)"
}

$state = [ordered]@{
    sourceSave = $sourceSave.FullName
    sourceLastWriteTimeUtc = $sourceSave.LastWriteTimeUtc.ToString('o')
    sourceLength = $sourceSave.Length
    sha256 = $autostartHash
    modSet = $ModSet
    activeMods = $activeMods
}
$state | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath `
    (Join-Path $script:AutomationProfilePath 'profile-state.json') -Encoding utf8

Write-Host "shared profile refreshed from $($sourceSave.FullName)"
Write-Host "save SHA256 $autostartHash"
Write-Host "profile $script:AutomationProfilePath"
