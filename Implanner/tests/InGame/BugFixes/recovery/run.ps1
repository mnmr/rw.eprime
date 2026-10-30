$ErrorActionPreference='Stop'
$tools='D:\Code\RimWorld\Shared\tools\automation'
$run=& "$tools\create-run.ps1" -Purpose 'Implanner fix verification: QBR healed-arm downgrade and fatal heart quality removal native outcomes' -ModSet implanner-review-recovery -GameRoot 'D:\Code\RimWorld\Readouts\temp\linux-validation\game-1.6' -WslDistribution Ubuntu-22.04 -WslRenderer Software -SavePattern Fisso-NAM.rws -BaselineConfigPath "$tools\profile-template\Config\ModsConfig-1.6.xml" -AdditionalModSource "$PSScriptRoot\Probe"
$out=Join-Path 'D:/Code/RimWorld/Implanner/temp/fix-verification' $run.runId
New-Item -ItemType Directory -Force -Path $out | Out-Null
$run.runId | Set-Content (Join-Path $out 'run-id.txt')
try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 $deadline=[DateTime]::UtcNow.AddSeconds(35)
 do {
  Invoke-SharedGameCommand @{command='status'} | Out-Null
  $file=Join-Path $run.profile 'recovery-stage.txt'
  if(Test-Path $file){$stage=(Get-Content $file -Raw).Trim();if($stage -eq 'SUCCESS'){break};if($stage.StartsWith('FAILED')){throw $stage}}
  if([DateTime]::UtcNow -gt $deadline){throw "Scenario timed out at $stage"}
  Start-Sleep -Milliseconds 150
 }while($true)
 & "$tools\capture.ps1" -RunId $run.runId -OutName 'recovery-result.png'
} finally {
 & "$tools\stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','run.json','launch.json','host.json','recovery-stage.txt','recovery-review.txt','Captures','stall.json','hard-cap.json') {
  $p=Join-Path $run.profile $name
  if(Test-Path $p){Copy-Item -LiteralPath $p -Destination $out -Recurse -Force}
 }
 & "$tools\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'recovery-review.txt')
