# Regenerates the help images in mod\Help\Images from the running mod (scenes
# in tests\InGame\HelpShots\Probe.cs, cuts in crops.json): one run without
# other mods, one with the implant mods the help pictures and one with a mod
# whose implants carry a quality (crops.json "variant"). See Shared\tools\automation\help-shots\update-help-images.ps1
# for the options (-Check, -RunFolder <folder>,<folder>, -NoBuild).
& (Join-Path $PSScriptRoot '..\..\Shared\tools\automation\help-shots\update-help-images.ps1') `
    -Mod Implanner -ModSet implanner-help-shots, implanner-help-shots-mods, implanner-help-shots-quality @args
