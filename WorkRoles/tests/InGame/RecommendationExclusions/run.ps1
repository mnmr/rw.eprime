param([ValidateSet('1.6','1.7')][string]$Version='1.7', [string]$Label='exclusions', [switch]$Clean)
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools=Join-Path $source 'Shared\tools\automation'
$params=@{Purpose="WorkRoles $Version excluded-role UI, persistence and applied colony assignments ($Label)";ModSourceRoot=$source}
if($Clean){$params.ModSet='own-mods-waylith'}
else{$params.ModSet='workroles-exclusions';$params.AdditionalModSource=@("$PSScriptRoot\Probe")}
if($Version -eq '1.6') {
 $params.GameRoot=Join-Path $source 'Readouts\temp\linux-validation\game-1.6'
 $params.WslDistribution='Ubuntu-22.04';$params.WslRenderer='NVIDIA'
 $params.SavePattern='Fisso-NAM.rws';$params.BaselineConfigPath="$tools\profile-template\Config\ModsConfig-1.6.xml"
}
$run=& "$tools\create-run.ps1" @params
$out=Join-Path $source "WorkRoles\temp\recommendation-exclusions\$Version-$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$run.runId | Set-Content (Join-Path $out 'run-id.txt')
try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 function Wait-Stage([string]$Expected) {
  $deadline=[DateTime]::UtcNow.AddSeconds(8)
  do {
   Invoke-SharedGameCommand @{command='status'} | Out-Null
   $file=Join-Path $run.profile 'exclusion-stage.txt'
   $stage=if(Test-Path $file){(Get-Content $file -Raw).Trim()}else{''}
   if($stage.StartsWith('FAILED')){throw $stage}
   if($stage -eq $Expected){return}
   if([DateTime]::UtcNow -gt $deadline){throw "Expected stage $Expected; got $stage"}
   Start-Sleep -Milliseconds 100
  }while($true)
 }
 function Click-Point([string]$Name) {
  $file=Join-Path $run.profile "exclusion-$Name.txt"
  $deadline=[DateTime]::UtcNow.AddSeconds(5)
  while(-not (Test-Path $file)) {
   Invoke-SharedGameCommand @{command='status'} | Out-Null
   if([DateTime]::UtcNow -gt $deadline){throw "No point $Name"}
   Start-Sleep -Milliseconds 80
  }
  $point=(Get-Content $file -Raw).Trim().Split(',')
  Invoke-SharedGameCommand @{command='click';x=[int]$point[0];y=[int]$point[1]} | Out-Null
 }
 function Capture([string]$Name){ & "$tools\capture.ps1" -RunId $run.runId -OutName "$Name.png" }
 if($Clean) { Capture 'clean-loaded' }
 else {
  Wait-Stage 'ready'
  Capture 'initial'
  Click-Point 'add'
  & "$tools\capture.ps1" -RunId $run.runId -OutName 'menu.png' -KeepCursor
  Click-Point 'menu'
  Wait-Stage 'excluded'
  Capture 'excluded'
  $point=(Get-Content (Join-Path $run.profile 'exclusion-hover.txt') -Raw).Trim().Split(',')
  Invoke-SharedGameCommand @{command='hover';x=[int]$point[0];y=[int]$point[1]} | Out-Null
  Start-Sleep -Milliseconds 650
  & "$tools\capture.ps1" -RunId $run.runId -OutName 'hover.png' -KeepCursor
  Capture 'leave'
  'preview' | Set-Content (Join-Path $run.profile 'exclusion-control.txt')
  Wait-Stage 'preview'
  Capture 'preview'
  Click-Point 'apply'
  Wait-Stage 'applied'
  Capture 'applied'
  Click-Point 'remove'
  Wait-Stage 'restored'
  Capture 'restored'
  'finish' | Set-Content (Join-Path $run.profile 'exclusion-control.txt')
  Wait-Stage 'SUCCESS'
 }
} finally {
 & "$tools\stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','Player.log.host-error.txt','run.json','launch.json','host.json','linux-host.json','linux-console.log','linux-host.log','stall.json','hard-cap.json','Captures','excluded-role-save.xml','default-exclusions-save.xml','exclusion-stage.txt') {
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 Get-ChildItem -LiteralPath $run.profile -Filter 'exclusion-*.txt' | Copy-Item -Destination $out -Force
 & "$tools\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Select-String -LiteralPath (Join-Path $out 'Player.log') -Pattern '\[ExclusionProbe\]' | ForEach-Object Line
if(-not $Clean -and (Get-Content (Join-Path $out 'exclusion-stage.txt') -Raw).Trim() -ne 'SUCCESS'){throw "Scenario did not pass: $out"}
