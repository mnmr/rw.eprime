---
title: Import and export
---
<!-- l10n:b-63a50794a977 -->
The Roles tab's corner buttons move role setups between saves and
friends:

<!-- l10n:b-4f44ec3259f6 -->
![Import, Export and Restore Defaults buttons](io-buttons.png)

<!-- l10n:b-fcca50ae80e8 -->
Export writes the complete role setup to a file or the clipboard: roles,
groups and their order, custom icons, palette colors (including unused
colors), rules, training paths, recommendation tuning and save-wide options.
It also includes your display preferences as an optional import section.
Colonist assignments, bill bindings and temporary window state stay with
their current save or session.

<!-- l10n:b-0420bcf88983 -->
![The export dialog](export-dialog.png)

<!-- l10n:b-9a255dfe5c80 -->
Import reads from a file or the clipboard:

<!-- l10n:b-8a2c8b9bcf40 -->
![The import dialog](import-dialog.png)

<!-- l10n:b-c5c6fbb19706 -->
A preview then offers Merge or Overwrite per section. Merge adds and
updates without deleting; Overwrite makes your setup match the file,
listing exactly which roles would be created, changed, or removed before
anything happens. Overwrite also restores the file's role order. Save-wide
options and your display preferences have separate checkboxes; display
preferences are off by default. Older files remain supported, and sections
missing from a file leave the corresponding settings unchanged.

<!-- l10n:b-67c0a3e8f921 -->
To reuse a setup in future colonies, open **Roles → Set defaults**
and choose **Use this setup**. New games start with those roles and options;
loading an existing save keeps its own setup. Clearing the personal default
restores the shipped starting setup for future games.

<!-- l10n:b-3fe7c915a062 -->
You can enable automatic updates when saving a default. After editing an
approved setup, closing the editor saves those changes to the default.
Opening the editor in a differing save asks whether to use that save's
setup, keep the saved default, or stop automatic updates. Keeping the default
protects it from later edits in that save. Starting a new game does not
replace your current display preferences.

<!-- l10n:b-b8d402e619ac -->
Unavailable mod jobs, skills and icons remain in imported setups so they can
be exported again. A named settlement or ship that cannot be matched stays
an inactive location restriction; it never becomes Anywhere automatically.
Change that restriction in the role editor when moving to a new colony.
Imports with broken composite or training references are rejected before
changing the setup.
