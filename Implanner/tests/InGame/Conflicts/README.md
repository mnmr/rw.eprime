# Conflict rules against a reporter's mods

Player report 2026-11-01 (Workshop comments): the archotech left kidney "is
the same as" the archotech liver, torso implants "replace" the archotech
skin, and Beyond Our Reach's tenebral kinds do not exclude the vanilla
ones. The named kinds come from Nighty's Archotech Bionics (ArchotechKidney,
ArchotechLiver, ArchotechSkinImplant) and Integrated Implants (subdermal
armour); FSF's synthetic skin gland and bionics are loaded as well.

Run from the repository root after building Implanner for RimWorld 1.6:

```powershell
dotnet build Implanner/tests/InGame/Conflicts/Probe.csproj -c Release -p:NuGetAudit=false
Implanner/tests/InGame/Conflicts/run.ps1                                  # implanner-conflicts
Implanner/tests/InGame/Conflicts/run.ps1 -ModSet implanner-conflicts-bor  # + Beyond Our Reach on VEF
```

Local 1.6 copy (the mods support 1.5/1.6 only). Logs land in
`Implanner/temp/conflicts/runs/<run-id>`.

The probe lists the plannable bodies with where kidneys and liver sit, the
catalog entries and slots of the named kinds, then checks
`ImplantConflicts.Conflicts` directly (K: kidney versus liver over every
kidney and liver kind; S: the archotech skin implant against every torso
kind, only ArmorGland kinds may exclude it; T: tenebral template kinds in
the catalog and excluding their vanilla counterparts on the same part) and
the plan command the picker issues (P: picking one keeps or replaces the
other as the rules say).

Verified 2026-11-01 on 1.2.6 (two runs, every check passed): kidney and
liver coexist for every kind, only the five ArmorGland skin glands exclude
the archotech skin implant (subdermal armour coexists), all twelve tenebral
template kinds are catalogued and exclude the vanilla kinds on the same
part. Only the human body was plannable in these runs, so the report's
race-body situation is not reproduced here; Beyond Our Reach's Royalty
implant variants (tenebral glands) were absent in the run despite the
mod's default setting, so T2 was skipped.
