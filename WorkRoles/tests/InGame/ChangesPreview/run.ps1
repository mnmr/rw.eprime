param([float]$UiScale = 1.25, [string]$Label = 'fixed', [switch]$SkipProbeBuild)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools = Join-Path $workspace 'Shared\tools\automation'
if (-not $SkipProbeBuild) {
    dotnet build (Join-Path $PSScriptRoot 'Probe.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Changes preview probe build failed' }
}
$run = & "$tools\create-run.ps1" -Purpose "WorkRoles WR30 legal localized role preview bounds and tooltip at UI scale $UiScale ($Label)" -ModSet workroles-changes-preview -AdditionalModSource @("$PSScriptRoot\Probe")
$out = Join-Path $workspace "WorkRoles\temp\changes-preview\$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    $prefsPath = Join-Path $run.profile 'Config\Prefs.xml'
    [xml]$prefs = Get-Content -LiteralPath $prefsPath -Raw
    $prefs.PrefsData.uiScale = $UiScale.ToString([Globalization.CultureInfo]::InvariantCulture)
    $prefs.Save($prefsPath)
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $deadline = [DateTime]::UtcNow.AddSeconds(50)
    $captured = $false
    do {
        Invoke-SharedGameCommand @{command='status'} | Out-Null
        $stagePath = Join-Path $run.profile 'changes-preview-stage.txt'
        $stage = if (Test-Path -LiteralPath $stagePath) { (Get-Content -LiteralPath $stagePath -Raw).Trim() } else { '' }
        if ($stage.StartsWith('FAILED')) { throw $stage }
        if ($stage -eq 'CAPTURE_MARKED' -and -not $captured) {
            Save-SharedGameCapture -OutName 'marked-preview.png' | Out-Null
            $point = (Get-Content -LiteralPath (Join-Path $run.profile 'changes-preview-hover.txt') -Raw).Split(',')
            Invoke-SharedGameCommand @{command='hover'; x=[int]$point[0]; y=[int]$point[1]} | Out-Null
            $hoverDeadline = [DateTime]::UtcNow.AddMilliseconds(1500)
            while ([DateTime]::UtcNow -lt $hoverDeadline) { Invoke-SharedGameCommand @{command='status'} | Out-Null; Start-Sleep -Milliseconds 100 }
            Save-SharedGameCapture -OutName 'full-name-tooltip.png' | Out-Null
            Invoke-SharedGameCommand @{command='hover'; x=10; y=10} | Out-Null
            [IO.File]::WriteAllText((Join-Path $run.profile 'changes-preview-captured.txt'), '')
            $captured = $true
        }
        if ($stage -eq 'SUCCESS') { Save-SharedGameCapture -OutName 'unmarked-preview.png' | Out-Null; break }
        if ([DateTime]::UtcNow -ge $deadline) { throw "Changes preview regression timeout: $stage" }
        Start-Sleep -Milliseconds 100
    } while ($true)
} finally {
    & "$tools\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log','run.json','launch.json','host.json','changes-preview-results.txt','changes-preview-stage.txt','changes-preview-hover.txt','Captures') {
        $path = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    & "$tools\remove-run.ps1" -RunId $run.runId
    Write-Output $out
}
Get-Content -LiteralPath (Join-Path $out 'changes-preview-results.txt')
