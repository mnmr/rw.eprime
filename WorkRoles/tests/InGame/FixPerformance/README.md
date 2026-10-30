# Native steady window comparison

Build the relevant WorkRoles artifacts and shared automation runtime first.
Build this probe once, then use the same assembly against both source roots:

```powershell
dotnet build WorkRoles/tests/InGame/FixPerformance/Probe.csproj -c Release
pwsh WorkRoles/tests/InGame/FixPerformance/run.ps1 -Label baseline -ModSourceRoot D:/Code/RimWorld/WorkRoles/temp/fix-verification-2026-10-30/baseline-source -SkipProbeBuild
pwsh WorkRoles/tests/InGame/FixPerformance/run.ps1 -Label fixed -ModSourceRoot D:/Code/RimWorld -SkipProbeBuild
```

The caller coordinates idle machine workload. Each command creates an independent
managed run, loads the native saved colony, pauses, selects Takeo, opens Colonists
with ideology grouping and the whole roster, warms 60 actual window repaints,
and samples 180 actual `MainTabWindow_WorkRoles.DoWindowContents` Repaint calls.
No synthetic draw loop or production instrumentation is used. Both runs use
the managed default 1920x1080 at 1.25 UI scale. Do not interact during sampling.

Test-only Harmony brackets record current-thread allocated bytes using the native
`GC.GetAllocatedBytesForCurrentThread` API and elapsed `Stopwatch` ticks around
each actual method call. The allocation reader is a cached delegate resolved
once, allowing an explicit failure if the native runtime lacks this API. Sampling
uses preallocated arrays. Reporting, reflection, file writes and capture happen
outside the measured calls. Neither periodic timing nor a stopwatch drives game
state; the game remains paused at its original tick.

Evidence is retained under `WorkRoles/temp/fix-performance/LABEL-RUNID`:

- `fix-performance-summary.json`: sample count, allocation/time min, median,
  p95, max and mean, GC collections, unchanged external/skill/section identities,
  native pawn-fact capture count, tick, pawn/map and display/content geometry.
- `fix-performance-samples.json`: every raw sample in observed order.
- `Captures/steady-colonists.png`: the exact window after measurement. Inspect
  matching selection, rows and geometry before comparing measurements.
- `comparison.json`, `run.json`, `launch.json`: source root, same probe SHA-256,
  source-save SHA-256, ordered mod configuration and copied assembly hashes.

Compare only runs with matching probe/save hashes, configuration, native pawn,
paused tick, display metrics and content rectangle. `-SourceSaveDirectory` and
`-SavePattern` can pin a read-only saved fixture if the player's latest save
changes between comparisons. Investigate GC or external workload differences
before treating timing differences as a code effect. The current-thread bytes
are allocations inside this window method, not process memory or background work.

The runner allows 50 seconds after shared launch readiness, captures once, then
stops its exact run before copying evidence and removes that run. Recoverable
native save errors stay in Player.log; only the automatic log popup is hidden.
