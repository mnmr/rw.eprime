param([ValidateSet('1.6','1.7')][string]$Version='1.7')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$tools=Join-Path $root 'Shared/tools/automation'
$arguments=@{Purpose="Quality Jobs $Version final clean Options and Help load (no verification probe)";ModSet='qualityjobs-only';ModSourceRoot=$root}
if($Version -eq '1.6') {
 $arguments.GameRoot="$root/Readouts/temp/linux-validation/game-1.6"
 $arguments.WslDistribution='Ubuntu-22.04';$arguments.WslRenderer='Software'
 $arguments.SavePattern='Fisso-NAM.rws';$arguments.BaselineConfigPath="$tools/profile-template/Config/ModsConfig-1.6.xml"
}
$run=& "$tools/create-run.ps1" @arguments
$out="$root/QualityJobs/temp/help/$Version-clean-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
 [xml]$prefs=Get-Content (Join-Path $run.profile 'Config/Prefs.xml') -Raw
 $prefs.PrefsData.uiScale='1';$prefs.Save((Join-Path $run.profile 'Config/Prefs.xml'))
 & "$tools/launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
 . "$tools/automation-common.ps1" -RunId $run.runId
 $state=Invoke-SharedGameCommand @{command='status'}
 Invoke-SharedGameCommand @{command='click';x=1908;y=950} | Out-Null
 & "$tools/capture.ps1" -RunId $run.runId -OutName 'clean-options.png'
 $scale=[double]$state.uiScale
 $helpX=[int](($state.width-1180*$scale)/2+303*$scale)
 $helpY=[int](($state.height-760*$scale)/2+34*$scale)
 Invoke-SharedGameCommand @{command='click';x=$helpX;y=$helpY} | Out-Null
 & "$tools/capture.ps1" -RunId $run.runId -OutName 'clean-help.png'
 $dll=Get-ChildItem (Join-Path $run.profile 'Game/Mods') -Recurse -Filter QualityJobs.dll | Where-Object FullName -like "*/$Version/Assemblies/*"
 # Native Windows separators are retained by Get-ChildItem.
 if(-not $dll){$dll=Get-ChildItem (Join-Path $run.profile 'Game/Mods') -Recurse -Filter QualityJobs.dll | Where-Object FullName -like "*\$Version\Assemblies\*"}
 if(@($dll).Count -ne 1){throw 'Expected exactly one matching Quality Jobs build'}
 $actual=(Get-FileHash -LiteralPath $dll.FullName).Hash
 $expected=(Get-FileHash "$root/QualityJobs/mod/$Version/Assemblies/QualityJobs.dll").Hash
 if($actual -ne $expected){throw 'Copied assembly differs from final build'}
 "PASS final $Version assembly SHA256 $actual; no verification probe loaded" | Set-Content (Join-Path $out 'result.txt')
} finally {
 & "$tools/stop.ps1" -RunId $run.runId
 foreach($name in 'Player.log','run.json','launch.json','ready.json','host.json','linux-host.json','stall.json','Captures'){
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
 }
 & "$tools/remove-run.ps1" -RunId $run.runId
}
Write-Output $out
