# Mechanical projection cache regression

Run against a fresh managed 1.7 copy of `Fisso-NAM-1.7.rws`:

```powershell
dotnet build WorkRoles/src/WorkRoles.slnx -c Release --no-restore
dotnet build WorkRoles/tests/InGame/MechanicalCache/Probe.csproj -c Release --no-restore
pwsh WorkRoles/tests/InGame/MechanicalCache/run.ps1 -Label verification
```

The disposable scenario generates two native player lifters and opens the real
Colonists window. Test-only Harmony counters instrument the mechanical producer
(`CaptureFromSnapshot`, or `Capture` on the baseline) and the shared external
fact producer independently. This is necessary because equal final snapshots
otherwise conceal unnecessary rebuilds.

Checks cover human skill/assignment changes, a single worker assignment change,
no-op edits, shared external facts, support off/on, missing-map/return and window
close. The lifters' native hauling capabilities are checked after re-admission;
hauling-only mechs are not assumed to have work skills. Production is never
patched or instrumented by the test assembly outside the disposable run.

Use `-ExpectRed` only for the pre-fix build. Evidence is retained under
`WorkRoles/temp/mechanical-cache/<label>-<run-id>`; each owned run is stopped and
removed. No third-party provider is exercised by this fixture.
