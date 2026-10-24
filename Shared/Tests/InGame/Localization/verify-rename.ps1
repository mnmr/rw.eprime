[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9._-]+$')][string]$EvidenceName)
$ErrorActionPreference = 'Stop'
$repository = 'D:\Code\RimWorld'
$automation = Join-Path $repository 'Shared\tools\automation'
$evidence = Join-Path $repository ('WorkRoles\temp\localization\' + $EvidenceName)
if (Test-Path -LiteralPath $evidence) { throw "Evidence already exists: $evidence" }
New-Item -ItemType Directory -Path $evidence | Out-Null
& (Join-Path $automation 'list-runs.ps1') -Json -Detailed | Set-Content -LiteralPath (Join-Path $evidence 'runs-before.json') -Encoding utf8
$run = & (Join-Path $automation 'create-run.ps1') -Purpose "Localized rename prefill and genuine edited length cap; $EvidenceName" `
    -ModSet localization-before -AdditionalModSource @((Join-Path $PSScriptRoot 'mod'), (Join-Path $PSScriptRoot 'companion'))
try {
    $prefsPath = Join-Path $run.profile 'Config\Prefs.xml'
    [xml]$prefs = Get-Content -LiteralPath $prefsPath -Raw
    $prefs.PrefsData.langFolderName = 'German (Deutsch)'
    $prefs.Save($prefsPath)
    Set-Content -LiteralPath (Join-Path $run.profile 'Config\rename-probe.enabled') -Value 'real TextField repaint and scoped input'
    & (Join-Path $automation 'launch.ps1') -RunId $run.runId -AllowRecoverableSaveErrors | Out-Null
    . (Join-Path $automation 'automation-common.ps1') -RunId $run.runId
    function Wait-RenameFile([string]$Name) {
        $deadline = [DateTime]::UtcNow.AddSeconds(12)
        $path = Join-Path $run.profile $Name
        do {
            $null = Invoke-SharedGameCommand @{command='status'}
            if (Test-Path -LiteralPath $path) { return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json }
            if ($Name -ne 'rename-report.json' -and (Test-Path -LiteralPath (Join-Path $run.profile 'rename-report.json'))) {
                throw (Get-Content -LiteralPath (Join-Path $run.profile 'rename-report.json') -Raw)
            }
            Start-Sleep -Milliseconds 100
        } while ([DateTime]::UtcNow -lt $deadline)
        throw "No rename probe response: $Name"
    }
    $untouched = Wait-RenameFile 'rename-untouched-ready.json'
    Save-SharedGameCapture 'rename-untouched.png' | Out-Null
    $null = Invoke-SharedGameCommand @{command='click';x=$untouched.okX;y=$untouched.okY;button=0}
    $edited = Wait-RenameFile 'rename-edited-ready.json'
    $null = Invoke-SharedGameCommand @{command='click';x=$edited.fieldX;y=$edited.fieldY;button=0}
    $null = Invoke-SharedGameCommand @{command='type';text='^aABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890'}
    Save-SharedGameCapture 'rename-edited.png' | Out-Null
    $null = Invoke-SharedGameCommand @{command='click';x=$edited.okX;y=$edited.okY;button=0}
    $null = Wait-RenameFile 'rename-report.json'
} finally {
    & (Join-Path $automation 'stop.ps1') -RunId $run.runId
    foreach ($name in @('run.json','launch.json','host.json','activity.json','ready.json','Player.log','fixture-repairs.json','localization-report.json','stall.json',
        'rename-report.json','rename-untouched-ready.json','rename-edited-ready.json')) {
        $source = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination (Join-Path $evidence $name) }
    }
    $captures = Join-Path $run.profile 'Captures'
    if (Test-Path -LiteralPath $captures) { Copy-Item -LiteralPath $captures -Destination (Join-Path $evidence 'Captures') -Recurse }
    & (Join-Path $automation 'remove-run.ps1') -RunId $run.runId
}
$report = Get-Content -LiteralPath (Join-Path $evidence 'rename-report.json') -Raw
$report
if (@($report | ConvertFrom-Json | Where-Object { -not $_.Passed }).Count -ne 0) { throw 'Rename regression failed.' }
