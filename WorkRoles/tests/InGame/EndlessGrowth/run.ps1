param()
$ErrorActionPreference = 'Stop'
$tools = 'D:\Code\RimWorld\Shared\tools\automation'
$run = & "$tools\create-run.ps1" -Purpose 'WorkRoles with Endless Growth: skills past 20 in facts, plans and the Colonists tab' -ModSet workroles-endless-growth -GameVersion 1.7 -SavePattern 'Fisso-NAM-1.7.rws' -AdditionalModSource @("$PSScriptRoot\Probe")
$out = Join-Path 'D:/Code/RimWorld/WorkRoles/temp/endless-growth' $run.runId
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(55)
    $lastCapture = ''
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        $stagePath = Join-Path $run.profile 'wr-endless-stage.txt'
        $stage = if (Test-Path -LiteralPath $stagePath) { (Get-Content -LiteralPath $stagePath -Raw).Trim() } else { '' }
        if ($stage.StartsWith('FAILED')) { throw $stage }
        if ($stage.StartsWith('SUCCESS')) { break }
        if ($stage.StartsWith('CAPTURE ')) {
            $name = $stage.Substring(8)
            if ($name -ne $lastCapture) {
                & "$tools\capture.ps1" -RunId $run.runId -OutName "$name.png"
                [IO.File]::WriteAllText((Join-Path $run.profile "wr-endless-captured-$name.txt"), 'captured')
                $lastCapture = $name
            }
        }
        if ([DateTime]::UtcNow -ge $deadline) { throw "Endless Growth scenario timeout at $stage" }
        Start-Sleep -Milliseconds 150
    } while ($true)
} finally {
    & "$tools\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log','run.json','launch.json','host.json','wr-endless.txt','wr-endless-stage.txt','Captures') {
        $path = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    & "$tools\remove-run.ps1" -RunId $run.runId
    Write-Output $out
}
Get-Content -LiteralPath (Join-Path $out 'wr-endless.txt')
