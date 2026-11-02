param([string]$ModSet = 'implanner-conflicts')
# Conflict rules against the reporter's mods (Archotech Bionics, Integrated
# Implants, FSF bionics; -ModSet implanner-conflicts-bor adds Beyond Our
# Reach). Local 1.6 copy: the mods support 1.5/1.6 only.
$ErrorActionPreference = 'Stop'
$tools = 'D:\Code\RimWorld\Shared\tools\automation'
$run = & "$tools\create-run.ps1" -Purpose "Implanner conflict rules ($ModSet): archotech kidney/liver, archotech skin vs torso implants, tenebral vs vanilla" -ModSet $ModSet -GameVersion 1.6 -SavePattern 'Fisso-NAM.rws' -BaselineConfigPath "$tools\profile-template\Config\ModsConfig-1.6.xml" -AdditionalModSource "$PSScriptRoot\Probe"
$out = Join-Path 'D:/Code/RimWorld/Implanner/temp/conflicts/runs' $run.runId
New-Item -ItemType Directory -Force -Path $out | Out-Null
$stage = ''
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(58)
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        $file = Join-Path $run.profile 'conflicts-stage.txt'
        if (Test-Path $file) { $stage = (Get-Content $file -Raw).Trim(); break }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Conflict probe timed out' }
        Start-Sleep -Milliseconds 150
    } while ($true)
} finally {
    try { & "$tools\stop.ps1" -RunId $run.runId } catch { Write-Warning $_ }
    foreach ($name in 'Player.log','run.json','launch.json','conflicts-stage.txt','conflicts.txt') {
        $path = Join-Path $run.profile $name
        if (Test-Path $path) { Copy-Item -LiteralPath $path -Destination $out -Force }
    }
    & "$tools\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'conflicts.txt')
if ($stage -ne 'SUCCESS') { throw "Scenario: $stage" }
