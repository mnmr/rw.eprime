---
title: Sharing groups
---
<!-- l10n:b-a06a00109adf -->
The four buttons at the top of the editor share, save or reset your setup:

<!-- l10n:b-3abef8172f5c -->
![Import, Export, Set defaults and Restore defaults](editor-buttons.png)

<!-- l10n:b-d2699fb79187 -->
## Export a setup

<!-- l10n:b-e4537f800fee -->
Export saves your groups, resource pools, thresholds, count overrides and player settings. Choose a location and file name, then click Save, or use Copy to Clipboard.

<!-- l10n:b-355662a5828e -->
![The export preview and save controls](export-setup.png)

<!-- l10n:b-fe372ca48bad -->
Player settings include enabled groups and your global display and counting options. Current tier depth, search text, scroll positions and temporary editor state are excluded.

<!-- l10n:b-cf6b7284c676 -->
## Choose what to import

<!-- l10n:b-5c19c2c6b9e4 -->
Import reads a saved file or the clipboard and shows a preview before changing anything. Select the categories you want to replace. The Player settings and enabled groups checkbox starts unchecked, so you can keep your own display preferences.

<!-- l10n:b-537b3af44d4d -->
![The import preview with category selection](import-selection.png)

<!-- l10n:b-1c0ab7a10c14 -->
Every category present in the file can be selected. You can change related categories one at a time, even if an intermediate choice is incomplete. Import stays disabled until the chosen setup has valid pool and group references; hover the disabled button to see what is missing.

<!-- l10n:import-dependency-recovery -->
If thresholds or resource count rules need a missing pool, import those settings together with their Resource pools, or leave those categories unchanged. If player settings need a missing group, import **Groups** and **Player settings and enabled groups** together, or leave both unchanged.

<!-- l10n:b-3081ef3ab31d -->
Categories absent from an older export stay unchanged. Entries requiring unavailable mod content are skipped.

<!-- l10n:b-84361107e203 -->
In multiplayer, imported groups, pools, thresholds and count overrides change the setup for everyone. Imported player settings affect only you.

<!-- l10n:b-f4e74689b23c -->
## Save or restore defaults

<!-- l10n:b-6181f2c14286 -->
Set defaults saves a personal setup for future colonies (see [New-game defaults](topic:new-game-defaults)). Restore defaults asks for confirmation, then replaces all groups and resource pools with the built-in defaults and clears all thresholds and count overrides. In multiplayer, that reset affects everyone's shared setup.
