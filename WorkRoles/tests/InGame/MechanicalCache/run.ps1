param([string]$Label='proof',[string]$ModSourceRoot='D:\Code\RimWorld',[switch]$ExpectRed)
$ErrorActionPreference='Stop'
$tools='D:\Code\RimWorld\Shared\tools\automation'
$run=& "$tools\create-run.ps1" -Purpose "WorkRoles PF1 mechanical cache selective invalidation: $Label" -ModSourceRoot $ModSourceRoot -ModSet workroles-mechanical-cache -AdditionalModSource "$PSScriptRoot\Probe" -GameVersion 1.7 -SavePattern Fisso-NAM-1.7.rws
$out=Join-Path $PSScriptRoot "../../../temp/mechanical-cache/$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
Write-Output "RUN $($run.runId) $out"
try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 $deadline=[DateTime]::UtcNow.AddSeconds(58)
 do {
  Invoke-SharedGameCommand @{command='status'} | Out-Null
  $path=Join-Path $run.profile 'mechanical-cache-stage.txt'
  $stage=if(Test-Path $path){(Get-Content -LiteralPath $path -Raw).Trim()}else{''}
  if($stage.StartsWith('ERROR')){throw $stage}
  if($stage -eq 'SUCCESS'){if($ExpectRed){throw 'Baseline unexpectedly passed'};break}
  if($stage.StartsWith('RED')){if(-not $ExpectRed){throw $stage};break}
  if([DateTime]::UtcNow -gt $deadline){throw "Timeout $stage"}
  Start-Sleep -Milliseconds 100
 }while($true)
} finally {
 & "$tools\stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','run.json','launch.json','host.json','mechanical-cache-stage.txt','mechanical-cache-results.txt') {
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 & "$tools\remove-run.ps1" -RunId $run.runId
 Write-Output $out
}

