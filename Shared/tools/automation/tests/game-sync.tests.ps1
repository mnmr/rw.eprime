$ErrorActionPreference = 'Stop'
$toolsRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $toolsRoot 'run-common.ps1')
. (Join-Path $toolsRoot 'game-sync-common.ps1')

$fixture = Join-Path $script:AutomationRepositoryRoot ('temp\game-sync-' + [Guid]::NewGuid().ToString('N'))
$script:SteamGameRoot = Join-Path $fixture 'steamapps\common\RimWorld'
$script:GameInstallsRoot = Join-Path $fixture 'GameInstalls'
$script:AutomationRunsRoot = Join-Path $fixture 'Runs'
$local = Join-Path $script:GameInstallsRoot '1.7'
$manifest = Join-Path $fixture 'steamapps\appmanifest_294100.acf'

function Put([string]$Root, [string]$Name, [string]$Contents) {
    $path = Join-Path $Root $Name
    New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
    Set-Content -LiteralPath $path -Value $Contents
}
function Check([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Set-SteamBuild([int]$Build, [int]$State, [int]$DepotManifest = 500) {
    Set-Content -LiteralPath $manifest -Value @('"AppState"', '{', '"appid" "294100"',
        ('"StateFlags" "' + $State + '"'), ('"buildid" "' + $Build + '"'),
        '"InstalledDepots"', '{', '"294104"', '{', ('"manifest" "' + $DepotManifest + '"'), '}', '}', '}')
}

try {
    foreach ($root in @($script:SteamGameRoot, $local)) {
        Put $root 'RimWorld.exe' 'launch'
        Put $root 'UnityStubs.dll' 'compatibility'
        Put $root 'Waylith.dll' 'engine'
        Put $root 'Assembly-CSharp.dll' 'gameplay'
        Put $root 'Data\Core\defs.xml' 'defs'
        Put $root 'Rendering\shader.bin' 'shader'
    }
    Put $script:SteamGameRoot 'Version.txt' '1.7.5013 rev802'
    Put $script:SteamGameRoot 'Data\new.bin' 'new asset'
    Put $script:SteamGameRoot 'Mods\Example\About.xml' 'Steam mod'
    Put $local 'Version.txt' '1.7.4995 rev309'
    Put $local 'Data\old.bin' 'retired asset'
    Put $local 'Mods\Example\About.xml' 'local mod'
    Set-SteamBuild 100 4

    Check ((Resolve-AutomationGameRoot) -ieq $local) 'The default automation flow must select the synced local game.'
    Check ((Get-Content (Join-Path $local 'Version.txt')) -ceq '1.7.5013 rev802') 'The local copy must refresh to the Steam version.'
    Check ((Get-Content (Join-Path $local 'Data\new.bin')) -ceq 'new asset') 'Updated game assets must be copied.'
    Check (-not (Test-Path (Join-Path $local 'Data\old.bin'))) 'Removed game assets must not survive the update.'
    Check ((Get-Content (Join-Path $local 'Mods\Example\About.xml')) -ceq 'local mod') 'Local mods must survive a game update.'
    Check ((Get-Content (Join-Path $script:SteamGameRoot 'Mods\Example\About.xml')) -ceq 'Steam mod') 'The Steam installation must remain read-only.'

    Check ((Resolve-AutomationGameRoot -GameRoot $script:SteamGameRoot) -ieq $local) 'An explicit Steam 1.7 path must also use the synced copy.'
    Put $script:SteamGameRoot 'Data\DLC\defs.xml' 'installed expansion'
    Set-SteamBuild 100 4 501
    Update-LocalGame17 | Out-Null
    Check ((Get-Content (Join-Path $local 'Data\DLC\defs.xml')) -ceq 'installed expansion') 'Depot changes must sync even if the Steam build ID stays the same.'

    $runId = 'b' * 32
    $profile = Join-Path $script:AutomationRunsRoot $runId
    New-Item -ItemType Directory -Path $profile -Force | Out-Null
    Write-RunJson (Join-Path $profile 'run.json') @{ runId=$runId; profile=$profile; gameRoot=$local }
    Check ((Resolve-AutomationGameRoot -GameVersion 1.7) -ieq $local) 'A current local copy must remain usable while a prepared run shares it.'
    Assert-RunGame17Current $local
    Put $script:SteamGameRoot 'Version.txt' '1.7.5014 rev803'
    Set-SteamBuild 101 4
    $blocked = $false
    try { Assert-RunGame17Current $local } catch { $blocked = $_.ToString().Contains('fresh run') }
    Check $blocked 'A launch must reject a run prepared before a Steam update.'
    $blocked = $false
    try { Update-LocalGame17 | Out-Null } catch { $blocked = $_.ToString().Contains($runId) }
    Check $blocked 'A prepared run must block replacement of its shared game assets.'
    Check ((Get-Content (Join-Path $local 'Version.txt')) -ceq '1.7.5013 rev802') 'A blocked update must keep the prepared run on its build.'

    Put $profile 'started.json' '{}'
    $lease = Open-RunLock (Join-Path $profile 'run.lock')
    try {
        $blocked = $false
        try { Update-LocalGame17 | Out-Null } catch { $blocked = $_.ToString().Contains($runId) }
        Check $blocked 'A running test must block replacement of its shared game assets.'
    } finally { $lease.Dispose() }

    Set-SteamBuild 101 6
    $blocked = $false
    try { Update-LocalGame17 | Out-Null } catch { $blocked = $_.ToString().Contains('Steam') }
    Check $blocked 'An incomplete Steam update must stop preflight.'
    Check ((Get-Content (Join-Path $local 'Version.txt')) -ceq '1.7.5013 rev802') 'An incomplete Steam update must leave the local build intact.'
    Check ((Resolve-AutomationGameRoot -GameVersion 1.6) -ieq (Join-Path $script:GameInstallsRoot '1.6')) 'Legacy automation must work independently of Steam sync.'
    Check ((Resolve-AutomationGameRoot -GameRoot $profile) -ieq $profile) 'An explicit independent fixture must bypass Steam sync.'

    Set-SteamBuild 101 4
    Update-LocalGame17 | Out-Null
    Check ((Get-Content (Join-Path $local 'Version.txt')) -ceq '1.7.5014 rev803') 'A completed run must allow a later Steam update.'
    Write-Host 'Game sync checks passed.'
} finally {
    if ((Split-Path -Parent ([IO.Path]::GetFullPath($fixture))) -ieq (Join-Path $script:AutomationRepositoryRoot 'temp') -and
        (Test-Path -LiteralPath $fixture)) {
        Remove-Item -LiteralPath $fixture -Recurse -Force
    }
}
