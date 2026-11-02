# Version guard regression (owner, 2026-11-01): publishing stops before any
# network access or prompt unless About.xml's modVersion is exactly the next
# patch after the last RELEASES.md row, or a republish of that row;
# -ApproveVersion <version> is the manual override.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$deployRoot = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('rimworld-publish-version-' + [guid]::NewGuid().ToString('N'))
function Check([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Write-FixtureFile([string]$Root, [string]$RelativePath, [string]$Value) {
    $path = Join-Path $Root $RelativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
    Set-Content -LiteralPath $path -Value $Value -NoNewline
}

. "$deployRoot\publish-release.ps1"
foreach ($case in @(
    @('1.2.6', '1.2.7', $true), @('1.2.9', '1.2.10', $true),
    @('1.2.6', '1.2.8', $false), @('1.2.6', '1.3.0', $false), @('1.2.6', '2.0.0', $false),
    @('1.2.6', '1.2.6', $false), @('1.2.6', '1.2.5', $false), @('1.2', '1.2.1', $false),
    @('1.2.6', '1.2.7.0', $false), @('1.2.4', '1.2.6', $false))) {
    Check ((Test-NextPatchVersion $case[0] $case[1]) -eq $case[2]) "Test-NextPatchVersion $($case[0]) -> $($case[1]) must be $($case[2])"
}
Check ((Get-NextPatchVersion '1.2.6') -ceq '1.2.7') 'Next patch after 1.2.6 is 1.2.7'

New-Item -ItemType Directory -Path "$fixture\publisher", "$fixture\repo\mod", "$fixture\installed\Example" -Force | Out-Null
try {
    Get-ChildItem -LiteralPath $deployRoot -Filter '*.ps1' -File | Copy-Item -Destination "$fixture\publisher"
    Add-Content -LiteralPath "$fixture\publisher\steam-workshop.ps1" -Value @'
function Invoke-SteamWorkshopProcess {
    param($RimWorldDir, $Action, $Parameters)
    $publishActions.Add('upload')
    return 1234567890
}
'@
    $publishActions = [Collections.Generic.List[string]]::new()
    function Read-Host { $publishActions.Add('prompt'); 'yes' }
    function Invoke-RestMethod {
        $publishActions.Add('network')
        @{ response = @{ publishedfiledetails = @(@{ preview_url = 'https://example.invalid/preview.png' }) } }
    }
    function Invoke-WebRequest {
        param($Uri, $OutFile)
        $publishActions.Add('download')
        Set-Content -LiteralPath $OutFile -Value 'preview fixture' -NoNewline
    }
    Write-FixtureFile "$fixture\repo" 'workshop-description.bbcode' 'Example description'

    function Set-Fixture([string]$Version, [string[]]$Released) {
        $about = "<ModMetaData><name>Example</name><modVersion>$Version</modVersion><supportedVersions><li>1.6</li><li>1.7</li></supportedVersions></ModMetaData>"
        foreach ($root in @("$fixture\repo\mod", "$fixture\installed\Example")) {
            Write-FixtureFile $root 'About\About.xml' $about
            Write-FixtureFile $root 'About\PublishedFileId.txt' '1234567890'
            Write-FixtureFile $root 'About\Preview.png' 'preview fixture'
        }
        $releases = Join-Path $fixture 'repo\RELEASES.md'
        if (Test-Path -LiteralPath $releases) { Remove-Item -LiteralPath $releases }
        foreach ($row in $Released) {
            Add-ReleaseRecord -ModRoot "$fixture\repo" -Line (Format-ReleaseRecordLine $row ([DateTime]::UtcNow) '1234567890')
        }
        $publishActions.Clear()
    }

    function Invoke-Publish([hashtable]$Extra) {
        $args = @{ ModRoot = "$fixture\repo"; DeployName = 'Example'; RimWorldMods = "$fixture\installed"; ChangeNote = 'Guard fixture' } + $Extra
        $global:LASTEXITCODE = 0
        $failure = ''
        try { & "$fixture\publisher\publish-mod.ps1" @args | Out-Null } catch { $failure = $_.Exception.Message }
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Failure = $failure; Actions = ($publishActions -join ',') }
    }
    # Stopped: exit 3 before any external action. Published: the existing
    # lookups (Workshop changelog, listing, preview) may run, then exactly one
    # prompt followed by the upload.
    function Assert-Stopped($Result, [string]$Case) {
        Check ($Result.ExitCode -eq 3 -and $Result.Actions -eq '' -and -not $Result.Failure) "$Case must stop with exit 3 before any external action; got exit $($Result.ExitCode), actions=[$($Result.Actions)], error=[$($Result.Failure)]"
    }
    function Assert-Published($Result, [string]$Case) {
        Check ($Result.Actions -match '(^|,)prompt,upload$' -and $Result.Actions -notmatch 'upload,' -and -not $Result.Failure) "$Case must reach the prompt and upload once; actions=[$($Result.Actions)], error=[$($Result.Failure)]"
    }

    # The 2026-11-01 incident: 1.2.4 released, About jumped to 1.2.6.
    Set-Fixture '1.2.6' @('1.2.4')
    Assert-Stopped (Invoke-Publish @{}) 'A skipped patch'

    Set-Fixture '1.2.6' @('1.2.4')
    Assert-Published (Invoke-Publish @{ ApproveVersion = '1.2.6' }) 'An approved skipped patch'

    Set-Fixture '1.2.6' @('1.2.4')
    $result = Invoke-Publish @{ ApproveVersion = '1.2.5' }
    Check ($result.Failure -match 'does not match' -and $result.Actions -eq '') 'Approving a different version than About.xml must be rejected before any external action.'

    Set-Fixture '1.2.5' @('1.2.4')
    Assert-Published (Invoke-Publish @{}) 'The next patch'

    Set-Fixture '1.3.0' @('1.2.6')
    Assert-Stopped (Invoke-Publish @{}) 'A minor step'

    Set-Fixture '1.2.6' @('1.2.4', '1.2.6')
    Assert-Published (Invoke-Publish @{ AllowRepublish = $true }) 'Republishing the last release'

    Set-Fixture '1.2.4' @('1.2.4', '1.2.6')
    Assert-Stopped (Invoke-Publish @{ AllowRepublish = $true }) 'Republishing an older release'

    Set-Fixture '1.2.4' @('1.2.4', '1.2.6')
    Assert-Published (Invoke-Publish @{ AllowRepublish = $true; ApproveVersion = '1.2.4' }) 'An approved older republish'

    Set-Fixture '1.0.0' @()
    Assert-Published (Invoke-Publish @{}) 'A publish without any RELEASES.md row'

    Write-Host 'Publish version guard regression checks passed.'
} finally {
    $resolvedFixture = [IO.Path]::GetFullPath($fixture)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolvedFixture.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
