param([string]$Label = 'fixed')
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools = Join-Path $source 'Shared\tools\automation'
# Build production and the shared runtime before invoking this single-use run.
dotnet build (Join-Path $PSScriptRoot 'Probe.csproj') -c Release -p:RimWorldVersion=1.7
if ($LASTEXITCODE -ne 0) { throw 'Mechanical exclusion probe build failed' }
$run = & "$tools\create-run.ps1" -Purpose "WorkRoles WR11 native mechanical exclusions through actual preview and Apply ($Label)" -ModSourceRoot $source -ModSet 'workroles-mechanical-exclusions' -AdditionalModSource "$PSScriptRoot\Probe"
$out = Join-Path $source "WorkRoles\temp\mechanical-exclusions\$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$run.runId | Set-Content -LiteralPath (Join-Path $out 'run-id.txt')
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(50)
    do {
        Invoke-SharedGameCommand @{ command = 'status' } | Out-Null
        $stagePath = Join-Path $run.profile 'mechanical-exclusion-stage.txt'
        $stage = if (Test-Path -LiteralPath $stagePath) { (Get-Content -LiteralPath $stagePath -Raw).Trim() } else { '' }
        if ($stage -eq 'FAILED') {
            & "$tools\capture.ps1" -RunId $run.runId -OutName 'failed.png'
            throw 'Mechanical exclusion regression failed'
        }
        if ($stage -eq 'READY') {
            & "$tools\capture.ps1" -RunId $run.runId -OutName 'mechanical-exclusions.png'
            break
        }
        if ([DateTime]::UtcNow -ge $deadline) { throw "Mechanical exclusion scenario timeout: $stage" }
        Start-Sleep -Milliseconds 100
    } while ($true)
} finally {
    & "$tools\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log','run.json','launch.json','host.json','mechanical-exclusion-results.txt','mechanical-exclusion-stage.txt','Captures') {
        $path = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    & "$tools\remove-run.ps1" -RunId $run.runId
    Write-Output $out
}
$result = Get-Content -LiteralPath (Join-Path $out 'mechanical-exclusion-results.txt') -Raw
Write-Output $result
if ($result -notmatch 'COMPLETE WR11' -or $result -match 'ERROR ') { throw "Mechanical exclusion regression failed: $out" }
