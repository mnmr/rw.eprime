# Builds the clean multiplayer fixture once: loads the player's Fisso save on
# the local 1.6 copy with only Harmony, Prepatcher, vanilla, the official DLCs
# and our four mods, clears jobs left by removed mods, and saves it.
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools=Join-Path $source 'Shared\tools\automation'
$fixture=Join-Path $source 'WorkRoles\temp\mp-desync\clean-fixture'
$run=& "$tools\create-run.ps1" -Purpose 'WorkRoles clean multiplayer fixture' -ModSourceRoot $source -ModSet 'workroles-mp-clean-prep' `
 -AdditionalModSource @("$PSScriptRoot\Probe") -SavePattern 'Fisso-NAM.rws' -BaselineConfigPath "$tools\profile-template\Config\ModsConfig-1.6.xml" -GameVersion '1.6'
[IO.File]::WriteAllText((Join-Path $run.profile 'mp-role.txt'),'prep')
try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 $deadline=[DateTime]::UtcNow.AddSeconds(60)
 $file=Join-Path $run.profile 'arbiter-stage.txt'
 do {
  Invoke-SharedGameCommand @{command='status'} | Out-Null
  $stage=if(Test-Path $file){(Get-Content $file -Raw).Trim()}else{''}
  if($stage -eq 'EXPORTED' -or $stage.StartsWith('FAILED')){break}
  if([DateTime]::UtcNow -gt $deadline){throw "Fixture timeout: $stage"}
  Start-Sleep -Milliseconds 250
 }while($true)
} finally {
 & "$tools\stop.ps1" -RunId $run.runId
 New-Item -ItemType Directory -Path $fixture -Force | Out-Null
 Copy-Item -LiteralPath (Join-Path $run.profile 'Config\ModsConfig.xml') -Destination $fixture -Force
 Get-ChildItem -LiteralPath (Join-Path $run.profile 'Saves') -Filter 'Fisso-NAM-clean.rws' | Copy-Item -Destination $fixture -Force
 Copy-Item -LiteralPath (Join-Path $run.profile 'Player.log') -Destination (Join-Path $fixture 'prepare-Player.log') -Force
 if(Test-Path (Join-Path $run.profile 'arbiter-status.txt')){Get-Content (Join-Path $run.profile 'arbiter-status.txt')}
 & "$tools\remove-run.ps1" -RunId $run.runId
}
Get-ChildItem $fixture | ForEach-Object { "$($_.Name) $($_.Length)" }
