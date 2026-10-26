param([string]$Label='after', [ValidateSet('1.6','1.7')][string]$Version='1.7', [switch]$Warnings, [switch]$Clean,
 [switch]$WelcomeOnly, [switch]$TiledWelcomes, [int]$Width=1920, [int]$Height=1080)
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$tools=Join-Path $source 'Shared/tools/automation'
$params=@{Purpose="QualityJobs $Version bill window identity and visual parity ($Label)";ModSourceRoot=$source;ModSet='qualityjobs-compatibility';AdditionalModSource=@("$PSScriptRoot/Probe")}
if($Warnings) {
 $params.ModSet='qualityjobs-compatibility-warnings'
 # QualityBuilder is present in the local Workshop library. The other two
 # empty packages exercise detection only; they contain no third-party code.
 $params.AdditionalModSource += @("$PSScriptRoot/Fixtures/NoJobAuthors", "$PSScriptRoot/Fixtures/FinishIt")
}
if($Clean) {
 $params.ModSet='own-mods-waylith'
 $params.AdditionalModSource=@()
 $params.Purpose="QualityJobs $Version final clean build load ($Label)"
}
if($TiledWelcomes){$params.ModSet='qualityjobs-welcome-warnings'}
if($Version -eq '1.6') {
 $params.GameRoot=Join-Path $source 'Readouts/temp/linux-validation/game-1.6'
 $params.WslDistribution='Ubuntu-22.04';$params.WslRenderer='Software'
 $params.SavePattern='Fisso-NAM.rws';$params.BaselineConfigPath="$tools/profile-template/Config/ModsConfig-1.6.xml"
}
$run=& "$tools/create-run.ps1" @params
$out=Join-Path $source "QualityJobs/temp/compatibility/$Version-$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
 if($Width -ne 1920 -or $Height -ne 1080) {
  $prefsPath=Join-Path $run.profile 'Config/Prefs.xml'
  [xml]$prefs=Get-Content -LiteralPath $prefsPath -Raw
  $prefs.PrefsData.screenWidth=[string]$Width
  $prefs.PrefsData.screenHeight=[string]$Height
  $prefs.Save($prefsPath)
 }
 & "$tools/launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools/automation-common.ps1" -RunId $run.runId
 function Wait-Stage([string]$Expected) {
  $deadline=[DateTime]::UtcNow.AddSeconds(12)
  do {
   Invoke-SharedGameCommand @{command='status'} | Out-Null
   $file=Join-Path $run.profile 'compat-stage.txt'
   $stage=if(Test-Path $file){(Get-Content $file -Raw).Trim()}else{''}
   if($stage.StartsWith('FAILED')){throw $stage}
   if($stage -eq $Expected){return}
   if([DateTime]::UtcNow -gt $deadline){throw "Scenario timeout; last stage: $stage"}
   Start-Sleep -Milliseconds 100
  }while($true)
 }
 if($Clean) {
  & "$tools/capture.ps1" -RunId $run.runId -OutName 'clean.png'
 } else {
 Wait-Stage 'normal'
 if(-not $WelcomeOnly) {
 Invoke-SharedGameCommand @{command='hover';x=1919;y=0} | Out-Null
 & "$tools/capture.ps1" -RunId $run.runId -OutName 'normal.png'
 & "$tools/capture.ps1" -RunId $run.runId -OutName 'stable.png'
 'custom' | Set-Content (Join-Path $run.profile 'compat-command.txt')
 Wait-Stage 'custom'
 & "$tools/capture.ps1" -RunId $run.runId -OutName 'custom.png'
 & "$tools/capture.ps1" -RunId $run.runId -OutName 'custom-stable.png'
 # Physical pixel positions at the standard 1920x1080 / 1.25 profile.
 # Exercise both the attached QJ checkbox and vanilla repeat-count button.
 Invoke-SharedGameCommand @{command='hover';x=1590;y=270} | Out-Null
 & "$tools/capture.ps1" -RunId $run.runId -OutName 'hover.png'
 Invoke-SharedGameCommand @{command='hover';x=1919;y=0} | Out-Null
 & "$tools/capture.ps1" -RunId $run.runId -OutName 'leave.png'
 Invoke-SharedGameCommand @{command='click';x=1590;y=270} | Out-Null
 Invoke-SharedGameCommand @{command='click';x=856;y=317} | Out-Null
 'interacted' | Set-Content (Join-Path $run.profile 'compat-command.txt')
 Wait-Stage 'interacted'
 'closed' | Set-Content (Join-Path $run.profile 'compat-command.txt')
 Wait-Stage 'closed'
 }
 $warningStage=if($TiledWelcomes){'warning-tiled'}elseif($Warnings){'warning'}else{'warning-none'}
  $warningStage | Set-Content (Join-Path $run.profile 'compat-command.txt')
  Wait-Stage $warningStage
  Invoke-SharedGameCommand @{command='hover';x=($Width-1);y=0} | Out-Null
  & "$tools/capture.ps1" -RunId $run.runId -OutName 'warning.png' -KeepCursor
  'acknowledged' | Set-Content (Join-Path $run.profile 'compat-command.txt')
  Wait-Stage 'acknowledged'
 }
} finally {
 & "$tools/stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','Player.log.host-error.txt','run.json','launch.json','host.json','linux-host.json','linux-console.log','linux-host.log','stall.json','hard-cap.json','Captures') {
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 Get-ChildItem -LiteralPath $run.profile -Filter 'compat-*' | Copy-Item -Destination $out -Force
 & "$tools/remove-run.ps1" -RunId $run.runId
}
Write-Output $out
if($Clean){return}
$checks=if($WelcomeOnly){@('warning','acknowledged')}else{@('identity','lifecycle','interaction','resolution','warning','acknowledged')}
if($Warnings){$checks += 'welcome-sizing'}
foreach($check in $checks) {
 $result=(Get-Content (Join-Path $out "compat-$check.txt") -Raw).Trim()
 Write-Output "$check $result"
 if($result -ne 'PASS'){throw "$check $result"}
}
