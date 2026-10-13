[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'run-common.ps1')
$layout = Get-GameLayout $script:InstalledGameRoot
$script:RimWorldExecutable = Join-Path $script:InstalledGameRoot $layout.executable
if (@(Get-AllRimWorldProcessInfo | Where-Object { $_.ExecutablePath -ieq $script:RimWorldExecutable }).Count -ne 0) {
    throw 'Stop the installed game before replacing its runtime. Managed runs use independent copies.'
}
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\Automation\mod'))
$destination = Join-Path (Split-Path $script:RimWorldExecutable -Parent) 'Mods\SharedAutomation'
if (-not (Test-Path (Join-Path $source ($layout.gameVersion + '\Assemblies\RimWorld.Automation.dll')))) {
    throw 'Build the shared runtime before deploying it: build-runtime.ps1'
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $destination -Recurse -Force
Get-ChildItem -LiteralPath (Join-Path $source ($layout.gameVersion + '\Assemblies')) -Filter '*.dll' | ForEach-Object {
    $installed = Join-Path $destination ($layout.gameVersion + '\Assemblies\' + $_.Name)
    if ((Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $installed).Hash) {
        throw "Installed runtime hash mismatch: $($_.Name)"
    }
}
Write-Host 'Shared automation runtime deployed and hashes verified.'
