# Translation maintenance

## Before publishing

Use Python 3.10 or newer on `PATH` (tested with Python 3.12). The check helper
needs no packages or network access. Run these from the repository root:

```powershell
scripts\check-localization.cmd
scripts\update-localization.cmd --dry-run
scripts\update-localization.cmd
scripts\check-localization.cmd
```

`check-localization.cmd` checks all thirteen active locales across all four mods and the
shared catalog in `Shared/Translations`. Shared keys are maintained there once
and embedded into each mod by `Translations.props`; no extra mod is required.
Community translations take precedence over embedded fallbacks. Exit
code 0 means the structural and source-revision checks passed; exit code 1 means
there are errors to fix before publishing. Unchanged-English warnings do not
fail the check. Both helpers resolve their tools relative to their own location,
so invoking them by absolute path also works from another directory.

Confirmed mod-name titles are exempt from unchanged-English warnings through
`BRAND_TITLES` in `translations.py`. Each exemption matches both the exact key
and its English text; it does not exempt surrounding prose or missing/invalid
translations. A title whose wording changes is reviewed again. These review
warnings alone do not trigger automatic retranslation.
Reviewed phrases that are identical in one language, such as French
“Classification”, use `UNCHANGED_TRANSLATIONS` with an additional exact locale
match. The exemption never applies to that phrase in other languages.

`update-localization.cmd` uses the **Claude Code CLI** (`claude`) from `PATH`,
in print mode with the Sonnet model by default (`--model` selects another alias
or id); use `--claude "C:\path\to\claude.cmd"` to select another executable
explicitly. Each request runs in an empty temporary directory with all tools,
settings sources, skills and session persistence disabled, and asks for
schema-validated JSON, so Claude never reads or writes the repository.
Sign in with `claude auth login` if needed; `claude auth status` checks the
login. Translation uses that account's normal usage. `--dry-run` lists affected
entries without calling Claude or changing content. An already-current
repository also makes no Claude calls and writes no files.

The updater translates missing entries, changed English text and structurally
broken translations in small batches. It removes retired XML entries and help
topics, preserves unaffected translations, and passes the existing translation
plus [translation guidance](translation-guidance.md) to Claude for terminology.
The guidance file is the maintained source for owner terminology and regional
language rules. Update it when terminology changes; existing valid translations
still need a deliberate edit when adopting a new terminology decision.

Help uses stable block IDs: `<!-- l10n:b-0123456789ab -->` immediately before
each paragraph, heading, image caption, directive or related list. The title
has the reserved ID `title`. Keep the comment attached when moving or editing
content within a topic; never regenerate existing IDs. New blocks need a new
unique ID (a descriptive ID such as `import-options` also works). A block must
contain one paragraph or one related list, not an entire section. Keep related
list items together for grammatical context. The game ignores these comments
while loading help; they add no rendering or tick work.

The checker hashes each block independently. English soft wrapping does not
change a hash. Section headings are context dependencies for their contents;
the preceding paragraph (allowing an intervening image) is a dependency for a
list. A changed heading or list introduction therefore requests review of its
dependent blocks too. Reports use `Mod/Help/chapter/page.md#block-id` and the
updater separates UI strings, affected help blocks and affected pages.

The translator receives the full English/translated page and previous English
block text for context, but can return only the requested blocks. The updater
patches those blocks, retaining every untouched block's exact bytes, including
owner wording, BOMs and line endings. Reordering within a page reuses existing
translations without a model call; new/deleted blocks are added/removed. Moving
content to another topic changes its page-qualified key and needs translation
review. Images themselves are not translation inputs; captions and paths are.

For a new page, or an older checkout with whole-page tracking, run:

```powershell
python Shared/tools/localization/prepare_help.py --dry-run
python Shared/tools/localization/prepare_help.py
```

This assigns IDs once and aligns all active locales by ordered block kinds and
literal anchors. Existing translations must match the last whole-page source
hash before it can carry their current status into block hashes. Missing
snapshots stay unacknowledged. Stale text, split/merged paragraphs, or ambiguous
alignment stop the entire migration without writes; review the indicated
boundaries first. It never retranslates wording. Duplicate IDs, unmarked extra
paragraphs, and new untracked pages after migration fail the checker.

Claude returns schema-validated JSON from a tool-less print-mode call. The helper
applies the text to a temporary copy, validates all selected locales, and only
then writes translations and their English-source manifests. Invalid responses,
authentication failures, timeouts or validation failures leave the original
content and hashes untouched. A missing manifest causes all of that locale's
entries to be translated; a malformed manifest or ambiguous XML needs manual
repair. Concurrent localization edits abort the update before it writes files.
Temporary copies are cleaned up after the run. Interrupted attempts during a
forced process termination can leave ignored files in `temp/localization-update-*`.

Review the resulting Git diff, then run the checker before publishing. These
helpers do not build, deploy, commit or publish. Automated checks cannot establish
fluency or detect every mistranslation; review sensitive wording and use the
in-game runner when layout or runtime behavior changes.

Useful options:

```powershell
scripts\check-localization.cmd --locale German --locale French --json temp/translations.json
scripts\update-localization.cmd --locale German --locale French
scripts\update-localization.cmd --model <model-id>
```

`--locale` is repeatable and defaults to all supported locales. `--model` selects
the Claude model (default `sonnet`). The updater disables the user's settings,
tools and skills for repeatable translation-only runs, while retaining CLI
authentication. Each batch has a ten-minute timeout; a failed run can be
retried after fixing the reported problem.

## Direct tools and manual maintenance

The underlying Python commands remain available:

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

Use `--locale German` (repeatable) for a smaller batch, `--no-manifest` during
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

After **manually** translating a changed source, explicitly update its snapshot.
The automatic update helper performs this step itself after validation:

```powershell
python Shared/tools/localization/translations.py snapshot --locale German --key 'Readouts/Help/2-groups/05-sharing-groups.md#<block-id>'
# Only after deliberately reviewing ALL pending entries:
python Shared/tools/localization/translations.py snapshot --locale German --all
```

Snapshots under `Shared/Translations/Manifests` record the English text hashes
that each locale was translated against, plus previous English block text for
translation context. Repeat `--key` to acknowledge individual entries/blocks;
other changed entries retain their old hashes. `--all` explicitly acknowledges
everything. Omitting both flags cannot clear pending diagnostics. The command
refuses incomplete or structurally broken content; it does not imply human
review. Do not refresh snapshots merely to silence stale-source diagnostics.

Active canonical game locale names are German, French, Spanish, Italian,
PortugueseBrazilian, SpanishLatin, ChineseSimplified, ChineseTraditional,
Ukrainian, Japanese, Korean, Turkish and Vietnamese.
Regional variants have separate content. Follow the game's selected language;
there is no mod picker.

Danish, Dutch, Swedish, Finnish, Norwegian (Bokmål) and Portuguese (Portugal)
are retired. Their XML, help and English-source manifests are preserved under
[`Shared/Translations/Retired`](../../Translations/Retired/README.md), mirroring
their original repository-relative paths. This archive is outside published mod
content and embedded shared catalogs. The helpers neither inspect nor update it;
all CLI commands reject retired `--locale` selections before writing files.
Archived English-source hashes remain frozen, even when active English changes.

To reactivate a locale, obtain owner approval, restore its archived files to
their original paths, and add its canonical name to `LOCALES` in
`translations.py`. Keep its archived source hashes until the updater has
translated and validated changes against current English, or a reviewer has
manually translated those changes and deliberately refreshed the snapshot.
Run a locale-specific check and update preview before updating; then check all
active locales. See the archive README for preservation and restoration details.

Owner-reviewed terminology is maintained in
[translation-guidance.md](translation-guidance.md), which the automatic updater
reads for every translation request.

Keep translation sources, source-hash
manifests, the checker, and reusable regression probes and fixtures in version
control. Verification logs, captures, copied saves and compiled assemblies are
local generated artifacts and remain ignored. The in-game verification README
records the completed runs and their evidence locations.
