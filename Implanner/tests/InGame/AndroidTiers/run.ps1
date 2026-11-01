param()
$ErrorActionPreference = 'Stop'
$tools = 'D:\Code\RimWorld\Shared\tools\automation'
$run = & "$tools\create-run.ps1" -Purpose 'Implanner Android Tiers: per-side android slots, android implant workers, Crafting surgeon floors' -ModSet implanner-android-tiers -GameRoot 'D:\Code\RimWorld\Readouts\temp\linux-validation\game-1.6' -WslDistribution Ubuntu-22.04 -WslRenderer Software -SavePattern Fisso-NAM.rws -BaselineConfigPath "$tools\profile-template\Config\ModsConfig-1.6.xml" -AdditionalModSource "$PSScriptRoot\Probe"
$out = Join-Path 'D:/Code/RimWorld/Implanner/temp/android-tiers' $run.runId
New-Item -ItemType Directory -Force -Path $out | Out-Null
$run.runId | Set-Content (Join-Path $out 'run-id.txt')
$stage = ''
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(58)
    $request = Join-Path $run.profile 'android-tiers-capture.txt'
    $ack = Join-Path $run.profile 'android-tiers-ack.txt'
    $done = 0
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        # The probe asks for a capture of the frame it prepared; answer once.
        if (Test-Path $request) {
            $parts = (Get-Content $request -Raw).Trim().Split(' ', 2)
            if ([int]$parts[0] -gt $done) {
                Save-SharedGameCapture ($parts[1] + '.png') | Out-Null
                $done = [int]$parts[0]
                Set-Content -Path $ack -Value $done -NoNewline
            }
        }
        $file = Join-Path $run.profile 'android-tiers-stage.txt'
        if (Test-Path $file) { $stage = (Get-Content $file -Raw).Trim(); break }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Android Tiers scenario timed out' }
        Start-Sleep -Milliseconds 150
    } while ($true)
} finally {
    # The stall guard may already be stopping the run; keep the evidence anyway.
    try { & "$tools\stop.ps1" -RunId $run.runId } catch { Write-Warning $_ }
    foreach ($name in 'Player.log','Player.log.host-error.txt','run.json','launch.json','host.json','android-tiers-stage.txt','android-tiers.txt','stall.json','hard-cap.json') {
        $path = Join-Path $run.profile $name
        if (Test-Path $path) { Copy-Item -LiteralPath $path -Destination $out -Force }
    }
    $captures = Join-Path $run.profile 'Captures'
    if (Test-Path $captures) { Copy-Item -Path (Join-Path $captures '*.png') -Destination $out -Force }
    & "$tools\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'android-tiers.txt')
if ($stage -ne 'SUCCESS') { throw "Scenario: $stage" }
