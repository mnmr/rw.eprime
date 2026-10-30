param([switch]$Quality)
$ErrorActionPreference='Stop'
$tools='D:\Code\RimWorld\Shared\tools\automation'
$run=& "$tools\create-run.ps1" -Purpose 'Implanner fix verification: production options, powered benches, VGE prerequisites and mixed-quality coverage' -ModSet implanner-review-production -SavePattern 'Fisso-NAM-1.7.rws' -AdditionalModSource "$PSScriptRoot\Probe"
if($Quality){'quality' | Set-Content (Join-Path $run.profile 'production-quality-mode.txt')}
$out=Join-Path 'D:/Code/RimWorld/Implanner/temp/fix-verification' "evidence-$($run.runId)"
New-Item -ItemType Directory -Force $out | Out-Null

try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 $deadline=[DateTime]::UtcNow.AddSeconds(120)
 do {
  $stage=Join-Path $run.profile 'production-review.txt'
  $text=if(Test-Path $stage){Get-Content -Raw $stage}else{''}
  if($text.Contains('FAILED')){throw $text}
  if(($text.Contains('QUALITY_READY') -or $text.Contains('SUCCESS'))){break}
  Invoke-SharedGameCommand @{command='hover';x=1919;y=0}|Out-Null
  Start-Sleep -Milliseconds 100
 }while([DateTime]::UtcNow -lt $deadline)
 if(-not ($text.Contains('QUALITY_READY') -or $text.Contains('SUCCESS'))){throw 'Mixed-quality setup did not complete'}
 & "$tools\capture.ps1" -RunId $run.runId -OutName 'mixed-quality-overview.png'
} finally {
 & "$tools\stop.ps1" -RunId $run.runId
 foreach($name in 'production-review.txt','Player.log','run.json','launch.json','host.json','stall.json','Captures'){
  $from=Join-Path $run.profile $name
  if(Test-Path $from){Copy-Item -LiteralPath $from -Destination $out -Recurse -Force}
 }
 Copy-Item -LiteralPath "$PSScriptRoot\Probe.cs" -Destination $out
 & "$tools\remove-run.ps1" -RunId $run.runId
}
$out
