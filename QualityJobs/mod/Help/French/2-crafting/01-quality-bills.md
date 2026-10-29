---
title: Configurer un ordre de production
---
<!-- l10n:open -->
Ouvrez **Détails** sur un ordre de fabrication d'un objet doté d'un niveau de qualité. Le panneau **{k:QJ_QualityPanelTitle}** apparaît à côté des commandes habituelles de l'ordre pour les recettes compatibles qui produisent un objet inachevé. Les recettes sans qualité ou sans étape de travail inachevé conservent leur fonctionnement habituel.

<!-- l10n:bill-image -->
![Un ordre de fabrication sur un établi avec son panneau Quality Jobs](crafting-bill.png)

<!-- l10n:setup -->
Activez **{k:QJ_ManageBill}**, puis choisissez les [exigences pour les finisseurs](topic:finishers). Les travailleurs ordinaires peuvent effectuer l'essentiel de la fabrication. Si le travailleur actuel remplit les critères, il termine directement l'objet ; sinon, l'objet presque terminé est mis de côté et l'ordre peut lancer la fabrication d'autres objets dans la limite de son plafond de stock.

<!-- l10n:dispatch -->
Lorsqu'un finisseur remplit les critères, Quality Jobs crée un ordre temporaire **Terminer …** qui lui est attribué. Il récupère l'objet inachevé et le termine. L'ordre temporaire se supprime ensuite automatiquement. Les objets en attente peuvent être transportés et stockés comme les autres objets inachevés.

<!-- l10n:finisher-bill-restrictions -->
Le niveau de compétence maximal et l'affectation à un travailleur précis définis dans l'ordre d'origine s'appliquent jusqu'à ce que l'objet soit mis de côté pour la finition. Vous pouvez confier le travail à un apprenti tandis qu'un colon plus compétent, un méca de la colonie ou un robot compatible prend en charge l'ordre de finition dédié. Les finisseurs doivent toutefois être capables d'exécuter la recette et satisfaire aux exigences de Quality Jobs.

<!-- l10n:status -->
La section **{k:QJ_StatusHeader}** indique les travaux **Inachevés** partagés, les objets **En attente** d'un artisan qualifié pour les terminer et ceux déjà attribués pour **Finition**. Survolez l'état et la liste des artisans chargés de la finition pour en savoir plus. Consultez [les travaux en attente](topic:waiting) si rien ne progresse.

<!-- l10n:disable -->
Décochez **{k:QJ_ManageBill}** pour permettre à nouveau son exécution normale. Si le partage est activé, d'autres travailleurs peuvent toujours poursuivre les travaux partagés de cet ordre.

