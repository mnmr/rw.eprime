param([ValidateSet(0,1,2)][int]$Mode=0,[string]$Stock='no',[string]$Label='',[string]$ModSourceRoot='',[string]$ExpectedRecipe='',[switch]$RaiseHoldback,[ValidateSet('production','decisions')][string]$Feature='production',[int]$CraftTicks=2550)
$ErrorActionPreference='Stop'
$tools=Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))) 'Shared\tools\automation'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
if($ModSourceRoot) {$source=$ModSourceRoot}
if(-not $Label){$Label="epoe-$Mode-$Stock-"+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')}
$out=Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))) "Implanner\temp\epoe-production\$Label"
if(Test-Path -LiteralPath $out){throw "Choose a new evidence Label; already exists: $out"}
New-Item -ItemType Directory -Path $out | Out-Null
$run=& "$tools\create-run.ps1" -Purpose "Implanner EPOE mode $Mode free-arm=$Stock $Label actual bill consumption" -ModSet implanner-epoe-pickle -ModSourceRoot $source -SavePattern Fisso-NAM.rws -GameRoot D:\Code\RimWorld\Readouts\temp\linux-validation\game-1.6 -BaselineConfigPath "$tools\profile-template\Config\ModsConfig-1.6.xml"
$run.runId | Set-Content (Join-Path $out 'run-id.txt')
try {
 $mod=Join-Path $run.profile 'Game\Mods\Implanner'
 foreach($folder in 'Features','Fixtures','Assemblies') {New-Item -ItemType Directory -Force -Path "$mod\Pickle\$folder"|Out-Null}
 Copy-Item "$PSScriptRoot\bin\ImplannerProductionSteps.dll" "$mod\Pickle\Assemblies"
 $featureText=(Get-Content "$PSScriptRoot\$Feature.feature" -Raw).Replace('with no free',"with $Stock free").Replace('crafting mode is 0',"crafting mode is $Mode").Replace('2550 ticks',"$CraftTicks ticks")
 if($RaiseHoldback){$featureText=$featureText.Replace('  When a crafter','  When the player reserves the base arm after the assembly bill queues'+"`n"+'  And a crafter')}
 if($ExpectedRecipe){$featureText+="`n  And the game used the $ExpectedRecipe production route`n"}
 $featureText | Set-Content "$mod\Pickle\Features\$Feature.feature"
 Copy-Item "$($run.profile)\Saves\Autostart.rws" "$mod\Pickle\Fixtures\fisso.rws"
 @"
<?xml version="1.0" encoding="utf-8"?>
<SettingsBlock><ModSettings Class="XmlExtensions.XmlModBaseSettings"><dataDict><keys><li>vat.epoeforked;CraftingMethod</li></keys><values><li>$Mode</li></values></dataDict><PinnedMods /></ModSettings></SettingsBlock>
"@ | Set-Content "$($run.profile)\Config\Mod_Workshop-2574315206_XmlMod.xml"
 $fixtureEvidence=Join-Path $out 'fixture'
 New-Item -ItemType Directory -Path $fixtureEvidence | Out-Null
 Copy-Item "$PSScriptRoot\ProductionSteps.cs","$PSScriptRoot\ImplannerProductionSteps.csproj","$mod\Pickle\Assemblies\ImplannerProductionSteps.dll","$mod\Pickle\Features\$Feature.feature","$($run.profile)\Config\Mod_Workshop-2574315206_XmlMod.xml" $fixtureEvidence
 Get-ChildItem -LiteralPath $fixtureEvidence -File | Get-FileHash | Select-Object @{n='file';e={Split-Path $_.Path -Leaf}},Hash | ConvertTo-Json | Set-Content (Join-Path $fixtureEvidence 'hashes.json')
 $report="/mnt/d/Code/RimWorld/AutomationProfiles/Shared/Runs/$($run.runId)/PickleReports"
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
if(Test-Path "$out\PickleReports\summary.json") {Get-Content "$out\PickleReports\summary.json" -Raw}
