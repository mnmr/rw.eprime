param(
 [ValidateSet('1.6','1.7')][string]$Version='1.7',
 [ValidateSet('baseline','features','har','clean')][string]$Scenario='features',
 [string]$ModSourceRoot='', [string]$SaveRoot=''
)
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
if(-not $ModSourceRoot){$ModSourceRoot=$source}
if(-not $SaveRoot){$SaveRoot=Join-Path $source 'WorkRoles\temp\work-capabilities\baseline'}
$tools=Join-Path $source 'Shared\tools\automation'
$params=@{Purpose="WorkRoles $Version work evidence ${Scenario}: exact Fisso input capture and completed mining";ModSourceRoot=$ModSourceRoot;SourceSaveDirectory=$SaveRoot}
if($Scenario -eq 'clean'){$params.ModSet='own-mods-waylith'}
else{$params.ModSet='work-evidence';$params.AdditionalModSource=@("$PSScriptRoot\Probe")}
if($Scenario -eq 'baseline'){$params.AdditionalModSource=@("$PSScriptRoot\BaselineProbe")}
if($Scenario -eq 'har'){$params.ModSet='work-evidence-har';$params.AdditionalModSource+=("$PSScriptRoot\HarFixture")}
if($Version -eq '1.6'){
 $params.GameRoot='D:\Code\RimWorld\Readouts\temp\linux-validation\game-1.6'
 $params.WslDistribution='Ubuntu-22.04';$params.WslRenderer='NVIDIA'
 $params.SavePattern='Fisso-NAM.rws';$params.BaselineConfigPath="$tools\profile-template\Config\ModsConfig-1.6.xml"
}else{$params.SavePattern='Fisso-NAM-1.7.rws'}
$run=& "$tools\create-run.ps1" @params
$out=Join-Path $source "WorkRoles\temp\work-capabilities\$Version-$Scenario-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$run.runId | Set-Content (Join-Path $out 'run-id.txt')
try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 if($Scenario -ne 'clean'){
  $deadline=[DateTime]::UtcNow.AddSeconds(40)
  do {
   Invoke-SharedGameCommand @{command='status'} | Out-Null
   $file=Join-Path $run.profile 'work-evidence-stage.txt'
   $state=if(Test-Path $file){(Get-Content $file -Raw).Trim()}else{''}
   if($state.StartsWith('FAILED')){throw $state}
   if($state -eq 'captured'){
    $(if($Scenario -in @('features','har')){$Scenario}else{'finish'}) | Set-Content (Join-Path $run.profile 'work-evidence-control.txt')
   }
   if($state -eq 'SUCCESS'){break}
   if([DateTime]::UtcNow -gt $deadline){throw "Verification timed out at $state"}
   Start-Sleep -Milliseconds 150
  }while($true)
 }
 & "$tools\capture.ps1" -RunId $run.runId -OutName 'verified.png'
}finally{
 & "$tools\stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','Player.log.host-error.txt','run.json','launch.json','host.json','linux-host.json','linux-console.log','linux-host.log','stall.json','hard-cap.json','Captures','work-evidence-colony.xml','work-evidence-stage.txt'){
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 & "$tools\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Select-String -LiteralPath (Join-Path $out 'Player.log') -Pattern '\[WorkEvidenceProbe\]' | ForEach-Object Line
if($Scenario -ne 'clean' -and (Get-Content (Join-Path $out 'work-evidence-stage.txt') -Raw).Trim() -ne 'SUCCESS'){throw "Scenario failed: $out"}
