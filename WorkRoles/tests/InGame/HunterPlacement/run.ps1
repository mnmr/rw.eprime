param([string]$Label='picker')
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools=Join-Path $source 'Shared\tools\automation'
$params=@{Purpose="WorkRoles 1.7 Hunter placement picker UI and plan per mode ($Label)";ModSourceRoot=$source;GameVersion='1.7';ModSet='workroles-hunter-placement';AdditionalModSource=@("$PSScriptRoot\Probe")}
$run=& "$tools\create-run.ps1" @params
$out=Join-Path $source "WorkRoles\temp\hunter-placement\$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$run.runId | Set-Content (Join-Path $out 'run-id.txt')
try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 function Wait-Stage([string]$Expected) {
  $deadline=[DateTime]::UtcNow.AddSeconds(8)
  do {
   Invoke-SharedGameCommand @{command='status'} | Out-Null
   $file=Join-Path $run.profile 'hunter-stage.txt'
   $stage=if(Test-Path $file){(Get-Content $file -Raw).Trim()}else{''}
   if($stage.StartsWith('FAILED')){throw $stage}
   if($stage -eq $Expected){return}
   if([DateTime]::UtcNow -gt $deadline){throw "Expected stage $Expected; got $stage"}
   Start-Sleep -Milliseconds 100
  }while($true)
 }
 function Get-Point([string]$Name) {
  $file=Join-Path $run.profile "hunter-$Name.txt"
  $deadline=[DateTime]::UtcNow.AddSeconds(5)
  while(-not (Test-Path $file)) {
   Invoke-SharedGameCommand @{command='status'} | Out-Null
   if([DateTime]::UtcNow -gt $deadline){throw "No point $Name"}
   Start-Sleep -Milliseconds 80
  }
  return (Get-Content $file -Raw).Trim().Split(',')
 }
 function Click-Point([string]$Name) {
  $point=Get-Point $Name
  Invoke-SharedGameCommand @{command='click';x=[int]$point[0];y=[int]$point[1]} | Out-Null
 }
 function Capture([string]$Name){ & "$tools\capture.ps1" -RunId $run.runId -OutName "$Name.png" }
 Wait-Stage 'poor'
 Get-Point 'seg2' | Out-Null
 Capture 'poor'
 $hover=Get-Point 'seg1'
 Invoke-SharedGameCommand @{command='hover';x=[int]$hover[0];y=[int]$hover[1]} | Out-Null
 Start-Sleep -Milliseconds 650
 & "$tools\capture.ps1" -RunId $run.runId -OutName 'hover-best.png' -KeepCursor
 Click-Point 'seg1'
 Wait-Stage 'best'
 Capture 'best'
 Click-Point 'seg2'
 Wait-Stage 'normal'
 Capture 'normal'
 Click-Point 'seg0'
 Wait-Stage 'SUCCESS'
 Capture 'poor-again'
} finally {
 & "$tools\stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','Player.log.host-error.txt','run.json','launch.json','host.json','stall.json','hard-cap.json','Captures','hunter-stage.txt') {
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 Get-ChildItem -LiteralPath $run.profile -Filter 'hunter-*.txt' | Copy-Item -Destination $out -Force
 & "$tools\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Select-String -LiteralPath (Join-Path $out 'Player.log') -Pattern '\[HunterPlacementProbe\]' | ForEach-Object Line
if((Get-Content (Join-Path $out 'hunter-stage.txt') -Raw).Trim() -ne 'SUCCESS'){throw "Scenario did not pass: $out"}
