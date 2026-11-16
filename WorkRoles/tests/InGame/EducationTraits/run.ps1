param([switch]$SkipProbeBuild)
$ErrorActionPreference = 'Stop'
$tools = 'D:\Code\RimWorld\Shared\tools\automation'
if (-not $SkipProbeBuild) {
    dotnet build (Join-Path $PSScriptRoot 'Probe.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Education traits probe build failed' }
}
$run = & "$tools\create-run.ps1" -Purpose 'WorkRoles with Progression: Education: proficiency traits left out of the Traits box; over-wide skill tooltip table wraps' -ModSet workroles-education -GameVersion 1.7 -SavePattern 'Fisso-NAM-1.7.rws' -AdditionalModSource @("$PSScriptRoot\Probe")
$out = Join-Path 'D:/Code/RimWorld/WorkRoles/temp/education-traits' $run.runId
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(55)
    $lastCapture = ''
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        $stagePath = Join-Path $run.profile 'wr-education-stage.txt'
        $stage = if (Test-Path -LiteralPath $stagePath) { (Get-Content -LiteralPath $stagePath -Raw).Trim() } else { '' }
        if ($stage.StartsWith('FAILED')) { throw $stage }
        if ($stage.StartsWith('SUCCESS')) { break }
        if ($stage.StartsWith('CAPTURE ')) {
            $name = $stage.Substring(8)
            if ($name -ne $lastCapture) {
                if ($name -eq 'wr-education-tooltip') {
                    # Rest the pointer on the Melee skill cell until its tooltip opens.
                    $point = (Get-Content -LiteralPath (Join-Path $run.profile 'wr-education-hover.txt') -Raw).Split(',')
                    Invoke-SharedGameCommand @{command='hover'; x=[int]$point[0]; y=[int]$point[1]} | Out-Null
                    $hoverDeadline = [DateTime]::UtcNow.AddMilliseconds(1500)
                    while ([DateTime]::UtcNow -lt $hoverDeadline) { Invoke-SharedGameCommand @{command='status'} | Out-Null; Start-Sleep -Milliseconds 100 }
                }
                & "$tools\capture.ps1" -RunId $run.runId -OutName "$name.png" -KeepCursor
                [IO.File]::WriteAllText((Join-Path $run.profile "wr-education-captured-$name.txt"), 'captured')
                $lastCapture = $name
            }
        }
        if ([DateTime]::UtcNow -ge $deadline) { throw "Education traits scenario timeout at $stage" }
        Start-Sleep -Milliseconds 150
    } while ($true)
} finally {
    & "$tools\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log','run.json','launch.json','host.json','wr-education.txt','wr-education-stage.txt','wr-education-hover.txt','Captures') {
        $path = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    & "$tools\remove-run.ps1" -RunId $run.runId
    Write-Output $out
}
Get-Content -LiteralPath (Join-Path $out 'wr-education.txt')
