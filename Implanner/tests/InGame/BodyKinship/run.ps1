param()
# Body kinship: the probe's human-copy race, Milira and Android Tiers
# against the catalog, acceptance, AssignPlan, evaluation and body lists.
$ErrorActionPreference = 'Stop'
$tools = 'D:\Code\RimWorld\Shared\tools\automation'
$run = & "$tools\create-run.ps1" -Purpose 'Implanner body kinship: human-copy race folds under human, Milira joins human plans and stays listed, androids stay apart' -ModSet implanner-body-kinship -GameVersion 1.6 -SavePattern 'Fisso-NAM.rws' -BaselineConfigPath "$tools\profile-template\Config\ModsConfig-1.6.xml" -AdditionalModSource "$PSScriptRoot\Probe"
$out = Join-Path 'D:/Code/RimWorld/Implanner/temp/body-kinship' $run.runId
New-Item -ItemType Directory -Force -Path $out | Out-Null
$stage = ''
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(58)
    $request = Join-Path $run.profile 'body-kinship-capture.txt'
    $done = 0
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        if (Test-Path $request) {
            $parts = (Get-Content $request -Raw).Trim().Split(' ', 2)
            if ([int]$parts[0] -gt $done) {
                Save-SharedGameCapture ($parts[1] + '.png') | Out-Null
                $done = [int]$parts[0]
            }
        }
        $file = Join-Path $run.profile 'body-kinship-stage.txt'
        if (Test-Path $file) { $stage = (Get-Content $file -Raw).Trim(); break }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Body kinship probe timed out' }
        Start-Sleep -Milliseconds 150
    } while ($true)
} finally {
    try { & "$tools\stop.ps1" -RunId $run.runId } catch { Write-Warning $_ }
    foreach ($name in 'Player.log','run.json','launch.json','body-kinship-stage.txt','body-kinship.txt') {
        $path = Join-Path $run.profile $name
        if (Test-Path $path) { Copy-Item -LiteralPath $path -Destination $out -Force }
    }
    $captures = Join-Path $run.profile 'Captures'
    if (Test-Path $captures) { Copy-Item -Path (Join-Path $captures '*.png') -Destination $out -Force }
    & "$tools\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'body-kinship.txt')
if ($stage -ne 'SUCCESS') { throw "Scenario: $stage" }
