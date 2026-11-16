param([string]$Label = 'final', [string]$GameVersion = '1.7', [string]$SavePattern = 'Fisso-NAM-1.7.rws', [switch]$NoNbt)
# The Bills tab of an Implanner bench: its production queue in place of the
# vanilla tab or Nice Bill Tab's layout, the toggle on other benches, and
# stray bills suspended as they are added. -NoNbt runs without Nice Bill Tab.
# Build Probe.csproj first.
$ErrorActionPreference = 'Stop'
$tools = 'D:\Code\RimWorld\Shared\tools\automation'
# 1.6: -GameVersion 1.6 -SavePattern 'Fisso-NAM.rws' (build v16\Probe16.csproj).
$create = @{
    Purpose = "Implanner bench production queue ($GameVersion, $(if ($NoNbt) { 'vanilla bills tab' } else { 'with Nice Bill Tab' })): queue tab, toggle, paused bills"
    ModSet = $(if ($NoNbt) { 'implanner-benchqueue' } else { 'implanner-nicebilltab' }); GameVersion = $GameVersion; SavePattern = $SavePattern
    AdditionalModSource = "$PSScriptRoot\Probe"
}
if ($GameVersion -eq '1.6') { $create.BaselineConfigPath = "$tools\profile-template\Config\ModsConfig-1.6.xml" }
$run = & "$tools\create-run.ps1" @create
$out = Join-Path 'D:/Code/RimWorld/Implanner/temp/nicebilltab' "$Label-$($run.runId)"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$stage = ''
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(58)
    $request = Join-Path $run.profile 'nicebilltab-capture.txt'
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
        $file = Join-Path $run.profile 'nicebilltab-stage.txt'
        if (Test-Path $file) { $stage = (Get-Content $file -Raw).Trim(); break }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Nice Bill Tab scenario timed out' }
        Start-Sleep -Milliseconds 150
    } while ($true)
} finally {
    try { & "$tools\stop.ps1" -RunId $run.runId } catch { Write-Warning $_ }
    foreach ($name in 'Player.log','Player.log.host-error.txt','run.json','launch.json','host.json','nicebilltab-stage.txt','nicebilltab.txt','stall.json','hard-cap.json') {
        $path = Join-Path $run.profile $name
        if (Test-Path $path) { Copy-Item -LiteralPath $path -Destination $out -Force }
    }
    $captures = Join-Path $run.profile 'Captures'
    if (Test-Path $captures) { Copy-Item -Path (Join-Path $captures '*.png') -Destination $out -Force }
    & "$tools\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'nicebilltab.txt')
if ($stage -ne 'SUCCESS') { throw "Scenario: $stage" }
