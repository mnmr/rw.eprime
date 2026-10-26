[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9._-]+$')][string]$EvidenceName,
    [string[]]$Locales = @('French','Italian','Japanese'),
    [switch]$Final
)
$ErrorActionPreference = 'Stop'
$automation = 'D:\Code\RimWorld\Shared\tools\automation'
$evidence = Join-Path 'D:\Code\RimWorld\WorkRoles\temp\release-readiness-2026-10-26\ui' $EvidenceName
if (Test-Path -LiteralPath $evidence) { throw "Evidence exists: $evidence" }
New-Item -ItemType Directory -Path $evidence | Out-Null
& (Join-Path $automation 'list-runs.ps1') -Json -Detailed | Set-Content (Join-Path $evidence 'runs-before.json')
foreach ($locale in $Locales) {
    $destination = Join-Path $evidence $locale
    New-Item -ItemType Directory -Path $destination | Out-Null
    $run = & (Join-Path $automation 'create-run.ps1') -Purpose "Release localized layout measurement $locale $EvidenceName" -ModSet localization-locales -AdditionalModSource @((Join-Path $PSScriptRoot 'mod'))
    try {
        $prefsPath = Join-Path $run.profile 'Config\Prefs.xml'
        [xml]$prefs = Get-Content -LiteralPath $prefsPath -Raw
        $folder = Get-ChildItem -LiteralPath 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Data\Core\Languages' -File -Filter "$locale (*.tar" | Select-Object -First 1 -ExpandProperty BaseName
        $prefs.PrefsData.langFolderName = $folder
        $prefs.Save($prefsPath)
        Set-Content -LiteralPath (Join-Path $run.profile 'Config\locale-probe.enabled') -Value $locale
        Set-Content -LiteralPath (Join-Path $run.profile 'Config\release-layout.enabled') -Value 'native-font-control-measurements'
        if ($Final) { Set-Content -LiteralPath (Join-Path $run.profile 'Config\release-layout-final.enabled') -Value 'assert production geometry' }
        & (Join-Path $automation 'launch.ps1') -RunId $run.runId -AllowRecoverableSaveErrors | Out-Null
        . (Join-Path $automation 'automation-common.ps1') -RunId $run.runId
        $deadline = [DateTime]::UtcNow.AddSeconds(20)
        while (-not (Test-Path (Join-Path $run.profile 'release-layout.json'))) {
            $null = Invoke-SharedGameCommand @{command='status'}
            if (Test-Path (Join-Path $run.profile 'release-layout-error.json')) { throw (Get-Content (Join-Path $run.profile 'release-layout-error.json') -Raw) }
            if ([DateTime]::UtcNow -gt $deadline) { throw 'Native layout probe did not produce measurements' }
        }
        $null = Invoke-SharedGameCommand @{command='hover';x=1919;y=0}
        $null = Invoke-SharedGameCommand @{command='status'}
        Save-SharedGameCapture 'release-layout.png' | Out-Null
        if ($Final) {
            Set-Content -LiteralPath (Join-Path $run.profile 'release-automation.request') -Value 'render actual narrow automation window'
            $deadline = [DateTime]::UtcNow.AddSeconds(12)
            while (-not (Test-Path (Join-Path $run.profile 'release-automation.json'))) {
                $null = Invoke-SharedGameCommand @{command='status'}
                if ([DateTime]::UtcNow -gt $deadline) { throw 'Actual automation window probe timed out' }
            }
            Save-SharedGameCapture 'implanner-narrow-automation.png' | Out-Null
        }
    } finally {
        & (Join-Path $automation 'stop.ps1') -RunId $run.runId | Out-Null
        foreach ($name in @('run.json','launch.json','host.json','activity.json','ready.json','Player.log','release-layout.json','release-layout-error.json','release-automation.json','fixture-repairs.json')) {
            $source = Join-Path $run.profile $name
            if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $destination }
        }
        if (Test-Path (Join-Path $run.profile 'Captures')) { Copy-Item -LiteralPath (Join-Path $run.profile 'Captures') -Destination $destination -Recurse }
        & (Join-Path $automation 'remove-run.ps1') -RunId $run.runId | Out-Null
    }
    Write-Output "$locale measurement complete"
    if ($Final) {
        $report = Get-Content (Join-Path $destination 'release-layout.json') -Raw | ConvertFrom-Json
        $automationReport = @(Get-Content (Join-Path $destination 'release-automation.json') -Raw | ConvertFrom-Json)
        if (-not $automationReport.Count -or @($automationReport | Where-Object { -not $_.passed }).Count) { throw ($automationReport | ConvertTo-Json -Depth 8) }
        $failed = @(@($report.names) + @($report.styledElements) + @($report.controls) | Where-Object { -not $_.passed })
        if ($report.search -and -not $report.search.passed) { throw ($report.search | ConvertTo-Json -Depth 10) }
        if (@($report.metrics | Where-Object { -not $_.detail.passed }).Count) { throw ($report.metrics | ConvertTo-Json -Depth 10) }
        if ($failed.Count) { $failed | ConvertTo-Json -Depth 8; throw "$locale failed release layout checks" }
    }
}
