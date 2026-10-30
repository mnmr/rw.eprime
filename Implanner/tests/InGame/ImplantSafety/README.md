# Implant safety regression

Run from the repository root after building Implanner and the shared automation
runtime for RimWorld 1.6:

```powershell
dotnet build Implanner/tests/InGame/ImplantSafety/Probe.csproj -c Release -p:NuGetAudit=false
Implanner/tests/InGame/ImplantSafety/run.ps1 -Scenario main
Implanner/tests/InGame/ImplantSafety/run.ps1 -Scenario settings
```

Each invocation creates, stops and removes its own managed run and preserves
logs and build manifests in `Implanner/temp/implant-safety/<run-id>`. The two
scenarios keep native doctor jobs within the shared 60-second run cap.

- `main`: tended healing wounds, mixed scars and wounds, permanent damage and
  missing-limb controls; nonmonotonic QBR multipliers; upgrade stock loss;
  healing after native surgery work has begun; and a safe implant actually
  consumed and installed by a doctor at 125% efficiency.
- `settings`: a safe owned operation begins normally, then its actual item's
  QBR multiplier becomes inferior. The native finish boundary must preserve
  both the natural arm and the implant ingredient.

The probe uses real medical bills, vanilla ingredient selection and native
doctor jobs. It never invokes `ApplyOnPawn` directly. The patient uses the
native bed-rest toil without autonomous job overrides because the drafted
test doctor is excluded from vanilla's available-doctor check. Surgery work
and success settings remove unrelated waiting and random failure. The
stock-loss case models the interval between removal and reinstallation, then
asserts the ordinary reconciler's published reservation and bill outcomes.

`-ExpectFailure` checks a pre-fix `main` run for IR1, IR2 and IR3 failures and
rejects fixture errors. The source save remains read-only. Its reduced mod
baseline can emit unrelated missing-content errors while loading; assertions
run after fixture setup removes unrelated saved pawns.

Verified on 2026-10-30 with the production 1.6 assemblies:

- `39b32bd13b3343a289cb23c5eb8bc458`: main scenario passed, including native
  rejection with the item preserved and safe installation at 125%.
- `edbc6eb4113546059232ab5d2678815c`: settings scenario passed.
- Pre-fix evidence: `f34fba86e3a84448b7257c1e8c4c7973` reproduced IR1–IR3,
  including actual unsafe surgery completion; its later safe control timed
  out. `ba10d76a0340464486b271c2e7ee7fe2` exposed the upgrade stock-loss gap
  before that cleanup was corrected.

The final copied production DLL hashes match the built artifacts. Both 1.6
and 1.7 compile cleanly; these native surgery runs cover 1.6 with QBR.
