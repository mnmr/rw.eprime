[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\Automation\src'))
foreach ($project in @('RimWorld.Automation', 'RimWorld.Automation.Host')) {
    dotnet build (Join-Path $source "$project\$project.csproj") -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "Automation build failed: $project" }
}
