# Robotic preparation and finishing verification

The test-only probe runs in a fresh owned automation run. It uses Misc. Robots'
`CreateRobot` factory and vanilla pawn generation plus a mechanitor relationship
for Fabricor/Constructoid fixtures. Bills come from `BillUtility.MakeNewBill`;
frames come from vanilla blueprint replacement. No production or quality-roll
method is replaced. Fast crafting reduces the fixture duration while real game
toils still consume ingredients, award XP, and produce the finished item.

Build Quality Jobs and the shared automation runtime for each version first,
then build this probe for `-p:RimWorldVersion=1.6` and `1.7`. Run from repository
root, for example:

```powershell
dotnet build QualityJobs/tests/InGame/RobotWorkers/Probe.csproj -c Release -p:RimWorldVersion=1.7
& QualityJobs/tests/InGame/RobotWorkers/run.ps1 -Version 1.7 -Scenario misc -Label misc-high
& QualityJobs/tests/InGame/RobotWorkers/run.ps1 -Version 1.7 -Scenario mech -NormalPriority -Label mech-normal
& QualityJobs/tests/InGame/RobotWorkers/run.ps1 -Version 1.6 -Scenario misc -NormalPriority -Label misc-normal
& QualityJobs/tests/InGame/RobotWorkers/run.ps1 -Version 1.6 -Scenario mech -Label mech-high
```

`-EdgesOnly` runs the shorter compatibility regression batch. `-Clean` excludes
the probe while retaining both robot mods for the final clean-load check.
Every invocation saves its manifests, log, assertions, and captures under
`QualityJobs/temp/robot-workers`, stops its exact owned game, then removes the run.

The fixture proves colonist Crafting/Construction XP, actual unfinished-item
and frame handover, native worker scheduling, final quality products/buildings,
and ON/OFF new/resumed/current robotic preparation. Normal priority keeps the
native work list and scheduler; unrelated work in the copied colony is made
unavailable using vanilla forbid flags and an empty old home area before test fixtures are created. Preexisting mechs are transferred to world ownership to keep them from consuming the test frames.

Misc scenarios also cover docked/packed robots, paused ownership changes,
temporarily berserk mechs, current-best display refresh after a worker dies
while paused, source-bill management through the API, shared-item
provenance, full ON/OFF save/reload, and unchanged bill-scan pruning. Final
captures show the actual Options window and tooltip; a scoped input click
must change the per-save preparation setting.

The initial 1.7 red baseline is retained in
`QualityJobs/temp/robot-workers/1.7-red-baseline-1d251c96b0e34ef0976160d957a0b368`.
Its real skill-20 Omni robot was omitted from both finisher selectors, and the
preparation option was absent. Later reviewer and packed-station red runs are
retained alongside the green evidence.
