param([switch]$Vertical, [float]$Scale=1.25)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$tools=Join-Path $root 'Shared/tools/automation'
& "$tools/list-runs.ps1" -Json | Out-Null
$run=& "$tools/create-run.ps1" -GameVersion 1.7 -SavePattern 'Fisso-NAM-1.7.rws' -Purpose "Readouts RD3 click through empty tiers; hover and idle collapse; vertical=$Vertical" -ModSet readouts-empty-tiers -AdditionalModSource @("$PSScriptRoot/mod")
$out=Join-Path $root "Readouts/temp/empty-tiers/$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$script:serial=0
function State([string]$action='inspect') {
    $script:serial++
    $id=$script:serial.ToString('D2')
    $request=Join-Path $run.profile 'tiers-request.txt'
    [IO.File]::WriteAllText("$request.tmp", "$id;$action`n")
    [IO.File]::Move("$request.tmp", $request, $true)
    $deadline=[DateTime]::UtcNow.AddSeconds(6)
    $path=Join-Path $run.profile "tiers-$id.xml"
    while(-not(Test-Path $path)) {
        if(Test-Path (Join-Path $run.profile 'tiers-error.txt')){throw (Get-Content (Join-Path $run.profile 'tiers-error.txt') -Raw)}
        if([DateTime]::UtcNow -gt $deadline){throw "Timed out waiting for $id $action"}
        Start-Sleep -Milliseconds 50
    }
    [xml]$xml=Get-Content $path -Raw
    return $xml.State
}
function Check($condition,[string]$message) {
    if(-not $condition){throw $message}
    Add-Content (Join-Path $out 'results.txt') "PASS $message"
}
try {
    $prefsPath=Join-Path $run.profile 'Config/Prefs.xml'
    [xml]$prefs=Get-Content $prefsPath -Raw
    $prefs.PrefsData.uiScale=$Scale.ToString([Globalization.CultureInfo]::InvariantCulture)
    $prefs.Save($prefsPath)
    & "$tools/launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -HardCapSeconds 60 -AllowRecoverableSaveErrors
    . "$tools/automation-common.ps1" -RunId $run.runId
    foreach($mode in @('False;False','True;False','True;True')) {
        Invoke-SharedGameCommand @{command='hover';x=1000;y=600} | Out-Null
        $state=State "setup;$mode;$([bool]$Vertical)"
        Check ($state.SelectNodes('Slot').Count -eq 0) "$mode initially hides zero-count items"
        Invoke-SharedGameCommand @{command='hover';x=[int]$state.x;y=[int]$state.y} | Out-Null
        $state=State
        Check ([int]$state.markers -eq 1) "$mode hover exposes clickable tier controls"
        if($mode -eq 'True;False') {
            Check ($state.Slot.token -eq '~Steel' -and [int]$state.Slot.count -gt 0) "$mode hover reveals stocked tier3"
            Check ((@($state.Arrow) -join ',') -eq 'Lit,HoverLit,HoverLit') "$mode hover preserves configured-depth tint"
        }
        else { Check ($state.SelectNodes('Slot').Count -eq 0 -and $state.Arrow[0] -eq 'Lit') "$mode tier1 empty but selected" }
        & "$tools/capture.ps1" -RunId $run.runId -OutName "$mode-tier1.png" -KeepCursor
        foreach($depth in 2,3) {
            Invoke-SharedGameCommand @{command='click';x=[int]$state.x;y=[int]$state.y;button=0} | Out-Null
            $state=State
            Check ([int]$state.depth -eq $depth) "$mode real click advances configured depth to $depth"
            Check ([int]$state.markers -eq 1) "$mode depth$depth retains click target"
            if($depth -eq 2 -and $mode -ne 'True;False') { Check ($state.SelectNodes('Slot').Count -eq 0) "$mode tier2 stays empty" }
        }
        Check ($state.Slot.token -eq '~Steel' -and [int]$state.Slot.count -gt 0) "$mode tier3 shows actual stock"
        & "$tools/capture.ps1" -RunId $run.runId -OutName "$mode-tier3.png" -KeepCursor
        Invoke-SharedGameCommand @{command='hover';x=1000;y=600} | Out-Null
        $state=State
        Check ([int]$state.depth -eq 3) "$mode pointer leave preserves selected depth"
        if($mode -eq 'True;True') { Check ($state.SelectNodes('Slot').Count -eq 0 -and [int]$state.markers -eq 0) "$mode idle collapses band" }
        else { Check ($state.Slot.token -eq '~Steel') "$mode pointer leave retains tier3" }
        Invoke-SharedGameCommand @{command='hover';x=[int]$state.x;y=[int]$state.y} | Out-Null
        $state=State
        Check ($state.Slot.token -eq '~Steel') "$mode reentry restores tier3"
        Invoke-SharedGameCommand @{command='click';x=[int]$state.x;y=[int]$state.y;button=0} | Out-Null
        $state=State
        Check ([int]$state.depth -eq 1 -and [int]$state.markers -eq 1) "$mode cycles back to clickable tier1"
    }
} finally {
    & "$tools/stop.ps1" -RunId $run.runId
    foreach($name in 'Player.log','run.json','launch.json','Captures','host.json') {
        $path=Join-Path $run.profile $name
        if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse}
    }
    Get-ChildItem -LiteralPath $run.profile -Filter 'tiers-*' -File | Copy-Item -Destination $out
    & "$tools/remove-run.ps1" -RunId $run.runId
    Write-Output $out
}
Get-Content (Join-Path $out 'results.txt')

