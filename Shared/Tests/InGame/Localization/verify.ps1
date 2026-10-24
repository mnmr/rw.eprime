[CmdletBinding()]
param(
    [string]$ModSourceRoot = 'D:\Code\RimWorld',
    [ValidateSet('before','after')][string]$Placement = 'before',
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9._-]+$')][string]$EvidenceName,
    [switch]$Welcome
)
$ErrorActionPreference = 'Stop'
$repository = 'D:\Code\RimWorld'
$automation = Join-Path $repository 'Shared\tools\automation'
$evidence = Join-Path $repository ('WorkRoles\temp\localization\' + $EvidenceName)
if (Test-Path -LiteralPath $evidence) { throw "Evidence destination already exists: $evidence" }
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
& (Join-Path $automation 'list-runs.ps1') -Json -Detailed |
    Set-Content -LiteralPath (Join-Path $evidence 'runs-before.json') -Encoding utf8
$additional = @((Join-Path $PSScriptRoot 'mod'), (Join-Path $PSScriptRoot 'companion'))
$run = & (Join-Path $automation 'create-run.ps1') -Purpose "Localization $EvidenceName, community $Placement bundled mods" `
    -ModSourceRoot $ModSourceRoot -ModSet "localization-$Placement" -AdditionalModSource $additional
try {
    $prefsPath = Join-Path $run.profile 'Config\Prefs.xml'
    [xml]$prefs = Get-Content -LiteralPath $prefsPath -Raw
    $prefs.PrefsData.langFolderName = 'German (Deutsch)'
    $prefs.Save($prefsPath)
    if ($Welcome) { Set-Content -LiteralPath (Join-Path $run.profile 'Config\welcome-probe.enabled') -Value 'four assembly instances' }
    & (Join-Path $automation 'launch.ps1') -RunId $run.runId -AllowRecoverableSaveErrors
    . (Join-Path $automation 'automation-common.ps1') -RunId $run.runId
    $report = Join-Path $run.profile 'localization-report.json'
    $deadline = [DateTime]::UtcNow.AddSeconds(8)
    do {
        $null = Invoke-SharedGameCommand @{command='status'}
        if (Test-Path -LiteralPath $report) { break }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not (Test-Path -LiteralPath $report)) { throw 'The localization probe did not publish its result.' }
    & (Join-Path $automation 'capture.ps1') -RunId $run.runId -OutName 'localization-ready.png'
    if ($Welcome) {
        Set-Content -LiteralPath (Join-Path $run.profile 'welcome-shrink.request') -Value 'shrink this managed run only'
        $shrunkPath = Join-Path $run.profile 'welcome-shrunk.json'
        $deadline = [DateTime]::UtcNow.AddSeconds(8)
        while (-not (Test-Path -LiteralPath $shrunkPath)) {
            $null = Invoke-SharedGameCommand @{command='status'}
            if ([DateTime]::UtcNow -gt $deadline) { throw 'Welcome overflow did not close after the UI-scale shrink.' }
        }
        & (Join-Path $automation 'capture.ps1') -RunId $run.runId -OutName 'welcome-shrunk.png' -KeepCursor
        Set-Content -LiteralPath (Join-Path $run.profile 'welcome-grow.request') -Value 'restore this managed run only'
        $grownPath = Join-Path $run.profile 'welcome-grown.json'
        $deadline = [DateTime]::UtcNow.AddSeconds(8)
        while (-not (Test-Path -LiteralPath $grownPath)) {
            $null = Invoke-SharedGameCommand @{command='status'}
            if ([DateTime]::UtcNow -gt $deadline) { throw 'Suspended welcomes did not reopen after restoring the UI scale.' }
        }
        & (Join-Path $automation 'capture.ps1') -RunId $run.runId -OutName 'welcome-grown.png'
        $ready = Get-Content -LiteralPath $grownPath -Raw | ConvertFrom-Json
        $null = Invoke-SharedGameCommand @{command='status'}
        $null = Invoke-SharedGameCommand @{command='drag';x=$ready.headerX;y=$ready.headerY;x2=($ready.headerX-24);y2=($ready.headerY-18)}
        $draggedPath = Join-Path $run.profile 'welcome-dragged.json'
        $deadline = [DateTime]::UtcNow.AddSeconds(4)
        while (-not (Test-Path -LiteralPath $draggedPath)) {
            $null = Invoke-SharedGameCommand @{command='status'}
            if ([DateTime]::UtcNow -gt $deadline) { throw 'Welcome did not respond to the scoped drag.' }
        }
        & (Join-Path $automation 'capture.ps1') -RunId $run.runId -OutName 'welcome-dragged.png'
        $dragged = Get-Content -LiteralPath $draggedPath -Raw | ConvertFrom-Json
        $null = Invoke-SharedGameCommand @{command='click';x=$dragged.closeX;y=$dragged.closeY;button=0}
        $closedPath = Join-Path $run.profile 'welcome-closed.json'
        $deadline = [DateTime]::UtcNow.AddSeconds(4)
        while (-not (Test-Path -LiteralPath $closedPath)) {
            $null = Invoke-SharedGameCommand @{command='status'}
            if ([DateTime]::UtcNow -gt $deadline) { throw 'Welcome Close did not dismiss the clicked window.' }
        }
        & (Join-Path $automation 'capture.ps1') -RunId $run.runId -OutName 'welcome-closed.png'
    }
} finally {
    & (Join-Path $automation 'stop.ps1') -RunId $run.runId
    foreach ($name in @('run.json','launch.json','host.json','activity.json','ready.json','Player.log','localization-report.json',
        'welcome-ready.json','welcome-shrunk.json','welcome-grown.json','welcome-dragged.json','welcome-closed.json','fixture-repairs.json')) {
        $path = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination (Join-Path $evidence $name) }
    }
    $captures = Join-Path $run.profile 'Captures'
    if (Test-Path -LiteralPath $captures) { Copy-Item -LiteralPath $captures -Destination (Join-Path $evidence 'Captures') -Recurse }
    & (Join-Path $automation 'remove-run.ps1') -RunId $run.runId
}
$reportJson = Get-Content -LiteralPath (Join-Path $evidence 'localization-report.json') -Raw
$reportJson
$failures = @($reportJson | ConvertFrom-Json | Where-Object { -not $_.Passed })
if ($failures.Count -gt 0) { throw ('Localization assertions failed: ' + (($failures | ForEach-Object Name) -join ', ')) }
