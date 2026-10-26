[CmdletBinding()]
param(
    [string[]]$Languages = @('Danish'),
    [string]$EvidenceRoot = 'D:\Code\RimWorld\WorkRoles\temp\vanilla-language-audit-2026-10-26\1.6'
)
$ErrorActionPreference = 'Stop'
$automation = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\tools\automation'))
$game = 'D:\Code\RimWorld\Readouts\temp\linux-validation\game-1.6'
$mod = Join-Path $PSScriptRoot 'mod'
New-Item -ItemType Directory -Force -Path $EvidenceRoot | Out-Null
& (Join-Path $automation 'list-runs.ps1') -Json -Detailed | Set-Content -LiteralPath (Join-Path $EvidenceRoot ('runs-before-' + ($Languages -join '-') + '.json'))
foreach ($language in $Languages) {
    $destination = Join-Path $EvidenceRoot $language
    if (Test-Path -LiteralPath $destination) { throw "Evidence already exists: $destination" }
    $run = & (Join-Path $automation 'create-run.ps1') -Purpose "Native 1.6 vanilla and all official DLC language audit: $language; no save load; zero-localization probe" `
        -ModSet 'vanilla-language-audit16' -AdditionalModSource $mod -GameRoot $game -WslRenderer Software -SavePattern Fisso-NAM.rws `
        -BaselineConfigPath (Join-Path $automation 'profile-template\Config\ModsConfig-1.6.xml')
    try {
        $expected = @('brrainz.harmony','ludeon.rimworld','eprime.sharedautomation','ludeon.rimworld.royalty','ludeon.rimworld.ideology','ludeon.rimworld.biotech','ludeon.rimworld.anomaly','ludeon.rimworld.odyssey','rimshared.vanillalanguages16.probe')
        if (($run.configuration.activeMods -join '|') -cne ($expected -join '|')) { throw "Unexpected active mods: $($run.configuration.activeMods -join ', ')" }
        $folder = if ($language -eq 'English') { 'English' } else {
            $archives = @(Get-ChildItem -LiteralPath (Join-Path $game 'Data\Core\Languages') -File | Where-Object { $_.Name.StartsWith($language + ' (') -and $_.Extension -eq '.tar' })
            if ($archives.Count -ne 1) { throw "Expected one native archive for $language" }; $archives[0].BaseName
        }
        $prefsPath = Join-Path $run.profile 'Config\Prefs.xml'
        [xml]$prefs = Get-Content -LiteralPath $prefsPath -Raw
        $lang = $prefs.PrefsData.SelectSingleNode('langFolderName')
        if ($null -eq $lang) { $lang = $prefs.CreateElement('langFolderName'); $null = $prefs.PrefsData.AppendChild($lang) }
        $lang.InnerText = $folder; $prefs.Save($prefsPath)
        Set-Content -LiteralPath (Join-Path $run.profile 'Config\vanilla-language-audit.enabled') -Value $language -NoNewline
        & (Join-Path $automation 'launch.ps1') -RunId $run.runId -GameArguments '-pickle-no-http' -HardCapSeconds 60 -TimeoutMinutes 5 | Out-Null
    } finally {
        & (Join-Path $automation 'stop.ps1') -RunId $run.runId
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        foreach ($name in @('run.json','launch.json','host.json','linux-host.json','profile-state.json','activity.json','Player.log','Player.log.host-error.txt')) {
            $source = Join-Path $run.profile $name
            if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $destination }
        }
        $reports = Join-Path $run.profile 'VanillaLanguageAudit'
        if (Test-Path -LiteralPath $reports) { Copy-Item -LiteralPath $reports -Destination $destination -Recurse }
        & (Join-Path $automation 'remove-run.ps1') -RunId $run.runId
    }
    $auditPath = Join-Path $destination 'VanillaLanguageAudit\audit.xml'
    if (-not (Test-Path -LiteralPath $auditPath)) { throw "No audit output for $language; inspect $destination" }
    [xml]$audit = Get-Content -LiteralPath $auditPath -Raw
    if ($audit.audit.status -ne 'complete' -or $audit.audit.language -cne $language -or @($audit.audit.section | Where-Object status -ne 'ok').Count) { throw "Native audit failed for $language; inspect $auditPath" }
    Write-Host "AUDIT COMPLETE $language $($audit.audit.version)"
}
