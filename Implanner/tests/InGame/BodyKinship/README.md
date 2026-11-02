# Body kinship regression

Owner decision 2026-11-01: a race whose implant slots are exactly the human
ones is the human body for planning purposes; a race carrying every human
slot and more joins plans for the human body and is still offered as a body
of its own (its extra slots can be planned); a race missing human slots is a
body of its own.

Run from the repository root after building Implanner for RimWorld 1.6:

```powershell
dotnet build Implanner/tests/InGame/BodyKinship/Probe.csproj -c Release -p:NuGetAudit=false
Implanner/tests/InGame/BodyKinship/run.ps1
```

The `implanner-body-kinship` mod set loads Humanoid Alien Races with Milira
(human slots plus two wings), Android Tiers Continued (distinct anatomy) and
the probe itself, which ships `Implanner_HumanCopy`: the human BodyDef under
another name with a race and a colonist kind of it (`Probe/Defs/Races.xml`,
generated from Core's `Bodies_Humanlike.xml`). Logs and the capture land in
`Implanner/temp/body-kinship/<run-id>`.

Checks: kinship per body (K1), the offered-body list folding the copy (K2),
`PlanBodies.Accepts` for plans pinned to Human, Milira, the folded copy and
the android (A1 to A4), the synced `AssignPlan` enlisting the copy and Milira
in a human-body plan and refusing the android there and a human in the Milira
plan (C1, C2), the evaluation of the enlisted Milira colonist counting the
human plan's bionic arm (E1), and the picker's body filter listing the
offered bodies only (U1, `plans-body-filter.png`).

Verified 2026-11-01: every check passed; the filter read "All bodies | Human
| Android | Mech | Milian | Milira" with the human copy absent.
