param([ValidateSet('Waylith','Unity')][string]$Engine='Waylith', [switch]$VerifyHelp)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$tools=Join-Path $root 'Shared/tools/automation'
$arguments=@{Purpose="Readouts help screenshots and Set defaults header interaction: $Engine, 1920x1080 at 1.0";ModSet='readouts-display-verification';AdditionalModSource=@("$PSScriptRoot/mod")}
if($Engine -eq 'Unity') {
    $arguments.GameRoot="$root/Readouts/temp/linux-validation/game-1.6"
    $arguments.WslDistribution='Ubuntu-22.04';$arguments.WslRenderer='NVIDIA'
    $arguments.SavePattern='Fisso-NAM.rws';$arguments.BaselineConfigPath="$tools/profile-template/Config/ModsConfig-1.6.xml"
}
$run=& "$tools/create-run.ps1" @arguments
$out="$root/Readouts/temp/help-setup/$Engine-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    [IO.File]::WriteAllText((Join-Path $run.profile 'help-media.txt'),'1920x1080 at 1.0')
    [IO.File]::WriteAllText((Join-Path $run.profile 'Config/EPrimeReadouts.Default.xml'),'<Readouts Version="2"><Pools/><Groups><Group Name="Another colony"><Tier><Slot>Steel</Slot></Tier></Group></Groups><Thresholds/><CountRules/></Readouts>')
    $prefsPath=Join-Path $run.profile 'Config/Prefs.xml'
    [xml]$prefs=Get-Content $prefsPath -Raw
    $prefs.PrefsData.uiScale='1';$prefs.PrefsData.screenWidth='1920';$prefs.PrefsData.screenHeight='1080'
    $prefs.Save($prefsPath)
    & "$tools/launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools/automation-common.ps1" -RunId $run.runId
    function Step([string]$id,[string]$scene) {
        [IO.File]::WriteAllText((Join-Path $run.profile 'display-request.txt'),"$id;1920;1080;1;False;$scene`n")
        $deadline=[DateTime]::UtcNow.AddSeconds(12)
        do {
            Invoke-SharedGameCommand @{command='status'} | Out-Null
            if(Test-Path (Join-Path $run.profile 'display-error.txt')){throw (Get-Content (Join-Path $run.profile 'display-error.txt') -Raw)}
            if(Test-Path (Join-Path $run.profile "display-$id.xml")){break}
            if([DateTime]::UtcNow -gt $deadline){throw "Help capture $id timed out"}
            Start-Sleep -Milliseconds 100
        }while($true)
        [xml]$r=Get-Content (Join-Path $run.profile "display-$id.xml") -Raw
        if($r.Display.width -ne '1920' -or $r.Display.height -ne '1080' -or $r.Display.scale -ne '1'){throw 'Incorrect capture metrics'}
        return $r.Display
    }
    function Control($document,[string]$label) {
        $item=@($document.Label | Where-Object text -eq $label)[0]
        if(-not $item){throw "Missing control $label"}
        return $item
    }
    function ClickControl($item) {
        Invoke-SharedGameCommand @{command='click';x=([int]$item.x+[int]$item.width-12);y=([int]$item.y+12);button=0} | Out-Null
    }
    $editor=Step 'editor' 'overview'
    $previous=-1
    foreach($name in 'Import','Export','Set defaults','Restore defaults') {
        $control=Control $editor $name
        if([float]$control.x -le $previous){throw 'Incorrect header button order'}
        $previous=[float]$control.x
    }
    & "$tools/capture.ps1" -RunId $run.runId -OutName 'editor.png'
    ClickControl (Control $editor 'Set defaults')
    $defaults=Step 'defaults-off' 'inspect'
    if($defaults.windowType -ne 'Dialog_SetupChoice'){throw 'Set defaults did not open its dialog'}
    ClickControl (Control $defaults 'Automatically update after configuration edits')
    $defaults=Step 'defaults' 'inspect'
    & "$tools/capture.ps1" -RunId $run.runId -OutName 'defaults.png'
    foreach($scene in 'export','import','conflict','options') {
        $null=Step $scene $scene
        & "$tools/capture.ps1" -RunId $run.runId -OutName "$scene.png"
    }
    if($VerifyHelp) {
        foreach($topic in 'the-editor-window','sharing-groups','new-game-defaults','panel-position-and-size','counter-font') {
            $page=Step "help-$topic" "help:$topic"
            if($page.helpTopic -ne $topic -or [int]$page.helpImages -lt 1 -or $page.missingHelpImages -ne '0'){throw "Help failed to load its topic/images: $topic"}
            if($page.visibleHelpMetadata -ne '0'){throw "Localization metadata appeared in rendered help: $topic"}
            & "$tools/capture.ps1" -RunId $run.runId -OutName "help-$topic.png"
        }
    }
    'PASS header order and Set defaults click; all sources captured at 1920x1080 / 1.0' | Set-Content (Join-Path $out 'results.txt')
} finally {
    & "$tools/stop.ps1" -RunId $run.runId
    foreach($name in 'Player.log','Player.log.host-error.txt','host.json','run.json','launch.json','Captures','stall.json') {
        $path=Join-Path $run.profile $name
        if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse}
    }
    Get-ChildItem -LiteralPath $run.profile -Filter 'display-*' -File | Copy-Item -Destination $out
    & "$tools/remove-run.ps1" -RunId $run.runId
}
Write-Output $out
