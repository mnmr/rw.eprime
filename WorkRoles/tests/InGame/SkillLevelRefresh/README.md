# Native whole skill level refresh regression

This test-only mod runs against a fresh managed copy of the full Fisso save.
It keeps the native 27-pawn recommendation cohort, authored roles, training
paths, demand, pins and assignment states. It restores the original assignment
sets between scenarios and exercises the real selected-pawn Make It So action.
The search field only narrows the visible rows; planning retains all 27 pawns.

The fixture warms Han at native Animals 7, calls native `SkillRecord.Learn` to
gain level 8, then checks the warm recommendation producer, roster skill column,
selected stats and applied role against the expected Handler training band.
After closing and reopening the actual window, it independently warms level 8
and uses negative native Learn XP to lose a level. The expected role is Herder.
The reopen occurs between scenarios, never between the tested mutation and its
observation. A fresh full-colony native-adapter plan corroborates each expected
band. Captures show both outcomes and are acknowledged before proceeding.

Additional controls cover positive XP without a level crossing, no-op Level
and EnsureMin operations, XP-only EnsureMin changes, several level transitions
coalescing before refresh, native EnsureMin whole-level changes, paused edits,
unrelated pawn snapshot identity, and the existing priority-grid cell shading.
Test-only Harmony counters observe pawn/capability captures and capability
refreshes. They require zero eager capture at the level event and exactly one
affected-pawn/capability capture at the subsequent UI refresh. No production
test seam is used.

Build once, then use the identical probe DLL for both immutable production
snapshots. The baseline mode must reproduce both gain and loss failures; final
mode requires every assertion to pass. The runner refuses a busy managed game
slot so timings are uncontended.

```powershell
dotnet build WorkRoles/tests/InGame/SkillLevelRefresh/Probe.csproj -c Release
& WorkRoles/tests/InGame/SkillLevelRefresh/run.ps1 -Mode baseline -Label baseline -SkipProbeBuild -ModSourceRoot D:\Code\RimWorld\WorkRoles\temp\wr28-skill-refresh\baseline-source
& WorkRoles/tests/InGame/SkillLevelRefresh/run.ps1 -Mode final -Label final -SkipProbeBuild -ModSourceRoot D:\Code\RimWorld\WorkRoles\temp\wr28-skill-refresh\fixed-source
```

The runner owns a fresh single-use run, permits only the known recoverable
source-save errors, enforces 50 seconds after launch-ready, and stops/removes
that exact run in `finally`. It retains the full log, source and launch metadata,
probe hash, authored-input export, assertions, screenshots and timing samples
under `WorkRoles/temp/skill-level-refresh/<label>-<run-id>`.

The ordinary-XP benchmark uses 2,000 warm-up pairs and 32 samples of 5,000
positive/negative pairs (320,000 measured native Learn calls). Exactly
representable XP amounts change XP on each call without crossing a level or
drifting the endpoint. A separate positive-only batch verifies ordinary gain.
Measurement loops contain no reflection, formatting or allocations of their
own. `skill-xp-samples.csv` records per-call nanoseconds and per-batch allocated
bytes; compare medians and allocation distributions using the same binary and
save. Runtime/JIT one-off allocations can occur, so preserve raw samples.

`skill-transitions.csv` columns are scenario, event microseconds, event bytes,
lazy-refresh microseconds, lazy-refresh bytes, pawn capture count, capability
capture count. These single instrumented measurements include native game and
test reflection work; they characterize the complete boundary, not isolated
mod overhead. In particular, a native level gain can create a mote. Ordinary
XP timings and capture identities are the steady-state regression controls.

Inspect `Captures/gain.png` and `Captures/loss.png` before accepting the result.
Known source-save mech load/draw errors remain in Player.log and must not be
mistaken for a clean-log result or silently discarded.
