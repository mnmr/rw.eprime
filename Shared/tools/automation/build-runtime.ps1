[CmdletBinding()]
param([string]$GameRoot = '')

$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\Automation\src'))
. (Join-Path $PSScriptRoot 'run-common.ps1')
if ($GameRoot) { $script:InstalledGameRoot = [IO.Path]::GetFullPath($GameRoot) }
$layout = Get-GameLayout $script:InstalledGameRoot
foreach ($project in @('RimWorld.Automation', 'RimWorld.Automation.Host')) {
    dotnet build (Join-Path $source "$project\$project.csproj") -c Release --nologo "-p:RimWorldVersion=$($layout.gameVersion)"
    if ($LASTEXITCODE -ne 0) { throw "Automation build failed: $project" }
}
