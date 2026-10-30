param(
    [string]$ModSourceRoot = 'D:\Code\RimWorld',
    [string]$Label = 'fixed',
    [switch]$SkipProbeBuild,
    [string]$SourceSaveDirectory = '',
    [string]$SavePattern = 'Fisso-NAM*.rws'
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools = Join-Path $workspace 'Shared\tools\automation'
# The comparison changes only deployed production artifacts. Both runs use
# this exact probe folder; --SkipProbeBuild allows one prebuilt binary for both.
if (-not $SkipProbeBuild) {
    dotnet build (Join-Path $PSScriptRoot 'Probe.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Fix performance probe build failed' }
}
$probeDll = Join-Path $PSScriptRoot 'Probe\1.7\Assemblies\WorkRoles.FixPerformanceProbe.dll'
if (-not (Test-Path -LiteralPath $probeDll)) { throw 'Build Probe.csproj before using -SkipProbeBuild' }
$parameters = @{
    Purpose = "WorkRoles steady Colonists window repaint comparison ($Label)"
    ModSourceRoot = [IO.Path]::GetFullPath($ModSourceRoot)
    ModSet = 'workroles-fix-performance'
    AdditionalModSource = @("$PSScriptRoot\Probe")
    SavePattern = $SavePattern
}
if ($SourceSaveDirectory) { $parameters.SourceSaveDirectory = $SourceSaveDirectory }
$run = & "$tools\create-run.ps1" @parameters
$out = Join-Path $workspace "WorkRoles\temp\fix-performance\$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$run.runId | Set-Content (Join-Path $out 'run-id.txt')
[ordered]@{
    label = $Label
    modSourceRoot = $parameters.ModSourceRoot
    probePath = $probeDll
    probeSha256 = (Get-FileHash -LiteralPath $probeDll -Algorithm SHA256).Hash
    purpose = $parameters.Purpose
} | ConvertTo-Json | Set-Content (Join-Path $out 'comparison.json')
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(50)
    $captured = $false
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        $stagePath = Join-Path $run.profile 'fix-performance-stage.txt'
        $stage = if (Test-Path -LiteralPath $stagePath) { (Get-Content -LiteralPath $stagePath -Raw).Trim() } else { '' }
        if ($stage -eq 'CAPTURE' -and -not $captured) {
            & "$tools\capture.ps1" -RunId $run.runId -OutName 'steady-colonists.png'
            [IO.File]::WriteAllText((Join-Path $run.profile 'fix-performance-captured.txt'), '')
            $captured = $true
        }
        if ($stage.StartsWith('FAILED')) {
            & "$tools\capture.ps1" -RunId $run.runId -OutName 'failed.png'
            throw $stage
        }
        if ($stage -eq 'SUCCESS') { break }
        if ([DateTime]::UtcNow -ge $deadline) { throw "Native steady repaint benchmark timeout: $stage" }
        Start-Sleep -Milliseconds 100
    } while ($true)
} finally {
    & "$tools\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log','run.json','launch.json','host.json','fix-performance-results.txt','fix-performance-summary.json','fix-performance-samples.json','fix-performance-stage.txt','Captures') {
        $path = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    & "$tools\remove-run.ps1" -RunId $run.runId
    Write-Output $out
}
$result = Get-Content -LiteralPath (Join-Path $out 'fix-performance-summary.json') -Raw | ConvertFrom-Json
if ($result.sampleCount -lt 100 -or -not $result.pausedSameTick) { throw "Incomplete native benchmark: $out" }
Get-Content -LiteralPath (Join-Path $out 'fix-performance-results.txt')
