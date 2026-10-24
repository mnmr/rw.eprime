[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('preview','metrics','lifecycle')][string]$Scenario,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9._-]+$')][string]$EvidenceName
)
$ErrorActionPreference = 'Stop'
$repository = 'D:\Code\RimWorld'
$automation = Join-Path $repository 'Shared\tools\automation'
$evidence = Join-Path $repository ('WorkRoles\temp\localization\' + $EvidenceName)
if (Test-Path -LiteralPath $evidence) { throw "Evidence destination already exists: $evidence" }
New-Item -ItemType Directory -Path $evidence | Out-Null
& (Join-Path $automation 'list-runs.ps1') -Json -Detailed | Set-Content (Join-Path $evidence 'runs-before.json')
$run = & (Join-Path $automation 'create-run.ps1') -Purpose "Welcome $Scenario lifecycle and rendering assertions" `
    -ModSourceRoot $repository -ModSet localization-after -AdditionalModSource @((Join-Path $PSScriptRoot 'mod'),(Join-Path $PSScriptRoot 'companion'))
$script:sequence = 0
try {
    [xml]$prefs = Get-Content (Join-Path $run.profile 'Config\Prefs.xml') -Raw
    $prefs.PrefsData.langFolderName = 'German (Deutsch)'
    $prefs.Save((Join-Path $run.profile 'Config\Prefs.xml'))
    Set-Content (Join-Path $run.profile 'Config\welcome-scenarios.enabled') $Scenario
    & (Join-Path $automation 'launch.ps1') -RunId $run.runId -AllowRecoverableSaveErrors
    . (Join-Path $automation 'automation-common.ps1') -RunId $run.runId
    function Wait-Scenario([int]$Number) {
        $result = Join-Path $run.profile "welcome-scenario-$Number.json"
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        while (-not (Test-Path -LiteralPath $result)) {
            $null = Invoke-SharedGameCommand @{ command='status' }
            if ([DateTime]::UtcNow -gt $deadline) { throw "Scenario $Number did not complete" }
            Start-Sleep -Milliseconds 80
        }
        $response = Get-Content -LiteralPath $result -Raw | ConvertFrom-Json
        if ($response.name -eq 'failed') { throw (Get-Content (Join-Path $run.profile 'welcome-scenarios-report.json') -Raw) }
    }
    function Step([string]$Action, [switch]$Capture) {
        $script:sequence++
        Set-Content (Join-Path $run.profile "welcome-scenario-$script:sequence.request") $Action
        Wait-Scenario $script:sequence
        if ($Capture) {
            & (Join-Path $automation 'capture.ps1') -RunId $run.runId -OutName "$script:sequence-$Action.png" -KeepCursor
        }
    }
    Wait-Scenario 0
    switch ($Scenario) {
        preview {
            Step four -Capture
            Step shrink -Capture
            Step hide-preview
            Step grow
            Step text-only -Capture
            Step shrink
            Step restore-preview
            Step grow
            Step image-restored -Capture
            Step font -Capture
        }
        metrics {
            Step quality -Capture
            Step metrics-change -Capture
            Step metrics-stable
            Step close
            Step grow
            Step implanner -Capture
            Step metrics-change -Capture
            Step metrics-stable
            Step font -Capture
        }
        lifecycle {
            Step four -Capture
            Step shrink -Capture
            Step exit -Capture
            Step reload -Capture
        }
    }
} finally {
    & (Join-Path $automation 'stop.ps1') -RunId $run.runId
    foreach ($file in Get-ChildItem -LiteralPath $run.profile -File) {
        if ($file.Extension -in @('.json','.log')) { Copy-Item -LiteralPath $file.FullName -Destination $evidence }
    }
    $captures = Join-Path $run.profile 'Captures'
    if (Test-Path -LiteralPath $captures) { Copy-Item -LiteralPath $captures -Destination (Join-Path $evidence 'Captures') -Recurse }
    & (Join-Path $automation 'remove-run.ps1') -RunId $run.runId
}
$report = Get-Content (Join-Path $evidence 'welcome-scenarios-report.json') -Raw
$report
if (@($report | ConvertFrom-Json | Where-Object { -not $_.Passed }).Count) { throw 'Welcome scenario assertions failed' }
