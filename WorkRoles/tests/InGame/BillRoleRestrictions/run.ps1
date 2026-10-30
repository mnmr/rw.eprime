param([Parameter(Mandatory)][string]$ComplexJobsRoot)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$automation = Join-Path $repo 'Shared\tools\automation'
$run = & "$automation\create-run.ps1" -Purpose 'WR1 regression: select a saved Cook role for a bill after Complex Jobs moves butchering' -ModSet 'workroles-bill-review' -AdditionalModSource @($ComplexJobsRoot, "$PSScriptRoot\Probe")
$out = Join-Path $repo "WorkRoles\temp\bill-role-regression\$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    & "$automation\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$automation\automation-common.ps1" -RunId $run.runId
    function Wait-Stage([string]$expected, [string]$pointName = '') {
        $deadline = [DateTime]::UtcNow.AddSeconds(8)
        do {
            Invoke-SharedGameCommand @{command='status'} | Out-Null
            $stagePath = Join-Path $run.profile 'bill-review-stage.txt'
            $stage = if (Test-Path -LiteralPath $stagePath) { (Get-Content -LiteralPath $stagePath -Raw).Trim() } else { '' }
            if ($stage -eq 'FAILED') { throw (Get-Content (Join-Path $run.profile 'bill-review-results.txt') -Raw) }
            if ($stage -eq $expected -and (!$pointName -or (Test-Path (Join-Path $run.profile "bill-review-$pointName-point.txt")))) { return }
            if ([DateTime]::UtcNow -gt $deadline) { throw "Expected $expected; received $stage" }
            Start-Sleep -Milliseconds 100
        } while ($true)
    }
    function Click-Point([string]$name, [string]$capture) {
        $point = (Get-Content (Join-Path $run.profile "bill-review-$name-point.txt") -Raw).Trim().Split(',')
        & "$automation\click-capture.ps1" -RunId $run.runId -X ([int]$point[0]) -Y ([int]$point[1]) -WaitMilliseconds 750 -OutName $capture
    }
    Wait-Stage 'READY' 'worker'
    Click-Point 'worker' 'worker-dropdown.png'
    Wait-Stage 'READY' 'cook'
    Click-Point 'cook' 'selected-cook.png'
    Wait-Stage 'SUCCESS'
} finally {
    & "$automation\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log','run.json','launch.json','host.json','stall.json','Captures') {
        $path = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    Get-ChildItem -LiteralPath $run.profile -Filter 'bill-review-*' | Copy-Item -Destination $out -Force
    & "$automation\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'bill-review-results.txt')
