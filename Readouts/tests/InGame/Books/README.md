# Container storage counts

Books on bookcases and apparel on outfit stands are held inside the building,
not spawned on the map. The probe loads the saved colony, tallies what every
non-slot-group haul source holds, and checks that Readouts publishes held plus
spawned stock per def. It then forbids one shelved book (unforbidden count
drops by one) and drops it on the floor (total unchanged).

Build Readouts and the shared 1.7 automation runtime, then `Probe.csproj`:

```powershell
pwsh Readouts/tests/InGame/Books/run.ps1
```

Evidence is kept in `Readouts/temp/books/<run-id>`.

- Before the fix, 2026-11-15, run `179f0d2f0d3b4d569e6d45f7355913d3`: 7 textbooks
  and 4 novels on bookcases published as 0.
- After the fix, 2026-11-15, run `268d9c50abee4326a7852b684230fa21`: all checks pass.

Runtime verification covers 1.7; 1.6 was build-verified.
