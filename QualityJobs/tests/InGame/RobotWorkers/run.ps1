param([ValidateSet('1.6','1.7')][string]$Version='1.7', [string]$Label='regression',
 [ValidateSet('misc','mech')][string]$Scenario='misc', [switch]$NormalPriority, [switch]$EdgesOnly, [switch]$Clean,
 [switch]$WorkRoles)
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools=Join-Path $source 'Shared\tools\automation'
$params=@{Purpose="QualityJobs $Version robot worker preparation and finishing ($Label)";ModSourceRoot=$source}
if($Clean){$params.ModSet='qualityjobs-robots-clean'}
else{$params.ModSet='qualityjobs-robot-workers';$params.AdditionalModSource=@("$PSScriptRoot\Probe")}
if($WorkRoles){$params.ModSet='qualityjobs-workroles-robots';$params.AdditionalModSource=@("$PSScriptRoot\Probe")}
if($Version -eq '1.6') {
 $params.GameRoot='D:\Code\RimWorld\Readouts\temp\linux-validation\game-1.6'
 $params.WslDistribution='Ubuntu-22.04';$params.WslRenderer='Software'
 $params.SavePattern='Fisso-NAM.rws';$params.BaselineConfigPath="$tools\profile-template\Config\ModsConfig-1.6.xml"
}
$run=& "$tools\create-run.ps1" @params
$out=Join-Path $source "QualityJobs\temp\robot-workers\$Version-$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$run.runId | Set-Content (Join-Path $out 'run-id.txt')
($Scenario + $(if($NormalPriority){'-normal'}else{''})) | Set-Content (Join-Path $run.profile 'robot-workers-scenario.txt')
if($EdgesOnly){'true' | Set-Content (Join-Path $run.profile 'robot-workers-edges-only.txt')}
if($WorkRoles){'true' | Set-Content (Join-Path $run.profile 'robot-workers-workroles.txt')}
try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 if(-not $Clean) {
  $deadline=[DateTime]::UtcNow.AddSeconds(45)
  do {
   Invoke-SharedGameCommand @{command='status'} | Out-Null
   $file=Join-Path $run.profile 'robot-workers-stage.txt'
   $stage=if(Test-Path $file){(Get-Content $file -Raw).Trim()}else{''}
   if($stage.StartsWith('FAILED')){throw $stage}
   if($stage -eq 'SUCCESS'){break}
   if([DateTime]::UtcNow -gt $deadline){throw "Scenario timeout; last stage: $stage"}
   Start-Sleep -Milliseconds 100
  }while($true)
 }
 & "$tools\capture.ps1" -RunId $run.runId -OutName 'result.png'
 if(-not $Clean -and -not $EdgesOnly) {
  $pointFile=Join-Path $run.profile 'robot-workers-checkbox.txt'
  $uiDeadline=[DateTime]::UtcNow.AddSeconds(5)
  while(-not (Test-Path $pointFile)) {
   Invoke-SharedGameCommand @{command='status'} | Out-Null
   if([DateTime]::UtcNow -gt $uiDeadline){throw 'Preparation checkbox did not render'}
  }
  $point=(Get-Content $pointFile -Raw).Trim().Split(' ')
  & "$tools\click.ps1" -RunId $run.runId -X ([int]$point[0]) -Y ([int]$point[1]) -WaitMilliseconds 200
  $actual=(Get-Content (Join-Path $run.profile 'robot-workers-setting.txt') -Raw).Trim()
  if($actual -ne 'True'){throw "Options checkbox did not enable preparation: $actual"}
  & "$tools\capture-hover.ps1" -RunId $run.runId -X ([int]$point[0]) -Y ([int]$point[1]) -OutName 'preparation-option-tooltip.png' -WaitMilliseconds 900
 }
} finally {
 & "$tools\stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','Player.log.host-error.txt','run.json','launch.json','host.json','linux-host.json','linux-console.log','linux-host.log','stall.json','hard-cap.json','Captures','robot-workers-stage.txt','robot-workers-scenario.txt','robot-workers-checkbox.txt','robot-workers-setting.txt') {
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 & "$tools\remove-run.ps1" -RunId $run.runId
 Write-Output $out
}
Select-String -LiteralPath (Join-Path $out 'Player.log') -Pattern '\[RobotWorkersProbe\]' | ForEach-Object Line
