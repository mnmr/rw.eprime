---
title: Options and defaults
---
<!-- l10n:options-image -->
![The Options tab with bill and construction defaults](options.png)

<!-- l10n:scope -->
With a save loaded, Options changes that colony's settings. From the main menu, the mod settings supply defaults for new saves. **{k:QJ_SettingsShowToolbarButton}** is your personal display preference; hiding it does not disable the mod. You can still open the mod settings from the game's Options menu.

<!-- l10n:bill-defaults -->
**{k:QJ_SettingsBillDefaults}** control bills that follow the defaults. Individual bill settings override them. **{k:QJ_SettingsManageNewBills}** affects new bills and existing bills that still follow that management default. Turning it off releases waiting items from those bills so they can finish normally. Other bill defaults can also affect existing bills without an override for that setting. The unfinished stock cap is shared by product, with each map counted separately.

<!-- l10n:construction-defaults -->
**{k:QJ_SettingsConstructionDefaults}** are applied when a build is enabled or automatically managed. Existing managed buildings keep their own settings. The construction target uses **{k:QJ_NoRetries}**; the crafting target uses **{k:QJ_AnyQuality}**.

<!-- l10n:global -->
**{k:QJ_SettingsShareWork}** and **{k:QJ_SettingsHighPriorityFinish}** apply across the colony. See [sharing](topic:sharing-work) and [finishers](topic:finishers) for their effects. **{k:QJ_DisableButton}** is intended for [safe removal](topic:removing), not for pausing one bill.

<!-- l10n:robot-preparation -->
**{k:QJ_SettingsAllowRobotPreparation}** is saved for the current colony and starts enabled, including in existing saves. Turn it off to reserve work on managed crafting bills and construction plans for colonists, so they receive the work's skill experience. This also stops mechs and robots already doing that work. Eligible mechs and supported robots can still finish jobs assigned by Quality Jobs. Ordinary unmanaged jobs are unaffected; [shared unfinished items](topic:sharing-work) keep the original managed bill's restriction. This option is available with a save loaded and is synchronized in Multiplayer.
