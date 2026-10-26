param([string]$Label='after', [switch]$Baseline, [string]$Locale='English', [switch]$CommunityFallback)
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$tools=Join-Path $source 'Shared/tools/automation'
$ids=@{
 'fluffy.worktab'='Work Tab'; 'arof.fluffy.worktab'='Work Tab fork'; 'arof.fluffy.worktab.continued'='Work Tab Continued';
 'natee.enhancedworktab'='Enhanced Work Tab'; 'coolnether123.betterworktab'='Better Work Tab';
 'freemapa.freewill'='Free Will'; 'lauriichan.prioritymaster'='PriorityMaster';
 'garwel.readoutsplus'='Readouts+'; 'customresourcereadout.1trickpwnyta'='Custom Resource Readout'; 'owlchemist.toggleablereadouts'='Toggleable Readouts';
 'azraelgodking.strata'='Strata'; 'telardo.multifloors'='MultiFloors'; 'astryl.asabovesobelow'='As above, so below'; 'astryl.asabovesobelow2'='As above, so below II';
 'hatti.qualitybuilder'='QualityBuilder'; 'doug.nojobauthors'='No Job Authors'; 'xandrmoro.rim.finishit'='Finish it!'
}
$additional=@("$PSScriptRoot/mod")
$modset=Get-Content "$tools/modsets/own-mods-waylith.txt"
$modset += @('after eprime.qualityjobs','rimshared.welcomeconflicts.probe')
foreach($id in ($ids.Keys | Sort-Object)) {
 $root=Join-Path $PSScriptRoot "Fixtures/$id"
 New-Item -ItemType Directory -Path "$root/About" -Force | Out-Null
 "<ModMetaData><name>$($ids[$id])</name><author>Package identity fixture only</author><packageId>fixture.$id</packageId><supportedVersions><li>1.7</li></supportedVersions></ModMetaData>" | Set-Content "$root/About/About.xml"
 $additional += $root
 $modset += "fixture.$id"
}
if($CommunityFallback) {
 $root=Join-Path $PSScriptRoot 'Fixtures/CommunityTranslations'
 New-Item -ItemType Directory -Path "$root/About","$root/Languages/English/Keyed" -Force | Out-Null
 '<ModMetaData><name>Community shared translation fixture</name><author>Test fixture</author><packageId>rimshared.welcomeconflicts.community</packageId><supportedVersions><li>1.7</li></supportedVersions></ModMetaData>' | Set-Content "$root/About/About.xml"
 '<LanguageData><RS_CompatibilityHeading>Community conflict heading</RS_CompatibilityHeading></LanguageData>' | Set-Content "$root/Languages/English/Keyed/Welcome.xml"
 $additional += $root
 $modset += 'rimshared.welcomeconflicts.community'
}
$modsetName=if($CommunityFallback){'welcome-conflicts-community'}else{'welcome-conflicts'}
$modset | Set-Content "$tools/modsets/$modsetName.txt"
$run=& "$tools/create-run.ps1" -Purpose "Shared welcome conflict catalog and acknowledgements ($Label)" -ModSourceRoot $source -ModSet $modsetName -AdditionalModSource $additional
$out=Join-Path $source "QualityJobs/temp/welcome-conflicts/$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
 $Locale | Set-Content (Join-Path $run.profile 'expected-language.txt')
 if($CommunityFallback) { 'Community conflict heading' | Set-Content (Join-Path $run.profile 'expected-shared-heading.txt') }
 if($Locale -ne 'English') {
  $prefsPath=Join-Path $run.profile 'Config/Prefs.xml'
  [xml]$prefs=Get-Content -LiteralPath $prefsPath -Raw
  $languageRoot='C:/Program Files (x86)/Steam/steamapps/common/RimWorld/Data/Core/Languages'
  $folder=Get-ChildItem -LiteralPath $languageRoot -File -Filter "$Locale (*.tar" | Select-Object -First 1 -ExpandProperty BaseName
  if(-not $folder){throw "Missing native language: $Locale"}
  $prefs.PrefsData.langFolderName=$folder
  $prefs.Save($prefsPath)
 }
 & "$tools/launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools/automation-common.ps1" -RunId $run.runId
 foreach($index in 0,1,2,3) {
  [string]$index | Set-Content (Join-Path $run.profile 'conflicts-command.txt')
  $deadline=[DateTime]::UtcNow.AddSeconds(30)
  do {
   Invoke-SharedGameCommand @{command='status'} | Out-Null
   $file=Join-Path $run.profile 'conflicts-stage.txt'
   $stage=if(Test-Path $file){(Get-Content $file -Raw).Trim()}else{''}
   if($stage.StartsWith('FAILED')){throw $stage}
   if($stage -eq [string]$index){break}
   if([DateTime]::UtcNow -gt $deadline){throw "Scenario timeout: $stage"}
   Start-Sleep -Milliseconds 100
  }while($true)
  & "$tools/capture.ps1" -RunId $run.runId -OutName "host-$index.png"
 }
} finally {
 & "$tools/stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','run.json','launch.json','Captures') {
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 Get-ChildItem -LiteralPath $run.profile -Filter 'conflicts-*' | Copy-Item -Destination $out -Force
 & "$tools/remove-run.ps1" -RunId $run.runId
}
Write-Output $out
foreach($hostName in 'WorkRoles','EPrimeReadouts','Implanner','QualityJobs') {
 $result=(Get-Content (Join-Path $out "conflicts-$hostName.txt") -Raw).Trim()
 Write-Output "$hostName $result"
 if(-not $Baseline -and $result -ne 'PASS'){throw "$hostName $result"}
}
