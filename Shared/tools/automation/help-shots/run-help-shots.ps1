# Help image capture for one mod: one managed run on the local 1.7 copy that
# walks every scene of the mod's capture probe (<Mod>\tests\InGame\HelpShots,
# built on the shared source in Shared\Tests\InGame\HelpShots) and saves a
# full frame per scene plus a manifest of the named regions in it
# (help-shots-manifest.jsonl) under <Mod>\temp\help-shots\run-<id>.
# crop-help.py cuts the help images from that folder.
#
# The game multiplies the UI scale preference by the desktop's DPI factor
# (the automation desktop is not at 96 dpi), so the probe sets the effective
# scale itself and fails the run if the frame is not at exactly -UiScale.
# The owner allows this run more than the usual 60 s after game-ready, so one
# run covers every image of a mod (-HardCapSeconds).
param([Parameter(Mandatory)][string]$Mod, [Parameter(Mandatory)][string]$ModSet,
    [string]$SavePattern = 'Fisso-NAM-1.7.rws',
    [int]$Width = 1920, [int]$Height = 1080, [double]$UiScale = 1.0, [int]$HardCapSeconds = 180)
$ErrorActionPreference = 'Stop'
$tools = Split-Path -Parent $PSScriptRoot
$source = [IO.Path]::GetFullPath((Join-Path $tools '..\..\..'))
$probe = Join-Path $source "$Mod\tests\InGame\HelpShots\Probe"
$run = & "$tools\create-run.ps1" -Purpose "$Mod help images at ${Width}x${Height} UI scale $UiScale" -ModSet $ModSet -GameVersion 1.7 -SavePattern $SavePattern -AdditionalModSource $probe
$prefsPath = Join-Path $run.profile 'Config\Prefs.xml'
[xml]$prefs = Get-Content -LiteralPath $prefsPath -Raw
$prefs.PrefsData.screenWidth = "$Width"
$prefs.PrefsData.screenHeight = "$Height"
$prefs.PrefsData.uiScale = $UiScale.ToString([Globalization.CultureInfo]::InvariantCulture)
$prefs.Save($prefsPath)
$UiScale.ToString([Globalization.CultureInfo]::InvariantCulture) | Set-Content -LiteralPath (Join-Path $run.profile 'help-shots-scale.txt')
# The probe picks its scenes by mod set when a mod needs more than one run.
$ModSet | Set-Content -LiteralPath (Join-Path $run.profile 'help-shots-variant.txt')
$out = Join-Path $source "$Mod\temp\help-shots\run-$($run.runId)"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$stage = ''
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -HardCapSeconds $HardCapSeconds -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds($HardCapSeconds - 5)
    $hoverFile = Join-Path $run.profile 'help-shots-hover.txt'
    $captureFile = Join-Path $run.profile 'help-shots-capture.txt'
    $stageFile = Join-Path $run.profile 'help-shots-stage.txt'
    $hovered = 0; $captured = 0
    # The pointer starts at the baseline frame's centre, outside a smaller frame.
    # A mod may open a dialog as the save loads (the probe closes it); status
    # acknowledges it so the first input is not refused as a surprise popup.
    for ($try = 0; ; $try++) {
        try {
            Invoke-SharedGameCommand @{command='status'} | Out-Null
            Invoke-SharedGameCommand @{command='hover'; x=5; y=5} | Out-Null
            break
        } catch { if ($try -ge 5) { throw }; Start-Sleep -Milliseconds 200 }
    }
    function Read-Request([string]$Path) {
        if (-not (Test-Path $Path)) { return $null }
        try { return (Get-Content $Path -Raw).Trim().Split(' ', 4) } catch { return $null }
    }
    # The probe polls the ack files, so a write can meet its open handle.
    function Write-Ack([string]$Name, [int]$Value) {
        for ($try = 0; ; $try++) {
            try { "$Value" | Set-Content (Join-Path $run.profile $Name) -ErrorAction Stop; return }
            catch { if ($try -ge 20) { throw }; Start-Sleep -Milliseconds 15 }
        }
    }
    do {
        # Each request is served once: a tooltip needs the pointer to rest,
        # so the hover is not repeated. Every scene sends one, which is also
        # the input the stall guard wants from a paused run.
        $hover = Read-Request $hoverFile
        if ($hover -and [int]$hover[0] -gt $hovered) {
            # A scene may have opened a dialog; status acknowledges it so the
            # fail-fast guard does not take it for a surprise popup.
            Invoke-SharedGameCommand @{command='status'} | Out-Null
            $verb = if ($hover.Count -gt 3) { $hover[3] } else { 'hover' }
            $command = @{command=$verb; x=[int]$hover[1]; y=[int]$hover[2]}
            if ($verb -eq 'rclick') { $command.command = 'click'; $command.button = 1 }
            Invoke-SharedGameCommand $command | Out-Null
            $hovered = [int]$hover[0]
            Write-Ack 'help-shots-hover-ack.txt' $hovered
        }
        $capture = Read-Request $captureFile
        if ($capture -and [int]$capture[0] -gt $captured) {
            Save-SharedGameCapture ($capture[1] + '.png') | Out-Null
            $captured = [int]$capture[0]
            Write-Ack 'help-shots-capture-ack.txt' $captured
        } else {
            Invoke-SharedGameCommand @{command='status'} | Out-Null
        }
        if (Test-Path $stageFile) { $stage = (Get-Content $stageFile -Raw).Trim(); break }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Help image capture timed out' }
        Start-Sleep -Milliseconds 40
    } while ($true)
} finally {
    try { & "$tools\stop.ps1" -RunId $run.runId } catch { Write-Warning $_ }
    foreach ($name in 'Player.log','run.json','launch.json','host.json','stall.json','hard-cap.json','help-shots-stage.txt','help-shots.txt','help-shots-manifest.jsonl','help-shots-variant.txt') {
        $path = Join-Path $run.profile $name
        if (Test-Path $path) { Copy-Item -LiteralPath $path -Destination $out -Force }
    }
    $captures = Join-Path $run.profile 'Captures'
    if (Test-Path $captures) { Copy-Item -Path (Join-Path $captures '*.png') -Destination $out -Force }
    & "$tools\remove-run.ps1" -RunId $run.runId
}
if ($stage -ne 'SUCCESS') { throw "Help image capture failed: $stage ($out)" }
Write-Output $out
