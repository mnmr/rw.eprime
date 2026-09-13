[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'automation-common.ps1')
Assert-NoSharedRimWorldProcess
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\Automation\mod'))
$destination = Join-Path (Split-Path $script:RimWorldExecutable -Parent) 'Mods\SharedAutomation'
if (-not (Test-Path (Join-Path $source '1.6\Assemblies\RimWorld.Automation.dll'))) {
    throw 'Build the shared runtime before deploying it: build-runtime.ps1'
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $destination -Recurse -Force
Get-ChildItem -LiteralPath (Join-Path $source '1.6\Assemblies') -Filter '*.dll' | ForEach-Object {
    $installed = Join-Path $destination ('1.6\Assemblies\' + $_.Name)
    if ((Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $installed).Hash) {
        throw "Installed runtime hash mismatch: $($_.Name)"
    }
}
Write-Host 'Shared automation runtime deployed and hashes verified.'
