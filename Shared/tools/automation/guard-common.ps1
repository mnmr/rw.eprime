# Stall and cap rules for run-guard.ps1. Progress is pipe input, advancing game
# ticks, or loading. A run fails fast when it has none for StallSeconds, when
# its driving script goes silent (no pipe or bridge command), when the game
# stops publishing its heartbeat, or at the hard cap after ready.
function New-RunStallTracker([DateTime]$ReadyUtc) { @{ ReadyUtc = $ReadyUtc; Ticks = $null; TickUtc = $ReadyUtc } }
function Get-RunStallReason {
    param([hashtable]$Tracker, $State, [DateTime]$NowUtc, [DateTime]$HeartbeatUtc, [DateTime]$ActivityUtc,
        [switch]$Pickle, [int]$StallSeconds = 10, [int]$CapSeconds = 60)
    $silent = ($NowUtc - $HeartbeatUtc).TotalSeconds
    if ($silent -gt 5) { return "game not responding: no heartbeat for $([Math]::Round($silent, 1)) s" }
    if (($NowUtc - $Tracker.ReadyUtc).TotalSeconds -ge $CapSeconds) { return "hard cap: $CapSeconds s after ready" }
    if ($State.loading) { $Tracker.TickUtc = $NowUtc }
    elseif ($null -eq $Tracker.Ticks) { $Tracker.Ticks = $State.ticks }
    elseif ($Tracker.Ticks -ne $State.ticks) { $Tracker.Ticks = $State.ticks; $Tracker.TickUtc = $NowUtc }
    $inputUtc = $NowUtc.AddSeconds(-[double]$State.inputAgeSeconds)
    $progressUtc = if ($inputUtc -gt $Tracker.TickUtc) { $inputUtc } else { $Tracker.TickUtc }
    $stalled = ($NowUtc - $progressUtc).TotalSeconds
    if ($stalled -ge $StallSeconds) {
        return "no progress for $([Math]::Round($stalled, 1)) s (no input, no ticks): paused=$($State.paused) forcePaused=$($State.forcePaused) blockers=[$(@($State.blockers) -join ', ')] focused=$($State.focused)"
    }
    $idle = [Math]::Min([double]$State.commandAgeSeconds, ($NowUtc - $ActivityUtc).TotalSeconds)
    if (-not $Pickle -and $idle -ge $StallSeconds) { return "driving script idle for $([Math]::Round($idle, 1)) s: no pipe or bridge command" }
    return $null
}
