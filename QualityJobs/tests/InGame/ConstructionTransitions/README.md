# Construction transition regression

This runtime probe exercises QualityJobs' real Harmony patches around real
RimWorld frames, blueprints and beds. It targets transition handling; it does
not repeat the pawn job scheduling coverage in `../CompletedBuildings`.

The scenarios verify:

- Suppressed completion, construction failure and destruction retain the live
  target, its plan and its revision, without spawning a replacement blueprint.
- Real completion retires a satisfied plan; real failure and deconstruction
  transfer the existing plan to the replacement; other destruction removes it.
- Settings edited after canceled demolition survive the subsequent rebuild.
- Successful replacement prefixes still transfer plans when they return false:
  early replacements cover all three methods, and a late replacement also covers
  completion. Suppression is tested at both ordinary `Priority.First` and
  `Priority.Last` ordering. Early demolition also verifies captured bed settings.

The early deconstruction test uses a Harmony reverse patch to run vanilla
`Building.Destroy` from the replacement prefix. Replacement construction uses
the game's factory and spawn APIs. Blueprint-to-frame conversion uses the real
game method. Only loose item blockers from demolition salvage are removed from
the disposable fixture; hauling is outside this regression.

## Run

Build the mod, probe and shared automation runtime for the intended version
before running. Coordinate version switches because the mod's restore/build
intermediates are shared between versions.

```powershell
dotnet build QualityJobs/src/QualityJobs.slnx -c Release -p:RimWorldVersion=1.7
dotnet build QualityJobs/tests/InGame/ConstructionTransitions/Probe.csproj -c Release -p:RimWorldVersion=1.7
& Shared/tools/automation/build-runtime.ps1
& QualityJobs/tests/InGame/ConstructionTransitions/run.ps1 -Version 1.7 -Label regression
```

For 1.6, build both projects with `-p:RimWorldVersion=1.6`, build the shared
runtime with `-GameRoot D:\Code\RimWorld\Readouts\temp\linux-validation\game-1.6`,
then run with `-Version 1.6`. The runner selects the matching save/baseline and
uses the private WSL software-rendered game.

Each invocation creates an independently owned, single-use managed run. The
runner stops it, preserves logs/captures/manifests below
`QualityJobs/temp/construction-transitions`, then removes it. `-Clean` loads the
same mod set without the probe for final clean-load verification.

## Ordering boundary

Read-only transition capture runs at `int.MaxValue` priority, ahead of ordinary
behavior prefixes. The completion gate remains separate and explicitly clears
its captured plan when QualityJobs itself pauses completion. Postfixes require
actual destruction and continued ownership of the captured plan.

Explicit Harmony before/after dependencies, or another capture using the same
maximum priority, can still determine order. The regression proves ordinary
First/Last replacement compatibility; it does not claim control over arbitrary
external ordering constraints.

## Observed evidence

All paths below are relative to `QualityJobs/temp/construction-transitions`.

- Initial red: `1.7-red-3baca8a731bc462f809dad0028a1742b` records all three
  suppressed-transition failures before the production fix.
- Ordering red: `1.7-early-order-red-b9d36d6f78b84ac1be4c4add12275a49` records
  early successful replacement failures for completion, failure and destruction.
- Expanded 1.7 green: `1.7-early-order-green-79af04158fa54557824f3a58d415d28d`
  records every suppression, transition, settings and ordering scenario passing.
- Expanded 1.6 green:
  `1.6-early-order-green-retry-2d7607e6a6df487daf20b188da728faf` records the
  same scenarios passing under Unity/Harmony 2.
- The preceding 1.6 attempt,
  `1.6-early-order-green-281f239d30a94c9eac1d33218f3879bb`, timed out during
  Unity startup before mod loading. Its evidence is retained separately; the
  fresh retry above passed. Both runs were stopped and removed by the runner.
