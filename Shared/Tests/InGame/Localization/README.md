# Targeted runtime checks

The default `verify-locales.ps1` matrix uses the thirteen active bundled locales:
German, French, Spanish, Italian, Brazilian Portuguese, Latin American Spanish,
Simplified Chinese, Traditional Chinese, Ukrainian, Japanese, Korean, Turkish
and Vietnamese. Historical results below also cover
translations retired on 2026-10-26; their files are preserved in
`Shared/Translations/Retired` and are no longer part of the default matrix.

These checks require an owned run created by the shared automation scripts.
Use PowerShell 7 with working process-discovery and named-pipe permissions.
The earlier low-integrity sandbox blocker was resolved by running the authorized
commands through the elevated execution boundary; process identity, token and
ownership validation remain intact.

Build each game project with `-c Release -p:RimWorldVersion=1.7`, the shared runtime with
`Shared/tools/automation/build-runtime.ps1`, and `LocalizationProbe.csproj`
before verification. The probe targets the installed RimWorld 1.7 API. Run the
verification script from the repository root and use a fresh evidence name for
each invocation. It creates, stops and removes its own managed run, retaining
logs, renderer captures and manifests under
`WorkRoles/temp/localization/<evidence-name>`. The player profile, installation
and source save are not modified.

The 1.7 build uses the installed Waylith game and Harmony 3 through
`Shared/Build/RimWorld17.props`. The default build remains RimWorld 1.6/net472.
Both versions must be rebuilt before packaging. WorkRoles loads its shared
definitions from `1.6` and selects the `1.7` assemblies through LoadFolders.xml.

`verify.ps1 -Placement before -EvidenceName <unique-name>` checks native
language loading with the community companion before the bundled mods.
Repeat with `-Placement after` in a fresh run to cover the opposite order.
The probe checks actual keyed and DefInjected output, legacy language folders,
companion help, translated seeded display names, custom names and persisted
author markers. `-Welcome` additionally exercises four loaded assembly copies,
UI-scale shrink/grow, released overflow previews, instance restoration and
process-scoped drag/Close input, preserving exact-window captures.

Additional welcome reproductions for the lifecycle/cache review:

1. Open the four welcomes, shrink the logical display until only one fits,
   then leave the game with another welcome still visible. Before the disposal
   fix, the game disposed its maps/world without closing the live welcome or
   explicitly destroying its preview. Check that all four assembly copies stop
   queue admission before any close callback, all live welcomes leave the stack,
   all previews are released, and no unseen queued welcome is marked seen.
   Load another save and confirm its own pending welcome can open normally.
2. With only QualityJobs or Implanner visible, change physical resolution and
   UI scale together so logical width and height are unchanged. Before the
   metric-observation fix, the welcome could retain its previous measurement
   stamp because no other UI in that assembly observed the preference change.
   Check that its metric revision and text geometry refresh, then remain stable
   on repeated unchanged updates.
3. Suspend a welcome, make its preview unavailable in that run's copied mod,
   then restore the original screen capacity. Before the reopen fix, the same
   tile dimensions could reuse geometry from the previous preview lifetime.
   Check that reopening builds text-only geometry, and that restoring the art
   for another suspend/reopen builds the image geometry again. Do not modify
   installed assets or the player's profile.

The close button also establishes the same Small font used by its measurement
builder and restores the incoming GUI state; a preceding window's font must
not change its text size.

Executable additional scenarios:

```powershell
& Shared/Tests/InGame/Localization/verify-welcome-scenarios.ps1 -Scenario preview -EvidenceName <unique-name>
& Shared/Tests/InGame/Localization/verify-welcome-scenarios.ps1 -Scenario metrics -EvidenceName <unique-name>
& Shared/Tests/InGame/Localization/verify-welcome-scenarios.ps1 -Scenario lifecycle -EvidenceName <unique-name>
& Shared/Tests/InGame/Localization/verify-locales.ps1 -LayoutChecks -EvidenceName <unique-name>
& Shared/Tests/InGame/Localization/verify-locales.ps1 -Locales German -LayoutChecks -Resize -EvidenceName <unique-name>
& Shared/Tests/InGame/Localization/verify-locales.ps1 -Locales German -LayoutChecks -Persistence -EvidenceName <unique-name>
& Shared/Tests/InGame/Localization/verify-rename.ps1 -EvidenceName <unique-name>
& Shared/Tests/InGame/Localization/verify-clean.ps1 -EvidenceName <unique-name>
& Shared/Tests/InGame/Localization/verify-release-layout.ps1 -EvidenceName <unique-name> -Locales French,German,Italian,Japanese,Korean,Turkish -Final
```

The release-layout probe records native font measurements, grapheme-safe name
entry, fitted readout labels, implant-caption identity, translated holder
summaries and metric-cache refreshes. It captures the real Quality Jobs bill
panel with long status text, then opens a narrow Implanner Automation tab and
checks every visible checkbox against its native wrapped text height. Turkish
also exercises the actual search consumers. Each run is independently owned,
stopped and removed; evidence is retained under
`WorkRoles/temp/release-readiness-2026-10-26/ui/<evidence-name>`.

`Fixture.cs` repairs three malformed jobs from the current Fisso source fixture
in the in-memory Scribe XML before pawn initialization. It records each pawn in
`fixture-repairs.json`; neither the source save nor the copied save is changed.
Vanilla subsequently removes eight orphaned reservations, recorded in Player.log.
These fixture diagnostics are distinct from mod failures. Visual scenarios hide
the developer log window but retain every log message.

Confirmed welcome evidence (under `WorkRoles/temp/localization`):

- `localization-before-final-20261025`, `localization-after-final-20261025`:
  community precedence and fallback with the final production assemblies.
  The before run also covers tiled four-assembly welcomes, scoped drag and
  Close; `localization-after-20261025-01` retains the opposite-order welcome
  run (the welcome implementation did not change afterward).
- `welcome-preview-20261025-02`: missing preview reopens with text-only geometry,
  restored preview reopens with image geometry, and the Close button uses Small
  while restoring incoming GUI state. `pixel-comparison.json` records zero
  changed pixels in each of the four window rectangles after the image roundtrip
  and under the incoming Medium-font test.
- `welcome-metrics-20261025-01`: QualityJobs and Implanner tested alone at
  1920×1080/1.25 and 2560×1440/1.666667 (both logical 1536×864); metric revisions
  refresh and then remain stable.
- `welcome-lifecycle-20261025-01`: one visible and three suspended welcomes,
  four additional unclaimed retries, actual world exit and save reload. All
  queues stopped before close callbacks, previews released, retries stayed
  unclaimed, and the next loaded game admitted its welcome.

Runtime regressions found during verification are retained in the German smoke
captures: fixed-width action buttons, display selectors and help-topic rows
clipped translated text. The corrected controls measure their labels behind
language/metric gates; help-topic rows wrap with cached geometry.

Help-cache checks execute during an actual window repaint to match the
production builder's text-measurement context. The Dutch regression retains
its strict identity assertion; `locale-dutch-repaint-20261025-01/Dutch` records
the successful reproduction. Temporary measurement dumps were removed after
diagnosis; the original evidence remains under the ignored `temp` directory.
`locale-cleanup-20261025-01/Dutch` confirms the same assertions pass without
that instrumentation.
The private request protocol uses immutable numbered files to avoid a Windows
file-replacement race observed in one earlier Brazilian Portuguese test.

Additional final evidence:

- `locale-final-index.json`: all fourteen requested locales passed native UI
  and help navigation, scrolling, tooltip and geometry checks. It maps each
  locale to its reports, captures and manifests. All twelve copied production
  DLLs in every locale run match the final 1.7 artifacts. Long European text
  and both Chinese glyph sets were visually reviewed; no clipping remained
  in the reviewed final captures. `locale-final-runs-after.json` confirms no
  verification games remain; only unrelated prepared management fixtures exist.
- `locale-layout-smoke-20261025-01/German`: corrected translated controls,
  wrapped help topics, navigation, scrolling, cache reuse and teardown, and
  a real drag against the Readouts minimum resize width.
- `locale-persistence-20261025-02/German`: actual save, return to the main menu,
  change from German to Traditional Chinese, and reload. Unicode custom names
  and player-authored `Food`/`Meals` remain unchanged. Seeded names carry explicit
  provenance; legacy names without that provenance remain verbatim because
  their authorship cannot safely be inferred from their spelling.
- `rename-before-20261025-03` and `rename-after-20261025-01`: the original
  rename dialog truncated an untouched long German display name on repaint;
  the fix preserves it when accepted unchanged and still caps actual edits.
- `verification-20261025`: fresh Release builds of all four mods for both 1.6
  and 1.7, the shared runtime build, all five shared/Core suites, translation
  validation, inventory and maintenance-tool tests. Each locale has all
  1,148 keyed strings, 96 definition fields and 89 help pages.
- `localization-clean-final-20261025-02`: fresh managed run with no diagnostic
  or community companion mods. `assembly-hashes.json` confirms all twelve
  deployed 1.7 DLLs match the tested artifacts. The unmodified source save still
  produces errors for its three malformed mech jobs (initialization, tick and
  vanilla pawn drawing); there were no additional localization or welcome
  errors. This is not an error-free source-save claim. The run was stopped and
  removed after its capture and logs were retained.

Runtime proof uses installed RimWorld 1.7.4985 rev1269; 1.6 was build- and
regression-tested. Translation coverage and rendering checks are not a
comprehensive human linguistic review. No Workshop availability audit is
claimed by these companion-pack tests. Locale-run logs also retain installed
base-game translation/configuration diagnostics, including the native
`Pawn_TrainingTracker` token `1_labelShort` in Dutch, Swedish and Finnish;
these are distinct from the four mods' translation bundles.

October 26 additions: Ukrainian, Japanese, Korean, Turkish and Vietnamese now
cover all 1,347 current entries per locale, including 89 help pages. The active
matrix is thirteen translations plus the English source. Evidence under
`WorkRoles/temp/localization-additions-2026-10-26` includes final checks,
both-version builds, package verification, research and `runtime-verification.json`.
The latter indexes final native 1.7 UI runs and community precedence in both
load orders, and checks all copied production DLLs and the selected locale's
content against the final artifacts. All owned runs were removed.

Screenshot review caught Vietnamese `WR_PlusOthers` wrapping in the assigned
workers row. The compact `+{0} nữa` preserves its meaning; a fresh final run
shows the complete label on one line. Japanese's initial run had one transient
test-probe request-file sharing error; its fresh final run passed without that
diagnostic. No production rendering or loader changes were needed.

Turkish's 1.7 save load also reports native description trailing whitespace and
the malformed `CannotTrainTooSmall` placeholder. `turkish-native-diagnostics.json`
traces these exact texts to the installed official archives in both 1.6 and
1.7. These are base-game text defects, separate from the clean mod catalogs and
the source save's known orphaned reservations. The earlier native coverage
audit did not exercise every formatted gameplay message. Turkish remains
included in the owner-requested language set; no native assets were modified.
