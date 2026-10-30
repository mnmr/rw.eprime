param([string]$Label='native-work',[string]$ModSourceRoot='D:\Code\RimWorld',[string]$Phase='trades-fixed',[string]$Version='1.7')
$ErrorActionPreference='Stop'
$tools='D:\Code\RimWorld\Shared\tools\automation'
$modset='workroles-native-work'
$parameters=@{Purpose="WorkRoles user report native robot capabilities, recommendations and outcomes: $Label";ModSourceRoot=$ModSourceRoot;ModSet=$modset;AdditionalModSource=@("$PSScriptRoot\Probe")}
if($Version -eq '1.6'){$parameters.GameRoot='D:\Code\RimWorld\Readouts\temp\linux-validation\game-1.6';$parameters.WslDistribution='Ubuntu-22.04';$parameters.WslRenderer='Software';$parameters.SavePattern='Fisso-NAM.rws';$parameters.BaselineConfigPath="$tools\profile-template\Config\ModsConfig-1.6.xml"}
$run=& "$tools\create-run.ps1" @parameters
$out=Join-Path $PSScriptRoot "../../../temp/mechanical-native-work/$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $run.profile 'robot-audit-phase.txt'),$Phase)
Write-Output "RUN $($run.runId) $out"
try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 $deadline=[DateTime]::UtcNow.AddSeconds(52)
 $captured=@{}
 do {
  Invoke-SharedGameCommand @{command='status'} | Out-Null
  $path=Join-Path $run.profile 'robot-audit-stage.txt'
  $stage=if(Test-Path $path){(Get-Content -LiteralPath $path -Raw).Trim()}else{''}
  if($stage.StartsWith('CAPTURE ') -and -not $captured.ContainsKey($stage)) {
   & "$tools\capture.ps1" -RunId $run.runId -OutName ($stage.Substring(8)+'.png')
   & "$tools\capture-hover.ps1" -RunId $run.runId -X 392 -Y 436 -OutName 'fire-warning.png' -WaitMilliseconds 1200
   $captured[$stage]=$true
   [IO.File]::WriteAllText((Join-Path $run.profile ('captured-'+$stage.Substring(8))),'')
  }
  if($stage.StartsWith('FAILED')){throw $stage}
  if($stage -eq 'SUCCESS'){break}
  if([DateTime]::UtcNow -gt $deadline){throw "Timeout $stage"}
  Start-Sleep -Milliseconds 100
 }while($true)
} finally {
 & "$tools\stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','run.json','launch.json','host.json','stall.json','hard-cap.json','Captures','robot-audit-stage.txt','robot-audit-results.txt','robot-audit-phase.txt') {
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 & "$tools\remove-run.ps1" -RunId $run.runId
 Write-Output $out
}
