param(
    [Parameter(Mandatory)][ValidateSet('WorkRoles', 'Implanner')][string]$Mod,
    [Parameter(Mandatory)][string[]]$Topics
)
# Opens the mod's Help tab at each topic in a managed 1.7 run (1920x1080,
# UI scale 1.25) and captures it. Build the mod and this probe first:
#   dotnet build Shared/Tests/InGame/HelpCapture/Probe.csproj -c Release
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$tools = Join-Path $root 'Shared/tools/automation'
$window = @{ WorkRoles = 'MainTabWindow_WorkRoles'; Implanner = 'MainTabWindow_Implanner' }[$Mod]
$modSet = @{ WorkRoles = 'workroles-help-capture'; Implanner = 'implanner-help-capture' }[$Mod]
$run = & "$tools/create-run.ps1" -Purpose "$Mod help: Mods chapter pages for review" -ModSet $modSet -GameVersion 1.7 -SavePattern 'Fisso-NAM-1.7.rws' -AdditionalModSource @("$PSScriptRoot/mod")
$out = "$root/$Mod/temp/help-mods/$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    & "$tools/launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools/automation-common.ps1" -RunId $run.runId
    foreach ($topic in $Topics) {
        $id = "help-$topic"
        [IO.File]::WriteAllText((Join-Path $run.profile 'help-capture-request.txt'), "$id;$window;$topic`n")
        $deadline = [DateTime]::UtcNow.AddSeconds(12)
        do {
            Invoke-SharedGameCommand @{command='status'} | Out-Null
            if (Test-Path (Join-Path $run.profile 'help-capture-error.txt')) { throw (Get-Content (Join-Path $run.profile 'help-capture-error.txt') -Raw) }
            if (Test-Path (Join-Path $run.profile "help-capture-$id.txt")) { break }
            if ([DateTime]::UtcNow -gt $deadline) { throw "Help capture $id timed out" }
            Start-Sleep -Milliseconds 100
        } while ($true)
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
