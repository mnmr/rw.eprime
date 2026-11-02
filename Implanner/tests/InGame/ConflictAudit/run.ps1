param([string]$ModSet = 'implanner-conflict-audit', [string]$Bodies = '', [string]$Shard = '')
# Oracle audit of the planned-slot conflict rule against the game's own
# surgery workers, over every catalog kind of the chosen mod set. Local 1.6.
# -Bodies limits the run to the named plannable BodyDefs (comma separated);
# -Shard 'k/n' takes the k-th of n pair slices, so a large catalog is
# covered by n runs. One body (or shard) per run keeps the sweep inside the
# 60 s budget.
$ErrorActionPreference = 'Stop'
$tools = 'D:\Code\RimWorld\Shared\tools\automation'
$scope = "bodies: $(if ($Bodies) { $Bodies } else { 'all' })$(if ($Shard) { "; shard $Shard" })"
$run = & "$tools\create-run.ps1" -Purpose "Implanner conflict audit ($ModSet; $scope): game surgery workers versus Implanner's planned-slot conflicts" -ModSet $ModSet -GameVersion 1.6 -SavePattern 'Fisso-NAM.rws' -BaselineConfigPath "$tools\profile-template\Config\ModsConfig-1.6.xml" -AdditionalModSource "$PSScriptRoot\Probe"
if ($Bodies) { Set-Content -Path (Join-Path $run.profile 'conflict-audit-bodies.txt') -Value $Bodies -NoNewline }
if ($Shard) { Set-Content -Path (Join-Path $run.profile 'conflict-audit-shard.txt') -Value $Shard -NoNewline }
$tag = (($Bodies, $Shard) -join '-') -replace '[^A-Za-z0-9]+', '-'
if (-not $tag -or $tag -eq '-') { $tag = 'all' }
$out = Join-Path 'D:/Code/RimWorld/Implanner/temp/conflicts/audit' "$ModSet-$tag-$($run.runId)"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$stage = ''
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(58)
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        $file = Join-Path $run.profile 'conflict-audit-stage.txt'
        if (Test-Path $file) { $stage = (Get-Content $file -Raw).Trim(); break }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Conflict audit timed out' }
        Start-Sleep -Milliseconds 150
    } while ($true)
} finally {
    try { & "$tools\stop.ps1" -RunId $run.runId } catch { Write-Warning $_ }
    foreach ($name in 'Player.log','run.json','launch.json','conflict-audit-stage.txt','conflict-audit.txt') {
        $path = Join-Path $run.profile $name
        if (Test-Path $path) { Copy-Item -LiteralPath $path -Destination $out -Force }
    }
    & "$tools\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'conflict-audit.txt')
if ($stage -ne 'SUCCESS') { throw "Scenario: $stage" }
