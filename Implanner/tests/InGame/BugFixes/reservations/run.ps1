$ErrorActionPreference='Stop'
$tools='D:\Code\RimWorld\Shared\tools\automation'
$run=& "$tools\create-run.ps1" -Purpose 'Implanner fix verification: native doctor consumes manually held-back arm after owned reservation release' -ModSet implanner-review-reservations -AdditionalModSource "$PSScriptRoot\Probe"
$out=Join-Path 'D:/Code/RimWorld/Implanner/temp/fix-verification'  $run.runId
New-Item -ItemType Directory -Force -Path $out | Out-Null
$run.runId | Set-Content (Join-Path $out 'run-id.txt')
try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 $deadline=[DateTime]::UtcNow.AddSeconds(35)
 do {
  Invoke-SharedGameCommand @{command='status'} | Out-Null
  $file=Join-Path $run.profile 'reservation-stage.txt'
  if(Test-Path $file){$stage=(Get-Content $file -Raw).Trim();if($stage -eq 'SUCCESS'){break};if($stage.StartsWith('FAILED')){throw $stage}}
  if([DateTime]::UtcNow -gt $deadline){throw "Scenario timed out at $stage"}
  Start-Sleep -Milliseconds 150
 }while($true)
 & "$tools\capture.ps1" -RunId $run.runId -OutName 'reservation-result.png'
} finally {
 & "$tools\stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','run.json','launch.json','host.json','reservation-stage.txt','reservation-review.txt','Captures','stall.json','hard-cap.json') {
  $p=Join-Path $run.profile $name
  if(Test-Path $p){Copy-Item -LiteralPath $p -Destination $out -Recurse -Force}
 }
 & "$tools\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'reservation-review.txt')
