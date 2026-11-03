param([string]$Label = 'final')
$ErrorActionPreference = 'Stop'
$tools = 'D:\Code\RimWorld\Shared\tools\automation'
$run = & "$tools\create-run.ps1" -Purpose 'WorkRoles and Quality Jobs with Smart Work Mode: SmartWork mechs count as workers' -ModSet workroles-smart-work-mode -GameVersion 1.7 -SavePattern 'Fisso-NAM-1.7.rws' -AdditionalModSource @("$PSScriptRoot\Probe")
$out = Join-Path 'D:/Code/RimWorld/WorkRoles/temp/smart-work-mode' "$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$stage = ''
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(55)
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        $stagePath = Join-Path $run.profile 'smart-work-stage.txt'
        if (Test-Path -LiteralPath $stagePath) { $stage = (Get-Content -LiteralPath $stagePath -Raw).Trim(); break }
        if ([DateTime]::UtcNow -ge $deadline) { throw 'Smart Work Mode scenario timeout' }
        Start-Sleep -Milliseconds 150
    } while ($true)
} finally {
    & "$tools\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log','run.json','launch.json','host.json','smart-work.txt','smart-work-stage.txt') {
        $path = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    & "$tools\remove-run.ps1" -RunId $run.runId
    Write-Output $out
}
Get-Content -LiteralPath (Join-Path $out 'smart-work.txt')
if ($stage -ne 'SUCCESS') { throw "Scenario: $stage" }
