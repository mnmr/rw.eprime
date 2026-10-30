param(
    [ValidateSet('all','bill-import-enabled','bill-import-revoked','bill-import-composite','bill-import-forced','restore-history')]
    [string]$Mode = 'all',
    [string]$Label = 'import-restore',
    [int]$ControlTicks = 0,
    [string]$ComplexJobsRoot = ''
)
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools = Join-Path $source 'Shared\tools\automation'
# Build WorkRoles and the shared runtime before invoking this runner. Only the
# test assembly is built here; production artifacts are read-only inputs.
$history = $Mode -eq 'restore-history'
if ($history -and !(Test-Path -LiteralPath (Join-Path $ComplexJobsRoot 'About/About.xml'))) { throw 'restore-history requires the real Complex Jobs source via -ComplexJobsRoot' }
dotnet build (Join-Path $PSScriptRoot 'Probe.csproj') -c Release "-p:HistoryOnly=$($history.ToString().ToLowerInvariant())"
if ($LASTEXITCODE -ne 0) { throw 'Runtime authority probe build failed' }
$modes = if ($Mode -eq 'all') {
    @('bill-import-enabled','bill-import-revoked','bill-import-composite','bill-import-forced')
} else { @($Mode) }
$durations = @{bill=$ControlTicks;fish=$ControlTicks}
foreach ($scenario in $modes) {
    $kind = $scenario.Split('-')[0]
    if (($scenario.EndsWith('-revoked') -or $scenario.EndsWith('-composite')) -and $durations[$kind] -le 0) {
        throw 'Run the enabled control first and supply its recorded duration using -ControlTicks, or use -Mode all'
    }
    $modSet = if ($history) { 'workroles-bill-review' } else { 'workroles-audit-persistence' }
    $additional = if ($history) { @($ComplexJobsRoot, "$PSScriptRoot\HistoryProbe") } else { @("$PSScriptRoot\Probe") }
    $run = & "$tools\create-run.ps1" -Purpose "WorkRoles WR19-WR21 import identity, viable recovery, and native $scenario outcome ($Label)" -ModSourceRoot $source -ModSet $modSet -AdditionalModSource $additional
    $out = Join-Path $source "WorkRoles\temp\import-restore\$Label-$scenario-$($run.runId)"
    New-Item -ItemType Directory -Path $out -Force | Out-Null
    $run.runId | Set-Content (Join-Path $out 'run-id.txt')
    $scenario | Set-Content (Join-Path $run.profile 'runtime-authority-mode.txt')
    $durations[$kind] | Set-Content (Join-Path $run.profile 'runtime-authority-controlticks.txt')
    try {
        & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
        . "$tools\automation-common.ps1" -RunId $run.runId
        # Each scenario gets a fresh single-use run and one native session,
        # bounded below the owner's shared 60-second post-ready host cap.
        $deadline = [DateTime]::UtcNow.AddSeconds(50)
        $results = Join-Path $run.profile 'runtime-authority-results.txt'
        do {
            Invoke-SharedGameCommand @{command='status'} | Out-Null
            $observed = if (Test-Path $results) { Get-Content -LiteralPath $results -Raw } else { '' }
            $syncPath = Join-Path $run.profile 'persistence-audit-results.txt'
            $sync = if (Test-Path $syncPath) { Get-Content -LiteralPath $syncPath -Raw } else { '' }
            if ($sync -match 'FAILED') { throw "Import/restore synchronous regression failed: $sync" }
            if ($history -and $sync -match 'SUCCESS WR21 history') { break }
            if ($observed -match 'FAILED|SUCCESS') { break }
            if ([DateTime]::UtcNow -ge $deadline) { throw "Runtime authority $scenario exceeded 50 seconds" }
            Start-Sleep -Milliseconds 250
        } while ($true)
        & "$tools\capture.ps1" -RunId $run.runId -OutName 'runtime-authority-result.png'
    } finally {
        & "$tools\stop.ps1" -RunId $run.runId
        foreach ($name in 'Player.log','run.json','launch.json','host.json','runtime-authority-results.txt','runtime-authority-mode.txt','runtime-authority-controlticks.txt','persistence-audit-results.txt','Captures') {
            $path = Join-Path $run.profile $name
            if (Test-Path $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
        }
        & "$tools\remove-run.ps1" -RunId $run.runId
    }
    $result = if ($history) { '' } else { Get-Content (Join-Path $out 'runtime-authority-results.txt') -Raw }
    $syncResult = Get-Content (Join-Path $out 'persistence-audit-results.txt') -Raw
    Write-Output $syncResult
    Write-Output $result
    Write-Output $out
    if ($history) {
        if ($syncResult -notmatch 'SUCCESS WR21 history' -or $syncResult -match 'FAILED') { throw "Historical recovery regression failed: $out" }
        continue
    }
    if ($result -notmatch "SUCCESS $scenario" -or $result -match 'FAILED' -or
        $syncResult -notmatch 'SUCCESS WR19 WR21' -or $syncResult -match 'FAILED') {
        throw "Runtime authority regression failed: $out"
    }
    if ($result -match "CONTROL $kind ticks=(\d+)") { $durations[$kind] = [int]$Matches[1] }
}
