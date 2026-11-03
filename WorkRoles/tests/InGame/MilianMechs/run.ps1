param([string]$Label='milian',[string]$ModSourceRoot='D:\Code\RimWorld',[string]$Version='1.7')
$ErrorActionPreference='Stop'
$tools='D:\Code\RimWorld\Shared\tools\automation'
$run=& "$tools\create-run.ps1" -Purpose "WorkRoles with Milira Race: Milian mechanoids as mechanical workers and their Ancot work tab: $Label" -ModSet workroles-milian-mechs -GameVersion $Version -ModSourceRoot $ModSourceRoot -AdditionalModSource @("$PSScriptRoot\Probe")
$out=Join-Path $PSScriptRoot "../../../temp/milian-mechs/$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
Write-Output "RUN $($run.runId) $out"
try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 $deadline=[DateTime]::UtcNow.AddSeconds(58)
 $captured=@{}
 do {
  Invoke-SharedGameCommand @{command='status'} | Out-Null
  $path=Join-Path $run.profile 'milian-stage.txt'
  $stage=if(Test-Path $path){(Get-Content -LiteralPath $path -Raw).Trim()}else{''}
  if($stage.StartsWith('CAPTURE ') -and -not $captured.ContainsKey($stage)) {
   & "$tools\capture.ps1" -RunId $run.runId -OutName ($stage.Substring(8)+'.png')
   $captured[$stage]=$true
   [IO.File]::WriteAllText((Join-Path $run.profile ('captured-'+$stage.Substring(8))),'')
  }
  if($stage.StartsWith('FAILED')){& "$tools\capture.ps1" -RunId $run.runId -OutName 'failed.png';throw $stage}
  if($stage -eq 'SUCCESS'){break}
  if([DateTime]::UtcNow -gt $deadline){throw "Timeout $stage"}
  Start-Sleep -Milliseconds 100
 }while($true)
} catch {
 $_ | Out-String | Set-Content -LiteralPath (Join-Path $out 'controller-error.txt')
 throw
} finally {
 & "$tools\stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','run.json','launch.json','host.json','stall.json','hard-cap.json','Captures','milian-stage.txt','milian-results.txt') {
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 & "$tools\remove-run.ps1" -RunId $run.runId
 Write-Output $out
}
