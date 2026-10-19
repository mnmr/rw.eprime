param([ValidateSet('1.6','1.7')][string]$Version='1.7', [string]$Label='batch', [switch]$Clean,
 [ValidateSet('NVIDIA','Software')][string]$Renderer='NVIDIA')
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools=Join-Path $source 'Shared\tools\automation'
$params=@{Purpose="QualityJobs $Version completed-building activation, rebuilding and settings ($Label)";ModSourceRoot=$source}
if($Clean){$params.ModSet='qualityjobs-only'}
else{$params.ModSet='qualityjobs-completed-buildings';$params.AdditionalModSource=@("$PSScriptRoot\Probe")}
if($Version -eq '1.6') {
 $params.GameRoot=Join-Path $source 'Readouts\temp\linux-validation\game-1.6'
 $params.WslDistribution='Ubuntu-22.04';$params.WslRenderer=$Renderer
 $params.SavePattern='Fisso-NAM.rws';$params.BaselineConfigPath="$tools\profile-template\Config\ModsConfig-1.6.xml"
}
$run=& "$tools\create-run.ps1" @params
$out=Join-Path $source "QualityJobs\temp\completed-buildings\$Version-$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$run.runId | Set-Content (Join-Path $out 'run-id.txt')
try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 function Wait-Stage([string]$Expected) {
  $deadline=[DateTime]::UtcNow.AddSeconds(90)
  do {
   Invoke-SharedGameCommand @{command='status'} | Out-Null
   $file=Join-Path $run.profile 'completed-stage.txt'
   $stage=if(Test-Path $file){(Get-Content $file -Raw).Trim()}else{''}
   if($stage.StartsWith('FAILED')){throw $stage}
   if($stage -eq $Expected){return}
   if([DateTime]::UtcNow -gt $deadline){throw "Scenario timeout; last stage: $stage"}
   Start-Sleep -Milliseconds 100
  }while($true)
 }
 function Click-Point([string]$Name) {
  $file=Join-Path $run.profile "completed-$Name.txt"
  $deadline=[DateTime]::UtcNow.AddSeconds(5)
  while(-not (Test-Path $file)) {
   Invoke-SharedGameCommand @{command='status'} | Out-Null
   if([DateTime]::UtcNow -gt $deadline){throw "No point $Name"}
   Start-Sleep -Milliseconds 80
  }
  $point=(Get-Content $file -Raw).Trim().Split(',')
  Invoke-SharedGameCommand @{command='click';x=[int]$point[0];y=[int]$point[1]} | Out-Null
 }
 if(-not $Clean) {
  Wait-Stage 'ready'
  Click-Point 'gizmo'
  Wait-Stage 'configure'
  & "$tools\capture.ps1" -RunId $run.runId -OutName 'configuration.png'
  Click-Point 'quality'
  Click-Point 'good'
  Wait-Stage 'draft'
  & "$tools\capture.ps1" -RunId $run.runId -OutName 'draft.png'
  Click-Point 'start'
  Wait-Stage 'active'
  'work' | Set-Content (Join-Path $run.profile 'completed-work.txt')
  Wait-Stage 'SUCCESS'
 }
 & "$tools\capture.ps1" -RunId $run.runId -OutName 'result.png'
} finally {
 & "$tools\stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','Player.log.host-error.txt','run.json','launch.json','host.json','linux-host.json','linux-console.log','linux-host.log','stall.json','hard-cap.json','Captures','completed-stage.txt') {
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 Get-ChildItem -LiteralPath $run.profile -Filter 'completed-*' | Copy-Item -Destination $out -Force
 & "$tools\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Select-String -LiteralPath (Join-Path $out 'Player.log') -Pattern '\[CompletedBuildingsProbe\]' | ForEach-Object Line
