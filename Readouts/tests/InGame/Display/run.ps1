param([ValidateSet('Waylith','Unity')][string]$Engine='Waylith', [ValidateSet('low','high','dialogs','combined','extreme')][string]$Matrix='low', [string]$Label='verification')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$tools=Join-Path $root 'Shared/tools/automation'
$arguments=@{Purpose="Readouts display: $Engine $Matrix $Label"; ModSet='readouts-display-verification'; AdditionalModSource=@("$PSScriptRoot/mod")}
if($Engine -eq 'Unity') {
    $arguments.GameRoot="$root/Readouts/temp/linux-validation/game-1.6"
    $arguments.WslDistribution='Ubuntu-22.04'; $arguments.WslRenderer='NVIDIA'
    $arguments.SavePattern='Fisso-NAM.rws'; $arguments.BaselineConfigPath="$tools/profile-template/Config/ModsConfig-1.6.xml"
}
$cases=switch($Matrix) {
    low {@('1024;768;1;False;options','1024;768;1;True;options','1280;960;1.25;False;options','1536;1152;1.5;False;options','1792;1344;1.75;False;options','1366;768;1;False;options','1920;1080;1.25;False;shrink','1024;768;1;False;reopen')}
    high {@('2048;1536;2;False;options','2560;1920;2.5;False;options','2560;1440;1.75;False;options','2560;1440;1.25;False;options')}
    # Requires a window manager/display that permits client heights above 2160.
    extreme {@('3072;2304;3;False;options','3584;2688;3.5;False;options','4096;3072;4;False;options')}
    dialogs {@('1024;768;1;False;overview','1024;768;1;False;defaults','1024;768;1;False;import','1024;768;1;False;export','1920;1080;1.25;False;overview','1920;1080;1.25;False;defaults','1920;1080;1.25;False;import','1920;1080;1.25;False;export')}
    combined {@('2048;1536;2;False;options','2560;1920;2.5;False;options','2560;1440;1.75;False;options','2560;1440;1.25;False;options','1024;768;1;False;overview','1024;768;1;False;defaults','1024;768;1;False;import','1024;768;1;False;export','1920;1080;1.25;False;defaults','1920;1080;1.25;False;import')}
}
$failed=$false
$run=& "$tools/create-run.ps1" @arguments
$out="$root/Readouts/temp/display/$Label-$Engine-$Matrix-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    & "$tools/launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools/automation-common.ps1" -RunId $run.runId
    $i=0
    foreach($case in $cases) {
        $id=('{0:D2}' -f (++$i))
        [IO.File]::WriteAllText((Join-Path $run.profile 'display-request.txt'),"$id;$case`n")
        $deadline=[DateTime]::UtcNow.AddSeconds(12)
        do {
            Invoke-SharedGameCommand @{command='status'} | Out-Null
            if(Test-Path (Join-Path $run.profile 'display-error.txt')){throw (Get-Content (Join-Path $run.profile 'display-error.txt') -Raw)}
            if(Test-Path (Join-Path $run.profile "display-$id.xml")){break}
            if([DateTime]::UtcNow -gt $deadline){throw "Display case $case timed out: $(Get-Content (Join-Path $run.profile 'display-wait.txt') -Raw)"}
            Start-Sleep -Milliseconds 100
        }while($true)
        [xml]$result=Get-Content (Join-Path $run.profile "display-$id.xml") -Raw
        $r=$result.Display
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        Invoke-SharedGameCommand @{command='hover';x=([int]$r.width-1);y=0} | Out-Null
        & "$tools/capture.ps1" -RunId $run.runId -OutName "$id.png" -KeepCursor
        $bad=@($r.Label | Where-Object fits -eq 'false')
        if($r.inScreen -ne 'true' -or $r.optionsFit -ne 'true' -or ($r.samples -ne '0' -and ($bad.Count -gt 0 -or [long]$r.drawAllocated -ne 0 -or [int]$r.steadyMeasures -ne 0)) -or [int]$r.setupBuilds -ne 0){$failed=$true}
        [pscustomobject]@{case=$case;screen=$r.inScreen;fit=$r.optionsFit;clipped=$bad.Count;bytesPerDraw=if([int]$r.samples){[long]$r.drawAllocated/[int]$r.samples}else{0};measurements=$r.steadyMeasures;setupBuilds=$r.setupBuilds;idleBytes=$r.idlePendingAllocated} | Format-Table -AutoSize
    }
} finally {
    & "$tools/stop.ps1" -RunId $run.runId
    foreach($name in 'Player.log','run.json','launch.json','Captures','stall.json','host.json','Player.log.host-error.txt','linux-host.log') {
        $path=Join-Path $run.profile $name
        if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse}
    }
    Get-ChildItem -LiteralPath $run.profile -Filter 'display-*' -File | Copy-Item -Destination $out
    & "$tools/remove-run.ps1" -RunId $run.runId
}
Write-Output $out
if($failed){throw 'Display bounds or steady-state regression failed.'}
