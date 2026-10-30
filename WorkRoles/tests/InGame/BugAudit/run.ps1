param([string]$Label = 'audit')
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools = Join-Path $source 'Shared\tools\automation'
dotnet build (Join-Path $PSScriptRoot 'Probe.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Bug audit probe build failed' }
$run = & "$tools\create-run.ps1" -Purpose "WorkRoles native cross-colonist pinned assignment drag audit ($Label)" -ModSourceRoot $source -ModSet workroles-bug-audit -AdditionalModSource @("$PSScriptRoot\Probe")
$out = Join-Path $source "WorkRoles\temp\bug-audit\$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(50)
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        $stagePath = Join-Path $run.profile 'bug-audit-stage.txt'
        $stage = if (Test-Path -LiteralPath $stagePath) { (Get-Content -LiteralPath $stagePath -Raw).Trim() } else { '' }
        if ($stage.StartsWith('FAILED')) { throw $stage }
        if ($stage.StartsWith('SUCCESS')) {
            & "$tools\capture.ps1" -RunId $run.runId -OutName 'audit-result.png'
            break
        }
        if ([DateTime]::UtcNow -ge $deadline) { throw "Bug audit timeout: $stage" }
        Start-Sleep -Milliseconds 100
    } while ($true)
} finally {
    & "$tools\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log','run.json','launch.json','host.json','bug-audit-results.txt','bug-audit-stage.txt','Captures') {
        $path = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    & "$tools\remove-run.ps1" -RunId $run.runId
    Write-Output $out
}
Get-Content -LiteralPath (Join-Path $out 'bug-audit-results.txt')
