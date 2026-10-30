$ErrorActionPreference='Stop'
$tools='D:/Code/RimWorld/Shared/tools/automation'
$run=& "$tools/create-run.ps1" -Purpose 'Implanner fix verification: blocked surgery cap starvation and existing bill doctor-floor update' -ModSet implanner-review-surgery-gates -AdditionalModSource @("$PSScriptRoot/Probe")
$out=Join-Path 'D:/Code/RimWorld/Implanner/temp/fix-verification'  "evidence-$($run.runId)"
New-Item -ItemType Directory -Force -Path $out|Out-Null
try {
 & "$tools/launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools/automation-common.ps1" -RunId $run.runId
 $deadline=[DateTime]::UtcNow.AddSeconds(10)
 do {
  Invoke-SharedGameCommand @{command='status'} | Out-Null
  $file=Join-Path $run.profile 'surgery-gates-stage.txt'
  if(Test-Path $file){$stage=(Get-Content $file -Raw).Trim();break}
  if([DateTime]::UtcNow -gt $deadline){throw 'Probe did not complete'}
  Start-Sleep -Milliseconds 100
 }while($true)
} finally {
 & "$tools/stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','run.json','launch.json','host.json','stall.json','hard-cap.json','surgery-gates-stage.txt','surgery-gates-evidence.txt'){
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 & "$tools/remove-run.ps1" -RunId $run.runId
}
Write-Output $out
if($stage -ne 'SUCCESS'){throw "Probe failed: $stage"}
Get-Content (Join-Path $out 'surgery-gates-evidence.txt')
