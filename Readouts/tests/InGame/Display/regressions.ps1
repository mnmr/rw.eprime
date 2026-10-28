param([ValidateSet('Waylith','Unity')][string]$Engine='Waylith',[string]$Label='verification')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$tools=Join-Path $root 'Shared/tools/automation'
$arguments=@{Purpose="Readouts import choices, tooltip transitions and option clicks: $Engine $Label";ModSet='readouts-display-verification';AdditionalModSource=@("$PSScriptRoot/mod")}
if($Engine -eq 'Unity'){$arguments.GameRoot="$root/Readouts/temp/linux-validation/game-1.6";$arguments.WslRenderer='NVIDIA';$arguments.SavePattern='Fisso-NAM.rws';$arguments.BaselineConfigPath="$tools/profile-template/Config/ModsConfig-1.6.xml"}
$run=& "$tools/create-run.ps1" @arguments
$out="$root/Readouts/temp/display/$Label-$Engine-regressions-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$results=[Collections.Generic.List[string]]::new()
function Check($condition,$name){$results.Add("$(if($condition){'PASS'}else{'FAIL'}) $name")}
try {
    & "$tools/launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools/automation-common.ps1" -RunId $run.runId
    function Step([string]$id,[string]$scene) {
        [IO.File]::WriteAllText((Join-Path $run.profile 'display-request.txt'),"$id;1024;768;1;False;$scene`n")
        $deadline=[DateTime]::UtcNow.AddSeconds(10)
        do {
            Invoke-SharedGameCommand @{command='status'} | Out-Null
            if(Test-Path (Join-Path $run.profile 'display-error.txt')){throw (Get-Content (Join-Path $run.profile 'display-error.txt') -Raw)}
            if(Test-Path (Join-Path $run.profile "display-$id.xml")){break}
            if([DateTime]::UtcNow -gt $deadline){throw "Case $id timed out."}
            Start-Sleep -Milliseconds 100
        }while($true)
        [xml]$r=Get-Content (Join-Path $run.profile "display-$id.xml") -Raw
        return $r.Display
    }
    function Point($document,[string]$text,[string]$command) {
        $label=@($document.Label | Where-Object text -eq $text)[0]
        if(-not $label){throw "Missing control '$text'."}
        Invoke-SharedGameCommand @{command=$command;x=([int]$label.x+12);y=([int]$label.y+12);button=0} | Out-Null
    }
    $initial=Step '01' 'dependency'
    Point $initial 'Groups' 'click'
    $one=Step '04' 'inspect'
    Check ($one.selected -eq '6' -and $one.canImport -eq 'false') 'incomplete selection can be edited while import stays disabled'
    Point $one 'Import' 'hover'
    Start-Sleep -Milliseconds 650
    $explanation=Step '04a' 'inspect'
    Check ($explanation.tip -match 'Old') 'disabled Import explains the outstanding dependency'
    & "$tools/capture.ps1" -RunId $run.runId -OutName 'dependency.png' -KeepCursor
    Point $initial 'Resource pools' 'click'
    $two=Step '05' 'inspect'
    Check ($two.selected -eq '4' -and $two.canImport -eq 'true') 'threshold-only import is reachable despite different pool dependencies'
    if($two.selected -eq '4' -and $two.canImport -eq 'true') {
        Point $two 'Import' 'click'
        $applied=Step '06' 'inspect'
        Check ($applied.firstGroup -eq 'Before' -and $applied.steelLow -eq '25') 'threshold import preserves current groups and pools'
    }
    $options=Step '07' 'reopen'
    Invoke-SharedGameCommand @{command='hover';x=1023;y=0} | Out-Null
    & "$tools/capture.ps1" -RunId $run.runId -OutName 'options-before.png' -KeepCursor
    Point $options 'Show search filter' 'click'
    $shown=Step '08' 'inspectoptions'
    Check ($shown.searchShown -eq 'true' -and -not @($shown.Label | Where-Object text -eq 'Show mod name').Count) 'search toggle applies and hides its conditional option'
    Point $shown 'Show search filter' 'click'
    $hidden=Step '09' 'inspectoptions'
    Check ($hidden.searchShown -eq 'false' -and @($hidden.Label | Where-Object text -eq 'Show mod name').Count -eq 1) 'search toggle restores its conditional option'
    Invoke-SharedGameCommand @{command='hover';x=1023;y=0} | Out-Null
    & "$tools/capture.ps1" -RunId $run.runId -OutName 'options.png' -KeepCursor
} finally {
    & "$tools/stop.ps1" -RunId $run.runId
    foreach($name in 'Player.log','run.json','launch.json','Captures','stall.json') { $path=Join-Path $run.profile $name;if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse} }
    Get-ChildItem -LiteralPath $run.profile -Filter 'display-*' -File | Copy-Item -Destination $out
    $results | Set-Content (Join-Path $out 'results.txt')
    & "$tools/remove-run.ps1" -RunId $run.runId
}
$results
Write-Output $out
if(@($results | Where-Object {$_ -like 'FAIL*'}).Count){throw 'Readouts interaction regressions failed.'}
