# Regenerates the help images in mod\Help\Images from the running mod (scenes
# in tests\InGame\HelpShots\Probe.cs, cuts in crops.json). See
# Shared\tools\automation\help-shots\update-help-images.ps1 for the options
# (-Check, -RunFolder <folder>, -NoBuild).
& (Join-Path $PSScriptRoot '..\..\Shared\tools\automation\help-shots\update-help-images.ps1') `
    -Mod WorkRoles -ModSet workroles-help-shots @args
