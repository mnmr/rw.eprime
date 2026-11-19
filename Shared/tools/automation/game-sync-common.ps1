function Test-Game17Payload {
    param([string]$Root)

    foreach ($file in @('RimWorld.exe', 'UnityStubs.dll', 'Waylith.dll', 'Assembly-CSharp.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $Root $file) -PathType Leaf)) { return $false }
    }
    foreach ($folder in @('Data', 'Rendering')) {
        if (-not (Test-Path -LiteralPath (Join-Path $Root $folder) -PathType Container)) { return $false }
    }
    return $true
}

function Get-SteamGame17Build {
    param([string]$Source)

    $steamApps = Split-Path -Parent (Split-Path -Parent $Source)
    $manifestPath = Join-Path $steamApps 'appmanifest_294100.acf'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw
    if ($manifest -notmatch '(?m)^\s*"StateFlags"\s+"4"\s*$' -or
        $manifest -notmatch '(?m)^\s*"buildid"\s+"([1-9][0-9]*)"\s*$') {
        throw 'Steam has no fully installed RimWorld build. Finish its update before syncing 1.7.'
    }
    $buildId = $Matches[1]
    $depots = [regex]::Match($manifest, '(?ms)^\s*"InstalledDepots"\s*\{((?:\s*"\d+"\s*\{[^{}]*\})+)\s*\}')
    if (-not $depots.Success) { throw 'Steam did not report the installed RimWorld depots.' }
    $depotBytes = [Text.Encoding]::UTF8.GetBytes(($depots.Groups[1].Value -replace '\s', ''))
    $depotHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($depotBytes))
    $version = (Get-Content -LiteralPath (Join-Path $Source 'Version.txt') -Raw).Trim()
    if ($version -notmatch '^1\.7\.\d+' -or -not (Test-Game17Payload $Source)) {
        throw "Steam has no complete Windows RimWorld 1.7 installation at $Source (reported version: $version)."
    }
    return "$buildId/$depotHash/$version"
}

function Resolve-GameDirectory {
    param([string]$Path)

    $fullPath = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Path))
    for ($directory = [IO.DirectoryInfo]::new($fullPath); $directory; $directory = $directory.Parent) {
        if ($directory.Exists -and ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            $target = $directory.ResolveLinkTarget($true)
            if ($target) {
                return [IO.Path]::GetFullPath([IO.Path]::GetRelativePath($directory.FullName, $fullPath), $target.FullName)
            }
        }
    }
    return $fullPath
}

function Assert-GameCopyCanChange {
    param([string]$Destination)

    if (-not (Test-Path -LiteralPath $script:AutomationRunsRoot -PathType Container)) { return }
    foreach ($folder in Get-ChildItem -LiteralPath $script:AutomationRunsRoot -Directory) {
        if ($folder.Name -cnotmatch '^[a-f0-9]{32}$') { continue }
        $run = Read-RunJson (Join-Path $folder.FullName 'run.json')
        if (-not $run.gameRoot -or (Resolve-GameDirectory $run.gameRoot) -ine $Destination) { continue }
        # An unstarted run can begin at any time. A started run is single-use
        # and its host holds run.lock until its child process tree is closed.
        if (-not (Test-Path -LiteralPath (Join-Path $folder.FullName 'started.json')) -or
            (Test-RunLocked (Join-Path $folder.FullName 'run.lock'))) {
            throw "Run $($folder.Name) still uses $Destination. Stop it or remove it if it has not started, then retry."
        }
    }
}

function Assert-GameSyncPath {
    param([string]$Root)

    for ($directory = [IO.DirectoryInfo]::new($Root); $directory; $directory = $directory.Parent) {
        if ($directory.Exists -and ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Game sync path contains a link: $($directory.FullName)"
        }
    }
}

# The caller holds Enter-GamePreparation through synchronization and any use
# that must match this build, including publication of a new run manifest.
function Update-LocalGame17 {
    $source = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($script:SteamGameRoot))
    $destination = [IO.Path]::GetFullPath((Join-Path $script:GameInstallsRoot '1.7'))
    $stamp = "$destination.steam.json"
    $comparison = [StringComparison]::OrdinalIgnoreCase
    if ($source.Equals($destination, $comparison) -or $source.StartsWith("$destination\", $comparison) -or
        $destination.StartsWith("$source\", $comparison)) { throw 'Steam and local game folders must be separate.' }
    Assert-GameSyncPath $source
    Assert-GameSyncPath $destination

    $build = Get-SteamGame17Build $source
    $localVersionPath = Join-Path $destination 'Version.txt'
    $localVersion = if (Test-Path -LiteralPath $localVersionPath -PathType Leaf) {
        (Get-Content -LiteralPath $localVersionPath -Raw).Trim()
    }
    $version = $build.Split('/', 3)[2]
    if ((Read-RunJson $stamp).build -ceq $build -and $version -ceq $localVersion -and
        (Test-Game17Payload $destination)) {
        return $destination
    }

    Assert-GameCopyCanChange $destination
    $sourceMods = Join-Path $source 'Mods'
    $localMods = Join-Path $destination 'Mods'
    # /XJ skips junctions. Reject other links too, so /MIR only affects
    # ordinary files below the verified local game root.
    foreach ($root in @($source, $destination)) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
        foreach ($entry in Get-ChildItem -LiteralPath $root -Force) {
            if ($entry.PSIsContainer -and $entry.Name -ieq 'Mods') { continue }
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Game sync contains a link: $($entry.FullName)"
            }
            if ($entry.PSIsContainer) {
                $link = Get-ChildItem -LiteralPath $entry.FullName -Recurse -Force -Attributes ReparsePoint | Select-Object -First 1
                if ($link) { throw "Game sync contains a link: $($link.FullName)" }
            }
        }
    }

    if (Test-Path -LiteralPath $stamp) { Remove-Item -LiteralPath $stamp -Force }
    Write-Host "Syncing RimWorld $version from Steam to $destination"
    $copyOptions = @('/R:2', '/W:1', '/XJ', '/NP', '/NFL', '/NDL')
    $copyLog = & robocopy.exe $source $destination /MIR /IS @copyOptions /XD $sourceMods $localMods
    if ($LASTEXITCODE -ge 8) { throw "Game sync failed (robocopy $($LASTEXITCODE)).`n$($copyLog -join "`n")" }
    if (-not (Test-Path -LiteralPath $localMods -PathType Container)) {
        New-Item -ItemType Directory -Path $localMods -Force | Out-Null
    }
    if ((Get-SteamGame17Build $source) -cne $build -or -not (Test-Game17Payload $destination) -or
        (Get-Content -LiteralPath $localVersionPath -Raw).Trim() -cne $version) {
        throw 'Steam changed during the game sync. Retry once Steam has finished its update.'
    }
    Write-RunJson $stamp @{build=$build}
    return $destination
}

function Resolve-AutomationGameRoot {
    param([string]$GameRoot, [string]$GameVersion)

    if ($GameRoot -and $GameVersion) { throw 'Pass -GameRoot or -GameVersion, not both.' }
    $local17 = Join-Path $script:GameInstallsRoot '1.7'
    if ($GameRoot) {
        $path = Resolve-GameDirectory $GameRoot
        if ($path -ine $local17 -and
            ($path -ine $script:SteamGameRoot -or (Get-GameLayout $path).gameVersion -cne '1.7')) { return $path }
    } elseif ($GameVersion -eq '1.6') {
        return (Join-Path $script:GameInstallsRoot '1.6')
    }
    Update-LocalGame17
}

function Assert-RunGame17Current {
    param([string]$GameRoot)

    $local17 = Join-Path $script:GameInstallsRoot '1.7'
    if (-not $GameRoot -or (Resolve-GameDirectory $GameRoot) -ine $local17) { return }
    $build = Get-SteamGame17Build $script:SteamGameRoot
    if ((Read-RunJson "$local17.steam.json").build -cne $build) {
        throw 'The Steam RimWorld 1.7 installation changed. Remove this prepared run and create a fresh run to sync it.'
    }
}
