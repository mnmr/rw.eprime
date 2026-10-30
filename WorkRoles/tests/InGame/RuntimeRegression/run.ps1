param([string]$Label = 'fixed')
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$automation = Join-Path $source 'Shared\tools\automation'
dotnet build (Join-Path $PSScriptRoot 'Probe.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Runtime regression probe build failed' }
$existingRuns = & "$automation\list-runs.ps1" -Json -Detailed | ConvertFrom-Json
$existingRuns | Select-Object runId, owner, purpose, state, inUse | ConvertTo-Json -Depth 3
$run = & "$automation\create-run.ps1" -Purpose 'WorkRoles WR23 native bill membership, WR24 attached cave Settle and WR29 warm ghoul roster regressions' -ModSourceRoot $source -ModSet 'workroles-runtime-authority' -AdditionalModSource @("$PSScriptRoot\Probe")
$out = Join-Path $source "WorkRoles\temp\runtime-regression\$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    & "$automation\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$automation\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(50)
    $results = Join-Path $run.profile 'runtime-audit-results.txt'
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        $observed = if (Test-Path -LiteralPath $results) { Get-Content -LiteralPath $results -Raw } else { '' }
        if ($observed -match 'FAILED') { throw $observed }
        $stagePath = Join-Path $run.profile 'runtime-regression-stage.txt'
        $captureAck = Join-Path $run.profile 'runtime-ghoul-captured.txt'
        if ((Test-Path -LiteralPath $stagePath) -and !(Test-Path -LiteralPath $captureAck)) {
            & "$automation\capture.ps1" -RunId $run.runId -OutName 'ghoul-removed.png'
            [IO.File]::WriteAllText($captureAck, 'captured')
        }
        if ($observed -match 'COMPLETE') {
            if ($observed -notmatch 'SUCCESS WR23 WR24 WR29') { throw 'Runtime regressions did not reach success' }
            & "$automation\capture.ps1" -RunId $run.runId -OutName 'runtime-regression-result.png'
            break
        }
        if ([DateTime]::UtcNow -ge $deadline) { throw 'Runtime regressions exceeded 50-second deadline' }
        Start-Sleep -Milliseconds 200
    } while ($true)
} finally {
    & "$automation\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log','run.json','launch.json','host.json','stall.json','runtime-audit-results.txt','Captures') {
        $path = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    & "$automation\remove-run.ps1" -RunId $run.runId
    Write-Output $out
}
Get-Content -LiteralPath (Join-Path $out 'runtime-audit-results.txt')
