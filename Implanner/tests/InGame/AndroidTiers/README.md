# Android Tiers Continued regression

Run from the repository root after building Implanner and the shared automation
runtime for RimWorld 1.6:

```powershell
dotnet build Implanner/tests/InGame/AndroidTiers/Probe.csproj -c Release -p:NuGetAudit=false
Implanner/tests/InGame/AndroidTiers/run.ps1
```

The run uses the `implanner-android-tiers` mod set (Humanoid Alien Races,
Enable Oversized Weapons, Android Tiers Continued; WorkRoles removed so work
priorities stay as set) and preserves logs and captures in
`Implanner/temp/android-tiers/<run-id>`.

- AT1 sides: the advanced light sensor has a labelled left and right slot,
  planning the RIGHT one reserves the item and schedules the operation on the
  android's right sight sensor, and a native doctor job installs it there.
  The VX0 chip lists the brain and the artificial brain; same-side sensors
  and same-shoulder arms conflict, opposite sides do not; old slot ordinals
  migrate to where they acted.
- AT2 workers: Android Tiers mind implants are ordinary implants, not
  modules needing an artificial host.
- AT3 floors: android operations check Crafting. With doctors operating, the
  bill asks for the doctors' best Crafting (8), not the doctor's Medicine (20).
  Implanner follows Android Tiers' "androids can only be healed by crafters":
  a mismatched option is corrected on the first update after a load, and
  switching the setting on then writing Android Tiers' settings (as its
  settings window does on close) turns the option on. The floor is then the
  best crafter with "repair androids" (12), the doctor gets no job, and a
  native crafter job installs the left sensor.

Captures: `picker.png` (side rows and the override caption),
`options-doctors.png` and `options-crafters.png` (the read-only "Android
operations" line). The probe holds each frame 2.5 s for the script's
capture, with time running so the stall guard sees progress.

Verified on 2026-10-31 with the production 1.6 assemblies:
`be2adda69c8d48ce88f3ba9e51340402` passed every check (680 game ticks).
The bug itself was reproduced offline against Android Tiers' defs and
decompiled workers before the fix (human-only slot enumeration; ordinal 0
resolving to the left sensor on android bodies).
