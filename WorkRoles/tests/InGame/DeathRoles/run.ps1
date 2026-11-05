param([string]$Label='proof',[string]$ModSourceRoot='D:\Code\RimWorld',[string]$Version='1.7')
# Kills a role-holding colonist, mech and Misc. Robots robot, resurrects the
# colonist, saves and reloads, then resurrects the mech and repairs the robot.
$ErrorActionPreference='Stop'
$tools='D:\Code\RimWorld\Shared\tools\automation'
$parameters=@{Purpose="WorkRoles owner report: roles survive death (resurrection, robot repair, save/reload): $Label";ModSourceRoot=$ModSourceRoot;ModSet='workroles-death-roles';AdditionalModSource=@("$PSScriptRoot\Probe");GameVersion=$Version;SavePattern="Fisso-NAM-$Version.rws"}
$run=& "$tools\create-run.ps1" @parameters
$out=Join-Path $PSScriptRoot "../../../temp/death-roles/$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
Write-Output "RUN $($run.runId) $out"
try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 $deadline=[DateTime]::UtcNow.AddSeconds(58)
 do {
  Invoke-SharedGameCommand @{command='status'} | Out-Null
  $path=Join-Path $run.profile 'deathroles-stage.txt'
  $stage=if(Test-Path $path){(Get-Content -LiteralPath $path -Raw).Trim()}else{''}
  if($stage.StartsWith('FAILED')){throw $stage}
  if($stage -eq 'SUCCESS'){break}
  if([DateTime]::UtcNow -gt $deadline){throw "Timeout $stage"}
  Start-Sleep -Milliseconds 100
 }while($true)
} catch {
 $_ | Out-String | Set-Content -LiteralPath (Join-Path $out 'controller-error.txt')
 throw
} finally {
 & "$tools\stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','run.json','launch.json','host.json','stall.json','hard-cap.json','deathroles-stage.txt','deathroles-results.txt') {
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 & "$tools\remove-run.ps1" -RunId $run.runId
 Write-Output $out
}
