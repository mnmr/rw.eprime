param([string]$Label='verification', [ValidateSet('loaded','newgame','manual','accept')][string]$Mode='loaded')
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$tools=Join-Path $source 'Shared/tools/automation'
$modsetName='readouts-setup-verification'
$run=& "$tools/create-run.ps1" -Purpose "Readouts complete setup, first-open decision and batched defaults ($Mode / $Label)" -ModSet $modsetName -AdditionalModSource @("$PSScriptRoot/mod")
$out=Join-Path $source "Readouts/temp/setup/$Label-$Mode-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    $Mode | Set-Content (Join-Path $run.profile 'setup-mode.txt')
    $xml='<Readouts Version="2"><Pools/><Groups><Group Name="Verification metals" DefaultEnabled="False"><Tier><Slot>Steel</Slot></Tier></Group></Groups><Thresholds><Threshold Token="Steel" Low="333" Critical="111"/></Thresholds><CountRules><Rule Token="Steel" StorageOnly="2" HideForbidden="1"/></CountRules><PlayerSettings ProfileId="fixture"><Option Name="panelWidth" Value="321"/><Group Name="Verification metals" Enabled="True"/></PlayerSettings></Readouts>'
    [IO.File]::WriteAllText((Join-Path $run.profile 'Config/EPrimeReadouts.Default.xml'),$xml)
    [IO.File]::WriteAllText((Join-Path $run.profile 'setup-original.xml'),$xml)
    $settingsPath=Join-Path $run.profile 'Config/Mod_EPrimeReadouts_EPrimeReadoutsMod.xml'
    [xml]$settings=Get-Content -LiteralPath $settingsPath -Raw
    foreach($name in @('autoUpdateDefault','panelWidth')) {
        $node=$settings.SettingsBlock.ModSettings.SelectSingleNode($name)
        if(-not $node){$node=$settings.CreateElement($name);[void]$settings.SettingsBlock.ModSettings.AppendChild($node)}
        $node.InnerText=if($name -eq 'autoUpdateDefault'){if($Mode -eq 'manual'){'False'}else{'True'}}else{'140'}
    }
    $settings.Save($settingsPath)
    if($Mode -eq 'newgame') { & "$tools/launch.ps1" -RunId $run.runId -TimeoutMinutes 5 }
    else { & "$tools/launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors }
    . "$tools/automation-common.ps1" -RunId $run.runId
    $deadline=[DateTime]::UtcNow.AddSeconds(30)
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        if((Test-Path (Join-Path $run.profile 'setup-await-choice.txt')) -and -not (Test-Path (Join-Path $run.profile 'setup-choose.txt'))) {
            & "$tools/capture.ps1" -RunId $run.runId -OutName 'conflict.png'
            [IO.File]::WriteAllText((Join-Path $run.profile 'setup-choose.txt'),'continue')
        }
        $stateFile=Join-Path $run.profile 'setup-stage.txt'
        if(Test-Path $stateFile){break}
        if([DateTime]::UtcNow -gt $deadline){throw 'Setup verification timed out.'}
        Start-Sleep -Milliseconds 200
    }while($true)
    & "$tools/capture.ps1" -RunId $run.runId -OutName 'setup.png'
} finally {
    & "$tools/stop.ps1" -RunId $run.runId
    foreach($name in 'Player.log','run.json','launch.json','Captures','setup-results.txt','setup-stage.txt','setup-export.xml') {
        $path=Join-Path $run.profile $name
        if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse -Force}
    }
    & "$tools/remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'setup-results.txt')
if((Get-Content (Join-Path $out 'setup-stage.txt') -Raw).Trim() -ne 'PASS'){throw 'Readouts setup verification failed.'}
