# Options and Help verification

`Probe.cs` is a separate, test-only mod. It observes real toolbar, tab, topic,
checkbox and close input sent through the shared run's scoped automation pipe.
It never ships in Quality Jobs. Each script creates, stops and removes its own
single-use run, preserving logs, manifests and captures under `QualityJobs/temp/help`.

## Running

From the repository root, build Quality Jobs and the shared automation runtime
for both versions before creating a run. Then build this probe:

```powershell
dotnet build QualityJobs/tests/InGame/Help/Probe.csproj -c Release -p:RimWorldVersion=1.6
dotnet build QualityJobs/tests/InGame/Help/Probe.csproj -c Release -p:RimWorldVersion=1.7
pwsh QualityJobs/tests/InGame/Help/run.ps1 -Version 1.6
pwsh QualityJobs/tests/InGame/Help/run.ps1 -Version 1.7
pwsh QualityJobs/tests/InGame/Help/run.ps1 -Version 1.7 -Locale German -Label german
pwsh QualityJobs/tests/InGame/Help/run.ps1 -Version 1.7 -Screenshots -Label screenshots
pwsh QualityJobs/tests/InGame/Help/clean.ps1 -Version 1.6
pwsh QualityJobs/tests/InGame/Help/clean.ps1 -Version 1.7
```

The 1.6 runner uses the existing Linux installation under
`Readouts/temp/linux-validation/game-1.6`, Ubuntu 22.04 and the shared software
renderer. The 1.7 runner uses the installed Windows game. Both operate on
independent copied saves and mod assemblies. The clean runner omits the probe
and verifies the copied Quality Jobs assembly's SHA-256 against the final build.

The probe asserts actual 1920x1080 rendering at UI scale 1.0. Waylith can quantize
the saved 1.0 preference to 0.9375 during startup; the probe sets 1.0 once after
loading, waits for the UI to settle, then checks the actual value before capture.
The clean runner leaves the game's startup scaling behavior untouched.

## Behavioral coverage

- Opening and navigating Help preserves the colony settings snapshot. The real
  sharing checkbox publishes a changed snapshot while paused; a second click
  restores the original setting. A no-op command preserves snapshot identity.
- All twelve topics build, every screenshot loads, and topic links resolve.
  Localization block markers and unresolved keyed-label tokens must not render.
  Native language selection is asserted before checking localized pages.
- Repeated Help reads reuse the draw model; changing wrapping width rebuilds it.
  Twenty settled Help passes call neither `Text.CalcSize` nor `Text.CalcHeight`.
  These targeted instrumentation checks are not FPS or timing benchmarks.
- Actual chapter/topic clicks select the requested topic. Returning to Options,
  closing with Escape and reopening work. Closing releases textures and tab
  delegates and persists personal reading history. Internal resource inspection
  is necessary because a closed window cannot visibly prove resource release.
- Screenshot fixtures use `BillUtility.MakeNewBill` and the game's blueprint
  factory. They open the actual bill and construction dialogs via normal UI.
  No bill, job, construction or finisher mechanic was changed by this feature.

## Recorded verification, 2026-10-28

Evidence directories below are relative to `QualityJobs/temp/help`.

| Boundary | Evidence and result |
| --- | --- |
| Missing feature, before implementation | `1.7-baseline-d5c1921458984d84b378e9547cc15c7d`: expected failure because the toolbar opened the legacy settings window. |
| Teardown regression, before fix | `1.7-verify-66b3e91943a34ffba93ff24f5b855f80`: failed tab-delegate release. Waylith's queued final GUI pass recreated the tabs after `PostClose`; the closed-window guard fixes this. |
| Final English, 1.6 | `1.6-final-labels-2bf014092ebe4fc19358a85bef6ccda4`: all scenario assertions passed, including release and reopening; successful script exit and run removal. |
| Final German, 1.7 | `1.7-german-labels-ea3a099e21534f64a93f43fb0abf318a`: all scenario steps passed, including real topic selection and reopening; successful script exit and run removal. |
| English lifecycle, 1.7 | `1.7-lifecycle-c21489ca89d44208aab4066275c00d35`: complete assertions in the log, including closed-window teardown and reopening. |
| Chinese layout, 1.7 | `1.7-chinese-fa1294e6b5cd440eb1cbb4488dcfc0b2`: native Simplified Chinese asserted; topics, navigation, settings and teardown passed; captures visually reviewed. |
| Unchanged Options rendering | Final English 1.6 and German 1.7: exact window region `(370,160)-(1550,920)` is pixel-identical before and after visiting Help. See each `options-pixel-comparison.txt`. |
| Clean release load, 1.6 | `1.6-clean-8ed1640e20c743fc8951719b4f6c0f4c`: no probe; matching final assembly hash; Options and Help captures. |
| Clean release load, 1.7 | `1.7-clean-8d4af780d7854cae844359c300de1118`: no probe; matching final assembly hash; Options and Help captures. |

The copied player saves reference mods excluded by the isolated test set, so
their logs contain missing-mod load diagnostics. The 1.7 save also has repeated
vanilla `Mech_Lifter` drawing errors, already present in the pre-change baseline.
These can exhaust the game's log limit; the German run's final `help-stage.txt`
and successful script completion establish that later assertions also passed.
No Options/Help exception remained after the teardown fix. The source saves,
player preferences and installed mods were not modified.

All four mod solutions built for 1.6 and 1.7 with zero warnings and errors.
Quality Jobs, Work Roles, Readouts, Implanner, shared Common and shared Automation
executable suites passed, as did the localization tooling suite.
`scripts/check-localization.cmd` reported zero errors and review signals in all
13 locales; `scripts/update-localization.cmd --dry-run` reported no pending work.
The build and localization outputs are retained beside the run evidence.

## Published screenshots

The source run is `1.7-screenshots3-d4d72bc77f9540e290fc900fd5c0aeda`.
Its log asserts 1920x1080 and UI scale 1.0. `crop.py <evidence-directory>` extracts
the six actual UI regions into `mod/Help/Images` without resizing and records
their source filenames and crop bounds in `help-crops.json`. The original
full-resolution captures remain in that run's evidence directory.

Help captions and prose are localized; images use the shared English fallback.
Static control names use `{k:QJ_...}` references, so the text follows the game's
actual labels, including community translation overrides. Stable localization
block IDs were retained throughout review and terminology corrections.
