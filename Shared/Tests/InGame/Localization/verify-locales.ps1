[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9._-]+$')][string]$EvidenceName,
    [string[]]$Locales = @('German','French','Spanish','Italian','PortugueseBrazilian','SpanishLatin','ChineseSimplified','ChineseTraditional','Ukrainian','Japanese','Korean','Turkish','Vietnamese'),
    [switch]$Persistence,
    [switch]$LayoutChecks,
    [switch]$Resize
)
$ErrorActionPreference = 'Stop'
$repository = 'D:\Code\RimWorld'
$automation = Join-Path $repository 'Shared\tools\automation'
$evidence = Join-Path $repository ('WorkRoles\temp\localization\' + $EvidenceName)
if (Test-Path -LiteralPath $evidence) { throw "Evidence already exists: $evidence" }
New-Item -ItemType Directory -Path $evidence | Out-Null
& (Join-Path $automation 'list-runs.ps1') -Json -Detailed | Set-Content -LiteralPath (Join-Path $evidence 'runs-before.json') -Encoding utf8
$languageRoot = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Data\Core\Languages'
$summary = [Collections.Generic.List[object]]::new()
foreach ($locale in $Locales) {
    $destination = Join-Path $evidence $locale
    New-Item -ItemType Directory -Path $destination | Out-Null
    $run = & (Join-Path $automation 'create-run.ps1') -Purpose "Bundled locale UI $locale; $EvidenceName" `
        -ModSet localization-locales -AdditionalModSource @((Join-Path $PSScriptRoot 'mod'))
    $script:localeRequestId = 0
    try {
        $prefsPath = Join-Path $run.profile 'Config\Prefs.xml'
        [xml]$prefs = Get-Content -LiteralPath $prefsPath -Raw
        $folder = Get-ChildItem -LiteralPath $languageRoot -File -Filter "$locale (*.tar" | Select-Object -First 1 -ExpandProperty BaseName
        if (-not $folder) { throw "No native language folder for $locale" }
        $prefs.PrefsData.langFolderName = $folder
        $prefs.Save($prefsPath)
        Set-Content -LiteralPath (Join-Path $run.profile 'Config\locale-probe.enabled') -Value $locale -Encoding utf8
        if ($LayoutChecks) { Set-Content -LiteralPath (Join-Path $run.profile 'Config\locale-layout-checks.enabled') -Value 'geometry cache, button fit, exact seed authored names' }
        & (Join-Path $automation 'launch.ps1') -RunId $run.runId -AllowRecoverableSaveErrors | Out-Null
        . (Join-Path $automation 'automation-common.ps1') -RunId $run.runId
        function Send-LocaleRequest([string]$Action, [int]$TimeoutSeconds = 8) {
            $script:localeRequestId++
            $temporary = Join-Path $run.profile 'locale-request.pending'
            @{ id = $script:localeRequestId; action = $Action } | ConvertTo-Json | Set-Content -LiteralPath $temporary -Encoding utf8
            Move-Item -LiteralPath $temporary -Destination (Join-Path $run.profile "locale-request-$script:localeRequestId.json")
            $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
            do {
                $null = Invoke-SharedGameCommand @{command='status'}
                $replyPath = Join-Path $run.profile "locale-response-$script:localeRequestId.json"
                if (Test-Path -LiteralPath $replyPath) {
                    $reply = Get-Content -LiteralPath $replyPath -Raw | ConvertFrom-Json
                    if ($reply.id -eq $script:localeRequestId) {
                        if (-not $reply.passed) { throw $reply.error }
                        return $reply
                    }
                }
                Start-Sleep -Milliseconds 70
            } while ([DateTime]::UtcNow -lt $deadline)
            throw "No locale response for $Action"
        }
        if ($Persistence) {
            $null = Send-LocaleRequest 'save-reload-language' 38
            $null = Send-LocaleRequest 'workroles-main'
            $null = Invoke-SharedGameCommand @{command='hover';x=1919;y=0}
            Save-SharedGameCapture 'persisted-after-language-change.png' | Out-Null
            $null = Send-LocaleRequest 'readouts-main'
            Save-SharedGameCapture 'persisted-readout.png' | Out-Null
        } else {
            foreach ($action in @('workroles-main','workroles-help','readouts-main','readouts-help','implanner-main','implanner-help','qualityjobs')) {
                $reply = Send-LocaleRequest $action
                $null = Invoke-SharedGameCommand @{command='hover';x=1919;y=0}
                Save-SharedGameCapture "$action.png" | Out-Null
                if ($Resize -and $action -eq 'readouts-main') {
                    $w = $reply.details.window; $s = $reply.details.scale
                    $dragX = [int](($w.x+$w.width-6)*$s); $dragY = [int](($w.y+$w.height-6)*$s)
                    $null = Invoke-SharedGameCommand @{command='drag';x=$dragX;y=$dragY;x2=($dragX-700);y2=$dragY}
                    $narrow = Send-LocaleRequest 'inspect'
                    if ($narrow.details.window.width -lt 959.9 -or $narrow.details.window.width -ge $w.width-10) { throw 'Readouts scoped resize did not respect its minimum width' }
                    $null = Invoke-SharedGameCommand @{command='hover';x=1919;y=0}
                    Save-SharedGameCapture 'readouts-minimum-width.png' | Out-Null
                    $newX = [int](($narrow.details.window.x+$narrow.details.window.width-6)*$s)
                    $null = Invoke-SharedGameCommand @{command='drag';x=$newX;y=$dragY;x2=$dragX;y2=$dragY}
                    $null = Send-LocaleRequest 'inspect'
                }
                if ($action -eq 'workroles-main') {
                    $w = $reply.details.window; $s = $reply.details.scale; $m = $reply.details.margin
                    $null = Invoke-SharedGameCommand @{command='hover';x=[int](($w.x+$w.width-$m-80)*$s);y=[int](($w.y+$m+16)*$s)}
                    $until = [DateTime]::UtcNow.AddMilliseconds(650)
                    do { $null = Invoke-SharedGameCommand @{command='status'} } while ([DateTime]::UtcNow -lt $until)
                    Save-SharedGameCapture 'workroles-tooltip.png' | Out-Null
                }
                if ($action.EndsWith('help')) {
                    $d = $reply.details; $w = $d.window; $s = $d.scale; $m = $d.margin
                    $pad = if ($action -eq 'workroles-help') { 8 } else { 10 }
                    $count = $d.chapterLabels.Count
                    $next = $d.chapter + 1
                    $x = ($w.x + $m + $pad + ($w.width-2*$m-2*$pad)*($next+0.5)/$count)*$s
                    $y = ($w.y + $m + 32 + $pad + 14)*$s
                    $null = Invoke-SharedGameCommand @{command='click';x=[int]$x;y=[int]$y;button=0}
                    $observed = Send-LocaleRequest 'inspect'
                    if ($observed.details.chapter -ne $next) { throw "$action chapter click was not applied" }
                    $null = Invoke-SharedGameCommand @{command='hover';x=1919;y=0}
                    Save-SharedGameCapture "$action-navigated.png" | Out-Null
                    if ($LayoutChecks) {
                        $null = Send-LocaleRequest 'help-cache'
                        $row = $observed.details.topicRows[1]
                        $topicX = ($w.x+$m+$pad+20)*$s
                        $topicY = ($w.y+$m+32+$pad+36+$row.y+$row.height/2)*$s+2
                        $null = Invoke-SharedGameCommand @{command='click';x=[int]$topicX;y=[int]$topicY;button=0}
                        $selected = Send-LocaleRequest 'inspect'
                        if ($selected.details.selectedSlugs[$next] -ne $observed.details.topicSlugs[1]) { throw "$action topic row click was not applied" }
                        $null = Invoke-SharedGameCommand @{command='hover';x=1919;y=0}
                        Save-SharedGameCapture "$action-topic.png" | Out-Null
                        $null = Invoke-SharedGameCommand @{command='scroll';x=[int](($w.x+$w.width*0.8)*$s);y=[int](($w.y+$w.height*0.6)*$s);notches=-5}
                        $null = Send-LocaleRequest 'inspect'
                        Save-SharedGameCapture "$action-scroll.png" | Out-Null
                    }
                }
            }
        }
        $null = Send-LocaleRequest 'complete'
    } finally {
        & (Join-Path $automation 'stop.ps1') -RunId $run.runId | Out-Null
        foreach ($name in @('run.json','launch.json','host.json','activity.json','ready.json','Player.log','locale-ready.json','locale-report.json','locale-response.json','fixture-repairs.json','stall.json')) {
            $path = Join-Path $run.profile $name
            if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination (Join-Path $destination $name) }
        }
        foreach ($folderName in @('Captures')) {
            $path = Join-Path $run.profile $folderName
            if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination (Join-Path $destination $folderName) -Recurse }
        }
        $saved = Join-Path $run.profile 'Saves\LocalizationAuthoredNames.rws'
        if (Test-Path -LiteralPath $saved) { Copy-Item -LiteralPath $saved -Destination (Join-Path $destination 'LocalizationAuthoredNames.rws') }
        & (Join-Path $automation 'remove-run.ps1') -RunId $run.runId | Out-Null
    }
    $report = Get-Content -LiteralPath (Join-Path $destination 'locale-report.json') -Raw | ConvertFrom-Json
    $failed = @($report | Where-Object { -not $_.passed })
    $summary.Add([pscustomobject]@{ locale=$locale; assertions=$report.Count; passed=($failed.Count -eq 0); failures=$failed })
    $summary | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $evidence 'summary.json') -Encoding utf8
    Write-Output "$locale : $($report.Count) checks, $($failed.Count) failures"
    if ($failed.Count -ne 0) { throw "Locale assertions failed: $locale" }
}
