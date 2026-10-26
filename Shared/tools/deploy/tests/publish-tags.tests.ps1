param([switch]$CheckUnsupportedRuntime)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$deployRoot = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('rimworld-publish-tags-' + [guid]::NewGuid().ToString('N'))
function Check([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }

New-Item -ItemType Directory -Path "$fixture\publisher", "$fixture\repo\mod\About", "$fixture\installed\Example\About" -Force | Out-Null
try {
    # Copy only for the legacy-host rejection check, which must happen before
    # loading any Steam helpers, contacting the network, or prompting.
    Copy-Item -LiteralPath "$deployRoot\publish-mod.ps1" -Destination "$fixture\publisher\publish-mod.ps1"
    $about = '<ModMetaData><name>Example</name><supportedVersions><li>1.6</li><li>1.7</li></supportedVersions></ModMetaData>'
    foreach ($aboutDir in @("$fixture\repo\mod\About", "$fixture\installed\Example\About")) {
        Set-Content -LiteralPath "$aboutDir\About.xml" -Value $about
        Set-Content -LiteralPath "$aboutDir\PublishedFileId.txt" -Value '1234567890'
        Set-Content -LiteralPath "$aboutDir\Preview.png" -Value 'preview fixture'
    }
    Set-Content -LiteralPath "$fixture\repo\workshop-description.bbcode" -Value 'Example description'
    $publishActions = [Collections.Generic.List[string]]::new()
    function Read-Host { $publishActions.Add('prompt'); 'yes' }
    function Invoke-RestMethod {
        $publishActions.Add('network')
        @{ response = @{ publishedfiledetails = @(@{ preview_url = 'https://example.invalid/preview.png' }) } }
    }
    function Invoke-WebRequest { param($Uri, $OutFile) Set-Content -LiteralPath $OutFile -Value 'preview fixture' }
    function Invoke-TestSteamCmd { $publishActions.Add('SteamCMD'); $global:LASTEXITCODE = 0 }
    $publishArgs = @{
        ModRoot = "$fixture\repo"; DeployName = 'Example'; RimWorldMods = "$fixture\installed"
        SteamCmd = 'Invoke-TestSteamCmd'; Username = 'fixture'; ChangeNote = 'Version support'
    }
    if ($CheckUnsupportedRuntime) {
        Check ($PSVersionTable.PSVersion.Major -eq 5) 'The compatibility regression must run in Windows PowerShell 5.1.'
        $failure = ''
        try { & "$fixture\publisher\publish-mod.ps1" @publishArgs } catch { $failure = $_.Exception.Message }
        Check ($publishActions.Count -eq 0) "Unsupported PowerShell must stop before network access, prompts or uploads. Observed: $($publishActions -join ', ')"
        Check ($failure -match 'PowerShell 7\.6' -and $failure -match 'pwsh') 'The version error must explain the required runtime and how to launch it.'
        Check (-not (Test-Path -LiteralPath "$fixture\repo\temp")) 'Rejected publishing must not prepare an upload.'
        Write-Host 'Unsupported PowerShell is rejected before publishing.'
        return
    }

    # Exercise the actual legacy host, so a passing modern-host run cannot hide
    # a late failure after a remote item has already been created or updated.
    & "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $PSCommandPath -CheckUnsupportedRuntime
    Check ($LASTEXITCODE -eq 0) 'Windows PowerShell compatibility regression failed.'

    & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File "$PSScriptRoot\publish-content.tests.ps1"
    Check ($LASTEXITCODE -eq 0) 'Publishing localization and assembly freshness regression failed.'

    & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File "$PSScriptRoot\steam-upload.tests.ps1"
    Check ($LASTEXITCODE -eq 0) 'Desktop Steam publishing regression failed.'

    # Dry-run the real tag synchronizer against metadata, without a Steam login.
    $preview = @(& "$deployRoot\sync-workshop-tags.ps1" -ModPath "$fixture\installed\Example" -WhatIf)
    Check (($preview[0].Tags -join ',') -ceq 'Mod,1.6,1.7') 'Both supported versions and the Mod category must be submitted.'
    Check ($preview[0].PublishedFileId -eq '1234567890') 'The existing listing must be updated, never a new item created.'
    Set-Content -LiteralPath "$fixture\installed\Example\About\About.xml" -Value '<ModMetaData><name>Translation</name><translationMod>true</translationMod><supportedVersions><li>1.7.1234</li><li>1.8</li><li>1.7</li></supportedVersions></ModMetaData>'
    $preview = @(& "$deployRoot\sync-workshop-tags.ps1" -ModPath "$fixture\installed\Example" -WhatIf)
    Check (($preview[0].Tags -join ',') -ceq 'Translation,1.7,1.8') 'Tags must follow metadata changes, normalize versions and match RimWorld translation categories.'
    Set-Content -LiteralPath "$fixture\installed\Example\About\About.xml" -Value '<ModMetaData><name>Broken</name><supportedVersions><li>not-a-version</li></supportedVersions></ModMetaData>'
    $rejected = $false
    try { & "$deployRoot\sync-workshop-tags.ps1" -ModPath "$fixture\installed\Example" -WhatIf | Out-Null } catch { $rejected = $true }
    Check $rejected 'Invalid metadata must fail before submitting a partial tag list.'
    Write-Host 'Workshop publishing regression checks passed.'
} finally {
    $resolvedFixture = [IO.Path]::GetFullPath($fixture)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolvedFixture.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
