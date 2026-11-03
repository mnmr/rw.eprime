param([string[]]$Topics = @('overview', 'strata', 'readouts-plus'))
# Captures the Help tab's Mods chapter pages at the managed-run default
# (1920x1080, UI scale 1.25) for visual review; same probe scenes as
# capture-help.ps1.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$tools = Join-Path $root 'Shared/tools/automation'
$run = & "$tools/create-run.ps1" -Purpose 'Readouts help: Mods chapter pages for review' -ModSet 'readouts-display-verification' -AdditionalModSource @("$PSScriptRoot/mod")
$out = "$root/Readouts/temp/help-mods/$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    & "$tools/launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools/automation-common.ps1" -RunId $run.runId
    foreach ($topic in $Topics) {
        $id = "help-$topic"
        [IO.File]::WriteAllText((Join-Path $run.profile 'display-request.txt'), "$id;1920;1080;1.25;False;help:$topic`n")
        $deadline = [DateTime]::UtcNow.AddSeconds(12)
        do {
            Invoke-SharedGameCommand @{command='status'} | Out-Null
            if (Test-Path (Join-Path $run.profile 'display-error.txt')) { throw (Get-Content (Join-Path $run.profile 'display-error.txt') -Raw) }
            if (Test-Path (Join-Path $run.profile "display-$id.xml")) { break }
            if ([DateTime]::UtcNow -gt $deadline) { throw "Help capture $id timed out" }
            Start-Sleep -Milliseconds 100
        } while ($true)
        [xml]$r = Get-Content (Join-Path $run.profile "display-$id.xml") -Raw
        if ($r.Display.helpTopic -ne $topic) { throw "Help opened $($r.Display.helpTopic) instead of $topic" }
        if ($r.Display.visibleHelpMetadata -ne '0') { throw "Localization metadata appeared in rendered help: $topic" }
        & "$tools/capture.ps1" -RunId $run.runId -OutName "$id.png"
    }
    "PASS $($Topics -join ', ')" | Set-Content (Join-Path $out 'results.txt')
} finally {
    & "$tools/stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log', 'host.json', 'run.json', 'launch.json', 'Captures', 'stall.json') {
        $path = Join-Path $run.profile $name
        if (Test-Path $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse }
    }
    & "$tools/remove-run.ps1" -RunId $run.runId
}
Write-Output $out
