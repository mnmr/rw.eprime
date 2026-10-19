param([string]$FixtureRoot)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$deployRoot = Split-Path -Parent $PSScriptRoot
if (-not $FixtureRoot) {
    $fixture = Join-Path ([IO.Path]::GetTempPath()) ('rimworld-steam-upload-' + [guid]::NewGuid().ToString('N'))
    try {
        # The child owns the loaded binding. Exit it before deleting its DLL.
        & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File $PSCommandPath -FixtureRoot $fixture
        if ($LASTEXITCODE -ne 0) { throw 'Desktop Steam publishing regression failed.' }
    } finally {
        $resolvedFixture = [IO.Path]::GetFullPath($fixture)
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
        if (-not $resolvedFixture.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test cleanup path.' }
        if (Test-Path -LiteralPath $resolvedFixture) { Remove-Item -LiteralPath $resolvedFixture -Recurse -Force }
    }
    return
}
$fixture = $FixtureRoot
function Check([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Failure([scriptblock]$Action) {
    try { & $Action | Out-Null } catch { return $_.Exception.Message }
    throw 'Expected the operation to fail.'
}
New-Item -ItemType Directory -Path "$fixture\game\Mods\Example\About", "$fixture\repo\mod\About" -Force | Out-Null
$savedAppId = $env:SteamAppId
$savedGameId = $env:SteamGameId
try {
    $binding = "$fixture\game\com.rlabrecque.steamworks.net.dll"
    Add-Type -Path "$PSScriptRoot\SteamworksStub.cs" -OutputAssembly $binding
    [Reflection.Assembly]::LoadFrom($binding) | Out-Null
    # The real loader still runs, but this harmless OS DLL has no Steam entrypoints.
    Copy-Item -LiteralPath "$env:SystemRoot\System32\version.dll" -Destination "$fixture\game\steam_api64.dll"
    $about = '<ModMetaData><name>Example</name><supportedVersions><li>1.6</li><li>1.7</li></supportedVersions></ModMetaData>'
    foreach ($dir in @("$fixture\repo\mod\About", "$fixture\game\Mods\Example\About")) {
        Set-Content -LiteralPath "$dir\About.xml" -Value $about
        Set-Content -LiteralPath "$dir\PublishedFileId.txt" -Value '1234567890'
        Set-Content -LiteralPath "$dir\Preview.png" -Value 'preview fixture'
    }
    Set-Content -LiteralPath "$fixture\repo\workshop-description.bbcode" -Value 'Example description'
    $syncArgs = @{ ModPath = "$fixture\game\Mods\Example"; RimWorldDir = "$fixture\game" }
    $publishArgs = @{
        ModRoot = "$fixture\repo"; DeployName = 'Example'; RimWorldMods = "$fixture\game\Mods"
        SteamCmd = 'Invoke-TestSteamCmd'; Username = 'fixture'; ChangeNote = 'Version support'
    }
    function Read-Host { 'yes' }
    function Invoke-RestMethod { @{ response = @{ publishedfiledetails = @(@{ preview_url = 'https://example.invalid/preview.png' }) } } }
    function Invoke-WebRequest { param($Uri, $OutFile) Set-Content -LiteralPath $OutFile -Value 'preview fixture' }
    function Invoke-TestSteamCmd { throw 'Publishing must not replace the desktop session by logging in through SteamCMD.' }

    # Reproduces the real failure: Init succeeds while the server session is gone.
    # Removing the connection guard must attempt a submission and fail this test.
    [Steamworks.Fixture]::Reset()
    [Steamworks.Fixture]::Online = $false
    $env:SteamAppId = 'original-app'
    $env:SteamGameId = 'original-game'
    $failure = Failure { & "$deployRoot\sync-workshop-tags.ps1" @syncArgs }
    Check ([Steamworks.Fixture]::Starts -eq 0) 'Disconnected Steam must be rejected before starting a remote update; Init alone is insufficient.'
    Check ($failure -match 'Steam.*(offline|disconnected|not connected|not logged)' -and $failure -match '(reconnect|restart|sign in|log in)') "Connection error must explain recovery: $failure"
    Check ([Steamworks.Fixture]::Shutdowns -eq 1 -and $env:SteamAppId -ceq 'original-app' -and $env:SteamGameId -ceq 'original-game') 'Failure must shut Steam down and restore the caller environment.'

    [Steamworks.Fixture]::Reset()
    & "$deployRoot\publish-mod.ps1" @publishArgs
    $upload = [Steamworks.Fixture]::Published
    Check ($null -ne $upload -and $upload.Item -eq 1234567890) 'An existing listing must be published through the desktop API.'
    Check ($upload.Content -eq "$fixture\game\Mods\Example" -and $upload.Description -ceq 'Example description') 'Publish the deployed files and repository description.'
    Check (($upload.Tags -join ',') -ceq 'Mod,1.6,1.7' -and $upload.ChangeNote -ceq 'Version support') 'The content upload must include the tags and the confirmed change note.'
    Check ($null -eq $upload.Title -and -not $upload.Public -and $null -eq $upload.Preview) 'Updates must preserve title, visibility, and an unchanged preview.'
    Check ([Steamworks.Fixture]::Submissions.Count -eq 1 -and [Steamworks.Fixture]::Creates -eq 0) 'Existing content and tags must be submitted together, without creating another item.'
    # Compatibility arguments must not be required for the normal desktop flow.
    $publishArgs.Remove('SteamCmd')
    $publishArgs.Remove('Username')

    [Steamworks.Fixture]::Reset()
    function Invoke-WebRequest { param($Uri, $OutFile) Set-Content -LiteralPath $OutFile -Value 'older preview' }
    & "$deployRoot\publish-mod.ps1" @publishArgs
    Check ([Steamworks.Fixture]::Published.Preview -eq "$fixture\game\Mods\Example\About\Preview.png") 'A changed preview must be included.'

    [Steamworks.Fixture]::Reset()
    $quotedDescription = "A `"quoted`" description`nwith BBCode [b]formatting[/b]."
    Set-Content -LiteralPath "$fixture\repo\workshop-description.bbcode" -Value $quotedDescription
    $publishArgs.ChangeNote = "A `"quoted`" note`nwith a second line."
    & "$deployRoot\publish-mod.ps1" @publishArgs
    Check ([Steamworks.Fixture]::Published.Description -ceq $quotedDescription -and [Steamworks.Fixture]::Published.ChangeNote -ceq $publishArgs.ChangeNote) 'The API must preserve quotes and multiline text without VDF escaping.'

    [Steamworks.Fixture]::Reset()
    & "$deployRoot\sync-workshop-tags.ps1" @syncArgs | Out-Null
    $upload = [Steamworks.Fixture]::Published
    Check (($upload.Tags -join ',') -ceq 'Mod,1.6,1.7' -and $null -eq $upload.Content -and $null -eq $upload.Description -and $null -eq $upload.Title -and $null -eq $upload.Preview -and -not $upload.Public -and [string]::IsNullOrEmpty($upload.ChangeNote)) 'Standalone tag repair must only change tags.'

    [Steamworks.Fixture]::Reset()
    [Steamworks.Fixture]::Online = $false
    $failure = Failure { & "$deployRoot\publish-mod.ps1" @publishArgs }
    Check ([Steamworks.Fixture]::Starts -eq 0 -and [Steamworks.Fixture]::Creates -eq 0) 'Publishing offline must not create or update a listing.'

    [Steamworks.Fixture]::Reset()
    [Steamworks.Fixture]::RejectTags = $true
    $failure = Failure { & "$deployRoot\publish-mod.ps1" @publishArgs }
    Check ([Steamworks.Fixture]::Submissions.Count -eq 0 -and $failure -match 'tags') 'Rejected tags must stop the content submission.'

    [Steamworks.Fixture]::Reset()
    [Steamworks.Fixture]::Result = [Steamworks.EResult]::k_EResultFail
    $failure = Failure { & "$deployRoot\publish-mod.ps1" @publishArgs }
    Check ($failure -match 'k_EResultFail' -and $null -eq [Steamworks.Fixture]::Published -and [Steamworks.Fixture]::Shutdowns -eq 1) 'A failed Steam callback must be reported and the session released.'

    [Steamworks.Fixture]::Reset()
    Remove-Item -LiteralPath "$fixture\game\Mods\Example\About\PublishedFileId.txt", "$fixture\repo\mod\About\PublishedFileId.txt"
    & "$deployRoot\publish-mod.ps1" @publishArgs
    $upload = [Steamworks.Fixture]::Published
    Check ($upload.Item -eq 9876543210 -and $upload.Title -ceq 'Example' -and $upload.Public -and $null -ne $upload.Preview) 'First publication must complete the newly created listing.'
    Check (($upload.Tags -join ',') -ceq 'Mod,1.6,1.7' -and [Steamworks.Fixture]::Creates -eq 1) 'First publication must include tags on the newly assigned item.'
    foreach ($dir in @("$fixture\repo\mod\About", "$fixture\game\Mods\Example\About")) {
        Check ((Get-Content -LiteralPath "$dir\PublishedFileId.txt" -Raw).Trim() -eq '9876543210') 'Persist the assigned ID to both copies.'
    }

    [Steamworks.Fixture]::Reset()
    [Steamworks.Fixture]::NeedsAgreement = $true
    Remove-Item -LiteralPath "$fixture\game\Mods\Example\About\PublishedFileId.txt", "$fixture\repo\mod\About\PublishedFileId.txt"
    $failure = Failure { & "$deployRoot\publish-mod.ps1" @publishArgs }
    Check ($failure -match 'agreement' -and (Get-Content "$fixture\repo\mod\About\PublishedFileId.txt" -Raw).Trim() -eq '9876543210') 'Persist a created item before reporting its agreement requirement, so retry cannot create a duplicate.'
    Check ([Steamworks.Fixture]::Submissions.Count -eq 0) 'Do not continue publication before the Workshop agreement is accepted.'

    # The retry journal is written before either ID file. A partial disk write
    # must not cause CreateItem to run a second time for the same publication.
    Remove-Item -LiteralPath "$fixture\game\Mods\Example\About\PublishedFileId.txt"
    [Steamworks.Fixture]::Reset()
    & "$deployRoot\publish-mod.ps1" @publishArgs
    $upload = [Steamworks.Fixture]::Published
    Check ([Steamworks.Fixture]::Creates -eq 0 -and $upload.Item -eq 9876543210 -and $upload.Title -ceq 'Example' -and $upload.Public -and $null -ne $upload.Preview) 'Retry after accepting the agreement must finish the same listing, including first-publication fields.'
    Check ((Get-Content -LiteralPath "$fixture\game\Mods\Example\About\PublishedFileId.txt" -Raw).Trim() -eq '9876543210') 'Recovery must repair the missing deployed ID before clearing its journal.'
    Check (-not (Test-Path "$fixture\repo\temp\workshop-created-item.txt")) 'A completed first publication must release its retry marker.'

    [Steamworks.Fixture]::Reset()
    function Read-Host { 'no' }
    & "$deployRoot\publish-mod.ps1" @publishArgs
    Check ([Steamworks.Fixture]::Initializations -eq 0 -and [Steamworks.Fixture]::Submissions.Count -eq 0) 'Declining publication must not start a Steam session or submit anything.'
    Write-Host 'Desktop Steam publishing regression checks passed.'
} finally {
    $env:SteamAppId = $savedAppId
    $env:SteamGameId = $savedGameId
}
