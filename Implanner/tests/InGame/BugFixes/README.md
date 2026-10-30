# Implanner audit regressions

These probes reproduce the player scenarios in `Implanner/BUG_TASKS.md`.
They use independent managed runs and never modify the source player save.
Run commands from `D:\Code\RimWorld`. Each script saves manifests, logs and
captures under `Implanner/temp/fix-verification`, then stops and removes its
own run in `finally`. Inspect captures for visual checks; a saved image alone
does not establish UI correctness.

## Build and run

Build the mod, runtime and selected probe before creating a run:

```powershell
dotnet build Implanner/src/Implanner.slnx -c Release -p:RimWorldVersion=1.7 -p:NuGetAudit=false
& Shared/tools/automation/build-runtime.ps1
dotnet build Implanner/tests/InGame/BugFixes/production/Probe.csproj -c Release -p:NuGetAudit=false
& Implanner/tests/InGame/BugFixes/production/run.ps1
```

Substitute `ranking`, `birthday`, `reservations` or `surgery-gates` for the
probe directory to run that scenario. Check each build's exit code before
launching. Production uses the `Fisso-NAM-1.7.rws` baseline and VGE; the other
1.7 scripts use the managed profile defaults and their declared mod sets.
Production's separate overview check is `production/run.ps1 -Quality`.

The `recovery` probe requires the installed QBR/EBF combination on RimWorld
1.6. Its script selects the Linux 1.6 game root, Ubuntu-22.04 software renderer
and 1.6 baseline explicitly. Build before running it:

```powershell
dotnet build Implanner/src/Implanner/Implanner.csproj -c Release -p:RimWorldVersion=1.6 -p:NuGetAudit=false
& Shared/tools/automation/build-runtime.ps1 -GameRoot D:/Code/RimWorld/Readouts/temp/linux-validation/game-1.6
dotnet build Implanner/tests/InGame/BugFixes/recovery/Probe.csproj -c Release -p:NuGetAudit=false
& Implanner/tests/InGame/BugFixes/recovery/run.ps1
```

Rebuild the automation runtime for the target game version when switching
versions. Core regressions, including IMP013's imported duplicate slots,
mixed-quality allocation, and alternative-ingredient routing, run with:

```powershell
dotnet Implanner/src/Implanner.Core.Tests/bin/Release/net10.0/Implanner.Core.Tests.dll --no-progress
```

## Observable checks

| Probe | Findings | Verification boundary |
|---|---|---|
| reservations | IMP001 | Holdback blocks the native doctor job; releasing it permits actual item consumption and installation. |
| surgery-gates | IMP002, IMP003 | A blocked first operation does not starve a valid second patient; retained bills obey changed doctor eligibility. |
| production | IMP004–006, IMP009, IMP010 | Changed policy affects real bills and native eligibility; powered benches win; lowered caps drain successors; native VGE jobs consume prerequisites and produce the implant. |
| production `-Quality` | IMP012 | Real Normal/Excellent demands with only Normal stock show 50% coverage and one item still needed. |
| ranking / birthday | IMP007, IMP011 | Actual horizontal mouse drag preserves order; natural simulation of the 13th birthday refreshes the open overview. |
| recovery | IMP008, IMP014 | QBR heart upgrade never queues fatal removal; healing releases inferior stock and the replacement preserves limb efficiency. |

Fixtures seed relevant stock, research and pawn conditions. Production
shortens recipe work while retaining native jobs and ingredient consumption.
Its interrupted-work control restores an unfinished item using the exact
construction in `Toils_Recipe.MakeUnfinishedThingIfNeeded`; it does not claim
a pawn started that control craft. It checks two started crafts with queued
successors, a lowered cap, repeated dispatch, cancellation, and resumed
allocation. Raising skill requirements must still allow the original creator
to resume the retained craft.

The QBR probe first attempts doctor navigation. If that times out, it invokes
the selected real recipe's `Worker.ApplyOnPawn` and `Worker.ConsumeIngredient`
and explicitly records that narrower boundary. This matches the original
reproduction and is not a full doctor-job claim. The heart patient is mortal
and has no death-prevention mechanism.

The copied baseline can contain recoverable errors from removed mods. These
probes assert their targeted outcomes; they do not certify an error-free save,
multiplayer transport, or every mod and display combination.
