[CmdletBinding()]
# Defaults to the Steam-synced 1.7 copy. -GameVersion 1.6 selects the legacy copy.
param([string]$GameRoot = '', [ValidateSet('','1.6','1.7')][string]$GameVersion = '')

$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\Automation\src'))
. (Join-Path $PSScriptRoot 'run-common.ps1')
. (Join-Path $PSScriptRoot 'game-sync-common.ps1')
$mutex = Enter-GamePreparation
try {
    $script:InstalledGameRoot = Resolve-AutomationGameRoot $GameRoot $GameVersion
    $layout = Get-GameLayout $script:InstalledGameRoot
    if ($GameVersion -and $layout.gameVersion -cne $GameVersion) {
        throw "$script:InstalledGameRoot holds RimWorld $($layout.gameVersion), not $GameVersion"
    }
    foreach ($project in @('RimWorld.Automation', 'RimWorld.Automation.Host')) {
        # Compile against the same installation the runtime will run in.
        dotnet build (Join-Path $source "$project\$project.csproj") -c Release --nologo "-p:RimWorldVersion=$($layout.gameVersion)" "-p:InstalledGameRoot=$($script:InstalledGameRoot)"
        if ($LASTEXITCODE -ne 0) { throw "Automation build failed: $project" }
    }
} finally { $mutex.ReleaseMutex(); $mutex.Dispose() }
