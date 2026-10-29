param([ValidateSet('1.6','1.7')][string]$Version='1.7', [string]$Label='native-work',
 [ValidateSet('all','native','reload-off','reload-on')][string]$Phase='all', [string]$SourceSaveDirectory='')
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools=Join-Path $source 'Shared\tools\automation'
$params=@{Purpose="WorkRoles $Version optional mechanical rows and native work ($Label)";ModSourceRoot=$source;ModSet='workroles-mechanical-workers';AdditionalModSource=@("$PSScriptRoot\Probe")}
if($Version -eq '1.6') {
 $params.GameRoot='D:\Code\RimWorld\Readouts\temp\linux-validation\game-1.6'
 $params.WslDistribution='Ubuntu-22.04';$params.WslRenderer='Software'
 $params.SavePattern='Fisso-NAM.rws';$params.BaselineConfigPath="$tools\profile-template\Config\ModsConfig-1.6.xml"
}
if($SourceSaveDirectory){
 $params.SourceSaveDirectory=$SourceSaveDirectory
 $params.SavePattern=if($Phase -eq 'reload-off'){'MechanicalSupportOff.rws'}else{'MechanicalSupportOn.rws'}
 $params.BaselineConfigPath=Join-Path $SourceSaveDirectory 'ModsConfig.xml'
}
$run=& "$tools\create-run.ps1" @params
$out=Join-Path $source "WorkRoles\temp\mechanical-workers\$Version-$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$run.runId | Set-Content (Join-Path $out 'run-id.txt')
$Phase | Set-Content (Join-Path $run.profile 'mechanical-phase.txt')
try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 $deadline=[DateTime]::UtcNow.AddSeconds(60)
 do {
  Invoke-SharedGameCommand @{command='status'} | Out-Null
  $file=Join-Path $run.profile 'mechanical-stage.txt'
  $stage=if(Test-Path $file){(Get-Content $file -Raw).Trim()}else{''}
  if($stage.StartsWith('FAILED')){throw $stage}
  if($stage -eq 'SUCCESS' -or $stage -eq 'EXPORTED'){break}
  if([DateTime]::UtcNow -gt $deadline){throw "Scenario timeout: $stage"}
  Start-Sleep -Milliseconds 100
 }while($true)
 & "$tools\capture.ps1" -RunId $run.runId -OutName 'result.png'
} finally {
 & "$tools\stop.ps1" -RunId $run.runId
 $saved=Join-Path $out 'SavedFixtures'
 New-Item -ItemType Directory -Path $saved -Force | Out-Null
 Copy-Item -LiteralPath (Join-Path $run.profile 'Config/ModsConfig.xml') -Destination $saved
 Get-ChildItem -LiteralPath (Join-Path $run.profile 'Saves') -Filter 'MechanicalSupport*.rws' | Copy-Item -Destination $saved
 foreach($name in 'Player.log','Player.log.host-error.txt','run.json','launch.json','host.json','linux-host.json','linux-console.log','linux-host.log','stall.json','hard-cap.json','Captures','mechanical-stage.txt') {
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 & "$tools\remove-run.ps1" -RunId $run.runId
 Write-Output $out
}
Select-String -LiteralPath (Join-Path $out 'Player.log') -Pattern '\[MechanicalProbe\]' | ForEach-Object Line
