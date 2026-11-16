---
title: Nice Bill Tab
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **Verdict** | ![Entièrement compatible](verdict-blue.png|16) Entièrement compatible (prise en charge dédiée) |
| **Preuve** | Analyse du code source, test automatisé en jeu |
| **Vérifié le** | 2026-11-16 |
| **Versions** | Implanner 1.3.0, Nice Bill Tab [Workshop 3520130671](https://steamcommunity.com/sharedfiles/filedetails/?id=3520130671), fichiers du 2026-11-13 |

<!-- l10n:summary -->
Un établi Implanner affiche sa file de production, et la case de l'établi a sa propre ligne.

<!-- l10n:toggle -->
Sur un établi capable de fabriquer des implants, la case **Établi Implanner** occupe sa propre ligne en haut de la liste des tâches de Nice Bill Tab, au-dessus du champ de recherche. Les établis qui ne fabriquent aucun implant affichent Nice Bill Tab sans changement.

<!-- l10n:vanilla-tab -->
Un établi marqué comme établi Implanner affiche la file de production d'Implanner à la place de la présentation de Nice Bill Tab, comme l'onglet qu'il affiche sans Nice Bill Tab (voir [options de production](topic:production-options)). L'interrupteur et le bouton de réglages propres à Nice Bill Tab ne sont pas affichés dans cet onglet. Décochez la case et la présentation de Nice Bill Tab revient pour cet établi.

<!-- l10n:stray-bills -->
Nice Bill Tab peut créer une tâche sur un établi disponible pour un ingrédient manquant. Les établis Implanner sont ignorés, et la tâche va donc à l'autre établi le plus proche capable de fabriquer l'objet. Quand seuls des établis Implanner peuvent le fabriquer, aucune tâche n'est créée.
