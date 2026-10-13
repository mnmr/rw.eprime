function Copy-RunSnapshot {
    param([Parameter(Mandatory)][string]$Source, [Parameter(Mandatory)][string]$Destination)
    $sourceRoot = [IO.Path]::GetFullPath($Source).TrimEnd('\')
    if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) { throw "Snapshot source is missing: $sourceRoot" }
    $files = @(Get-ChildItem -LiteralPath $sourceRoot -File -Recurse | Sort-Object FullName)
    $hashes = @($files | ForEach-Object {
        [pscustomobject]@{path=[IO.Path]::GetRelativePath($sourceRoot, $_.FullName); sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
    })
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    foreach ($file in $hashes) {
        $target = Join-Path $Destination $file.path
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $sourceRoot $file.path) -Destination $target
        if ((Get-FileHash -LiteralPath $target).Hash -cne $file.sha256 -or
            (Get-FileHash -LiteralPath (Join-Path $sourceRoot $file.path)).Hash -cne $file.sha256) {
            throw "Snapshot source changed during preparation: $sourceRoot\$($file.path)"
        }
    }
    $after = @(Get-ChildItem -LiteralPath $sourceRoot -File -Recurse | Sort-Object FullName)
    if ($after.Count -ne $files.Count) { throw "Snapshot file set changed: $sourceRoot" }
    for ($i=0; $i -lt $files.Count; $i++) {
        if ($after[$i].FullName -cne $files[$i].FullName) { throw "Snapshot file set changed: $sourceRoot" }
    }
    [pscustomobject]@{source=$sourceRoot; files=$hashes}
}
function New-RunGameSnapshot {
    param([Parameter(Mandatory)][string]$Profile, [Parameter(Mandatory)][string]$ModSourceRoot)
    $game = Join-Path $Profile 'Game'
    $layout = Get-GameLayout $script:InstalledGameRoot
    New-Item -ItemType Directory -Path $game | Out-Null
    # Vanilla locates local mods beside its executable. Copy launch binaries;
    # only large immutable game assets use junctions.
    $nativeFiles = @()
    foreach ($file in Get-ChildItem -LiteralPath $script:InstalledGameRoot -File) {
        $sha = Copy-VerifiedFile $file.FullName (Join-Path $game $file.Name)
        $nativeFiles += [pscustomobject]@{path=$file.Name;sha256=$sha}
    }
    foreach ($name in $layout.assetFolders) {
        New-Item -ItemType Junction -Path (Join-Path $game $name) -Target (Join-Path $script:InstalledGameRoot $name) | Out-Null
    }
    $sources = [ordered]@{}
    foreach ($installed in Get-ChildItem -LiteralPath (Join-Path $script:InstalledGameRoot 'Mods') -Directory) {
        $sources[$installed.Name] = $installed.FullName
    }
    foreach ($pair in @(@('EPrimeReadouts','Readouts'), @('WorkRoles','WorkRoles'), @('QualityJobs','QualityJobs'),
        @('Implanner','Implanner'), @('SharedAutomation','Shared\Automation'))) {
        $sources[$pair[0]] = Join-Path $ModSourceRoot ($pair[1] + '\mod')
    }
    if ($layout.executable -ceq 'RimWorldLinux') {
        # Linux launched outside Steam cannot discover the Windows Workshop.
        # Copy only active Workshop packages into this run's local mod snapshot.
        [xml]$configuration = Get-Content -LiteralPath (Join-Path $Profile 'Config\ModsConfig.xml') -Raw
        $active = @($configuration.ModsConfigData.activeMods.li)
        $provided = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($source in $sources.Values) {
            $about = Join-Path $source 'About\About.xml'
            if (Test-Path -LiteralPath $about) {
                [xml]$metadata = Get-Content -LiteralPath $about -Raw
                $null = $provided.Add([string]$metadata.ModMetaData.packageId)
            }
        }
        foreach ($folder in Get-ChildItem -LiteralPath $script:InstalledWorkshopRoot -Directory) {
            $about = Join-Path $folder.FullName 'About\About.xml'
            if (-not (Test-Path -LiteralPath $about)) { continue }
            [xml]$metadata = Get-Content -LiteralPath $about -Raw
            $packageId = [string]$metadata.ModMetaData.packageId
            if ($active -contains $packageId -and $provided.Add($packageId)) { $sources['Workshop-' + $folder.Name] = $folder.FullName }
        }
    }
    $mods = [ordered]@{}
    foreach ($name in $sources.Keys) {
        $mods[$name] = Copy-RunSnapshot $sources[$name] (Join-Path $game ('Mods\' + $name))
    }
    $hostSource = Join-Path $ModSourceRoot 'Shared\Automation\src\RimWorld.Automation.Host\bin\Release\net10.0-windows'
    $hostSnapshot = Copy-RunSnapshot $hostSource (Join-Path $Profile 'Host')
    foreach ($required in @(('Game\' + $layout.executable),('Game\Mods\SharedAutomation\' + $layout.gameVersion + '\Assemblies\RimWorld.Automation.dll'),'Host\RimWorld.Automation.Host.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $Profile $required) -PathType Leaf)) { throw "Build before creating the run; missing $required" }
    }
    [pscustomobject]@{executable=$layout.executable; gameVersion=$layout.gameVersion; nativeFiles=$nativeFiles; mods=$mods; host=$hostSnapshot}
}
