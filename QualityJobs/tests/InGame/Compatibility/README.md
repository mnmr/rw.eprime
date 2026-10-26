# Compatibility verification

Build Quality Jobs, the shared automation runtime and `Probe.csproj` for the
target game version, then run `run.ps1 -Version 1.7 -Warnings` or
`run.ps1 -Version 1.6`. Each invocation creates, captures, stops and removes its
own isolated run. Evidence remains in `QualityJobs/temp/compatibility`.

The probe supplies a foreign `Dialog_BillConfig` subclass from an earlier
factory prefix. It checks that the returned instance and open/draw/close
callbacks survive, clicks both the vanilla repeat control and the QJ checkbox,
and checks repeated resolution notifications. Captures cover steady drawing,
hover/leave, the conflict notice and the no-conflict welcome. The script fails
on any failed assertion.

The 1.7 before capture (`1.7-before-0513120967144704a84f702426a8a266`)
recorded the lost foreign window and callbacks. Compare the exact bill-window
crop `(285,125)-(1635,955)` at 1920x1080 / UI scale 1.25, using RGB pixels.
The same standard window after attachment is pixel-identical. The foreign
window adds only its test caption; wait for its draw callbacks before capture.

`-Warnings` loads the installed QualityBuilder and two empty package-ID fixtures
for No Job Authors and Finish it!. These fixtures verify detection and shared
presentation, not the third-party implementations. Without this switch, the
probe verifies that an empty match list produces no warning text or panel.
Acknowledged conflicts must not reopen the welcome. Pure shared tests exercise
new conflicts on an acknowledged save, independent saves, case-insensitive IDs,
duplicate active entries, unrelated mods and an empty catalog.

For content-driven welcome height, use `-Warnings -WelcomeOnly` at the default
1920x1080, then add `-TiledWelcomes` to open the other three real welcome hosts.
The probe verifies the first case fits without scrolling and the tiled case
retains the warning at the top with scrolling, checks screen bounds and repeated
resolution notifications, and captures `warning.png`. `-Width 1280 -Height 720`
also exercises the game's automatic UI-scale reduction; that reduction leaves
enough logical height for the single welcome to fit without scrolling.
The shared layout regression was observed failing at 520px when 720px was
requested, then passing after expansion within independent vertical row bands.

## Conflict evidence

The catalog records active packages only; disabled subscriptions cannot patch
the game. These are warning conditions, not automatic configuration changes.

- `hatti.qualitybuilder`: QualityBuilder controls builder selection and
  quality-driven rebuilding, overlapping QJ's construction management.
  [Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3512466087)
  and [source](https://github.com/pkp24/QualityBuilder/tree/c063417d46b2252d15851bdd02a6712a5a971d0d).
- `doug.nojobauthors`: with `forceFinishUnfinishedFirst` or
  `enableAchtungCompat`, its custom matching can select a QJ-paused unfinished
  item through an unmanaged same-recipe bill. QJ's completion gate does not
  gate unmanaged bills. This is settings-dependent, as the warning states.
  [Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2009825774)
  and [source](https://github.com/Jellypowered/no-job-authors/blob/43868ab0a5cdc128ac8d77759f989ff7086f168c/Source/Main.cs).
- `xandrmoro.rim.finishit`: its prioritization action dereferences `BoundBill`
  in the null/error path. QJ detaches pooled or quality-paused items from their
  bill, including paused items when sharing is disabled.
  [Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3089282379)
  and [source](https://github.com/xandrmoro/RIM.FinishIt/blob/982d0c0225d739be5ed1c322e4ea1e217790000d/Source/UnfinishedThing_GetGizmos.cs).

ManyCats Toolbox advertises takeover but lacked verifiable implementation
evidence during this investigation; it is excluded from the warning catalog.

## Shared welcome API

Each host supplies translated `WelcomeModConflict` descriptions to
`WelcomeDialog.FindConflicts`, shares the returned immutable notice with the
`ClaimSave` overload and `ConflictingMods` property. Heading and removal advice
default to shared translation keys; an optional `ConflictAdvice` override or
`AdditionalConflictAdvice` supplies genuinely mod-specific guidance. Persist the caller-owned shown-save and
conflict-acknowledgment lists after a successful claim. Hosts without a catalog
can continue using the original API unchanged.
