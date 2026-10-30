param([string]$Label = 'regression', [switch]$SkipProbeBuild)
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools = Join-Path $source 'Shared\tools\automation'
if (-not $SkipProbeBuild) {
    dotnet build (Join-Path $PSScriptRoot 'Probe.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Training policy probe build failed' }
}
& "$tools\list-runs.ps1" -Json | ConvertFrom-Json | Select-Object runId, owner, purpose, state, inUse
$run = & "$tools\create-run.ps1" -Purpose "WorkRoles WR25 atomic tuning reset and WR27 trainer eligibility plus blocker confirmation clicks ($Label)" -ModSourceRoot $source -ModSet workroles-training-policy -AdditionalModSource @("$PSScriptRoot\Probe")
$out = Join-Path $source "WorkRoles\temp\training-policy\$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(50)
    $lastStage = ''
    do {
        Invoke-SharedGameCommand @{ command = 'status' } | Out-Null
        $stagePath = Join-Path $run.profile 'training-policy-stage.txt'
        $stage = if (Test-Path -LiteralPath $stagePath) { (Get-Content -LiteralPath $stagePath -Raw).Trim() } else { '' }
        if ($stage.StartsWith('FAILED')) { throw $stage }
        if ($stage -eq 'SUCCESS') {
            & "$tools\capture.ps1" -RunId $run.runId -OutName 'complete.png'
            break
        }
        if ($stage -and $stage -ne $lastStage) {
            & "$tools\capture.ps1" -RunId $run.runId -OutName "$stage.png"
            $point = (Get-Content -LiteralPath (Join-Path $run.profile 'training-policy-point.txt') -Raw).Trim().Split(',')
            Invoke-SharedGameCommand @{ command = 'click'; x = [int]$point[0]; y = [int]$point[1] } | Out-Null
            $lastStage = $stage
        }
        if ([DateTime]::UtcNow -ge $deadline) { throw "Training policy timeout at $stage" }
        Start-Sleep -Milliseconds 100
    } while ($true)
} finally {
    & "$tools\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log', 'run.json', 'launch.json', 'host.json', 'training-policy-results.txt', 'training-policy-stage.txt', 'Captures') {
        $path = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    & "$tools\remove-run.ps1" -RunId $run.runId
    Write-Output $out
}
Get-Content -LiteralPath (Join-Path $out 'training-policy-results.txt')
