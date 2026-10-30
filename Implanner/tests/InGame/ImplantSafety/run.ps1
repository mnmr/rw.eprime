param([switch]$ExpectFailure)
$ErrorActionPreference = 'Stop'
$tools = 'D:\Code\RimWorld\Shared\tools\automation'
$run = & "$tools\create-run.ps1" -Purpose 'Implanner IR1-IR3: healing limbs, nonmonotonic quality and native surgery final safety' -ModSet implanner-implant-safety -GameRoot 'D:\Code\RimWorld\Readouts\temp\linux-validation\game-1.6' -WslDistribution Ubuntu-22.04 -WslRenderer Software -SavePattern Fisso-NAM.rws -BaselineConfigPath "$tools\profile-template\Config\ModsConfig-1.6.xml" -AdditionalModSource "$PSScriptRoot\Probe"
$out = Join-Path 'D:/Code/RimWorld/Implanner/temp/implant-safety' $run.runId
New-Item -ItemType Directory -Force -Path $out | Out-Null
$run.runId | Set-Content (Join-Path $out 'run-id.txt')
$stage = ''
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        $file = Join-Path $run.profile 'implant-safety-stage.txt'
        if (Test-Path $file) { $stage = (Get-Content $file -Raw).Trim(); break }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Native surgery scenario timed out' }
        Start-Sleep -Milliseconds 150
    } while ($true)
} finally {
    & "$tools\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log','run.json','launch.json','host.json','implant-safety-stage.txt','implant-safety.txt','stall.json','hard-cap.json') {
        $path = Join-Path $run.profile $name
        if (Test-Path $path) { Copy-Item -LiteralPath $path -Destination $out -Force }
    }
    & "$tools\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'implant-safety.txt')
if ($ExpectFailure) {
    if (!$stage.StartsWith('FAILED')) { throw 'Expected a failing pre-fix regression' }
} elseif ($stage -ne 'SUCCESS') { throw "Scenario: $stage" }
