# Mechanical worker verification

Run commands from the repository root. Build WorkRoles, this probe, and the
shared automation runtime for the chosen game version before creating a run.
The runner creates its own managed run, copies the built files, then stops and
removes that run after preserving evidence under `WorkRoles/temp/mechanical-workers`.

```powershell
dotnet build WorkRoles/src/WorkRoles.slnx -c Release -p:RimWorldVersion=1.7 -m:1
dotnet build WorkRoles/tests/InGame/MechanicalWorkers/Probe.csproj -c Release -p:RimWorldVersion=1.7 -m:1
Shared/tools/automation/build-runtime.ps1
WorkRoles/tests/InGame/MechanicalWorkers/run.ps1 -Version 1.7 -Phase presentation -Label presentation
WorkRoles/tests/InGame/MechanicalWorkers/run.ps1 -Version 1.7 -Phase presentation -WithoutBiotech -Label no-biotech
```

For 1.6, build both projects with `-p:RimWorldVersion=1.6` and build the runtime
with `-GameRoot D:\Code\RimWorld\Readouts\temp\linux-validation\game-1.6`.
Pass `-Version 1.6` to the runner. It uses the existing WSL game fixture.

## Presentation regression

The scenario creates a native Misc. Robots cleaner and a Misc. Robots++ omni
robot. With Biotech enabled it also creates a cleansweeper and constructoid.
It assigns cleaning, farming, and a mixed cleaning/farming role through actual
WorkRoles commands, then opens the actual Colonists view at 1920x1080, UI scale
1.25. Native cleaner names are preserved; the omni gets a deliberately long
name, then is renamed while the simulation remains paused.

The assertions and captures verify:

- Impossible roles have the two-red-star verdict. Assigned impossible work has
  the critical lightning marker; partly doable work has the caution marker;
  fully doable work has neither. Warning explanations are present, and manual
  assignments never admit unsupported work into the compiled job order.
- Stats show levels only for skills used by native supported work.
  Cleaners have no usable skill entries, the omni retains its native levels,
  and the constructoid exposes its native fixed construction level. Robots do
  not display fractional XP progress or human passion indicators.
- Roster names fit one measured line with no grouping and with race grouping.
  The selected-card name fits its tag; hovering it shows the complete name.
  A paused rename refreshes both names immediately, preserves the stats
  snapshot, and subsequent unchanged row reads reuse the same snapshot.

Before the fix, native and long names exceeded their rectangles, mechs without
skill trackers had no skill rows, and specialist robots exposed generic skills.
The no-Biotech fixture is significant: Biotech's native disabled-work handling
otherwise masks much of the missing robot capability evidence.

Success requires `mechanical-stage.txt` to contain `SUCCESS` and every entry in
`mechanical-checks.txt` to pass. Inspect the named screenshots as well. The
no-Biotech fixture removes a DLC used by the copied source save, so unrelated
missing-definition load errors are expected; the probe restricts its UI cohort
to the native test robots and records assertions separately from the game log.
