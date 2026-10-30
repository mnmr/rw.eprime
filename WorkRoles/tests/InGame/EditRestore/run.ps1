$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools = Join-Path $source 'Shared\tools\automation'
$run = & "$tools\create-run.ps1" -Purpose 'WorkRoles WR6-WR10: named palette swaps, duplicated native training, one-pass selective restore, executable Doctor work and blocker coverage' -ModSourceRoot $source -ModSet 'workroles-review-persistence' -AdditionalModSource "$PSScriptRoot\Probe"
$out = Join-Path $source "WorkRoles\temp\edit-restore\$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    function Wait-Stage([string]$Expected) {
        $deadline = [DateTime]::UtcNow.AddSeconds(60)
        do {
            Invoke-SharedGameCommand @{ command = 'status' } | Out-Null
            $path = Join-Path $run.profile 'persistence-review-stage.txt'
            $stage = if (Test-Path -LiteralPath $path) { (Get-Content -LiteralPath $path -Raw).Trim() } else { '' }
            if ($stage.StartsWith('FAILED')) { throw $stage }
            if ($stage -eq $Expected) { return }
            if ([DateTime]::UtcNow -gt $deadline) { throw "Expected $Expected; got $stage" }
            Start-Sleep -Milliseconds 100
        } while ($true)
    }
    Wait-Stage 'restore-preview'
    & "$tools\capture.ps1" -RunId $run.runId -OutName 'missing-owner-training-preview.png'
    'continue' | Set-Content -LiteralPath (Join-Path $run.profile 'persistence-review-continue.txt')
    Wait-Stage 'SUCCESS'
} finally {
    & "$tools\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log', 'run.json', 'launch.json', 'host.json', 'Captures') {
        $path = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    Get-ChildItem -LiteralPath $run.profile -Filter 'persistence-review-*' | Copy-Item -Destination $out -Force
    & "$tools\remove-run.ps1" -RunId $run.runId
}
Get-Content -LiteralPath (Join-Path $out 'persistence-review-results.txt')
if ((Get-Content -LiteralPath (Join-Path $out 'persistence-review-stage.txt') -Raw).Trim() -ne 'SUCCESS') { throw "Scenario did not pass: $out" }
Write-Output $out
