[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Purpose,
    [string]$Owner = '',
    [string]$ModSet = '',
    [string]$SourceSaveDirectory = 'C:\Users\morte\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves',
    [string]$SavePattern = 'Fisso-NAM*.rws',
    [string]$ModSourceRoot = 'D:\Code\RimWorld'
)
# A refresh always creates an independent run; it never rewrites a live profile.
& (Join-Path $PSScriptRoot 'create-run.ps1') @PSBoundParameters
