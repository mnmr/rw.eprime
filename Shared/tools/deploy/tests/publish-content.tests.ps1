$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$deployRoot = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('rimworld-publish-content-' + [guid]::NewGuid().ToString('N'))
function Check([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Write-FixtureFile([string]$Root, [string]$RelativePath, [string]$Value) {
    $path = Join-Path $Root $RelativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
    Set-Content -LiteralPath $path -Value $Value -NoNewline
}

New-Item -ItemType Directory -Path "$fixture\publisher", "$fixture\repo\mod", "$fixture\installed\Example" -Force | Out-Null
try {
    Copy-Item -LiteralPath "$deployRoot\publish-mod.ps1" -Destination "$fixture\publisher\publish-mod.ps1"
    # Copy sibling helpers so this exercises the real publisher and preflight.
    Get-ChildItem -LiteralPath $deployRoot -Filter '*.ps1' -File | Where-Object Name -ne 'publish-mod.ps1' |
        Copy-Item -Destination "$fixture\publisher"
    # Keep metadata parsing real; replace only the external Steam process.
    Add-Content -LiteralPath "$fixture\publisher\steam-workshop.ps1" -Value @'
function Invoke-SteamWorkshopProcess {
    param($RimWorldDir, $Action, $Parameters)
    $publishActions.Add('upload')
    return 1234567890
}
'@
    $source = "$fixture\repo\mod"
    $deployed = "$fixture\installed\Example"
    $files = @{
        'About\About.xml' = '<ModMetaData><name>Example</name><supportedVersions><li>1.6</li><li>1.7</li></supportedVersions></ModMetaData>'
        'About\PublishedFileId.txt' = '1234567890'
        'About\Preview.png' = 'preview fixture'
        'FallbackLanguages\German\Keyed\Example.xml' = '<LanguageData><Example>Aktuell</Example></LanguageData>'
        'Languages\English\Keyed\Example.xml' = '<LanguageData><Example>Current</Example></LanguageData>'
        'Help\German\guide.md' = 'Current guide'
        '1.6\Assemblies\Example.dll' = 'Current 1.6 embedded catalog'
        '1.7\Assemblies\Example.dll' = 'Current 1.7 embedded catalog'
        'Assemblies\Dependency.dll' = 'Current root binary'
    }
    foreach ($entry in $files.GetEnumerator()) {
        Write-FixtureFile $source $entry.Key $entry.Value
        Write-FixtureFile $deployed $entry.Key $entry.Value
    }
    Write-FixtureFile "$fixture\repo" 'workshop-description.bbcode' 'Example description'
    # Archives outside the shipped mod must not become required deployed files.
    Write-FixtureFile "$fixture\repo" 'Shared\Translations\Retired\Example\mod\FallbackLanguages\Danish\Keyed\Example.xml' 'Archived translation'

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
    $publishArgs = @{
        ModRoot = "$fixture\repo"; DeployName = 'Example'; RimWorldMods = "$fixture\installed"
        ChangeNote = 'Localization retirement'
    }
    # Removing/weakening the preflight lets any of these deployments reach the
    # network, prompt and upload boundary despite unchanged About/Preview files.
    $cases = @(
        @{ Path = 'FallbackLanguages\Danish\Keyed\Example.xml'; Kind = 'extra'; Value = 'Retired translation' }
        @{ Path = 'Languages\Dutch\Keyed\Example.xml'; Kind = 'extra'; Value = 'Retired translation' }
        @{ Path = 'Help\Swedish\guide.md'; Kind = 'extra'; Value = 'Retired help' }
        @{ Path = 'FallbackLanguages\German\Keyed\Example.xml'; Kind = 'different'; Value = 'Stale active translation' }
        @{ Path = 'Languages\English\Keyed\Example.xml'; Kind = 'missing' }
        @{ Path = 'Help\German\guide.md'; Kind = 'different'; Value = 'Stale help' }
        @{ Path = '1.6\Assemblies\Example.dll'; Kind = 'different'; Value = 'Retired 1.6 embedded catalog' }
        @{ Path = '1.7\Assemblies\Example.dll'; Kind = 'different'; Value = 'Retired 1.7 embedded catalog' }
        @{ Path = 'Assemblies\Dependency.dll'; Kind = 'different'; Value = 'Retired root binary' }
        @{ Path = '1.7\Assemblies\Removed.dll'; Kind = 'extra'; Value = 'Old binary' }
        @{ Path = '1.6\Assemblies\Example.dll'; Kind = 'missing' }
    )
    $failures = [Collections.Generic.List[string]]::new()
    foreach ($case in $cases) {
        $publishActions.Clear()
        $path = Join-Path $deployed $case.Path
        if ($case.Kind -eq 'missing') { Remove-Item -LiteralPath $path }
        else { Write-FixtureFile $deployed $case.Path $case.Value }
        $failure = ''
        try { & "$fixture\publisher\publish-mod.ps1" @publishArgs | Out-Null }
        catch { $failure = $_.Exception.Message }
        if ($publishActions.Count -ne 0 -or -not $failure.Contains($case.Path) -or $failure -notmatch 'deploy') {
            $failures.Add("$($case.Kind) $($case.Path): expected rejection identifying the stale file before external actions; actions=[$($publishActions -join ', ')], error=[$failure]")
        }
        if ($case.Kind -eq 'extra') { Remove-Item -LiteralPath $path }
        else { Write-FixtureFile $deployed $case.Path $files[$case.Path] }
    }
    Check ($failures.Count -eq 0) ($failures -join "`n")

    $publishActions.Clear()
    & "$fixture\publisher\publish-mod.ps1" @publishArgs | Out-Null
    Check (($publishActions -join ',') -ceq 'network,download,prompt,upload') 'A matching deployment must pass the preflight and complete publishing through the mocked external boundary, ignoring archived source content.'
    Write-Host 'Publishing localization and assembly freshness regression checks passed.'
} finally {
    $resolvedFixture = [IO.Path]::GetFullPath($fixture)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolvedFixture.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
