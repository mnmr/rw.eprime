# Ghoul plans (Anomaly)

In-game proof that a plan made for ghouls enlists a ghoul, lists the ghoul
kit, and that automation installs a planned ghoul implant through the
game's own doctor job. Runs on the local 1.7 copy with the Fisso save (Anomaly
and Odyssey are part of it) and no third-party mods (mod set
`implanner-ghouls`).

Build the mod, then the probe, then run:

```powershell
dotnet build -c Release src/Implanner.slnx --no-restore
dotnet build -c Release tests/InGame/Ghouls/Probe.csproj
pwsh tests/InGame/Ghouls/run.ps1
```

The probe turns a healthy colonist into a ghoul (`MutantUtility
.SetPawnAsMutantInstantly`), builds an ordinary "Everyone" plan (bionic
arm) and a "Ghoul kit" plan extending it (plating, barbs, revenant
vertebrae) with ASAP iteration, spawns a medical bed, a ghoul plating item
and bioferrite, runs one reconcile pass, then drives the game's doctor job
until the plating hediff is on the ghoul. Checks G1 to G6 are listed at the
top of `Probe.cs`; evidence (log, captures `ghoul-plans.png` and
`ghoul-overview.png`) lands under `temp/ghouls/<label>-<run-id>`.
