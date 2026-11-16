# Default: the full marker sequence with real clicks (tooltip, right-click
# menu) at the baseline 1920x1080 @1.25; its click coordinates are only valid
# there.
# -PreviewsOnly: just the Options previews under both palette groupings and
# the wrapped traits/recommendations panel.
# -Tour: every display state without clicks (so it works at any resolution
# and UI scale): markers dots/icons/off, Options, remove icon off,
# recommendations off (Colonists and Options), Groups palette, the Options
# previews under both groupings and the wrapped panel.
# -Width/-Height/-UiScale change this run's own prepared preferences.
# -GridClicks: fixed-width chips; clicks the first chip of the first row
# through its states (off, always on, on) and captures each, so the chips to
# its right can be compared for movement (baseline 1920x1080 @1.25 only).
param([string]$Label = 'markers', [switch]$PreviewsOnly, [switch]$Tour, [switch]$GridClicks,
    [int]$Width = 1920, [int]$Height = 1080, [double]$UiScale = 1.25)
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools = Join-Path $source 'Shared\tools\automation'
$run = & "$tools\create-run.ps1" -Purpose "WorkRoles chip markers and display options at ${Width}x${Height} @$UiScale" -ModSet workroles-chip-markers -GameVersion 1.7 -SavePattern 'Fisso-NAM-1.7.rws' -AdditionalModSource @("$PSScriptRoot\Probe")
$out = Join-Path $source "WorkRoles\temp\chip-markers\$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    $prefsPath = Join-Path $run.profile 'Config\Prefs.xml'
    [xml]$prefs = Get-Content -LiteralPath $prefsPath -Raw
    $prefs.PrefsData.screenWidth = "$Width"
    $prefs.PrefsData.screenHeight = "$Height"
    $prefs.PrefsData.uiScale = $UiScale.ToString([Globalization.CultureInfo]::InvariantCulture)
    $prefs.Save($prefsPath)
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $stageFile = Join-Path $run.profile 'markers-stage.txt'
    $deadline = [DateTime]::UtcNow.AddSeconds(58)
    function Wait-Stage([string]$Expected) {
        do {
            Invoke-SharedGameCommand @{command='status'} | Out-Null
            $stage = if (Test-Path $stageFile) { (Get-Content $stageFile -Raw).Trim() } else { '' }
            if ($stage.StartsWith('FAILED')) { throw $stage }
            if ($stage -eq $Expected) { return }
            if ([DateTime]::UtcNow -gt $deadline) { throw "Expected stage $Expected; got $stage" }
            Start-Sleep -Milliseconds 100
        } while ($true)
    }
    function Capture([string]$Name) { & "$tools\capture.ps1" -RunId $run.runId -OutName "$Name.png" | Out-Null }
    # A stage without clicks: the hover is input for the stall guard (which
    # stops a paused run 10 s after its last input) and rests on empty chrome.
    function Step([string]$Go, [string]$Stage) {
        Invoke-SharedGameCommand @{command='hover'; x=5; y=5} | Out-Null
        $Go | Set-Content $stageFile
        Wait-Stage $Stage; Capture $Stage
    }
    # The virtual pointer starts at the baseline frame's centre, which lies
    # outside a smaller frame and fails the first capture.
    Invoke-SharedGameCommand @{command='hover'; x=5; y=5} | Out-Null
    Wait-Stage 'dots'; Capture 'dots'
    if ($GridClicks) {
        'go-grid' | Set-Content $stageFile
        Wait-Stage 'grid'; Capture 'grid'
        foreach ($n in 1..3) {
            Invoke-SharedGameCommand @{command='click'; x=400; y=431} | Out-Null
            Start-Sleep -Milliseconds 200; Capture "grid-click$n"
        }
    } elseif ($PreviewsOnly) {
        'skip-previews' | Set-Content $stageFile
        Wait-Stage 'groups'
    } elseif ($Tour) {
        Step 'go-icons' 'icons'
        Step 'go-off' 'off'
        Step 'go-dots' 'dots-again'
        Step 'go-options' 'options'
        Step 'go-noicon' 'noicon'
        Step 'go-norecs' 'norecs'
        Step 'go-norecs-options' 'norecs-options'
        Step 'go-recs' 'recs'
        Step 'go-groups' 'groups'
    } else {
        # A click ends the chip's open tip; it reopens showing the new state.
        & "$tools\capture-hover.ps1" -RunId $run.runId -X 400 -Y 431 -OutName 'tip-before.png'
        Invoke-SharedGameCommand @{command='click'; x=400; y=431} | Out-Null
        Start-Sleep -Milliseconds 150; Capture 'tip-clicked'
        Start-Sleep -Milliseconds 700; Capture 'tip-after'
        'go-icons' | Set-Content $stageFile
        Wait-Stage 'icons'; Capture 'icons'
        'go-off' | Set-Content $stageFile
        Wait-Stage 'off'; Capture 'off'
        'go-dots' | Set-Content $stageFile
        Wait-Stage 'dots-again'; Capture 'dots-again'
        'go-options' | Set-Content $stageFile
        Wait-Stage 'options'; Capture 'options'
        'go-noicon' | Set-Content $stageFile
        Wait-Stage 'noicon'; Capture 'noicon'
        # First chip of the first colonist row (physical pixels, 1920x1080 @1.25).
        Invoke-SharedGameCommand @{command='click'; x=400; y=431; button=1} | Out-Null
        Wait-Stage 'menu'; & "$tools\capture.ps1" -RunId $run.runId -OutName 'menu.png' -KeepCursor | Out-Null
        'go-remove' | Set-Content $stageFile
        Wait-Stage 'removed'; Capture 'removed'
        'go-norecs' | Set-Content $stageFile
        Wait-Stage 'norecs'; Capture 'norecs'
        'go-norecs-options' | Set-Content $stageFile
        Wait-Stage 'norecs-options'; Capture 'norecs-options'
        'go-recs' | Set-Content $stageFile
        Wait-Stage 'recs'; Capture 'recs'
        'go-groups' | Set-Content $stageFile
        Wait-Stage 'groups'; Capture 'groups'
    }
    if ($PreviewsOnly -or $Tour) {
        Step 'go-groups-options' 'groups-options'
        Step 'go-skills-options' 'skills-options'
        Step 'go-wrap' 'wrap'
    }
    'go-done' | Set-Content $stageFile
    Wait-Stage 'SUCCESS'
} finally {
    try { & "$tools\stop.ps1" -RunId $run.runId } catch { Write-Warning $_ }
    foreach ($name in 'Player.log','Player.log.host-error.txt','run.json','launch.json','host.json','stall.json','hard-cap.json','Captures','markers-stage.txt','chip-markers.txt') {
        $path = Join-Path $run.profile $name
        if (Test-Path $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    & "$tools\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'chip-markers.txt')
if ((Get-Content (Join-Path $out 'markers-stage.txt') -Raw).Trim() -ne 'SUCCESS') { throw "Scenario did not pass: $out" }
