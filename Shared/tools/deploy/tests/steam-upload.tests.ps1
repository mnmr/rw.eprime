param([string]$FixtureRoot)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$deployRoot = Split-Path -Parent $PSScriptRoot
if (-not $FixtureRoot) {
    $fixture = Join-Path ([IO.Path]::GetTempPath()) ('rimworld-steam-upload-' + [guid]::NewGuid().ToString('N'))
    try {
        # Keep fixture compilation and environment changes out of the test runner.
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
function Reset-Fixture([bool]$Online = $true, [bool]$NeedsAgreement = $false, [bool]$RejectTags = $false, [int]$Result = 1) {
    @{ Online = $Online; NeedsAgreement = $NeedsAgreement; RejectTags = $RejectTags; Result = $Result } |
        ConvertTo-Json | Set-Content -LiteralPath $env:WORKSHOP_TEST_INPUT
    if (Test-Path -LiteralPath $env:WORKSHOP_TEST_OUTPUT) { Remove-Item -LiteralPath $env:WORKSHOP_TEST_OUTPUT }
}
function Read-Fixture {
    $report = Get-Content -LiteralPath $env:WORKSHOP_TEST_OUTPUT -Raw | ConvertFrom-Json
    # Shutdown alone leaves Steam/overlay libraries resident. The Steam owner
    # must be a different process and must have exited before the script returns.
    Check ($report.ProcessId -ne $PID) 'The Steam session must run outside the calling PowerShell process.'
    Check ($null -eq (Get-Process -Id $report.ProcessId -ErrorAction SilentlyContinue)) 'The Steam worker must exit before returning, including on failure.'
    Check ($null -eq ('Steamworks.SteamAPI' -as [type])) 'The caller must not load the Steam binding.'
    Check ($env:SteamAppId -ceq 'original-app' -and $env:SteamGameId -ceq 'original-game') 'The caller Steam environment must stay unchanged.'
    $report
}
New-Item -ItemType Directory -Path "$fixture\game\Mods\Example\About", "$fixture\repo\mod\About" -Force | Out-Null
$savedTestInput = $env:WORKSHOP_TEST_INPUT
$savedTestOutput = $env:WORKSHOP_TEST_OUTPUT
$env:WORKSHOP_TEST_INPUT = "$fixture\input.json"
$env:WORKSHOP_TEST_OUTPUT = "$fixture\output.json"
$savedAppId = $env:SteamAppId
$savedGameId = $env:SteamGameId
try {
    $binding = "$fixture\game\com.rlabrecque.steamworks.net.dll"
    Add-Type -Path "$PSScriptRoot\SteamworksStub.cs" -OutputAssembly $binding

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
    Reset-Fixture -Online $false
    $env:SteamAppId = 'original-app'
    $env:SteamGameId = 'original-game'
    # A caller opting into native exit-code errors must still receive the
    # specific Steam recovery message, not a generic child-process failure.
    $PSNativeCommandUseErrorActionPreference = $true
    $failure = Failure { & "$deployRoot\sync-workshop-tags.ps1" @syncArgs }
    Check ((Read-Fixture).Starts -eq 0) 'Disconnected Steam must be rejected before starting a remote update; Init alone is insufficient.'
    Check ($failure -match 'Steam.*(offline|disconnected|not connected|not logged)' -and $failure -match '(reconnect|restart|sign in|log in)') "Connection error must explain recovery: $failure"
    Check ((Read-Fixture).Shutdowns -eq 1 -and $env:SteamAppId -ceq 'original-app' -and $env:SteamGameId -ceq 'original-game') 'Failure must shut Steam down and restore the caller environment.'

    Reset-Fixture
    & "$deployRoot\publish-mod.ps1" @publishArgs
    $upload = (Read-Fixture).Published
    Check ($null -ne $upload -and $upload.Item -eq 1234567890) 'An existing listing must be published through the desktop API.'
    Check ($upload.Content -eq "$fixture\game\Mods\Example" -and $upload.Description -ceq 'Example description') 'Publish the deployed files and repository description.'
    Check (($upload.Tags -join ',') -ceq 'Mod,1.6,1.7' -and $upload.ChangeNote -ceq 'Version support') 'The content upload must include the tags and the confirmed change note.'
    Check ($null -eq $upload.Title -and -not $upload.Public -and $null -eq $upload.Preview) 'Updates must preserve title, visibility, and an unchanged preview.'
    Check ((Read-Fixture).Submissions.Count -eq 1 -and (Read-Fixture).Creates -eq 0) 'Existing content and tags must be submitted together, without creating another item.'
    # Compatibility arguments must not be required for the normal desktop flow.
    $publishArgs.Remove('SteamCmd')
    $publishArgs.Remove('Username')

    Reset-Fixture
    function Invoke-WebRequest { param($Uri, $OutFile) Set-Content -LiteralPath $OutFile -Value 'older preview' }
    & "$deployRoot\publish-mod.ps1" @publishArgs
    Check ((Read-Fixture).Published.Preview -eq "$fixture\game\Mods\Example\About\Preview.png") 'A changed preview must be included.'

    Reset-Fixture
    $quotedDescription = "A `"quoted`" description`nwith BBCode [b]formatting[/b]."
    Set-Content -LiteralPath "$fixture\repo\workshop-description.bbcode" -Value $quotedDescription
    $publishArgs.ChangeNote = "A `"quoted`" note`nwith a second line and literal " + '$(1 + 1); å'
    $expectedNote = "A 'quoted' note`nwith a second line and literal " + '$(1 + 1); å'
    $preview = @(& "$deployRoot\publish-mod.ps1" @publishArgs 6>&1)
    Check ((Read-Fixture).Published.Description -ceq $quotedDescription -and (Read-Fixture).Published.ChangeNote -ceq $expectedNote) 'Change-note quotes must become apostrophes while description quotes, multiline text and literal expressions remain intact.'
    Check (@($preview | ForEach-Object { $_.ToString() }) -ccontains $expectedNote) 'The confirmation preview must show the normalized note that will be submitted.'

    Reset-Fixture
    $interactiveArgs = $publishArgs.Clone()
    $interactiveArgs.Remove('ChangeNote')
    $noteLines = [Collections.Generic.Queue[string]]::new()
    foreach ($line in @('First "quoted" line', 'Second line', '')) { $noteLines.Enqueue($line) }
    function Read-Host {
        param([string]$Prompt)
        if ($Prompt) {
            Check ($noteLines.Count -eq 0) 'Confirmation must follow the complete change note.'
            Check (-not (Test-Path -LiteralPath $env:WORKSHOP_TEST_OUTPUT)) 'Steam must not start before confirmation.'
            return 'yes'
        }
        Check ($noteLines.Count -gt 0) 'Change-note entry must finish after one empty line (two Enter presses after the final text).'
        $noteLines.Dequeue()
    }
    $preview = @(& "$deployRoot\publish-mod.ps1" @interactiveArgs 6>&1)
    $expectedNote = "First 'quoted' line`nSecond line"
    Check ((Read-Fixture).Published.ChangeNote -ceq $expectedNote -and @($preview | ForEach-Object { $_.ToString() }) -ccontains $expectedNote) 'Interactive input must preview and submit the same normalized multiline note without including the confirmation answer.'
    function Read-Host { 'yes' }

    Reset-Fixture
    $maximumDescription = 'a' * 8000
    Set-Content -LiteralPath "$fixture\repo\workshop-description.bbcode" -Value $maximumDescription -NoNewline
    & "$deployRoot\publish-mod.ps1" @publishArgs
    Check ((Read-Fixture).Published.Description -ceq $maximumDescription) 'A valid maximum-size description must reach Steam without exceeding the Windows command-line limit.'
    Set-Content -LiteralPath "$fixture\repo\workshop-description.bbcode" -Value $quotedDescription

    Reset-Fixture
    $secondMod = "$fixture\game\Mods\Other mod's [test]"
    New-Item -ItemType Directory -Path "$secondMod\About" -Force | Out-Null
    Set-Content -LiteralPath "$secondMod\About\About.xml" -Value '<ModMetaData><translationMod>true</translationMod><supportedVersions><li>1.7</li></supportedVersions></ModMetaData>'
    Set-Content -LiteralPath "$secondMod\About\PublishedFileId.txt" -Value '1234567891'
    $batch = @(& "$deployRoot\sync-workshop-tags.ps1" -ModPath @($syncArgs.ModPath, $secondMod) -RimWorldDir $syncArgs.RimWorldDir)
    Check ($batch.Count -eq 2 -and $batch[0].PublishedFileId -eq 1234567890 -and $batch[1].PublishedFileId -eq 1234567891) 'Tag synchronization must return one result per item, without child host messages leaking into the results.'
    Check ((Read-Fixture).Submissions.Count -eq 2 -and ($batch[0].Tags -join ',') -ceq 'Mod,1.6,1.7' -and ($batch[1].Tags -join ',') -ceq 'Translation,1.7') 'A batch must preserve each item and its tags across the process boundary.'
    $upload = (Read-Fixture).Published
    Check (($upload.Tags -join ',') -ceq 'Translation,1.7' -and $null -eq $upload.Content -and $null -eq $upload.Description -and $null -eq $upload.Title -and $null -eq $upload.Preview -and -not $upload.Public -and [string]::IsNullOrEmpty($upload.ChangeNote)) 'Standalone tag repair must only change tags.'

    Reset-Fixture -Online $false
    $failure = Failure { & "$deployRoot\publish-mod.ps1" @publishArgs }
    Check ((Read-Fixture).Starts -eq 0 -and (Read-Fixture).Creates -eq 0) 'Publishing offline must not create or update a listing.'

    Reset-Fixture -RejectTags $true
    $failure = Failure { & "$deployRoot\publish-mod.ps1" @publishArgs }
    Check ((Read-Fixture).Submissions.Count -eq 0 -and $failure -match 'tags') 'Rejected tags must stop the content submission.'

    Reset-Fixture -Result 2
    $failure = Failure { & "$deployRoot\publish-mod.ps1" @publishArgs }
    Check ($failure -match 'k_EResultFail' -and $null -eq (Read-Fixture).Published -and (Read-Fixture).Shutdowns -eq 1) 'A failed Steam callback must be reported and the session released.'

    Reset-Fixture
    Remove-Item -LiteralPath "$fixture\game\Mods\Example\About\PublishedFileId.txt", "$fixture\repo\mod\About\PublishedFileId.txt"
    & "$deployRoot\publish-mod.ps1" @publishArgs
    $upload = (Read-Fixture).Published
    Check ($upload.Item -eq 9876543210 -and $upload.Title -ceq 'Example' -and $upload.Public -and $null -ne $upload.Preview) 'First publication must complete the newly created listing.'
    Check (($upload.Tags -join ',') -ceq 'Mod,1.6,1.7' -and (Read-Fixture).Creates -eq 1) 'First publication must include tags on the newly assigned item.'
    foreach ($dir in @("$fixture\repo\mod\About", "$fixture\game\Mods\Example\About")) {
        Check ((Get-Content -LiteralPath "$dir\PublishedFileId.txt" -Raw).Trim() -eq '9876543210') 'Persist the assigned ID to both copies.'
    }

    Reset-Fixture -NeedsAgreement $true
    Remove-Item -LiteralPath "$fixture\game\Mods\Example\About\PublishedFileId.txt", "$fixture\repo\mod\About\PublishedFileId.txt"
    $failure = Failure { & "$deployRoot\publish-mod.ps1" @publishArgs }
    Check ($failure -match 'agreement' -and (Get-Content "$fixture\repo\mod\About\PublishedFileId.txt" -Raw).Trim() -eq '9876543210') 'Persist a created item before reporting its agreement requirement, so retry cannot create a duplicate.'
    Check ((Read-Fixture).Submissions.Count -eq 0) 'Do not continue publication before the Workshop agreement is accepted.'

    # The retry journal is written before either ID file. A partial disk write
    # must not cause CreateItem to run a second time for the same publication.
    Remove-Item -LiteralPath "$fixture\game\Mods\Example\About\PublishedFileId.txt"
    Reset-Fixture
    & "$deployRoot\publish-mod.ps1" @publishArgs
    $upload = (Read-Fixture).Published
    Check ((Read-Fixture).Creates -eq 0 -and $upload.Item -eq 9876543210 -and $upload.Title -ceq 'Example' -and $upload.Public -and $null -ne $upload.Preview) 'Retry after accepting the agreement must finish the same listing, including first-publication fields.'
    Check ((Get-Content -LiteralPath "$fixture\game\Mods\Example\About\PublishedFileId.txt" -Raw).Trim() -eq '9876543210') 'Recovery must repair the missing deployed ID before clearing its journal.'
    Check (-not (Test-Path "$fixture\repo\temp\workshop-created-item.txt")) 'A completed first publication must release its retry marker.'

    Reset-Fixture
    function Read-Host { 'no' }
    & "$deployRoot\publish-mod.ps1" @publishArgs
    Check (-not (Test-Path -LiteralPath $env:WORKSHOP_TEST_OUTPUT)) 'Declining publication must not start a Steam session or submit anything.'
    & "$deployRoot\sync-workshop-tags.ps1" @syncArgs -WhatIf | Out-Null
    Check (-not (Test-Path -LiteralPath $env:WORKSHOP_TEST_OUTPUT)) 'Previewing tags must not start a Steam session.'
    Write-Host 'Desktop Steam publishing regression checks passed.'
} finally {
    $env:WORKSHOP_TEST_INPUT = $savedTestInput
    $env:WORKSHOP_TEST_OUTPUT = $savedTestOutput
    $env:SteamAppId = $savedAppId
    $env:SteamGameId = $savedGameId
}
