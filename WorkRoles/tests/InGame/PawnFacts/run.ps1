param([ValidateSet('ui','external','birthday')][string]$Mode = 'ui', [string]$Label = 'pawn-facts')
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools = Join-Path $source 'Shared\tools\automation'
# Build WorkRoles and the shared runtime first. The probe references completed
# production artifacts; this script never rebuilds or deploys the player install.
dotnet build (Join-Path $PSScriptRoot 'Probe.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Pawn facts probe build failed' }
$run = & "$tools\create-run.ps1" -Purpose "WorkRoles WR14-WR18 native pawn facts regression: $Mode ($Label)" -ModSourceRoot $source -ModSet workroles-pawn-facts -AdditionalModSource @("$PSScriptRoot\Probe")
$out = Join-Path $source "WorkRoles\temp\pawn-facts\$Label-$Mode-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$run.runId | Set-Content (Join-Path $out 'run-id.txt')
$Mode | Set-Content (Join-Path $run.profile 'pawn-facts-mode.txt')
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(50)
    $captured = ''
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        $stagePath = Join-Path $run.profile 'pawn-facts-stage.txt'
        $stage = if (Test-Path $stagePath) { (Get-Content -LiteralPath $stagePath -Raw).Trim() } else { '' }
        if ($stage.StartsWith('CAPTURE ') -and $captured -ne $stage) {
            $name = $stage.Substring(8)
            & "$tools\capture.ps1" -RunId $run.runId -OutName ($name + '.png')
            [IO.File]::WriteAllText((Join-Path $run.profile ('pawn-facts-captured-' + $name + '.txt')), '')
            $captured = $stage
        }
        if ($stage.StartsWith('FAILED')) {
            & "$tools\capture.ps1" -RunId $run.runId -OutName 'failed.png'
            throw $stage
        }
        if ($stage.StartsWith('SUCCESS ')) { break }
        if ([DateTime]::UtcNow -ge $deadline) { throw "Pawn facts $Mode scenario timeout: $stage" }
        Start-Sleep -Milliseconds 100
    } while ($true)
} finally {
    & "$tools\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log','run.json','launch.json','host.json','pawn-facts-results.txt','pawn-facts-stage.txt','Captures') {
        $path = Join-Path $run.profile $name
        if (Test-Path $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    & "$tools\remove-run.ps1" -RunId $run.runId
    Write-Output $out
}
$result = Get-Content -LiteralPath (Join-Path $out 'pawn-facts-results.txt') -Raw
Write-Output $result
if ($result -notmatch "SUCCESS $Mode " -or $result -match 'FAILED') { throw "Pawn facts regression failed: $out" }
