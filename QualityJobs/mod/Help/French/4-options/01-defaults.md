---
title: Options et valeurs par défaut
---
<!-- l10n:options-image -->
![L'onglet Options avec les paramètres par défaut des ordres et des constructions](options.png)

<!-- l10n:scope -->
Lorsqu'une sauvegarde est chargée, l'onglet Options modifie les paramètres de cette colonie. Depuis le menu principal, les paramètres du mod définissent les valeurs par défaut des nouvelles parties. **{k:QJ_SettingsShowToolbarButton}** est une préférence d'affichage personnelle ; masquer ce bouton ne désactive pas le mod. Vous pouvez toujours accéder aux paramètres du mod depuis le menu Options du jeu.

<!-- l10n:bill-defaults -->
Les **{k:QJ_SettingsBillDefaults}** s'appliquent aux ordres qui utilisent les valeurs par défaut. Les paramètres propres à chaque ordre sont prioritaires. Modifier une valeur par défaut peut donc affecter les ordres existants qui n'ont pas été personnalisés. Le plafond de stock d'objets inachevés est partagé par produit, et non défini séparément pour chaque ordre.

<!-- l10n:construction-defaults -->
Les **{k:QJ_SettingsConstructionDefaults}** s'appliquent lorsque la gestion d'une construction est activée ou automatique. Les bâtiments déjà gérés conservent leurs propres paramètres. La qualité cible de construction utilise **{k:QJ_NoRetries}** ; celle de fabrication utilise **{k:QJ_AnyQuality}**.

<!-- l10n:global -->
**{k:QJ_SettingsShareWork}** et **{k:QJ_SettingsHighPriorityFinish}** s'appliquent à toute la colonie. Consultez les pages sur le [partage](topic:sharing-work) et la [finition](topic:finishers) pour connaître leurs effets. **{k:QJ_DisableButton}** sert à [retirer le mod sans risque](topic:removing), et non à suspendre un seul ordre.

<!-- l10n:robot-preparation -->
**{k:QJ_SettingsAllowRobotPreparation}** est enregistré pour la colonie actuelle et activé par défaut, y compris dans les sauvegardes existantes. Désactivez cette option pour réserver aux colons le travail sur les ordres de fabrication et les plans de construction gérés, afin qu'ils gagnent l'expérience de compétence liée à ce travail. Cela interrompt aussi les mécas et les robots qui effectuent déjà ce travail. Les mécas admissibles et les robots compatibles peuvent toujours terminer les travaux attribués par Quality Jobs. Les travaux ordinaires non gérés ne sont pas affectés ; les [objets inachevés partagés](topic:sharing-work) conservent la restriction de l'ordre géré d'origine. Cette option est disponible lorsqu'une sauvegarde est chargée et est synchronisée dans Multiplayer.

