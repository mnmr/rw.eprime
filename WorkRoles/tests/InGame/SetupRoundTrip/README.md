# Portable setup verification

This test-only mod runs in independent Shared automation profiles on RimWorld
1.7. It uses production commands for fixture setup and process-scoped clicks
for the import preview and new-game defaults dialogs. It never deploys to the
player installation or edits the source save/profile.

Build WorkRoles for 1.7, then build `Probe.csproj` and the shared runtime with
`Shared/tools/automation/build-runtime.ps1`. From the repository root, run:

```powershell
& WorkRoles/tests/InGame/SetupRoundTrip/run.ps1 -Mode roundtrip
```

The command prints its evidence directory. Use that directory's generated
defaults in the independent lifecycle scenarios:

```powershell
& WorkRoles/tests/InGame/SetupRoundTrip/run.ps1 -Mode newgame -DefaultFile '<evidence>/WorkRoles.Default.xml'
& WorkRoles/tests/InGame/SetupRoundTrip/run.ps1 -Mode existing -DefaultFile '<evidence>/WorkRoles.Default.xml'
& WorkRoles/tests/InGame/SetupRoundTrip/run.ps1 -Mode newgame-sparse -DefaultFile '<evidence>/setup-sparse-default.xml'
```

The round trip compares the complete canonical export after a real preview
Apply, including groups/order, composite members, training bands, palette,
icons, options, recommendation tuning and local preferences. It also checks
legacy omission semantics, atomic dependency rejection, unchanged revisions,
missing-mod retention, live skill-column refresh, comma-containing location
names through real Scribe save/load, default write permissions and world
teardown. UI captures use the supported 1920×1080 at 1.25 scale.

The new-game scenarios intercept only the test run's autoload and invoke
vanilla `SetupForQuickTestPlay`, `PrepForMapGen`, `PreMapGenerate` and
`Game.InitNewGame`. They generate a real world and starting pawns; they never
fabricate a new-world flag on an existing store. The sparse scenario checks
that startup preserves exactly the authored five roles and leaves starting
pawns with usable work priorities. The existing-save scenario loads the
normal copied Fisso save and verifies that personal defaults do not replace it.

Each command stops its exact game, preserves assertions, XML, captures and
run manifests under `WorkRoles/temp/setup-roundtrip`, then removes its stopped
managed run. A failed assertion fails the command and retains its evidence.
