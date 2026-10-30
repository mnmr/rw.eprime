param([ValidateSet('roundtrip','newgame','newgame-sparse','existing')][string]$Mode='roundtrip', [string]$DefaultFile='')
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools=Join-Path $source 'Shared\tools\automation'
$run=& "$tools\create-run.ps1" -Purpose "WorkRoles complete setup ${Mode}: import/export, safe defaults and unavailable content" -ModSourceRoot $source -ModSet 'workroles-setup-roundtrip' -AdditionalModSource "$PSScriptRoot\Probe"
$out=Join-Path $source "WorkRoles\temp\setup-roundtrip\$Mode-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$Mode | Set-Content (Join-Path $run.profile 'setup-mode.txt')
if($DefaultFile){Copy-Item -LiteralPath $DefaultFile -Destination (Join-Path $run.profile 'Config\WorkRoles.Default.xml')}
try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 function Wait-Stage([string]$Expected) {
  $deadline=[DateTime]::UtcNow.AddSeconds(10)
  do {
   Invoke-SharedGameCommand @{command='status'} | Out-Null
   $file=Join-Path $run.profile 'setup-stage.txt'
   $stage=if(Test-Path $file){(Get-Content $file -Raw).Trim()}else{''}
   if($stage.StartsWith('FAILED')){throw $stage}
   if($stage -eq $Expected){return}
   if([DateTime]::UtcNow -gt $deadline){throw "Expected $Expected; got $stage"}
   Start-Sleep -Milliseconds 100
  }while($true)
 }
 function Click-Point([string]$Name) {
  $file=Join-Path $run.profile "setup-$Name.txt"
  $deadline=[DateTime]::UtcNow.AddSeconds(5)
  while(-not(Test-Path $file)){
   Invoke-SharedGameCommand @{command='status'} | Out-Null
   if([DateTime]::UtcNow -gt $deadline){throw "No point $Name"}
   Start-Sleep -Milliseconds 80
  }
  $point=(Get-Content $file -Raw).Trim().Split(',')
  Invoke-SharedGameCommand @{command='click';x=[int]$point[0];y=[int]$point[1]} | Out-Null
 }
 function Capture([string]$Name){& "$tools\capture.ps1" -RunId $run.runId -OutName "$Name.png"}
 if($Mode -eq 'roundtrip'){
  Wait-Stage 'import'
  Capture 'import-preview'
  Click-Point 'preferences'
  Click-Point 'apply'
  Wait-Stage 'roles'
  Capture 'roles'
  Click-Point 'defaults'
  Click-Point 'auto'
  Capture 'defaults-choice'
  Click-Point 'use'
  Wait-Stage 'conflict'
  Capture 'defaults-conflict'
  Click-Point 'keep'
  Wait-Stage 'protected'
  Capture 'verified'
  'teardown' | Set-Content (Join-Path $run.profile 'setup-teardown.txt')
 }
 Wait-Stage 'SUCCESS'
 if($Mode -ne 'roundtrip'){Capture 'verified'}
} finally {
 & "$tools\stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','run.json','launch.json','host.json','stall.json','hard-cap.json','Captures'){
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 Get-ChildItem -LiteralPath $run.profile -Filter 'setup-*' | Copy-Item -Destination $out -Force
 $path=Join-Path $run.profile 'Config\WorkRoles.Default.xml'
 if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out}
 & "$tools\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'setup-results.txt')
if((Get-Content (Join-Path $out 'setup-stage.txt') -Raw).Trim() -ne 'SUCCESS'){throw "Scenario did not pass: $out"}
