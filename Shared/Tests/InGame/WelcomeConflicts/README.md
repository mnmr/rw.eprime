# Welcome conflict integration regression

Build all four mods and the shared runtime, then build `Probe.csproj` and run
`run.ps1 -Label after`. This uses an independent managed 1.7 run and writes
evidence under `QualityJobs/temp/welcome-conflicts`. It never changes the
player's profile or installed Workshop content.

The fixture packages contain only names and IDs. They are namespaced during
startup to avoid substituting installed Workshop packages. The probe then
exposes their player-facing package IDs at the real welcome admission boundary.
These checks prove warning detection and presentation, not third-party gameplay
compatibility. The evidence for each catalog is in its owning mod's compatibility
documentation and source.

Each real QueueWelcome entry point must reopen an already-welcomed save when a
new conflict is present, display its complete catalog with the shared heading,
stay dismissed after acknowledgement, and omit the warning panel when all
fixture packages are inactive. Captures show each real welcome. The Quality Jobs
case also checks migration onto the shared text defaults.

`-Locale German` checks a translated shared catalog. `-Locale Japanese
-CommunityFallback` checks that an unsupported locale uses a community English
override through the game's ordinary fallback. The probe checks the actual
selected language, rather than assuming a preference edit took effect.

The pre-fix run `before-fixed-probe-97556b28ad62447189e86f82a32ddb07` failed all
three newly integrated mods because existing saves received no warning. The
initial integrated run `after-a2d0eba9211d4ca18e8da1222f799849` passed detection,
acknowledgement and absent-panel checks for WorkRoles, Readouts and Implanner.

Final verification (2026-10-25):

- `fallback-before-native-folder-3a834bfa33044ab6ba9439e6d5986bdb` reproduced the
  community-English fallback failure for all four hosts. The selected language
  was asserted to be Japanese; built-in English incorrectly shadowed the
  community heading.
- `fallback-after-c84b8fe9dec44cdc8b008076ebfb9c51` passed the same scenario for
  all four hosts after restricting embedded English to the default language.
- `translated-after-e9fb704feaf247c49948540cb56c8141` passed all four hosts in
  German; captures were inspected for complete warning text and usable controls.
- Clean production run `1.7-all-hosts-clean-e3dc76169bc04524b62c9c9346346adc`
  (under `QualityJobs/temp/compatibility`) matched all four final assembly
  hashes and logged no welcome or localization errors. The copied source save
  still has unrelated missing-job/mech errors when unrelated mods are removed.
- Both 1.6 and 1.7 builds had no warnings/errors. All six .NET behavioral suites,
  Python localization regressions, and structural/revision checks for fourteen
  locales passed. The updater reports no pending translations.

All managed runs were stopped and removed after preserving evidence.
