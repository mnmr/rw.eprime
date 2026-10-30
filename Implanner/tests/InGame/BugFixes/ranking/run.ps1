$ErrorActionPreference='Stop'
$tools='D:/Code/RimWorld/Shared/tools/automation'
$run=& "$tools/create-run.ps1" -Purpose 'Implanner fix verification: actual small horizontal same-row ranking drag at UI scale 1.25' -ModSet implanner-review-ranking -AdditionalModSource @("$PSScriptRoot/Probe")
$out=Join-Path 'D:/Code/RimWorld/Implanner/temp/fix-verification'  "evidence-$($run.runId)"
New-Item -ItemType Directory -Force -Path $out|Out-Null
try {
 & "$tools/launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools/automation-common.ps1" -RunId $run.runId
 function Wait-Stage([string]$want){$end=[DateTime]::UtcNow.AddSeconds(12);do{Invoke-SharedGameCommand @{command='status'}|Out-Null;$file=Join-Path $run.profile 'ranking-stage.txt';$stage=if(Test-Path $file){(Get-Content $file -Raw).Trim()}else{''};if($stage -eq $want){return};if($stage.StartsWith('FAILED') -or [DateTime]::UtcNow -gt $end){throw "Expected $want, got $stage"};Start-Sleep -Milliseconds 100}while($true)}
 Wait-Stage 'ready'
 & "$tools/capture.ps1" -RunId $run.runId -OutName 'ranking-before.png'
 $point=(Get-Content (Join-Path $run.profile 'ranking-point.txt') -Raw).Trim().Split(',');$x=[int]$point[0];$y=[int]$point[1]
 "drag $x $y $($x+10) $y 500" | Set-Content (Join-Path $out 'actual.actions')
 & "$tools/run-sequence.ps1" -RunId $run.runId -File (Join-Path $out 'actual.actions')
 Wait-Stage 'SUCCESS'
 & "$tools/capture.ps1" -RunId $run.runId -OutName 'ranking-after.png'
} finally {
 & "$tools/stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','run.json','launch.json','host.json','stall.json','hard-cap.json','ranking-stage.txt','ranking-evidence.txt','ranking-point.txt','Captures'){
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 & "$tools/remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'ranking-evidence.txt')
