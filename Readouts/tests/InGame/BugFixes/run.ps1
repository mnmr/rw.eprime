param([string]$Label='verification', [float]$Scale=1.25, [ValidateSet('all','ui')][string]$Mode='all')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$tools=Join-Path $root 'Shared/tools/automation'
$run=& "$tools/create-run.ps1" -Purpose "Readouts RD001-RD005 native crafting, Strata counts, pool editing and German search ($Label)" -ModSet readouts-bug-fixes -AdditionalModSource @("$PSScriptRoot/mod")
$out=Join-Path $root "Readouts/temp/bug-fixes/$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    [IO.File]::WriteAllText((Join-Path $run.profile 'fix-mode.txt'), $Mode)
    $prefsPath=Join-Path $run.profile 'Config/Prefs.xml'
    [xml]$prefs=Get-Content -LiteralPath $prefsPath -Raw
    foreach($name in @('langFolderName','uiScale')) {
        $node=$prefs.DocumentElement.SelectSingleNode($name)
        if(-not $node){$node=$prefs.CreateElement($name);[void]$prefs.DocumentElement.AppendChild($node)}
        $node.InnerText=if($name -eq 'langFolderName'){'German (Deutsch)'}else{$Scale.ToString([Globalization.CultureInfo]::InvariantCulture)}
    }
    $prefs.Save($prefsPath)
    & "$tools/launch.ps1" -RunId $run.runId -TimeoutMinutes 5
    . "$tools/automation-common.ps1" -RunId $run.runId
    $deadline=[DateTime]::UtcNow.AddMinutes(8)
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        $request=Join-Path $run.profile 'capture-request.txt'
        if(Test-Path $request) {
            $name=(Get-Content -LiteralPath $request -Raw).Trim()
            $ack=Join-Path $run.profile "capture-$name.done"
            if(-not (Test-Path $ack)) {
                & "$tools/capture.ps1" -RunId $run.runId -OutName "$name.png"
                [IO.File]::WriteAllText($ack,'captured')
            }
        }
        if(Test-Path (Join-Path $run.profile 'fix-done.txt')) { break }
        if([DateTime]::UtcNow -gt $deadline) { throw 'Bug-fix probe timed out.' }
        Start-Sleep -Milliseconds 250
    } while($true)
} finally {
    & "$tools/stop.ps1" -RunId $run.runId
    foreach($name in 'Player.log','run.json','launch.json','Captures','fix-results.txt','fix-done.txt','stall.json','game-state.json') {
        $path=Join-Path $run.profile $name
        if(Test-Path $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    & "$tools/remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'fix-results.txt')
if((Get-Content (Join-Path $out 'fix-done.txt') -Raw).Trim() -ne 'PASS') { throw 'Readouts bug-fix verification failed.' }
