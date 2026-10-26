# Retired translations

The Dutch, Swedish, Finnish, Danish, Norwegian, and European Portuguese
translations were retired from built-in support on 2026-10-26. Their reviewed
XML, translated help pages, and English-source manifests are preserved here
without changing their contents.

This directory mirrors repository-relative paths. For example:

- `WorkRoles/mod/FallbackLanguages/Danish` restores to the same path at the repository root.
- `Implanner/mod/Help/Danish` restores to the same path at the repository root.
- `Shared/Translations/FallbackLanguages/Danish` contains the shared phrases.
- `Shared/Translations/Manifests/Danish.json` records which English text was last reviewed.

Quality Jobs has no translated help directory; its archived content is XML.
Shared English help artwork remains with the active mods.

## Publication and updates

The archive is outside every published `mod` directory and outside the
`Languages` and `FallbackLanguages` trees embedded by `Translations.props`.
It is not copied or embedded into released mods. Deployment mirrors each
`mod` directory, removing obsolete files from an earlier deployment. The
publisher also checks deployed localization files and assemblies against the
current mod folder before uploading.

Normal localization checks and updates operate only on the eight active
non-English locales: German, French, Spanish, Italian, Brazilian Portuguese,
Latin American Spanish, Simplified Chinese, and Traditional Chinese. They do
not update this archive or advance its source hashes.

## Reactivation

After the owner approves a language's return:

1. Restore that locale's directories for all four mods and the shared catalog
   to their original repository-relative paths. Restore its manifest too.
   Keep the archived copy until the restored translation has been verified.
2. Add the canonical locale name to `LOCALES` in
   `Shared/tools/localization/translations.py`.
3. Run the localization updater for that locale. Keep the old manifest hashes
   until changed English text has been translated and reviewed; creating a
   fresh snapshot first would hide changes made during retirement.
4. Run the localization checks, review the restored wording, and rebuild all
   four mods for both supported game versions so shared phrases are embedded.
5. Update the Workshop descriptions and changelogs, then deploy the rebuilt
   mod folders before publishing.

The terminology decisions retained in
`Shared/tools/localization/translation-guidance.md` still apply when restoring
these translations. Retirement does not erase previous owner corrections.
