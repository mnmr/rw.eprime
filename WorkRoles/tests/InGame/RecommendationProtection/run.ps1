param([string]$Label = 'fixed')
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools = Join-Path $source 'Shared\tools\automation'
# Production mods and the shared runtime must already be built. Only the
# probe is compiled here; run creation copies the completed artifacts.
dotnet build (Join-Path $PSScriptRoot 'Probe.csproj') -c Release -p:RimWorldVersion=1.7
if ($LASTEXITCODE -ne 0) { throw 'Recommendation protection probe build failed' }
$run = & "$tools\create-run.ps1" -Purpose "WorkRoles WR2-WR5 composite semantics, protected order and ordinary meal staffing ($Label)" -ModSourceRoot $source -ModSet 'workroles-recommendation-protection' -AdditionalModSource "$PSScriptRoot\Probe"
$out = Join-Path $source "WorkRoles\temp\recommendation-protection\$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$run.runId | Set-Content -LiteralPath (Join-Path $out 'run-id.txt')
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(50)
    do {
        Invoke-SharedGameCommand @{ command = 'status' } | Out-Null
        $stagePath = Join-Path $run.profile 'recommendation-protection-stage.txt'
        $stage = if (Test-Path -LiteralPath $stagePath) { (Get-Content -LiteralPath $stagePath -Raw).Trim() } else { '' }
        if ($stage -eq 'FAILED') {
            & "$tools\capture.ps1" -RunId $run.runId -OutName 'failed.png'
            throw 'Recommendation protection regression failed'
        }
        if ($stage -eq 'SUCCESS') {
            & "$tools\capture.ps1" -RunId $run.runId -OutName 'recommendation-protection.png'
            break
        }
        if ([DateTime]::UtcNow -ge $deadline) { throw "Recommendation protection scenario timeout: $stage" }
        Start-Sleep -Milliseconds 100
    } while ($true)
} finally {
    & "$tools\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log','run.json','launch.json','host.json','recommendation-protection-results.txt','recommendation-protection-stage.txt','Captures') {
        $path = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    & "$tools\remove-run.ps1" -RunId $run.runId
    Write-Output $out
}
$result = Get-Content -LiteralPath (Join-Path $out 'recommendation-protection-results.txt') -Raw
Write-Output $result
if ($result -notmatch 'COMPLETE WR2 WR3 WR4 WR5' -or $result -match 'FAIL ') { throw "Recommendation protection regression failed: $out" }
