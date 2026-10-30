param([ValidateSet('1.6','1.7')][string]$Version = '1.7')
$ErrorActionPreference = 'Stop'
$repo = 'D:\Code\RimWorld'
$automation = Join-Path $repo 'Shared\tools\automation'
$parameters = @{
    Purpose = "Quality Jobs $Version final bug-fix load without diagnostic probe; assembly identity and relevant error check"
    ModSet = 'qualityjobs-only'
}
if ($Version -eq '1.6') {
    $parameters.ModSet = 'qualityjobs-bug-fixes-clean-cwc'
    $parameters.GameRoot = "$repo\Readouts\temp\linux-validation\game-1.6"
    $parameters.WslDistribution = 'Ubuntu-22.04'
    $parameters.WslRenderer = 'Software'
    $parameters.SavePattern = 'Fisso-NAM.rws'
    $parameters.BaselineConfigPath = "$automation\profile-template\Config\ModsConfig-1.6.xml"
}
$run = & "$automation\create-run.ps1" @parameters
$evidence = Join-Path $repo "QualityJobs\temp\bug-fixes\evidence\$($run.runId)"
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
try {
    & "$automation\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    & "$automation\capture.ps1" -RunId $run.runId -OutName 'clean-loaded.png'
} finally {
    & "$automation\stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log','run.json','launch.json','host.json','linux-host.json','Captures','stall.json','hard-cap.json') {
        $path = Join-Path $run.profile $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $evidence -Recurse -Force }
    }
    & "$automation\remove-run.ps1" -RunId $run.runId
}
$manifest = Get-Content -LiteralPath (Join-Path $evidence 'run.json') -Raw | ConvertFrom-Json
$results = @("Loaded=True; Version=$Version; no diagnostic probe")
foreach ($assembly in 'QualityJobs.dll','QualityJobs.Core.dll') {
    $relative = "$Version\Assemblies\$assembly"
    $snapshot = @($manifest.snapshots.mods.QualityJobs.files | Where-Object path -eq $relative)
    $hash = (Get-FileHash -LiteralPath (Join-Path "$repo\QualityJobs\mod" $relative) -Algorithm SHA256).Hash
    if ($snapshot.Count -ne 1 -or $snapshot[0].sha256 -ne $hash) { throw "Build/snapshot hash mismatch: $assembly" }
    $results += "$assembly hash matches tested build: $hash"
}
$log = Get-Content -LiteralPath (Join-Path $evidence 'Player.log')
$errors = @($log | Where-Object { $_ -match '^\s*at QualityJobs\.|Error in static constructor of QualityJobs|Exception patching.*QualityJobs' })
$results += "RelevantQualityJobsErrors=$($errors.Count); source-save missing-mod warnings remain in the retained log"
$results | Set-Content -LiteralPath (Join-Path $evidence 'clean-results.txt')
$results
$evidence
if ($errors.Count -ne 0) { throw 'Quality Jobs error stack found in clean load; inspect retained log.' }
