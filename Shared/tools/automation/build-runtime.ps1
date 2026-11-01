[CmdletBinding()]
# -GameVersion 1.6|1.7 builds against the persistent local copy GameInstalls\<version>.
param([string]$GameRoot = '', [ValidateSet('','1.6','1.7')][string]$GameVersion = '')

$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\Automation\src'))
. (Join-Path $PSScriptRoot 'run-common.ps1')
if ($GameVersion) { $GameRoot = Join-Path $script:GameInstallsRoot $GameVersion }
if ($GameRoot) { $script:InstalledGameRoot = [IO.Path]::GetFullPath($GameRoot) }
$layout = Get-GameLayout $script:InstalledGameRoot
foreach ($project in @('RimWorld.Automation', 'RimWorld.Automation.Host')) {
    # Compile against the same installation the runtime will run in.
    dotnet build (Join-Path $source "$project\$project.csproj") -c Release --nologo "-p:RimWorldVersion=$($layout.gameVersion)" "-p:InstalledGameRoot=$($script:InstalledGameRoot)"
    if ($LASTEXITCODE -ne 0) { throw "Automation build failed: $project" }
}
