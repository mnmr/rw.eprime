[CmdletBinding()]
param(
    [string]$SourceSaveDirectory =
        'C:\Users\morte\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves',
    [string]$SavePattern = 'Fisso-NAM*.rws'
)

. (Join-Path $PSScriptRoot 'automation-common.ps1')

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

$state = [ordered]@{
    sourceSave = $sourceSave.FullName
    sourceLastWriteTimeUtc = $sourceSave.LastWriteTimeUtc.ToString('o')
    sourceLength = $sourceSave.Length
    sha256 = $autostartHash
    activeMods = $saveModIds
}
$state | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath `
    (Join-Path $script:AutomationProfilePath 'profile-state.json') -Encoding utf8

Write-Host "shared profile refreshed from $($sourceSave.FullName)"
Write-Host "save SHA256 $autostartHash"
Write-Host "profile $script:AutomationProfilePath"
