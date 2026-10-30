# Recommendation protection regression probe

Build WorkRoles and the shared automation runtime before preparing each fresh
managed run. Compile this probe against the completed build:

```powershell
dotnet build WorkRoles/tests/InGame/RecommendationProtection/Probe.csproj -c Release -p:RimWorldVersion=1.7
```

Run the complete owned-run lifecycle with
`pwsh WorkRoles/tests/InGame/RecommendationProtection/run.ps1 -Label fixed`.
It creates a fresh run, polls for at most 50 seconds after launch, captures the
result, stops the exact run, copies evidence, and removes the run.

Create a run with purpose `WorkRoles WR2-WR5 recommendation protections`,
`-ModSet workroles-recommendation-protection`, and
`-AdditionalModSource D:\Code\RimWorld\WorkRoles\tests\InGame\RecommendationProtection\Probe`.
Launch only that run with its explicit `-RunId`. The probe runs once after the
real saved colony loads, using RoleCommands and the real AutoOptimizer; it
does not write a save. Inspect `recommendation-protection-results.txt` inside
the run. Success requires `COMPLETE WR2 WR3 WR4 WR5` and no `FAIL` entry.
Stop the exact run before analyzing results or building again.

The assertions cover an ordinary Core/Basics composite as a positive control,
disabled/blocker/conditional/excluded composites, preservation of an existing
conditional/blocker/pinned composite, disabled and forced-on source assignment
states, an existing composite's own disabled/forced-on assignment with and
without separately enabled member assignments, competing equal/superset/partial
member windows and overlapping held overrides, a removed partial owner whose
unneeded member drops while its still-wanted active member remains (including
global-off/ForceOn and recommendation exclusions), retained/projected work on
both sides of surviving blockers and excluded pinned blocker/rule roles through
final Apply, unpinned normal placement, and a paused actual
UI preview/Apply toggle through transitive composite ownership. It also covers
a pinned Hunter placed before a conditional role, the native Niklas
disabled Cook case, and a real nonzero one-hour Cook shift outside the current
hour. Meal cooking must remain executable with and without excluding the shift.
Original assignments, temporary roles, Cook rules, and auto-optimization are
restored in `finally`.
