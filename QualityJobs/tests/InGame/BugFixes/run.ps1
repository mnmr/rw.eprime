param([switch]$Compact)
$ErrorActionPreference = 'Stop'
$repo = 'D:\Code\RimWorld'
$automation = Join-Path $repo 'Shared\tools\automation'
$purpose = if ($Compact) { 'Quality Jobs compact regressions: native bill/options windows retained across 1.25-to-1-to-1.25 UI-scale transitions' } else { 'Quality Jobs fixes QJ001-QJ012: recorded regressions, native Do-1 completion and ingredient accounting' }
$run = & "$automation\create-run.ps1" -Purpose $purpose -ModSet qualityjobs-bug-review -AdditionalModSource @("$PSScriptRoot\Probe\Mod")
$evidence = Join-Path $repo "QualityJobs\temp\bug-fixes\evidence\$($run.runId)"
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$run.runId | Set-Content -LiteralPath (Join-Path $evidence 'run-id.txt')
try {
    if ($Compact) {
        $prefsPath = Join-Path $run.profile 'Config\Prefs.xml'
        [xml]$prefs = Get-Content -LiteralPath $prefsPath -Raw
        $prefs.PrefsData.screenWidth = '1280'
        $prefs.PrefsData.screenHeight = '960'
        $prefs.PrefsData.uiScale = '1.25'
        $prefs.Save($prefsPath)
        'true' | Set-Content -LiteralPath (Join-Path $run.profile 'review-ui-only.txt')
        'true' | Set-Content -LiteralPath (Join-Path $run.profile 'review-ui-scale-transitions.txt')
    }
    & "$automation\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$automation\automation-common.ps1" -RunId $run.runId
    # Keep the complete active batch inside the shared launcher's 60-second
    # hard cap, with margin for stopping and the final capture command.
    $activeDeadline = [DateTime]::UtcNow.AddSeconds(53)
    $stagePath = Join-Path $run.profile 'review-stage.txt'
    function Assert-ActiveDeadline {
        if ([DateTime]::UtcNow -gt $activeDeadline) { throw 'Probe active batch exceeded its 53-second deadline.' }
    }
    function Wait-ProbeStage([string]$Expected, [int]$TimeoutSeconds = 7) {
        $stageDeadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        if ($Compact) {
            # A stage owns one real hover-leave input, clearing tooltip state
            # before its stable capture. Status polling alone is not progress
            # for a force-paused dialog under the shared ten-second guard.
            Invoke-SharedGameCommand @{command='status'} | Out-Null
            Invoke-SharedGameCommand @{command='hover'; x=1; y=1} | Out-Null
        }
        do {
            Assert-ActiveDeadline
            Invoke-SharedGameCommand @{command='status'} | Out-Null
            $stage = if (Test-Path -LiteralPath $stagePath) { (Get-Content -LiteralPath $stagePath -Raw).Trim() } else { '' }
            if ($stage -eq $Expected) { return }
            if ($stage -eq 'UI-SCALE-FAILED') {
                & "$automation\capture.ps1" -RunId $run.runId -OutName 'ui-scale-failed.png' -KeepCursor
                throw 'Native UI-scale transition failed; see probe evidence and ui-scale-failed.png.'
            }
            if ([DateTime]::UtcNow -gt $stageDeadline) { throw "Probe stage timeout: expected $Expected, observed $stage" }
            Start-Sleep -Milliseconds 150
        } while ($true)
    }
    $initialStage = if ($Compact) { 'BILL-125' } else { 'READY' }
    Wait-ProbeStage -Expected $initialStage -TimeoutSeconds 53
    Assert-ActiveDeadline
    & "$automation\capture.ps1" -RunId $run.runId -OutName 'bill-review.png' -KeepCursor
    if ($Compact) {
        # The probe waits for native resize notification and two successful
        # repaint passes, then holds that exact window/scale until this capture
        # acknowledgement. Each native transition has a five-second probe
        # timeout and seven-second script timeout; capture acknowledgement has
        # eight seconds. One hover-leave input begins each script stage.
        $ackPath = Join-Path $run.profile 'review-capture-ack.txt'
        function Confirm-ProbeCapture([string]$CapturedStage) {
            $CapturedStage | Set-Content -LiteralPath $ackPath
            # Unique existence markers avoid reading while the script rewrites
            # the last-stage summary. The capture has already completed here.
            'captured' | Set-Content -LiteralPath (Join-Path $run.profile "review-captured-$CapturedStage.txt")
        }
        Confirm-ProbeCapture -CapturedStage 'BILL-125'
        $captures = @(
            @{ Stage = 'BILL-100'; Name = 'bill-review-scale-100.png' }
            @{ Stage = 'BILL-125-RESTORED'; Name = 'bill-review-scale-125-restored.png' }
            @{ Stage = 'OPTIONS-125'; Name = 'options-review.png' }
            @{ Stage = 'OPTIONS-100'; Name = 'options-review-scale-100.png' }
            @{ Stage = 'OPTIONS-125-RESTORED'; Name = 'options-review-scale-125-restored.png' }
        )
        foreach ($capture in $captures) {
            Wait-ProbeStage -Expected $capture.Stage
            Assert-ActiveDeadline
            & "$automation\capture.ps1" -RunId $run.runId -OutName $capture.Name -KeepCursor
            Confirm-ProbeCapture -CapturedStage $capture.Stage
        }
        Wait-ProbeStage -Expected 'UI-SCALE-COMPLETE'
    }
} finally {
    & "$automation\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log','run.json','launch.json','host.json','Captures','review-results.txt','review-stage.txt','review-capture-ack.txt','stall.json','hard-cap.json') {
        $source = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $evidence -Recurse -Force }
    }
    $save = Join-Path $run.profile 'Saves\QualityJobsBugReview-MinifiedBill.rws'
    if (Test-Path -LiteralPath $save) { Copy-Item -LiteralPath $save -Destination $evidence }
    & "$automation\remove-run.ps1" -RunId $run.runId
}
Write-Output $evidence
Get-Content -LiteralPath (Join-Path $evidence 'review-results.txt')
$results = Get-Content -LiteralPath (Join-Path $evidence 'review-results.txt') -Raw
if ($results -match 'passed=False|ERROR|TIMEOUT') { throw 'Regression probe failed; see retained evidence.' }
