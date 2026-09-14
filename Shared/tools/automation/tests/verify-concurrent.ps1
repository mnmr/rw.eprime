[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunIdsFile, [Parameter(Mandatory)][string]$EvidenceDirectory)
$ErrorActionPreference = 'Stop'
$toolsRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $toolsRoot 'automation-common.ps1')
. (Join-Path $toolsRoot 'foreground-audit.ps1')
$ids = @(Get-Content -LiteralPath $RunIdsFile -Raw | ConvertFrom-Json)
if ($ids.Count -ne 3) { throw 'This regression scenario needs three prepared runs.' }
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
$jobs = @()
$gameIds = @()
$states = @()
function Check([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Rejects([scriptblock]$Action, [string]$Message) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true; Write-Host "Expected rejection: $_" }
    Check $rejected $Message
}
function Capture([string]$Name) {
    $path = Save-SharedGameCapture $Name
    Copy-Item -LiteralPath $path -Destination (Join-Path $evidence $Name) -Force
}
[SharedForegroundAudit]::Start()
try {
    $b = Get-RunRecord $ids[1] -RequireOwner
    [xml]$prefs = Get-Content -LiteralPath (Join-Path $b.profile 'Config\Prefs.xml') -Raw
    $prefs.PrefsData.uiScale = '1'
    $prefs.Save((Join-Path $b.profile 'Config\Prefs.xml'))
    foreach ($id in $ids) {
        $jobs += Start-Job -ScriptBlock { param($toolsRoot,$id)
            $ErrorActionPreference = 'Stop'
            & (Join-Path $toolsRoot 'launch.ps1') -RunId $id -TimeoutMinutes 6
        } -ArgumentList $toolsRoot,$id
    }
    $deadline = [DateTime]::UtcNow.AddMinutes(7)
    while (@($jobs | Where-Object State -eq 'Running').Count) {
        foreach ($job in $jobs) { Receive-Job $job }
        if (@($jobs | Where-Object State -eq 'Failed').Count) { throw 'A concurrent launch failed.' }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Concurrent launch deadline expired.' }
        Start-Sleep -Seconds 2
    }
    foreach ($job in $jobs) { Receive-Job $job -ErrorAction Stop }
    foreach ($id in $ids) {
        Set-AutomationRun $id
        $state = Invoke-SharedGameCommand @{command='status'}
        Check $state.ready 'Each game must consume the save and reach a ready map.'
        Check ($state.audioVolume -eq 0) 'Every game must remain muted.'
        Check ([IO.Path]::GetFullPath($state.runtimeModRoot).StartsWith($script:AutomationProfilePath + '\', [StringComparison]::OrdinalIgnoreCase)) 'Each game must load its own copied runtime.'
        $states += $state
        $gameIds += $state.processId
    }
    Check ((@($gameIds | Sort-Object -Unique)).Count -eq 3) 'Three distinct games must coexist.'
    Check ($states[0].uiScale -eq 1.25 -and $states[1].uiScale -eq 1 -and $states[2].uiScale -eq 1.25) 'Games must consume their independent preference files.'
    $states | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $evidence 'ready-states.json')
    & (Join-Path $toolsRoot 'list-runs.ps1') -Json -Detailed | Set-Content (Join-Path $evidence 'all-running.json')
    Set-AutomationRun $ids[2]
    Capture 'c-before.png'
    foreach ($index in 0..1) {
        Set-AutomationRun $ids[$index]
        Invoke-SharedGameCommand @{command='click';x=80;y=24} | Out-Null
        Invoke-SharedGameCommand @{command='type';text=(@('steel','silver')[$index])} | Out-Null
        Capture (@('a-steel.png','b-silver.png')[$index])
    }
    Set-AutomationRun $ids[2]
    Capture 'c-after-other-input.png'
    $cState = Invoke-SharedGameCommand @{command='status'}
    Check ($cState.x -eq -1000 -and $cState.y -eq -1000) 'Other runs must not move the third virtual pointer.'
    Rejects { & (Join-Path $toolsRoot 'launch.ps1') -RunId $ids[0] } 'A duplicate launch must be rejected.'
    Rejects { & (Join-Path $toolsRoot 'remove-run.ps1') -RunId $ids[1] } 'A live run must not be removed.'
    $owner = $env:RIMWORLD_AUTOMATION_OWNER
    try {
        $env:RIMWORLD_AUTOMATION_OWNER = 'unrelated-owner-regression'
        Rejects { & (Join-Path $toolsRoot 'stop.ps1') -RunId $ids[1] } 'A foreign owner must not stop another game.'
    } finally { $env:RIMWORLD_AUTOMATION_OWNER = $owner }
    # Preparing another run is permitted while all three games are active.
    $additional = & (Join-Path $toolsRoot 'create-run.ps1') -Purpose 'Preparation while three runs are active'
    & (Join-Path $toolsRoot 'remove-run.ps1') -RunId $additional.runId
    & (Join-Path $toolsRoot 'stop.ps1') -RunId $ids[0]
    Set-AutomationRun $ids[1]
    Invoke-SharedGameCommand @{command='click';x=80;y=24} | Out-Null
    Invoke-SharedGameCommand @{command='type';text='^agold'} | Out-Null
    Capture 'b-after-a-stopped.png'
    Check ((Invoke-SharedGameCommand @{command='status'}).ready) 'Second game must still respond after first stops.'
    Set-AutomationRun $ids[2]
    Capture 'c-after-a-stopped.png'
    Check ((Invoke-SharedGameCommand @{command='status'}).ready) 'Third game must still respond after first stops.'
    Write-Host 'PASS three concurrent games, separate preferences/input transport, mute, runtime copies, registry and independent stop. Inspect the captured UI outcomes and compare the untouched readout before accepting interaction proof.'
} finally {
    $cleanupErrors = [Collections.Generic.List[string]]::new()
    foreach ($job in $jobs) {
        # Launch has its own deadline/cleanup. Let any remaining launch complete
        # before its run's serialized stop; do not abandon a launching host.
        try {
            $deadline = [DateTime]::UtcNow.AddMinutes(7)
            while ($job.State -eq 'Running' -and [DateTime]::UtcNow -lt $deadline) { Wait-Job $job -Timeout 10 | Out-Null }
            Receive-Job $job -ErrorAction Continue
            Remove-Job $job -Force
        } catch { $cleanupErrors.Add($_.ToString()) }
    }
    try {
        foreach ($id in $ids) {
            try { & (Join-Path $toolsRoot 'stop.ps1') -RunId $id } catch { $cleanupErrors.Add($_.ToString()) }
            try {
                $record = Get-RunRecord $id -RequireOwner
                $destination = Join-Path $evidence $id
                New-Item -ItemType Directory -Path $destination -Force | Out-Null
                foreach ($name in @('Player.log','run.json','launch.json','host.json','activity.json','ready.json')) {
                    $source = Join-Path $record.profile $name
                    if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $destination -Force }
                }
            } catch { $cleanupErrors.Add($_.ToString()) }
        }
    } finally {
        $foreground = [SharedForegroundAudit]::Finish()
        @{gameProcessIds=$gameIds;foregroundProcessIds=$foreground} | ConvertTo-Json | Set-Content (Join-Path $evidence 'foreground.json')
        foreach ($id in $gameIds) { Check ($foreground -notcontains [uint32]$id) 'A test game stole foreground focus.' }
    }
    if ($cleanupErrors.Count) { throw ($cleanupErrors -join [Environment]::NewLine) }
}
