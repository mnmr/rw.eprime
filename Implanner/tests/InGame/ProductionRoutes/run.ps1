param([ValidateSet(0,1,2)][int]$Mode=0,[string]$Stock='no',[string]$Label='',[string]$ModSourceRoot='',[string]$ExpectedRecipe='',[switch]$RaiseHoldback,[ValidateSet('production','decisions')][string]$Feature='production',[int]$CraftTicks=2550,[ValidateSet('1.6','1.7')][string]$RimWorldVersion='1.6',[switch]$ResetOrphanedJobs)
$ErrorActionPreference='Stop'
$tools=Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))) 'Shared\tools\automation'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
if($ModSourceRoot) {$source=$ModSourceRoot}
if(-not $Label){$Label="epoe-$Mode-$Stock-"+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')}
$out=Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))) "Implanner\temp\epoe-production\$Label"
if(Test-Path -LiteralPath $out){throw "Choose a new evidence Label; already exists: $out"}
New-Item -ItemType Directory -Path $out | Out-Null
$engine = if($RimWorldVersion -eq '1.7') {
 @{SavePattern='Fisso-NAM-1.7.rws';GameRoot='C:\Program Files (x86)\Steam\steamapps\common\RimWorld';BaselineConfigPath="$tools\profile-template\Config\ModsConfig.xml"}
} else {
 @{SavePattern='Fisso-NAM.rws';GameRoot='D:\Code\RimWorld\Readouts\temp\linux-validation\game-1.6';BaselineConfigPath="$tools\profile-template\Config\ModsConfig-1.6.xml"}
}
$stepAssembly="$PSScriptRoot\bin\$RimWorldVersion\ImplannerProductionSteps.dll"
if(-not (Test-Path -LiteralPath $stepAssembly)){throw "Build the $RimWorldVersion test assembly first: $stepAssembly"}
$run=& "$tools\create-run.ps1" -Purpose "Implanner RimWorld $RimWorldVersion EPOE mode $Mode free-arm=$Stock $Label actual bill consumption" -ModSet implanner-epoe-pickle -ModSourceRoot $source @engine
$run.runId | Set-Content (Join-Path $out 'run-id.txt')
try {
 $mod=Join-Path $run.profile 'Game\Mods\Implanner'
 foreach($folder in 'Features','Fixtures','Assemblies') {New-Item -ItemType Directory -Force -Path "$mod\Pickle\$folder"|Out-Null}
 if($run.snapshots.gameVersion -ne $RimWorldVersion){throw "Expected RimWorld $RimWorldVersion, got $($run.snapshots.gameVersion)"}
 Copy-Item $stepAssembly "$mod\Pickle\Assemblies"
 $featureText=(Get-Content "$PSScriptRoot\$Feature.feature" -Raw).Replace('with no free',"with $Stock free").Replace('crafting mode is 0',"crafting mode is $Mode").Replace('2550 ticks',"$CraftTicks ticks")
 if($RaiseHoldback){$featureText=$featureText.Replace('  When a crafter','  When the player reserves the base arm after the assembly bill queues'+"`n"+'  And a crafter')}
 if($ExpectedRecipe){$featureText+="`n  And the game used the $ExpectedRecipe production route`n"}
 $featureText | Set-Content "$mod\Pickle\Features\$Feature.feature"
 Copy-Item "$($run.profile)\Saves\Autostart.rws" "$mod\Pickle\Fixtures\fisso.rws"
 # Some converted saves retain current jobs without a def. Reset only those
 # invalid jobs and their path state in the test fixture; the verified source
 # copy stays untouched. Retain the exact fixture and repair record as evidence.
 $fixtureRepairs=@()
 if($ResetOrphanedJobs) {
  [xml]$fixtureSave=Get-Content "$mod\Pickle\Fixtures\fisso.rws" -Raw
  foreach($job in @($fixtureSave.SelectNodes("//jobs/curJob[not(def) and not(@IsNull='True')]"))) {
   $pawnNode=$job.ParentNode.ParentNode
   $fixtureRepairs+=([ordered]@{pawnId=[string]$pawnNode.id;jobLoadId=[string]$job.loadID;reason='Current job has no def';reset='current job, driver, path follower'})
   $job.RemoveAll(); $job.SetAttribute('IsNull','True')
   $driver=$job.ParentNode.SelectSingleNode('curDriver')
   if($driver){$driver.RemoveAll();$driver.SetAttribute('IsNull','True')}
   $pather=$pawnNode.SelectSingleNode('pather')
   if($pather){$null=$pawnNode.RemoveChild($pather)}
  }
  if($fixtureRepairs.Count){$fixtureSave.Save("$mod\Pickle\Fixtures\fisso.rws")}
 }
 $settingsName=if($run.backend -eq 'wsl'){'Mod_Workshop-2574315206_XmlMod.xml'}else{'Mod_2574315206_XmlMod.xml'}
 @"
<?xml version="1.0" encoding="utf-8"?>
<SettingsBlock><ModSettings Class="XmlExtensions.XmlModBaseSettings"><dataDict><keys><li>vat.epoeforked;CraftingMethod</li></keys><values><li>$Mode</li></values></dataDict><PinnedMods /></ModSettings></SettingsBlock>
"@ | Set-Content "$($run.profile)\Config\$settingsName"
 $fixtureEvidence=Join-Path $out 'fixture'
 New-Item -ItemType Directory -Path $fixtureEvidence | Out-Null
 ConvertTo-Json -InputObject $fixtureRepairs -Depth 4 | Set-Content (Join-Path $fixtureEvidence 'repairs.json')
 Copy-Item "$mod\Pickle\Fixtures\fisso.rws" $fixtureEvidence
 Copy-Item "$PSScriptRoot\ProductionSteps.cs","$PSScriptRoot\ImplannerProductionSteps.csproj","$mod\Pickle\Assemblies\ImplannerProductionSteps.dll","$mod\Pickle\Features\$Feature.feature","$($run.profile)\Config\$settingsName" $fixtureEvidence
 Get-ChildItem -LiteralPath $fixtureEvidence -File | Get-FileHash | Select-Object @{n='file';e={Split-Path $_.Path -Leaf}},Hash | ConvertTo-Json | Set-Content (Join-Path $fixtureEvidence 'hashes.json')
 $report=if($run.backend -eq 'wsl'){"/mnt/d/Code/RimWorld/AutomationProfiles/Shared/Runs/$($run.runId)/PickleReports"}else{Join-Path $run.profile 'PickleReports'}
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 3 -AllowRecoverableSaveErrors -HardCapSeconds 60 -GameArguments "-pickle-run=$Feature.feature","-pickle-report-dir=$report",'-pickle-no-http','-pickle-scenario-timeout=55'
 $deadline=[DateTime]::UtcNow.AddSeconds(60)
 do {
  $hostState=Get-Content "$($run.profile)\host.json" -Raw|ConvertFrom-Json
  if($hostState.state -in 'stopped','failed') {break}
  if(Test-Path "$($run.profile)\PickleReports\summary.json") {break}
  . "$tools\automation-common.ps1" -RunId $run.runId
  try { Invoke-SharedGameCommand @{command='status'} | Out-Null } catch { if(Test-Path "$($run.profile)\PickleReports\summary.json"){break}; throw }
 } while([DateTime]::UtcNow -lt $deadline)
} finally {
 for($stopAttempt=0; $stopAttempt -lt 8; $stopAttempt++) {
  try { & "$tools\stop.ps1" -RunId $run.runId; break } catch { if($stopAttempt -eq 7){throw}; Start-Sleep -Milliseconds 500 }
 }
 foreach($name in 'PickleReports','Player.log','run.json','launch.json','host.json','linux-host.json','stall.json','hard-cap.json','linux-console.log','epoe-evidence.txt') {
  if(Test-Path "$($run.profile)\$name") {Copy-Item "$($run.profile)\$name" $out -Recurse -Force}
 }
 & "$tools\remove-run.ps1" -RunId $run.runId
}
if(-not (Test-Path "$out\PickleReports\summary.json")){throw "No scenario result; inspect $out"}
$summary=Get-Content "$out\PickleReports\summary.json" -Raw
$summary
$result=$summary|ConvertFrom-Json
if($result.failed -ne 0 -or $result.passed -lt 1){throw "In-game verification failed; inspect $out"}
