param(
    [string]$ModSourceRoot = 'D:\Code\RimWorld',
    [ValidateSet('baseline','final')][string]$Mode = 'final',
    [string]$Label = 'final',
    [switch]$SkipProbeBuild
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools = Join-Path $workspace 'Shared\tools\automation'
if (-not $SkipProbeBuild) {
    dotnet build (Join-Path $PSScriptRoot 'Probe.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Skill-level refresh probe build failed' }
}
$runs = @(& "$tools\list-runs.ps1" -Json -Detailed | ConvertFrom-Json)
$runs | Select-Object runId, owner, purpose, state, inUse | ConvertTo-Json -Depth 3
if (@($runs | Where-Object { $_.inUse }).Count) { throw 'Timing verification requires the managed game slot to be idle.' }
$run = & "$tools\create-run.ps1" -Purpose "WorkRoles WR28 native skill gain/loss, lazy UI refresh and ordinary-XP timing ($Mode)" -ModSourceRoot ([IO.Path]::GetFullPath($ModSourceRoot)) -ModSet workroles-skill-level-refresh -AdditionalModSource @("$PSScriptRoot\Probe")
$out = Join-Path $workspace "WorkRoles\temp\skill-level-refresh\$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $run.profile 'skill-mode.txt'), $Mode)
[ordered]@{mode=$Mode; modSourceRoot=$ModSourceRoot; probeSha256=(Get-FileHash "$PSScriptRoot\Probe\1.7\Assemblies\WorkRoles.SkillLevelRefreshProbe.dll").Hash} | ConvertTo-Json | Set-Content (Join-Path $out 'comparison.json')
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(50)
    $lastCapture = ''
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        $stagePath = Join-Path $run.profile 'skill-stage.txt'
        $stage = if (Test-Path -LiteralPath $stagePath) { (Get-Content -LiteralPath $stagePath -Raw).Trim() } else { '' }
        if ($stage.StartsWith('FAILED')) { throw $stage }
        if ($stage.StartsWith('SUCCESS') -or ($Mode -eq 'baseline' -and $stage.StartsWith('RED EXPECTED'))) { break }
        if ($stage.StartsWith('CAPTURE ')) {
            $name = $stage.Substring(8)
            if ($name -ne $lastCapture) {
                & "$tools\capture.ps1" -RunId $run.runId -OutName "$name.png"
                [IO.File]::WriteAllText((Join-Path $run.profile "skill-captured-$name.txt"), 'captured')
                $lastCapture = $name
            }
        }
        if ([DateTime]::UtcNow -ge $deadline) { throw "Skill-level refresh native verification timeout at $stage" }
        Start-Sleep -Milliseconds 100
    } while ($true)
} finally {
    & "$tools\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log','run.json','launch.json','host.json','skill-results.txt','skill-stage.txt','skill-xp-metrics.json','skill-xp-samples.csv','skill-transitions.csv','authored-input.xml','Captures') {
        $path = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    & "$tools\remove-run.ps1" -RunId $run.runId
    Write-Output $out
}
Get-Content -LiteralPath (Join-Path $out 'skill-results.txt')
