[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Purpose,
    [string]$Owner = '',
    [string]$ModSet = '',
    [string]$SourceSaveDirectory = 'C:\Users\morte\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves',
    [string]$SavePattern = 'Fisso-NAM*.rws',
    [string]$ModSourceRoot = 'D:\Code\RimWorld'
)
. (Join-Path $PSScriptRoot 'run-common.ps1')
. (Join-Path $PSScriptRoot 'profile-common.ps1')
. (Join-Path $PSScriptRoot 'snapshot-common.ps1')
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
    $mutex = [Threading.Mutex]::new($false, 'Local\RimWorld-Automation-Prepare')
    $acquired = $false
    try {
        try { $acquired = $mutex.WaitOne(60000) } catch [Threading.AbandonedMutexException] { $acquired = $true }
        if (-not $acquired) { throw 'Another profile preparation did not release its lock.' }
        $configuration = Initialize-RunProfile $profile $SourceSaveDirectory $SavePattern $ModSet $ModSourceRoot
        $snapshots = New-RunGameSnapshot $profile $ModSourceRoot
    } finally { if ($acquired) { $mutex.ReleaseMutex() }; $mutex.Dispose() }
    $record = [ordered]@{
        schemaVersion=1; runId=$id; owner=$Owner; purpose=$Purpose
        createdUtc=[DateTime]::UtcNow.ToString('o'); profile=$profile
        configuration=$configuration; modSourceRoot=[IO.Path]::GetFullPath($ModSourceRoot)
        snapshots=$snapshots
        display=[ordered]@{width=1920; height=1080; uiScale=1.25; muted=$true; pausedOnLoad=$true}
    }
    Write-RunJson (Join-Path $profile 'run.json') $record
    Remove-Item -LiteralPath (Join-Path $profile 'preparing.json')
    Write-Host "Prepared run $id ($Purpose), owner=$Owner"
    [pscustomobject]$record
} catch {
    Write-RunJson (Join-Path $profile 'preparation-failed.json') @{utc=[DateTime]::UtcNow.ToString('o'); error=$_.ToString()}
    throw
} finally { $guard.Dispose() }
