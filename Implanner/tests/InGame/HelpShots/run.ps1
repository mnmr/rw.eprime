param()
# Help screenshots: 1920x1080 at UI scale 1.0 (the run's prepared
# preferences are changed before launch, as a display-metric test may).
# The 1.7 runtime clamps UI scale to its design floor (0.9375 at this
# resolution), so the local 1.6 copy is used; the UI code is the same.
$ErrorActionPreference = 'Stop'
$tools = 'D:\Code\RimWorld\Shared\tools\automation'
$run = & "$tools\create-run.ps1" -Purpose 'Implanner help screenshots: body-typed plans and body filter at UI scale 1.0' -ModSet implanner-help-shots -GameVersion 1.6 -SavePattern 'Fisso-NAM.rws' -BaselineConfigPath "$tools\profile-template\Config\ModsConfig-1.6.xml" -AdditionalModSource "$PSScriptRoot\Probe"
$prefsPath = Join-Path $run.profile 'Config\Prefs.xml'
[xml]$prefs = Get-Content -LiteralPath $prefsPath -Raw
$prefs.PrefsData.uiScale = '1'
$prefs.Save($prefsPath)
$out = Join-Path 'D:/Code/RimWorld/Implanner/temp/help-shots' $run.runId
New-Item -ItemType Directory -Force -Path $out | Out-Null
$stage = ''
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(58)
    $request = Join-Path $run.profile 'help-shots-capture.txt'
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
        $file = Join-Path $run.profile 'help-shots-stage.txt'
        if (Test-Path $file) { $stage = (Get-Content $file -Raw).Trim(); break }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Help shots timed out' }
        Start-Sleep -Milliseconds 150
    } while ($true)
} finally {
    try { & "$tools\stop.ps1" -RunId $run.runId } catch { Write-Warning $_ }
    foreach ($name in 'Player.log','run.json','launch.json','help-shots-stage.txt','help-shots.txt') {
        $path = Join-Path $run.profile $name
        if (Test-Path $path) { Copy-Item -LiteralPath $path -Destination $out -Force }
    }
    $captures = Join-Path $run.profile 'Captures'
    if (Test-Path $captures) { Copy-Item -Path (Join-Path $captures '*.png') -Destination $out -Force }
    & "$tools\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'help-shots.txt')
if ($stage -ne 'SUCCESS') { throw "Scenario: $stage" }
