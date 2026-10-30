param(
    [ValidateSet('all','bill-enabled','bill-revoked','bill-forced','fish-enabled','fish-revoked','fish-forced')]
    [string]$Mode = 'all',
    [string]$Label = 'runtime-authority',
    [int]$ControlTicks = 0
)
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools = Join-Path $source 'Shared\tools\automation'
# Build WorkRoles and the shared runtime before invoking this runner. Only the
# test assembly is built here; production artifacts are read-only inputs.
dotnet build (Join-Path $PSScriptRoot 'Probe.csproj') -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Runtime authority probe build failed' }
$modes = if ($Mode -eq 'all') {
    @('bill-enabled','bill-revoked','bill-forced','fish-enabled','fish-revoked','fish-forced')
} else { @($Mode) }
$durations = @{bill=$ControlTicks;fish=$ControlTicks}
foreach ($scenario in $modes) {
    $kind = $scenario.Split('-')[0]
    if ($scenario.EndsWith('-revoked') -and $durations[$kind] -le 0) {
        throw 'Run the enabled control first and supply its recorded duration using -ControlTicks, or use -Mode all'
    }
    $run = & "$tools\create-run.ps1" -Purpose "WorkRoles WR12/WR13 native $scenario outcome ($Label)" -ModSourceRoot $source -ModSet workroles-runtime-authority -AdditionalModSource @("$PSScriptRoot\Probe")
    $out = Join-Path $source "WorkRoles\temp\runtime-authority\$Label-$scenario-$($run.runId)"
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
            if ($observed -match 'FAILED|SUCCESS') { break }
            if ([DateTime]::UtcNow -ge $deadline) { throw "Runtime authority $scenario exceeded 50 seconds" }
            Start-Sleep -Milliseconds 250
        } while ($true)
        & "$tools\capture.ps1" -RunId $run.runId -OutName 'runtime-authority-result.png'
    } finally {
        & "$tools\stop.ps1" -RunId $run.runId
        foreach ($name in 'Player.log','run.json','launch.json','host.json','runtime-authority-results.txt','runtime-authority-mode.txt','runtime-authority-controlticks.txt','Captures') {
            $path = Join-Path $run.profile $name
            if (Test-Path $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
        }
        & "$tools\remove-run.ps1" -RunId $run.runId
    }
    $result = Get-Content (Join-Path $out 'runtime-authority-results.txt') -Raw
    Write-Output $result
    Write-Output $out
    if ($result -notmatch "SUCCESS $scenario" -or $result -match 'FAILED') {
        throw "Runtime authority regression failed: $out"
    }
    if ($result -match "CONTROL $kind ticks=(\d+)") { $durations[$kind] = [int]$Matches[1] }
}
