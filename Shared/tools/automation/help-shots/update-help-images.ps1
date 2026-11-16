# Regenerates a mod's help images (<Mod>\mod\Help\Images) from the running
# mod: builds the mod with its own scripts\build.cmd (root contract; that also
# deploys it to the player's installation, -NoBuild skips it) and its capture
# probe (<Mod>\tests\InGame\HelpShots), plays one scripted run on the local 1.7 copy (run-help-shots.ps1: one full
# frame per scene plus a manifest of the named regions in it), then cuts every
# image listed in the probe folder's crops.json by region name (crop-help.py).
# An image whose pixels did not change is left untouched.
#
# Each mod calls this through its own scripts\update-help-images.ps1:
#   pwsh <Mod>\scripts\update-help-images.ps1            capture and cut
#   pwsh <Mod>\scripts\update-help-images.ps1 -Check     capture, report only
#   pwsh <Mod>\scripts\update-help-images.ps1 -RunFolder <folder>[,<folder>]
#                                                        cut again from an earlier capture
#
# Help images are 1920x1080 at UI scale 1.0; the other sizes are for looking
# at the UI, not for shipping (use -Check with them).
[CmdletBinding()] param([Parameter(Mandatory)][string]$Mod,
    [Parameter(Mandatory)][string[]]$ModSet, [string]$SavePattern = 'Fisso-NAM-1.7.rws',
    [int]$Width = 1920, [int]$Height = 1080, [double]$UiScale = 1.0, [int]$HardCapSeconds = 180,
    [string[]]$RunFolder = @(), [switch]$Check, [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$modRoot = Join-Path $source $Mod
if (-not $RunFolder) {
    if (-not $NoBuild) {
        & cmd /c (Join-Path $modRoot 'scripts\build.cmd')
        if ($LASTEXITCODE) { throw "$Mod build failed" }
        # Always rebuilt: a stale probe assembly looks like a stalled run.
        dotnet build (Join-Path $modRoot 'tests\InGame\HelpShots\Probe.csproj') -c Release -v q --nologo
        if ($LASTEXITCODE) { throw 'Capture probe build failed' }
    }
    # One run per mod set: a mod whose images need different mod lists
    # (crops.json "variant") names several, the first being the default.
    $RunFolder = foreach ($set in $ModSet) {
        & (Join-Path $PSScriptRoot 'run-help-shots.ps1') -Mod $Mod -ModSet $set -SavePattern $SavePattern `
            -Width $Width -Height $Height -UiScale $UiScale -HardCapSeconds $HardCapSeconds | Select-Object -Last 1
    }
}
$arguments = @((Join-Path $PSScriptRoot 'crop-help.py'), $modRoot) + @($RunFolder)
if ($Check) { $arguments += '--check' }
python @arguments
if ($LASTEXITCODE) { throw "Some help images could not be cut; frames and manifest are in $RunFolder" }
Write-Output "Frames and manifest: $RunFolder"
