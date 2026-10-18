# EPOE production verification

Run these tests from the repository root in PowerShell 7. They require .NET SDK,
Ubuntu-22.04 in WSL with Python 3, Xvfb and xauth, and the preserved Linux RimWorld
1.6 installation at `D:\Code\RimWorld\Readouts\temp\linux-validation\game-1.6`.
The player save `Fisso-NAM.rws` must match the shared 1.6 baseline mod list.
Harmony, EPOE-Forked, XML Extensions, Pickle and RimLogging must be installed;
the baseline save's other content mods remain enabled. Build artifacts for
Readouts, WorkRoles and QualityJobs must also exist before run creation.

Build Implanner, the shared runtime, and this run-only step assembly:

```powershell
dotnet build Implanner/src/Implanner/Implanner.csproj -c Release
& Shared/tools/automation/build-runtime.ps1 -GameRoot D:\Code\RimWorld\Readouts\temp\linux-validation\game-1.6
dotnet build Implanner/tests/InGame/ProductionRoutes/ImplannerProductionSteps.csproj -c Release
```

Each invocation creates a new owned run, copies completed build artifacts, runs
on an authenticated private Xvfb display, retains evidence under
`Implanner/temp/epoe-production/<Label>`, then stops and removes that run.
Run the examples sequentially to keep the fixed scenario budgets independent of CPU contention. Choose a new label for every invocation. `-ModSourceRoot` optionally selects an
already-built immutable source tree. The player's profile, saves and Workshop
files are unchanged.

```powershell
$verify = './Implanner/tests/InGame/ProductionRoutes/run.ps1'
# Upgrade-only: craft a base arm, then consume it in the upgrade.
& $verify -Mode 0 -Stock no -CraftTicks 4080 -ExpectedRecipe CreateAdvancedBionicArm -Label upgrade-only
# Direct-only: legacy upgrade recipes must remain inactive.
& $verify -Mode 2 -Stock no -ExpectedRecipe Make_AdvancedBionicArm -Label direct-only
# Combined: available stock decides between the two real routes.
& $verify -Mode 1 -Stock no -ExpectedRecipe Make_AdvancedBionicArm -Label combined-direct
& $verify -Mode 1 -Stock one -ExpectedRecipe CreateAdvancedBionicArm -Label combined-upgrade
# A new holdback must protect an arm before an already-queued upgrade can use it.
& $verify -Mode 1 -Stock one -RaiseHoldback -ExpectedRecipe Make_AdvancedBionicArm -Label raised-holdback
# Optional pre-existing manual holdback.
& $verify -Mode 1 -Stock reserved -ExpectedRecipe Make_AdvancedBionicArm -Label reserved-arm
# Decision publication, equal-refresh identity, overview invalidation, and reset.
& $verify -Mode 2 -Feature decisions -Label decisions
```

Modes are the actual EPOE XML Extensions `CraftingMethod` settings: 0 normal
upgrade crafting, 1 combined, and 2 direct-only. The runner writes only its
isolated settings file; the fixture asserts the loaded value and does not alter
recipe definitions. Debug fast crafting shortens work, while real
`WorkGiver_DoBill` jobs still select, haul and consume ingredients and create the
product. A pass requires an actual advanced arm on the map or carried by the
crafter, the expected real job, and consumption or preservation of the original
base arm as appropriate. Upgrade-only additionally requires both real base-arm
and advanced-arm jobs. Merely seeing a bill is insufficient.

`epoe-evidence.txt` records ingredient identities, physical output, current job
and bill repeat counts. Pickle's `summary.json` records assertions; `run.json`
and `launch.json` record source/mod configuration and copied artifact hashes.
The game-ready cap remains 60 seconds and the scenario cap 55 seconds. Final
accepted run/hash results are summarized in
`Implanner/temp/epoe-production/verification.json`.
