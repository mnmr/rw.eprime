$ErrorActionPreference = 'Stop'
$repo = 'D:\Code\RimWorld'
$automation = Join-Path $repo 'Shared\tools\automation'
$run = & "$automation\create-run.ps1" -Purpose 'Quality Jobs Craft with Color: consume native source and finish bill products and compare selected style' -ModSet qualityjobs-cwc-bug-review -AdditionalModSource @("$PSScriptRoot\CwcProbe\Mod") -GameRoot "$repo\Readouts\temp\linux-validation\game-1.6" -WslDistribution 'Ubuntu-22.04' -WslRenderer Software -SavePattern 'Fisso-NAM.rws' -BaselineConfigPath "$automation\profile-template\Config\ModsConfig-1.6.xml"
$evidence = Join-Path $repo "QualityJobs\temp\bug-fixes\evidence\$($run.runId)"
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$run.runId | Set-Content -LiteralPath (Join-Path $evidence 'run-id.txt')
$prefsPath = Join-Path $run.profile 'Config/Prefs.xml'
$prefs = [xml](Get-Content -LiteralPath $prefsPath -Raw)
$prefs.PrefsData.screenWidth = '1024'
$prefs.PrefsData.screenHeight = '768'
$prefs.PrefsData.uiScale = '1'
$prefs.Save($prefsPath)
try {
    & "$automation\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$automation\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(150)
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        $stagePath = Join-Path $run.profile 'cwc-review-stage.txt'
        $stage = if (Test-Path -LiteralPath $stagePath) { (Get-Content -LiteralPath $stagePath -Raw).Trim() } else { '' }
        if ($stage -eq 'DONE') { break }
        if ([DateTime]::UtcNow -gt $deadline) { throw "CWC probe timeout: $stage" }
        Start-Sleep -Milliseconds 150
    } while ($true)
    & "$automation\capture.ps1" -RunId $run.runId -OutName 'cwc-review.png' -KeepCursor
} finally {
    & "$automation\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log','run.json','launch.json','host.json','linux-host.json','linux-console.log','linux-host.log','Captures','cwc-review-results.txt','cwc-review-stage.txt','cwc-style-snapshot.xml','stall.json','hard-cap.json') {
        $source = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $evidence -Recurse -Force }
    }
    & "$automation\remove-run.ps1" -RunId $run.runId
}
Write-Output $evidence
Get-Content -LiteralPath (Join-Path $evidence 'cwc-review-results.txt')
$results = Get-Content -LiteralPath (Join-Path $evidence 'cwc-review-results.txt') -Raw
if ($results -match 'ERROR|passed=False' -or $results -notmatch 'QJ013 passed=True') { throw 'CWC regression failed; see retained evidence.' }
