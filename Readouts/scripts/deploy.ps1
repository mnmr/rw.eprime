param(
    [string]$RimWorldMods = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods"
)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repo "mod"
$dest = Join-Path $RimWorldMods "EPrimeReadouts"
# /MIR deletes stale files; /XF-excluded files are neither copied nor deleted,
# which keeps Steam's PublishedFileId.txt alive in the destination.
robocopy $source $dest /MIR /XF PublishedFileId.txt *.pdb | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
Write-Host "Deployed to $dest"
exit 0
