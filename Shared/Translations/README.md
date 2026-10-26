# Shared translations

Reusable UI phrases belong in `Languages/English/Keyed` here, with the `RS_`
prefix. Their translations live in `FallbackLanguages/<locale>/Keyed`. Keep
mod-specific explanations in the owning mod's catalog.

Each game-integration project imports `Translations.props`, embedding this one
source in its assembly. A separate installed library mod is unnecessary. The
shared language loader fills missing keys after normal/community translations;
English remains the game's ordinary default-language fallback. Its separate
Harmony owner also works when an older mod already installed the loose-file
fallback loader.

`Scripts/update-localization.cmd` and `Scripts/check-localization.cmd` include
this catalog alongside all four mods. Update translations before building, so
the assemblies embed the current text. Existing owner translations are retained
when keys move here without changing their English source.

Active bundled locales are German, French, Spanish, Italian,
PortugueseBrazilian, SpanishLatin, ChineseSimplified, ChineseTraditional,
Ukrainian, Japanese, Korean, Turkish and Vietnamese.
Russian is intentionally excluded from support at the owner's request.
Danish, Dutch, Swedish, Finnish, Norwegian and Portuguese (Portugal) are
preserved in [Retired](Retired/README.md), outside embedded and published content.
The archive mirrors original repository-relative paths, including shared and
mod catalogs, help and English-source manifests. Its source hashes stay frozen;
normal check, inventory, snapshot and update commands only accept active locales.
See [translation maintenance](../tools/localization/README.md) and the archive
README before restoring an owner-approved locale and updating it against current
English.
