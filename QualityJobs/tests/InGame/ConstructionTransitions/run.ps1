param([ValidateSet('1.6','1.7')][string]$Version='1.7', [string]$Label='regression', [switch]$Clean)
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools=Join-Path $source 'Shared\tools\automation'
$params=@{Purpose="QualityJobs $Version suppressed construction transitions ($Label)";ModSourceRoot=$source}
if($Clean){$params.ModSet='qualityjobs-only'}
else{$params.ModSet='qualityjobs-construction-transitions';$params.AdditionalModSource=@("$PSScriptRoot\Probe")}
if($Version -eq '1.6') {
 $params.GameRoot=Join-Path $source 'Readouts\temp\linux-validation\game-1.6'
 $params.WslDistribution='Ubuntu-22.04';$params.WslRenderer='Software'
 $params.SavePattern='Fisso-NAM.rws';$params.BaselineConfigPath="$tools\profile-template\Config\ModsConfig-1.6.xml"
}
$run=& "$tools\create-run.ps1" @params
$out=Join-Path $source "QualityJobs\temp\construction-transitions\$Version-$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$run.runId | Set-Content (Join-Path $out 'run-id.txt')
try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 if(-not $Clean) {
  $deadline=[DateTime]::UtcNow.AddSeconds(25)
  do {
   Invoke-SharedGameCommand @{command='status'} | Out-Null
   $file=Join-Path $run.profile 'transitions-stage.txt'
   $stage=if(Test-Path $file){(Get-Content $file -Raw).Trim()}else{''}
   if($stage.StartsWith('FAILED')){throw $stage}
   if($stage -eq 'SUCCESS'){break}
   if([DateTime]::UtcNow -gt $deadline){throw "Scenario timeout; last stage: $stage"}
   Start-Sleep -Milliseconds 100
  }while($true)
 }
 & "$tools\capture.ps1" -RunId $run.runId -OutName 'result.png'
} finally {
 & "$tools\stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','Player.log.host-error.txt','run.json','launch.json','host.json','linux-host.json','linux-console.log','linux-host.log','stall.json','hard-cap.json','Captures','transitions-stage.txt') {
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 & "$tools\remove-run.ps1" -RunId $run.runId
 Write-Output $out
}
Select-String -LiteralPath (Join-Path $out 'Player.log') -Pattern '\[ConstructionTransitionsProbe\]' | ForEach-Object Line
