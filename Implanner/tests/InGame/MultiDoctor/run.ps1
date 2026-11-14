param([string]$Label = 'final', [switch]$Control)
# -Control runs the same probe without Multi-Doctor Surgery: the surgery-team
# option is locked to Solo and a saved team mode changes nothing.
$ErrorActionPreference = 'Stop'
$tools = 'D:\Code\RimWorld\Shared\tools\automation'
$modSet = if ($Control) { 'implanner-multidoctor-control' } else { 'implanner-multidoctor' }
$purpose = if ($Control) { 'Implanner without Multi-Doctor Surgery: surgery teams locked to Solo, plain eye operation' } else { 'Implanner + Multi-Doctor Surgery: solo, best-team and default-team eye operations completed by the game doctor job' }
$run = & "$tools\create-run.ps1" -Purpose $purpose -ModSet $modSet -GameVersion 1.7 -SavePattern 'Fisso-NAM-1.7.rws' -AdditionalModSource "$PSScriptRoot\Probe"
$out = Join-Path 'D:/Code/RimWorld/Implanner/temp/multidoctor' "$Label-$($run.runId)"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$stage = ''
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(58)
    $request = Join-Path $run.profile 'multidoctor-capture.txt'
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
        $file = Join-Path $run.profile 'multidoctor-stage.txt'
        if (Test-Path $file) { $stage = (Get-Content $file -Raw).Trim(); break }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Multi-Doctor scenario timed out' }
        Start-Sleep -Milliseconds 150
    } while ($true)
} finally {
    try { & "$tools\stop.ps1" -RunId $run.runId } catch { Write-Warning $_ }
    foreach ($name in 'Player.log','Player.log.host-error.txt','run.json','launch.json','host.json','multidoctor-stage.txt','multidoctor.txt','stall.json','hard-cap.json') {
        $path = Join-Path $run.profile $name
        if (Test-Path $path) { Copy-Item -LiteralPath $path -Destination $out -Force }
    }
    $captures = Join-Path $run.profile 'Captures'
    if (Test-Path $captures) { Copy-Item -Path (Join-Path $captures '*.png') -Destination $out -Force }
    & "$tools\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'multidoctor.txt')
if ($stage -ne 'SUCCESS') { throw "Scenario: $stage" }
