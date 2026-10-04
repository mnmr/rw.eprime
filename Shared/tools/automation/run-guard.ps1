# Started detached by launch.ps1 at game-ready. Stops the run and records
# stall.json when guard-common.ps1's rules fire: no progress, a silent driving
# script, a frozen game, or the hard cap.
param([Parameter(Mandatory)][string]$RunId, [int]$CapSeconds = 60, [int]$StallSeconds = 10, [switch]$Pickle)
. (Join-Path $PSScriptRoot 'automation-common.ps1') -RunId $RunId
. (Join-Path $PSScriptRoot 'guard-common.ps1')
$record = Get-SelectedRun
$statePath = Join-Path $record.profile 'game-state.json'
$activityPath = Join-Path $record.profile 'activity.json'
$gameId = [int](Get-Content -LiteralPath (Join-Path $record.profile 'ready.json') -Raw | ConvertFrom-Json).processId
$tracker = New-RunStallTracker ([DateTime]::UtcNow)
while ($true) {
    Start-Sleep -Milliseconds 250
    $game = Get-Process -Id $gameId -ErrorAction SilentlyContinue
    if (-not $game -or $game.HasExited) { return }
    try {
        $heartbeatUtc = [IO.File]::GetLastWriteTimeUtc($statePath)
        # A missing file (mid-replace) reports 1601-01-01: a racing read, not a stall.
        if ($heartbeatUtc.Year -lt 2000) { continue }
        $state = Get-Content -LiteralPath $statePath -Raw -ErrorAction Stop | ConvertFrom-Json
    } catch { continue } # the game replaces the file 4x per second; retry on a racing read
    $activityUtc = if (Test-Path -LiteralPath $activityPath) { [IO.File]::GetLastWriteTimeUtc($activityPath) } else { $tracker.ReadyUtc }
    $reason = Get-RunStallReason -Tracker $tracker -State $state -NowUtc ([DateTime]::UtcNow) -HeartbeatUtc $heartbeatUtc `
        -ActivityUtc $activityUtc -Pickle:$Pickle -StallSeconds $StallSeconds -CapSeconds $CapSeconds
    if ($reason) {
        Write-RunJson (Join-Path $record.profile 'stall.json') ([ordered]@{ utc = [DateTime]::UtcNow.ToString('o'); reason = $reason; state = $state })
        & (Join-Path $PSScriptRoot 'stop.ps1') -RunId $RunId
        return
    }
}
