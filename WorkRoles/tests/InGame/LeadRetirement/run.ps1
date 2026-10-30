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
    if ($LASTEXITCODE -ne 0) { throw 'Lead retirement probe build failed' }
}
$runs = & "$tools\list-runs.ps1" -Json -Detailed | ConvertFrom-Json
$runs | Select-Object runId, owner, purpose, state, inUse | ConvertTo-Json -Depth 3
$run = & "$tools\create-run.ps1" -Purpose "WorkRoles WR26 lead retirement full native Fisso outputs and Recommendations UI ($Mode)" -ModSourceRoot ([IO.Path]::GetFullPath($ModSourceRoot)) -ModSet workroles-lead-retirement -AdditionalModSource @("$PSScriptRoot\Probe")
$out = Join-Path $workspace "WorkRoles\temp\lead-retirement\$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $run.profile 'lead-mode.txt'), $Mode)
[ordered]@{mode=$Mode; modSourceRoot=$ModSourceRoot; probeSha256=(Get-FileHash "$PSScriptRoot\Probe\1.7\Assemblies\WorkRoles.LeadRetirementProbe.dll").Hash} | ConvertTo-Json | Set-Content (Join-Path $out 'comparison.json')
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(50)
    $lastStage = ''
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        $stagePath = Join-Path $run.profile 'lead-stage.txt'
        $stage = if (Test-Path -LiteralPath $stagePath) { (Get-Content -LiteralPath $stagePath -Raw).Trim() } else { '' }
        if ($stage.StartsWith('FAILED')) { throw $stage }
        if ($stage -eq 'SUCCESS') {
            & "$tools\capture.ps1" -RunId $run.runId -OutName 'global-sections-final.png'
            break
        }
        if ($stage -and $stage -ne $lastStage) {
            & "$tools\capture.ps1" -RunId $run.runId -OutName "$stage.png"
            $point = (Get-Content -LiteralPath (Join-Path $run.profile 'lead-point.txt') -Raw).Trim().Split(',')
            Invoke-SharedGameCommand @{command='click'; x=[int]$point[0]; y=[int]$point[1]} | Out-Null
            $lastStage = $stage
        }
        if ([DateTime]::UtcNow -ge $deadline) { throw "Lead retirement native verification timeout at $stage" }
        Start-Sleep -Milliseconds 100
    } while ($true)
} finally {
    & "$tools\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log','run.json','launch.json','host.json','lead-results.txt','lead-outcomes.txt','lead-stage.txt','global-sections-bounds.txt','legacy-options-export.xml','Captures') {
        $path = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    & "$tools\remove-run.ps1" -RunId $run.runId
    Write-Output $out
}
Get-Content -LiteralPath (Join-Path $out 'lead-results.txt')
