$ErrorActionPreference = 'Stop'
$script:AutomationRepositoryRoot = 'D:\Code\RimWorld'
$script:AutomationSharedRoot = Join-Path $script:AutomationRepositoryRoot 'AutomationProfiles\Shared'
$script:AutomationRunsRoot = Join-Path $script:AutomationSharedRoot 'Runs'
$script:InstalledGameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld'
$script:InstalledWorkshopRoot = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\294100'

function Get-AutomationOwner {
    if ($env:RIMWORLD_AUTOMATION_OWNER) { return $env:RIMWORLD_AUTOMATION_OWNER }
    if ($env:CODEX_THREAD_ID) { return $env:CODEX_THREAD_ID }
    return "manual:$env:USERNAME"
}
function Assert-RunPath {
    param([Parameter(Mandatory)][string]$RunId)
    if ($RunId -cnotmatch '^[a-f0-9]{32}$') { throw 'RunId must be the 32-character ID returned by create-run.ps1.' }
    $path = Join-Path $script:AutomationRunsRoot $RunId
    # No run or ancestor may redirect writes/deletion outside the managed root.
    $parent = $path
    while ($parent) {
        if (Test-Path -LiteralPath $parent) {
            if ((Get-Item -LiteralPath $parent -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Run path contains a link: $parent"
            }
        }
        $parent = Split-Path -Parent $parent
    }
    return $path
}
function Read-RunJson {
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
        [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
    $reader = [IO.StreamReader]::new($stream)
    try { return $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
}
function Write-RunJson {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)]$Value)
    $temporary = $Path + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        $Value | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $temporary -Encoding utf8
        [IO.File]::Move($temporary, $Path, $true)
    } finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
}
function Get-RunRecord {
    param([Parameter(Mandatory)][string]$RunId, [switch]$RequireOwner, [switch]$AllowRemoving)
    $path = Assert-RunPath $RunId
    $record = Read-RunJson (Join-Path $path 'run.json')
    if ($null -eq $record -or $record.runId -cne $RunId -or $record.profile -ine $path) { throw "No valid prepared run: $RunId" }
    if ($RequireOwner -and $record.owner -cne (Get-AutomationOwner)) { throw "Run $RunId belongs to '$($record.owner)'; create your own run." }
    if (-not $AllowRemoving -and ((Test-Path -LiteralPath (Join-Path $path 'removing.json')) -or
        (Test-Path -LiteralPath (Join-Path $script:AutomationRunsRoot ('.removing-' + $RunId + '.json'))))) { throw "Run $RunId is being removed." }
    return $record
}
function Open-RunLock {
    param([Parameter(Mandatory)][string]$Path)
    $deadline = [DateTime]::UtcNow.AddSeconds(2)
    do {
        try { return [IO.File]::Open($Path, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
        catch [IO.IOException] {
            if (($_.Exception.HResult -band 0xffff) -notin 32,33) { throw }
            if ([DateTime]::UtcNow -ge $deadline) { throw "Run is in use; could not acquire $Path" }
            Start-Sleep -Milliseconds 10
        }
    } while ($true)
}
function Test-RunLocked {
    param([Parameter(Mandatory)][string]$Path)
    try {
        $handle = [IO.File]::Open($Path, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $handle.Dispose(); return $false
    }
    catch { return $true }
}
function Test-RunCommandLine {
    param([string]$CommandLine, [string]$Profile, [string]$Token)
    $path = [regex]::Escape($Profile)
    $nonce = [regex]::Escape($Token)
    return $CommandLine -match ('(?i)(?:^|\s)-savedatafolder=(?:"' + $path + '"|' + $path + ')(?=\s|$)') -and
        $CommandLine -cmatch ('(?:^|\s)-automationtoken=' + $nonce + '(?=\s|$)')
}
function Test-RunStartupFailure {
    param([string]$Content, [string]$Token, [switch]$AllowRecoverableSaveErrors)
    if (-not $Content.Contains($Token)) { return $false }
    $caughtSaveNode = if ($AllowRecoverableSaveErrors) { '' } else { 'SaveableFromNode exception:|' }
    $Content -match ('(?m)^(?:\d{2}:\d{2}:\d{2}\s+\w+\s+)?(?:' + $caughtSaveNode + 'Exception while loading|Crash!!!|Could not find (?:game|world) XML node\.|Called InitSaving\(\) but current mode is|Mods config data is .*Resetting\.)|SharedAutomation.*Exception|Could not execute post-long-event action\. Exception:')
}
function Get-GameLayout {
    param([Parameter(Mandatory)][string]$Root)
    if (Test-Path -LiteralPath (Join-Path $Root 'RimWorldLinux')) {
        return [pscustomobject]@{executable='RimWorldLinux';gameVersion='1.6';assetFolders=@('Data','RimWorldLinux_Data')}
    }
    if ((Test-Path -LiteralPath (Join-Path $Root 'RimWorld.exe')) -and
        (Test-Path -LiteralPath (Join-Path $Root 'UnityStubs.dll'))) {
        return [pscustomobject]@{executable='RimWorld.exe';gameVersion='1.7';assetFolders=@('Data','Rendering')}
    }
    if (Test-Path -LiteralPath (Join-Path $Root 'RimWorldWin64.exe')) {
        return [pscustomobject]@{executable='RimWorldWin64.exe';gameVersion='1.6';assetFolders=@('Data','RimWorldWin64_Data','MonoBleedingEdge')}
    }
    throw "No supported RimWorld executable in $Root"
}
function Get-RunExecutable {
    param([Parameter(Mandatory)]$Record)
    $name = $Record.snapshots.executable
    if (-not $name) { $name = 'RimWorldWin64.exe' } # Existing 1.6 manifests.
    if ($name -cnotin @('RimWorldWin64.exe','RimWorld.exe','RimWorldLinux')) { throw 'Invalid run executable.' }
    Join-Path $Record.profile ('Game\' + $name)
}
function Get-AllRimWorldProcessInfo { @(Get-CimInstance Win32_Process -Filter "name = 'RimWorldWin64.exe' OR name = 'RimWorld.exe'") }
function Get-RunProcesses {
    param([Parameter(Mandatory)]$Record)
    if ($Record.backend -ceq 'wsl') {
        $identity = Invoke-WslRunControl $Record 'inspect'
        if ($identity) {
            [pscustomobject]@{ProcessId=$identity.pid; ExecutablePath=$identity.executable; CommandLine=($identity.arguments -join ' '); CreationDate=$identity.start}
        }
        return
    }
    @(Get-AllRimWorldProcessInfo | Where-Object {
        (Test-RunCommandLine $_.CommandLine $Record.profile ('rimworld-shared-' + $Record.runId)) -and
        $_.ExecutablePath -ieq (Get-RunExecutable $Record)
    })
}
function Get-WslRunPath {
    param([Parameter(Mandatory)]$Record)
    '/mnt/d/Code/RimWorld/AutomationProfiles/Shared/Runs/' + $Record.runId
}
function Invoke-WslRunControl {
    param([Parameter(Mandatory)]$Record, [Parameter(Mandatory)][string]$Action, [string]$Payload = '')
    # Run selection and ownership are checked before every Linux process/pipe operation.
    $checked = Get-RunRecord $Record.runId -RequireOwner:($Action -cnotin @('inspect','idle')) -AllowRemoving:($Action -ceq 'remove')
    if ($checked.backend -cne 'wsl' -or $checked.wslDistribution -cnotmatch '^[A-Za-z0-9._-]+$') { throw 'Invalid WSL run backend.' }
    $profile = Get-WslRunPath $checked
    $start = [Diagnostics.ProcessStartInfo]::new('wsl.exe')
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($argument in @('--distribution',$checked.wslDistribution,'--exec','python3',($profile + '/Host/linux-host.py'),$Action,$profile,('rimworld-shared-' + $checked.runId),$checked.owner,$Payload)) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $output = $process.StandardOutput.ReadToEndAsync(); $errorText = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(45000)) { $process.Kill(); throw 'Linux control command timed out.' }
        $result = $output.GetAwaiter().GetResult(); $failure = $errorText.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "Linux control failed: $failure" }
        if ($result.Trim()) { $result | ConvertFrom-Json }
    } finally { $process.Dispose() }
}
function Set-AutomationRun {
    param([Parameter(Mandatory)][string]$RunId)
    $record = Get-RunRecord $RunId -RequireOwner
    $script:AutomationRunId = $RunId
    $script:AutomationProfilePath = $record.profile
    $script:RimWorldExecutable = Get-RunExecutable $record
    $script:RimWorldPlayerLog = Join-Path $record.profile 'Player.log'
}
function Get-SelectedRun {
    if (-not $script:AutomationRunId) { throw 'Select your run with -RunId or RIMWORLD_AUTOMATION_RUN_ID; there is no implicit target.' }
    Get-RunRecord $script:AutomationRunId -RequireOwner
}
function Update-RunActivity {
    param([Parameter(Mandatory)][string]$Activity, [bool]$Ready = $false)
    $record = Get-SelectedRun
    Write-RunJson (Join-Path $record.profile 'activity.json') ([ordered]@{
        owner = $record.owner; utc = [DateTime]::UtcNow.ToString('o'); activity = $Activity; ready = $Ready
    })
}
function Remove-RunTree {
    param([Parameter(Mandatory)][string]$RunId)
    $root = Assert-RunPath $RunId
    # Enumerate one level at a time. Delete junctions themselves, never recurse
    # into their installed-game targets. Verify every resolved path before use.
    function Remove-Entry([string]$Path) {
        $absolute = [IO.Path]::GetFullPath($Path)
        if ($absolute -ine $root -and -not $absolute.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Deletion escaped run root: $absolute"
        }
        $item = Get-Item -LiteralPath $absolute -Force
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            if ($item.PSIsContainer) { [IO.Directory]::Delete($absolute) } else { [IO.File]::Delete($absolute) }
        } elseif ($item.PSIsContainer) {
            foreach ($child in Get-ChildItem -LiteralPath $absolute -Force) { Remove-Entry $child.FullName }
            [IO.Directory]::Delete($absolute)
        } else { Remove-Item -LiteralPath $absolute -Force }
    }
    if (Test-Path -LiteralPath $root) { Remove-Entry $root }
}
