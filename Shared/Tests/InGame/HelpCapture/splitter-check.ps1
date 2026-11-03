param([ValidateSet('WorkRoles', 'Implanner')][string]$Mod = 'WorkRoles', [string]$Topic = 'overview')
# Help topic-list splitter: resize cursor over the gap, drag to resize, double-click to fit.
# Build the mod and this probe first: dotnet build Shared/Tests/InGame/HelpCapture/Probe.csproj -c Release
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$tools = Join-Path $root 'Shared/tools/automation'
$window = @{ WorkRoles = 'MainTabWindow_WorkRoles'; Implanner = 'MainTabWindow_Implanner' }[$Mod]
$modSet = @{ WorkRoles = 'workroles-help-capture'; Implanner = 'implanner-help-capture' }[$Mod]
$run = & "$tools/create-run.ps1" -Purpose "$Mod help: topic-list splitter drag and double-click" -ModSet $modSet -GameVersion 1.7 -SavePattern 'Fisso-NAM-1.7.rws' -AdditionalModSource @("$PSScriptRoot/mod")
$out = "$root/$Mod/temp/help-splitter/$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$results = @()
function Check([bool]$ok, [string]$what) { $script:results += "$(if ($ok) { 'PASS' } else { 'FAIL' }) $what" }
try {
    & "$tools/launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools/automation-common.ps1" -RunId $run.runId
    function Request([string]$text, [string]$file) {
        [IO.File]::WriteAllText((Join-Path $run.profile 'help-capture-request.txt'), "$text`n")
        $deadline = [DateTime]::UtcNow.AddSeconds(12)
        do {
            Invoke-SharedGameCommand @{command='status'} | Out-Null
            if (Test-Path (Join-Path $run.profile 'help-capture-error.txt')) { throw (Get-Content (Join-Path $run.profile 'help-capture-error.txt') -Raw) }
            if (Test-Path (Join-Path $run.profile $file)) { return (Get-Content (Join-Path $run.profile $file) -Raw).Trim() }
            if ([DateTime]::UtcNow -gt $deadline) { throw "$text timed out" }
            Start-Sleep -Milliseconds 100
        } while ($true)
    }
    $step = 0
    function Splitter() {
        $script:step++
        $line = Request "splitter;s$script:step" "help-splitter-s$script:step.txt"
        $parts = $line.Split(';'); $box = $parts[0].Split(',') | ForEach-Object { [int]$_ }
        return [pscustomobject]@{ X = [int](($box[0] + $box[2]) / 2); Y = [int](($box[1] + $box[3]) / 2); Width = [double]$parts[1]; Fit = [double]$parts[2]; Cursor = ($parts[3] -eq 'cursor') }
    }
    $null = Request "help-$Topic;$window;$Topic" "help-capture-help-$Topic.txt"
    $start = Splitter
    Check (-not $start.Cursor) "arrow cursor away from the gap (width $($start.Width), fit $($start.Fit))"
    Invoke-SharedGameCommand @{command='hover'; x=$start.X; y=$start.Y} | Out-Null
    $hover = Splitter
    Check $hover.Cursor "resize cursor over the gap"
    Invoke-SharedGameCommand @{command='hover'; x=($start.X + 300); y=$start.Y} | Out-Null
    $away = Splitter
    Check (-not $away.Cursor) "arrow cursor again over the content"
    Invoke-SharedGameCommand @{command='drag'; x=$start.X; y=$start.Y; x2=($start.X + 200); y2=$start.Y} | Out-Null
    $dragged = Splitter
    $expected = $start.Width + 200 / 1.25
    Check ([Math]::Abs($dragged.Width - $expected) -le 2) "drag of 200px widened the list to $($dragged.Width) (expected $expected)"
    Check ($dragged.X -gt $start.X + 150) "the gap moved with the list ($($start.X) to $($dragged.X))"
    & "$tools/capture.ps1" -RunId $run.runId -OutName 'splitter-dragged.png'
    Invoke-SharedGameCommand @{command='click'; x=$dragged.X; y=$dragged.Y} | Out-Null
    Invoke-SharedGameCommand @{command='click'; x=$dragged.X; y=$dragged.Y} | Out-Null
    $fitted = Splitter
    Check ([Math]::Abs($fitted.Width - $dragged.Fit) -le 0.01) "double-click set the list to the fit width $($dragged.Fit) (now $($fitted.Width))"
    Invoke-SharedGameCommand @{command='hover'; x=1919; y=0} | Out-Null
    & "$tools/capture.ps1" -RunId $run.runId -OutName 'splitter-fit.png'
    $results | Set-Content (Join-Path $out 'results.txt')
    if ($results -match '^FAIL') { throw "splitter check failed: $(($results -match '^FAIL') -join '; ')" }
} finally {
    if ($results.Count -gt 0) { $results | Set-Content (Join-Path $out 'results.txt') }
    & "$tools/stop.ps1" -RunId $run.runId
    foreach ($name in 'Player.log', 'host.json', 'run.json', 'launch.json', 'Captures', 'stall.json') {
        $path = Join-Path $run.profile $name
        if (Test-Path $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse }
    }
    Get-ChildItem -LiteralPath $run.profile -Filter 'help-*' -File | Copy-Item -Destination $out
    & "$tools/remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'results.txt')
