# Mechanical exclusion regression probe

After building WorkRoles and the shared automation runtime, compile:

```powershell
dotnet build WorkRoles/tests/InGame/MechanicalExclusions/Probe.csproj -c Release -p:RimWorldVersion=1.7
```

Use `pwsh WorkRoles/tests/InGame/MechanicalExclusions/run.ps1 -Label fixed`
for the complete create/launch/poll/capture/stop/copy/remove lifecycle. Polling
is bounded to 50 seconds after launch; the script acts only on its own run.

Create an independent managed run with purpose `WorkRoles WR11 mechanical
exclusion preview and apply`, `-ModSet workroles-mechanical-exclusions`, and
`-AdditionalModSource D:\Code\RimWorld\WorkRoles\tests\InGame\MechanicalExclusions\Probe`.
Launch with the exact `-RunId`. Inspect `mechanical-exclusion-results.txt` and
`mechanical-exclusion-stage.txt` in the run. Require `COMPLETE WR11`, `READY`,
and no `ERROR`. Stop the exact run before analysis or another build.

The probe uses a real native Constructoid (generating one through the game's
factory if necessary). It creates real construction duties through commands,
then invokes the actual selected-worker Fix My Colony preview and Apply path.
Excluded unpinned nonprimary conditional and blocker assignments must disappear,
the pinned excluded control and accepted primary remain, and the formerly
blocked native giver must compile again. No save is written; the run is single
use and is discarded after verification.
