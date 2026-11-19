[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Purpose,
    [string]$Owner = '',
    [string]$ModSet = '',
    # Runs load the repository's own save copies (D:\Code\RimWorld\Saves), never the
    # player's folder. -RefreshFromPlayer copies the newest matching player save in
    # first (read-only source); that is the only time the player's saves are read.
    [string]$SourceSaveDirectory = '',
    [switch]$RefreshFromPlayer,
    [string]$SavePattern = 'Fisso-NAM*.rws',
    [string]$ModSourceRoot = 'D:\Code\RimWorld',
    [string]$GameRoot = '',
    # Defaults to the Steam-synced 1.7 copy. Select 1.6 for legacy scenarios.
    [ValidateSet('','1.6','1.7')][string]$GameVersion = '',
    [ValidatePattern('^[A-Za-z0-9._-]+$')][string]$WslDistribution = 'Ubuntu-22.04',
    [ValidateSet('Default','NVIDIA','Software')][string]$WslRenderer = 'Default',
    [string]$BaselineConfigPath = '',
    [string[]]$AdditionalModSource = @()
)
. (Join-Path $PSScriptRoot 'run-common.ps1')
. (Join-Path $PSScriptRoot 'game-sync-common.ps1')
. (Join-Path $PSScriptRoot 'profile-common.ps1')
. (Join-Path $PSScriptRoot 'snapshot-common.ps1')
if ($GameRoot -and $GameVersion) { throw 'Pass -GameRoot or -GameVersion, not both.' }
if (-not $SourceSaveDirectory) { $SourceSaveDirectory = $script:LocalSavesRoot }
if ($RefreshFromPlayer) { Copy-PlayerSave -SavePattern $SavePattern -Destination $script:LocalSavesRoot }
if (-not $Owner) { $Owner = Get-AutomationOwner }
if ($Owner -cne (Get-AutomationOwner)) { throw 'Create runs with your own owner identity (CODEX_THREAD_ID or RIMWORLD_AUTOMATION_OWNER).' }
if ($ModSet -and $ModSet -notmatch '^[a-z0-9-]+$') { throw 'Invalid mod set name.' }
$id = [Guid]::NewGuid().ToString('N')
$profile = Assert-RunPath $id
New-Item -ItemType Directory -Path $profile | Out-Null
$guard = Open-RunLock (Join-Path $profile 'run.lock')
try {
    Write-RunJson (Join-Path $profile 'preparing.json') ([ordered]@{
        runId=$id; owner=$Owner; purpose=$Purpose; createdUtc=[DateTime]::UtcNow.ToString('o')
    })
    # Only preparation is serialized. Running games hold no shared lock;
    # there is no configured instance cap.
    $mutex = Enter-GamePreparation
    try {
        $script:InstalledGameRoot = Resolve-AutomationGameRoot $GameRoot $GameVersion
        $layout = Get-GameLayout $script:InstalledGameRoot
        if ($GameVersion -and $layout.gameVersion -cne $GameVersion) {
            throw "$script:InstalledGameRoot holds RimWorld $($layout.gameVersion), not $GameVersion"
        }
        $additionalSources = @(Get-AdditionalModSources -ModSourceRoot $ModSourceRoot -AdditionalModSource $AdditionalModSource)
        $AdditionalModSource = @($additionalSources | ForEach-Object { $_.source })
        $configuration = Initialize-RunProfile $profile $SourceSaveDirectory $SavePattern $ModSet $ModSourceRoot $BaselineConfigPath -AdditionalModSource $AdditionalModSource
        # Windows runs launch from a reserved persistent slot (stable exe path).
        $slot = if ($layout.executable -cne 'RimWorldLinux') { Request-RunSlot $id $Owner } else { $null }
        $gameFolder = if ($slot) { Join-Path (Join-Path $script:AutomationSlotsRoot $slot) 'Game' } else { '' }
        $snapshots = New-RunGameSnapshot $profile $ModSourceRoot -AdditionalModSource $AdditionalModSource -GameFolder $gameFolder
        $snapshots | Add-Member -NotePropertyName slot -NotePropertyValue $slot
        $record = [ordered]@{
            schemaVersion=1; runId=$id; owner=$Owner; purpose=$Purpose
            createdUtc=[DateTime]::UtcNow.ToString('o'); profile=$profile
            configuration=$configuration; modSourceRoot=[IO.Path]::GetFullPath($ModSourceRoot)
            additionalModSources=$additionalSources
            backend=$(if ($layout.executable -ceq 'RimWorldLinux') { 'wsl' } else { 'windows' })
            wslDistribution=$WslDistribution; wslRenderer=$WslRenderer; gameRoot=$script:InstalledGameRoot
            snapshots=$snapshots
            display=[ordered]@{width=1920; height=1080; uiScale=1.25; muted=$true; pausedOnLoad=$true}
        }
        Write-RunJson (Join-Path $profile 'run.json') $record
    } finally { $mutex.ReleaseMutex(); $mutex.Dispose() }
    Remove-Item -LiteralPath (Join-Path $profile 'preparing.json')
    Write-Host "Prepared run $id ($Purpose), owner=$Owner$(if ($slot) { ", $slot" })"
    [pscustomobject]$record
} catch {
    Write-RunJson (Join-Path $profile 'preparation-failed.json') @{utc=[DateTime]::UtcNow.ToString('o'); error=$_.ToString()}
    throw
} finally { $guard.Dispose() }
