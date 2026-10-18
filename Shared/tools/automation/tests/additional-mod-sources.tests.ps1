$ErrorActionPreference = 'Stop'
$toolsRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $toolsRoot 'run-common.ps1')
. (Join-Path $toolsRoot 'profile-common.ps1')
. (Join-Path $toolsRoot 'snapshot-common.ps1')
function Check([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Rejects([scriptblock]$Action, [string]$Expected) {
    try { & $Action | Out-Null } catch {
        if ($_.Exception.Message -notlike "*$Expected*") { throw }
        return
    }
    throw "Expected rejection: $Expected"
}
function New-Mod([string]$Root, [string]$Id) {
    New-Item -ItemType Directory -Path (Join-Path $Root 'About') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $Root 'About\About.xml'), "<ModMetaData><packageId>$Id</packageId></ModMetaData>")
}
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('additional-mods-' + [Guid]::NewGuid().ToString('N'))
$originalGame = $script:InstalledGameRoot
$originalWorkshop = $script:InstalledWorkshopRoot
$originalRepository = $script:AutomationRepositoryRoot
New-Item -ItemType Directory -Path $fixture | Out-Null
try {
    # Removing the explicit-source forwarding breaks real mod-set activation;
    # replacing copies with links breaks the independent published bytes below.
    $script:InstalledGameRoot = Join-Path $fixture 'installation'
    $script:InstalledWorkshopRoot = Join-Path $fixture 'workshop'
    $script:AutomationRepositoryRoot = Join-Path $fixture 'automation'
    foreach ($folder in @('Mods', 'Data', 'RimWorldWin64_Data', 'MonoBleedingEdge')) {
        New-Item -ItemType Directory -Path (Join-Path $script:InstalledGameRoot $folder) -Force | Out-Null
    }
    New-Item -ItemType Directory -Path $script:InstalledWorkshopRoot -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $script:InstalledGameRoot 'RimWorldWin64.exe'), 'fixture; never launched')
    $worktree = Join-Path $fixture 'builds'
    foreach ($folder in @('Readouts', 'WorkRoles', 'QualityJobs', 'Implanner', 'Shared\Automation')) {
        New-Item -ItemType Directory -Path (Join-Path $worktree ($folder + '\mod')) -Force | Out-Null
    }
    New-Mod (Join-Path $worktree 'Shared\Automation\mod') 'eprime.sharedautomation'
    $runtime = Join-Path $worktree 'Shared\Automation\mod\1.6\Assemblies'
    New-Item -ItemType Directory -Path $runtime -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $runtime 'RimWorld.Automation.dll'), 'fixture runtime')
    $hostSource = Join-Path $worktree 'Shared\Automation\src\RimWorld.Automation.Host\bin\Release\net10.0-windows'
    New-Item -ItemType Directory -Path $hostSource -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $hostSource 'RimWorld.Automation.Host.exe'), 'fixture host')
    $external = Join-Path $fixture 'downloads\OptionalIntegration'
    New-Mod $external 'Regression.Explicit'
    [IO.File]::WriteAllText((Join-Path $external 'payload.txt'), 'downloaded original')
    $set = Join-Path $fixture 'optional.txt'
    @('after ludeon.rimworld', 'regression.explicit') | Set-Content -LiteralPath $set
    $active = @(Build-ModSetList -SaveModIds @('ludeon.rimworld', 'regression.kept') -SetPath $set -ModSourceRoot $worktree -AdditionalModSource @($external))
    Check (($active -join ',') -ceq 'ludeon.rimworld,regression.explicit,regression.kept') 'Explicit source must resolve in its mod-set order, without installation.'
    Rejects { Build-ModSetList -SaveModIds @('ludeon.rimworld') -SetPath $set -ModSourceRoot $worktree } 'absent'
    $profile = Join-Path $fixture 'run'
    New-Item -ItemType Directory -Path $profile | Out-Null
    $snapshot = New-RunGameSnapshot -Profile $profile -ModSourceRoot $worktree -AdditionalModSource @($external)
    $copied = Join-Path $profile 'Game\Mods\OptionalIntegration\payload.txt'
    Check (Test-Path -LiteralPath $copied) 'Explicit source must be copied into the isolated game.'
    Check (-not (Test-Path -LiteralPath (Join-Path $script:InstalledGameRoot 'Mods\OptionalIntegration'))) 'Preparing a source must leave the installation untouched.'
    [IO.File]::WriteAllText((Join-Path $external 'payload.txt'), 'later download update')
    Check ((Get-Content -LiteralPath $copied -Raw) -ceq 'downloaded original') 'Source edits must not change the run copy.'
    $manifest = $snapshot.mods['OptionalIntegration']
    Check ($manifest.source -ceq [IO.Path]::GetFullPath($external)) 'Manifest must record the exact additional source.'
    $payloadHash = @($manifest.files | Where-Object path -eq 'payload.txt')[0].sha256
    Check ($payloadHash -ceq (Get-FileHash -LiteralPath $copied).Hash) 'Manifest must hash the published bytes.'

    # Profile preparation must use the same explicit availability without
    # weakening source-save ordering or activating unrequested packages.
    $bridge = Join-Path $fixture 'downloads\BridgeDependency'
    New-Mod $bridge 'brrainz.rimbridgeserver'
    $saveDirectory = Join-Path $fixture 'saves'
    New-Item -ItemType Directory -Path $saveDirectory | Out-Null
    $savePath = Join-Path $saveDirectory 'Fisso-NAM-fixture.rws'
    [IO.File]::WriteAllText($savePath, '<savegame><meta><modIds><li>brrainz.harmony</li><li>ludeon.rimworld</li></modIds></meta></savegame>')
    $sourceHash = (Get-FileHash -LiteralPath $savePath).Hash
    $baseline = Join-Path $fixture 'baseline.xml'
    [IO.File]::WriteAllText($baseline, '<ModsConfigData><activeMods><li>brrainz.harmony</li><li>ludeon.rimworld</li></activeMods></ModsConfigData>')
    $preparedProfile = Join-Path $fixture 'prepared-profile'
    $prepared = Initialize-RunProfile -Profile $preparedProfile -SourceSaveDirectory $saveDirectory -SavePattern 'Fisso-NAM*.rws' -ModSourceRoot $worktree -BaselineConfigPath $baseline -AdditionalModSource @($external, $bridge)
    Check (($prepared.activeMods -join ',') -ceq 'brrainz.harmony,brrainz.rimbridgeserver,ludeon.rimworld,eprime.sharedautomation') 'Profile preparation must resolve explicit dependencies while preserving mod-set order.'
    Check ((Get-FileHash -LiteralPath $savePath).Hash -ceq $sourceHash) 'Profile preparation must preserve the source save.'
    [IO.File]::WriteAllText($baseline, '<ModsConfigData><activeMods><li>ludeon.rimworld</li><li>brrainz.harmony</li></activeMods></ModsConfigData>')
    Rejects { Initialize-RunProfile -Profile (Join-Path $fixture 'bad-baseline') -SourceSaveDirectory $saveDirectory -SavePattern 'Fisso-NAM*.rws' -ModSourceRoot $worktree -BaselineConfigPath $baseline -AdditionalModSource @($external, $bridge) } 'mod-list mismatch'

    $duplicateId = Join-Path $fixture 'downloads\DuplicatePackage'
    New-Mod $duplicateId 'regression.EXPLICIT'
    $collision = Join-Path $fixture 'other\OptionalIntegration'
    New-Mod $collision 'regression.other'
    $reserved = Join-Path $fixture 'other\WorkRoles'
    New-Mod $reserved 'regression.reserved'
    $missingId = Join-Path $fixture 'other\MissingId'
    New-Mod $missingId ''
    foreach ($case in @(
        @{Sources=@($external, $external); Error='duplicate'; Name='repeated-root'},
        @{Sources=@($external, $duplicateId); Error='package'; Name='duplicate-package'},
        @{Sources=@($external, $collision); Error='folder'; Name='folder-collision'},
        @{Sources=@($reserved); Error='folder'; Name='reserved-folder'},
        @{Sources=@($missingId); Error='package'; Name='missing-package'},
        @{Sources=@((Join-Path $fixture 'absent')); Error='About'; Name='missing-root'}
    )) {
        $rejectedProfile = Join-Path $fixture $case.Name
        New-Item -ItemType Directory -Path $rejectedProfile | Out-Null
        Rejects { New-RunGameSnapshot -Profile $rejectedProfile -ModSourceRoot $worktree -AdditionalModSource $case.Sources } $case.Error
        Check (-not (Test-Path -LiteralPath (Join-Path $rejectedProfile 'Game'))) 'Invalid source sets must fail before game snapshot creation.'
    }
    # Linux also copies active Workshop mods. An explicit folder must not
    # overwrite that independent dependency or leave a partial game snapshot.
    [IO.File]::WriteAllText((Join-Path $script:InstalledGameRoot 'RimWorldLinux'), 'fixture; never launched')
    New-Item -ItemType Directory -Path (Join-Path $script:InstalledGameRoot 'RimWorldLinux_Data') | Out-Null
    New-Mod (Join-Path $script:InstalledWorkshopRoot '77') 'regression.workshop'
    $workshopCollision = Join-Path $fixture 'other\Workshop-77'
    New-Mod $workshopCollision 'regression.collision'
    $linuxProfile = Join-Path $fixture 'linux-collision'
    New-Item -ItemType Directory -Path (Join-Path $linuxProfile 'Config') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $linuxProfile 'Config\ModsConfig.xml'), '<ModsConfigData><activeMods><li>regression.workshop</li></activeMods></ModsConfigData>')
    Rejects { New-RunGameSnapshot -Profile $linuxProfile -ModSourceRoot $worktree -AdditionalModSource @($workshopCollision) } 'folder collision'
    Check (-not (Test-Path -LiteralPath (Join-Path $linuxProfile 'Game'))) 'Workshop folder collisions must fail before game snapshot creation.'
    $linuxSnapshot = New-RunGameSnapshot -Profile $linuxProfile -ModSourceRoot $worktree -AdditionalModSource @($external)
    Check (Test-Path -LiteralPath (Join-Path $linuxProfile 'Game\Mods\Workshop-77\About\About.xml')) 'Linux must retain active Workshop dependencies beside explicit sources.'
    Check (Test-Path -LiteralPath (Join-Path $linuxProfile 'Game\Mods\OptionalIntegration\About\About.xml')) 'Linux must also copy explicit sources.'
    New-Mod (Join-Path $script:InstalledGameRoot 'Mods\AlreadyInstalled') 'regression.explicit'
    Rejects { Build-ModSetList -SaveModIds @('ludeon.rimworld') -SetPath $set -ModSourceRoot $worktree -AdditionalModSource @($external) } 'package'
    Write-Host 'PASS: additional mod sources resolve, copy independently, record hashes and reject ambiguous inputs.'
} finally {
    $script:InstalledGameRoot = $originalGame
    $script:InstalledWorkshopRoot = $originalWorkshop
    $script:AutomationRepositoryRoot = $originalRepository
    # The generated absolute fixture is the only recursive cleanup target;
    # remove the known game asset junctions before deleting its private tree.
    if (-not ([IO.Path]::GetFullPath($fixture).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase))) { throw 'Unsafe fixture cleanup target.' }
    Get-ChildItem -LiteralPath $fixture -Directory -Recurse | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint } | ForEach-Object { [IO.Directory]::Delete($_.FullName) }
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
