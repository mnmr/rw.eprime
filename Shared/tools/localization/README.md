# Translation maintenance

Run from the repository root with Python 3. The checker needs no packages or
network access.

```powershell
python Shared/tools/localization/translations.py check
python Shared/tools/localization/translations.py inventory
python -m unittest discover -s Shared/tools/localization/tests -v
```

`check` checks every supported locale across the four mods. It reports missing,
empty, duplicate and obsolete keys; malformed XML; changed format placeholders,
rich-text tags and paragraph escapes; changed help links, image paths and demo
identifiers; and English sources changed since the last translation snapshot.
Identical English text is reported as a review signal, because names and cognates
can legitimately be identical. Structural success does not certify fluency.

Use `--locale Danish` (repeatable) for a smaller batch, `--no-manifest` during
initial drafting, and `--json temp/translations.json` for all diagnostics.

English UI text remains in each mod's `mod/Languages/English/Keyed` tree.
Bundled non-English XML lives under `mod/FallbackLanguages/<locale>/Keyed` and
`DefInjected`; normal community translation mods continue using RimWorld's
`Languages/<locale>` layout. The shared loader fills missing entries after
normal mod translations have loaded, so community translations win in either
mod order. A partial community pack leaves bundled entries available as fallback.

Help follows `mod/Help/<locale>` with the same topic filenames as English.
Translate titles, prose and visible link labels; retain filenames, link targets,
`@demo` IDs, code identifiers, placeholders and markup. Images may fall back to
English assets. Companion help packs can use
`Help/<target-package-id>/<locale>/<chapter>/<topic>.md` to override individual
topics. Images resolve from the companion's locale `Images`, its shared
`Images`, the target mod's locale `Images`, its English `Images`, then its shared
`Help/Images` folder. Missing companion topics retain the bundled help.

The in-game regression runner is documented in
`Shared/Tests/InGame/Localization/README.md`; it uses the shared automation
framework to check community load order and welcome-window behavior.

After translating a changed source, explicitly update the source snapshot:

```powershell
python Shared/tools/localization/translations.py snapshot --locale Danish
```

Snapshots under `Shared/Translations/Manifests` record the English text hashes
that each locale was translated against. The command refuses incomplete or
structurally broken content; it does not imply human review. Do not refresh
snapshots merely to silence stale-source diagnostics.

Canonical game locale names are Danish, German, French, Spanish, Italian,
PortugueseBrazilian, Dutch, Swedish, Finnish, Norwegian (Bokmål), Portuguese,
SpanishLatin, ChineseSimplified and ChineseTraditional. Regional variants have
separate content. Follow the game's selected language; there is no mod picker.

Owner-reviewed terminology: Danish `bonde` / `bønder`, German `Bauer` / `Bauern`
for pawn; Danish `Færdiggørelseskrav: {0}`, German `Fertigstellen ab Stufe {0}`
for the finisher skill label. Keep `colonist` distinct, preserve mod names and
player-authored names, and distinguish implants held back for manual use from
implants allocated by automation. The pilot is preserved as review history.
