param([ValidateSet('1.6','1.7')][string]$Version='1.7', [string]$Label='tabs', [switch]$Baseline, [switch]$Screenshots, [string]$Locale='English')
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools=Join-Path $source 'Shared\tools\automation'
$params=@{Purpose="Quality Jobs $Version help navigation and screenshots ($Label)";ModSourceRoot=$source;ModSet='qualityjobs-help';AdditionalModSource=@("$PSScriptRoot\Probe")}
if($Version -eq '1.6') {
 $params.GameRoot=Join-Path $source 'Readouts\temp\linux-validation\game-1.6'
 $params.WslDistribution='Ubuntu-22.04';$params.WslRenderer='Software'
 $params.SavePattern='Fisso-NAM.rws';$params.BaselineConfigPath="$tools\profile-template\Config\ModsConfig-1.6.xml"
}
$run=& "$tools\create-run.ps1" @params
$out=Join-Path $source "QualityJobs\temp\help\$Version-$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
$prefs=Join-Path $run.profile 'Config\Prefs.xml'
$text=[IO.File]::ReadAllText($prefs).Replace('<uiScale>1.25</uiScale>','<uiScale>1</uiScale>')
[IO.File]::WriteAllText($prefs,$text)
[xml]$preferences=Get-Content -LiteralPath $prefs -Raw
$languageRoot=if($Version -eq '1.6'){Join-Path $params.GameRoot 'Data/Core/Languages'}else{'C:/Program Files (x86)/Steam/steamapps/common/RimWorld/Data/Core/Languages'}
$language=Get-ChildItem -LiteralPath $languageRoot -Filter "$Locale*" | Where-Object { $_.BaseName -eq $Locale -or $_.BaseName.StartsWith("$Locale (") } | Select-Object -First 1
if(-not $language){throw "Native language not found: $Locale"}
$preferences.PrefsData.langFolderName=$language.BaseName
$preferences.Save($prefs)
[IO.File]::WriteAllText((Join-Path $run.profile 'help-locale.txt'),$Locale)
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 function Wait-Stage([string]$Expected) {
  $deadline=[DateTime]::UtcNow.AddSeconds(24)
  do {
   Invoke-SharedGameCommand @{command='status'} | Out-Null
   $file=Join-Path $run.profile 'help-stage.txt'
   $stage=if(Test-Path $file){(Get-Content $file -Raw).Trim()}else{''}
   if($stage.StartsWith('FAILED')){throw $stage}
   if($stage -eq $Expected){return}
   if([DateTime]::UtcNow -gt $deadline){throw "Scenario timeout; last stage: $stage"}
   Start-Sleep -Milliseconds 80
  }while($true)
 }
 function Request([string]$Action,[string]$Expected) {
  $stagePath=Join-Path $run.profile 'help-stage.txt'
  if(Test-Path -LiteralPath $stagePath){Remove-Item -LiteralPath $stagePath}
  $pending=Join-Path $run.profile 'help-request.pending'
  [IO.File]::WriteAllText($pending,$Action)
  Move-Item -LiteralPath $pending -Destination (Join-Path $run.profile 'help-request.txt') -Force
  Wait-Stage $Expected
 }
 function Click-Point([string]$Name) {
  $path=Join-Path $run.profile "help-$Name.txt"
  $deadline=[DateTime]::UtcNow.AddSeconds(12)
  while(-not (Test-Path -LiteralPath $path)) {
   Invoke-SharedGameCommand @{command='status'} | Out-Null
   if([DateTime]::UtcNow -gt $deadline){throw "No rendered point $Name"}
   Start-Sleep -Milliseconds 80
  }
  $point=(Get-Content -LiteralPath $path -Raw).Trim().Split(',')
  Invoke-SharedGameCommand @{command='click';x=[int]$point[0];y=[int]$point[1]} | Out-Null
 }
 Wait-Stage 'ready'
 if($Version -eq '1.7' -or $Screenshots){ & "$tools\capture.ps1" -RunId $run.runId -OutName 'toolbar.png' }
 Click-Point 'toolbar'
 & "$tools\capture.ps1" -RunId $run.runId -OutName 'options.png'
 Request 'check-window' 'window'
 if($Screenshots) {
  Request 'crafting' 'crafting'
  & "$tools\capture.ps1" -RunId $run.runId -OutName 'crafting-bill.png'
  Request 'construction' 'construction'
  Invoke-SharedGameCommand @{command='status'} | Out-Null
  $point=(Get-Content (Join-Path $run.profile 'help-gizmo.txt') -Raw).Trim().Split(',')
  Invoke-SharedGameCommand @{command='click';button=1;x=[int]$point[0];y=[int]$point[1]} | Out-Null
  Request 'check-construction' 'construction-open'
  & "$tools\capture.ps1" -RunId $run.runId -OutName 'construction.png'
  Request 'rebuild' 'rebuild'
  Click-Point 'gizmo'
  Request 'check-construction' 'construction-open'
  & "$tools\capture.ps1" -RunId $run.runId -OutName 'rebuild.png'
 } elseif(-not $Baseline) {
  Click-Point 'help-tab'
  Request 'check-help' 'help'
  Request 'sample-start' 'steady'
  & "$tools\capture.ps1" -RunId $run.runId -OutName 'help.png'
  $topics=if($Version -eq '1.6'){@('2:1')}else{@('0:1','0:2','1:0','1:1','2:0','2:1','2:2','3:0','3:1','3:2','3:3')}
  foreach($topic in $topics) {
   Request "topic:$topic" 'topic-points'
   Click-Point 'chapter'
   Request "topic-row:$($topic.Split(':')[1])" 'topic-row'
   Click-Point 'topic'
   Request "check-topic:$topic" 'topic-checked'
  }
  if($Version -eq '1.7'){ & "$tools\capture.ps1" -RunId $run.runId -OutName 'help-last-topic.png' }
  Click-Point 'options-tab'
  Request 'check-options' 'options'
  & "$tools\capture.ps1" -RunId $run.runId -OutName 'options-return.png'
  Click-Point 'sharing'
  Request 'check-toggle' 'toggled'
  Click-Point 'sharing'
  Request 'check-restored' 'restored'
  Click-Point 'help-tab'
  Invoke-SharedGameCommand @{command='type';text='{ESC}'} | Out-Null
  Request 'check-closed' 'closed'
  Click-Point 'toolbar'
  Request 'check-window' 'window'
  if($Version -eq '1.7'){ & "$tools\capture.ps1" -RunId $run.runId -OutName 'options-reopened.png' }
 }
} finally {
 & "$tools\stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','run.json','launch.json','ready.json','host.json','linux-host.json','linux-console.log','stall.json','Captures') {
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 Get-ChildItem -LiteralPath $run.profile -Filter 'help-*' | Copy-Item -Destination $out -Force
 & "$tools\remove-run.ps1" -RunId $run.runId
 Write-Output $out
}
