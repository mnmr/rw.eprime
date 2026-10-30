# Mechanical review regressions

This disposable managed-run probe exercises the actual Colonists view, priority
grid and native lifecycle. It never changes production code or player saves.
`run.ps1` accepts `-ModSourceRoot` so each run can copy a coordinated isolated
build; its `run.json` records copied assembly hashes and source paths.

Build the chosen source mods and shared automation runtime first, then this
probe against those artifacts. For a probe outside that source checkout, pass
`-p:WorkRolesModRoot=<source>/WorkRoles/mod` and
`-p:QualityJobsModRoot=<source>/QualityJobs/mod` to `dotnet build`.

```powershell
dotnet build WorkRoles/tests/InGame/MechanicalReview/Probe.csproj -c Release -p:RimWorldVersion=1.7
WorkRoles/tests/InGame/MechanicalReview/run.ps1 -Version 1.7 -Phase visual -UiScale 1.25 -ModSourceRoot '<built source>'
WorkRoles/tests/InGame/MechanicalReview/run.ps1 -Version 1.7 -Phase visual -UiScale 1 -ModSourceRoot '<built source>'
WorkRoles/tests/InGame/MechanicalReview/run.ps1 -Version 1.7 -Phase native -ModSourceRoot '<built source>'
python WorkRoles/tests/InGame/MechanicalReview/compare.py '<preserved visual run directory>'
```

The visual fixture keeps real colonists and slaves, native skills and role
assignments. Native backstory incapability supplies the skillless-hauling
suitability check. Human work priorities and ordered recommendations are checked
across the options' ON-to-OFF transition. Long names model legitimate localized
names; the pre-feature behavior may itself wrap or overflow and is deliberately
preserved by the parity assertion.

`VisualReference.cs` reproduces only the name drawing from commit `d47bdc4c`:
`ColonistsTabView.DrawColonistCell`, `DrawStatsPanel`, and
`ColonistSelectedPanelState.BuildChrome`. A test-only Harmony observer substitutes
their full-name label, inherited wrapping/font and 20px portrait-tag geometry
on reference frames. Current frames use production rendering. The enclosing
window, real pawn data, captions, portraits, surrounding controls and layout
remain identical. The reference-control scene deliberately shifts labels four
logical pixels in its current frame; `compare.py` must detect it before accepting
the real parity comparisons. This is a validity control, not a production bug.

Capture acknowledgement stores the latest stable repaint geometry. Comparisons
use exact pixel differences over the union of current/reference name bounds,
with two horizontal pixels and two line-box heights of vertical context to
include baseline wrapped overflow. Inspect full screenshots and the exported comparison
strips (current, reference, difference); a passing numeric comparison does not
replace visual review. The scale and source hashes in each run are required
evidence; a label such as `before` does not prove which source was compiled.

Native checks run separately to leave the entire active-run budget for consumed
jobs and save/load transitions. `NativeChecks.cs` and `QualityModeChecks.cs`
exercise disabled-option hooks, unmanaged death, native dispatch modes and
gravship travel persistence. With `-WithoutBiotech`, this probe replaces only
copied human gene trackers containing missing definitions with native empty
trackers and records their count. This makes the copied Biotech save a usable
no-DLC fixture while preserving native humans, skills and names. The separate
`MechanicalWorkers` presentation scenario isolates native robot rows from
unrelated missing-DLC definitions in the copied colony save.
