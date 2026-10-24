[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9._-]+$')][string]$EvidenceName)
$ErrorActionPreference = 'Stop'
$automation = 'D:\Code\RimWorld\Shared\tools\automation'
$evidence = Join-Path 'D:\Code\RimWorld\WorkRoles\temp\localization' $EvidenceName
if (Test-Path -LiteralPath $evidence) { throw "Evidence already exists: $evidence" }
New-Item -ItemType Directory -Path $evidence | Out-Null
& (Join-Path $automation 'list-runs.ps1') -Json -Detailed | Set-Content (Join-Path $evidence 'runs-before.json')
$run = & (Join-Path $automation 'create-run.ps1') -Purpose 'Clean localization build without diagnostic or companion mods' -ModSet own-mods-waylith
try {
    [xml]$prefs = Get-Content (Join-Path $run.profile 'Config\Prefs.xml') -Raw
    $prefs.PrefsData.langFolderName = 'German (Deutsch)'
    $prefs.Save((Join-Path $run.profile 'Config\Prefs.xml'))
    & (Join-Path $automation 'launch.ps1') -RunId $run.runId -AllowRecoverableSaveErrors
    . (Join-Path $automation 'automation-common.ps1') -RunId $run.runId
    $null = Invoke-SharedGameCommand @{ command='status' }
    Save-SharedGameCapture 'clean-loaded.png' | Out-Null
} finally {
    & (Join-Path $automation 'stop.ps1') -RunId $run.runId
    foreach ($name in @('run.json','launch.json','host.json','activity.json','ready.json','Player.log')) {
        $source = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $evidence }
    }
    if (Test-Path (Join-Path $run.profile 'Captures')) {
        Copy-Item -LiteralPath (Join-Path $run.profile 'Captures') -Destination (Join-Path $evidence 'Captures') -Recurse
    }
    & (Join-Path $automation 'remove-run.ps1') -RunId $run.runId
}
$manifest = Get-Content (Join-Path $evidence 'run.json') -Raw | ConvertFrom-Json
if (@($manifest.configuration.activeMods | Where-Object { $_ -like 'rimshared.tests.*' }).Count) {
    throw 'Clean run unexpectedly includes diagnostics'
}
$hashes = foreach ($entry in $manifest.snapshots.mods.PSObject.Properties) {
    if ($entry.Name -notin @('WorkRoles','EPrimeReadouts','QualityJobs','Implanner')) { continue }
    foreach ($file in $entry.Value.files) {
        if ($file.path -notlike '1.7\Assemblies\*.dll') { continue }
        $source = Join-Path $entry.Value.source $file.path
        $actual = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        [pscustomobject]@{ mod=$entry.Name; file=$file.path; copiedHash=$file.sha256; buildHash=$actual; matches=$actual -eq $file.sha256 }
    }
}
$hashes | ConvertTo-Json | Set-Content (Join-Path $evidence 'assembly-hashes.json')
if (@($hashes | Where-Object { -not $_.matches }).Count) { throw 'Copied assembly differs from current tested build' }
"Clean run retained at $evidence; $($hashes.Count) assembly hashes match. Review Player.log for relevant errors."
