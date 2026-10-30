param([ValidateSet('1.6','1.7')][string]$Version='1.7', [string]$Label='review',
 [ValidateSet('1','1.25')][string]$UiScale='1.25', [switch]$WithoutBiotech,
 [ValidateSet('visual','native')][string]$Phase='visual', [string]$ModSourceRoot='')
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools=Join-Path $source 'Shared\tools\automation'
if(-not $ModSourceRoot){$ModSourceRoot=$source}
$params=@{Purpose="WorkRoles mechanical $Phase regressions ($Version $Label scale $UiScale)";ModSourceRoot=$ModSourceRoot;ModSet='workroles-mechanical-review';AdditionalModSource=@("$PSScriptRoot\Probe")}
if($WithoutBiotech){$params.ModSet='workroles-mechanical-review-no-biotech'}
if($Version -eq '1.6') {
 $params.GameRoot='D:\Code\RimWorld\Readouts\temp\linux-validation\game-1.6'
 $params.WslDistribution='Ubuntu-22.04';$params.WslRenderer='Software'
 $params.SavePattern='Fisso-NAM.rws';$params.BaselineConfigPath="$tools\profile-template\Config\ModsConfig-1.6.xml"
}
$run=& "$tools\create-run.ps1" @params
$out=Join-Path $source "WorkRoles\temp\mechanical-review\$Version-$Label-scale$UiScale-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$run.runId | Set-Content (Join-Path $out 'run-id.txt')
$Phase | Set-Content -LiteralPath (Join-Path $run.profile 'review-phase.txt')
[xml]$prefs=Get-Content -LiteralPath (Join-Path $run.profile 'Config\Prefs.xml') -Raw
$prefs.PrefsData.uiScale=$UiScale
$prefs.Save((Join-Path $run.profile 'Config\Prefs.xml'))
Write-Output "SNAPSHOT_READY $($run.runId) $out"
try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 $deadline=[DateTime]::UtcNow.AddSeconds(58)
 $captured=@{}
 do {
  Invoke-SharedGameCommand @{command='status'} | Out-Null
  $file=Join-Path $run.profile 'review-stage.txt'
  $stage=if(Test-Path $file){(Get-Content $file -Raw).Trim()}else{''}
  if($stage.StartsWith('CAPTURE ') -and -not $captured.ContainsKey($stage)){
   $name=$stage.Substring(8)
   if($name -eq 'human-hover-current') {
    $point=(Get-Content -LiteralPath (Join-Path $run.profile 'review-hover.txt') -Raw).Trim().Split(',')
    & "$tools\capture-hover.ps1" -RunId $run.runId -X ([int][math]::Round([double]$point[0])) -Y ([int][math]::Round([double]$point[1])) -OutName ($name+'.png') -WaitMilliseconds 1200
   } else { & "$tools\capture.ps1" -RunId $run.runId -OutName ($name+'.png') }
   [IO.File]::WriteAllText((Join-Path $run.profile ('captured-'+$name)), '')
   $captured[$stage]=$true
  }
  if($stage.StartsWith('FAILED')){& "$tools\capture.ps1" -RunId $run.runId -OutName 'failed.png';throw $stage}
  if($stage.StartsWith('COMPLETE ')){throw $stage}
  if($stage -eq 'SUCCESS'){break}
  if([DateTime]::UtcNow -gt $deadline){throw "Scenario timeout: $stage"}
  Start-Sleep -Milliseconds 100
 }while($true)
} catch {
 # Keep the primary controller failure if run teardown also reports an error.
 $_ | Out-String | Set-Content -LiteralPath (Join-Path $out 'controller-error.txt')
 throw
} finally {
 & "$tools\stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','Player.log.host-error.txt','run.json','launch.json','host.json','linux-host.json','linux-console.log','linux-host.log','stall.json','hard-cap.json','Captures','review-stage.txt','review-checks.txt','review-observations.txt','review-geometry.tsv','Saves/MechanicalReviewAirborne.rws') {
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 & "$tools\remove-run.ps1" -RunId $run.runId
 Write-Output $out
}

