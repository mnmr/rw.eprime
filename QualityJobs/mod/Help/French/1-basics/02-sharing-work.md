---
title: Partager les travaux inachevés
---
<!-- l10n:sharing -->
Lorsque **{k:QJ_SettingsShareWork}** est activé dans les options, un objet inachevé laissé en attente peut être repris par un autre travailleur admissible, même si son artisan d’origine est mort. Le partage fonctionne indépendamment de **{k:QJ_ManageBill}**.

<!-- l10n:matching -->
Le travailleur qui prend le relais doit disposer d’un ordre de fabrication utilisable pour la même recette. Les ingrédients de l’objet doivent être autorisés par cet ordre, et son style doit correspondre. L’objet doit aussi être accessible, autorisé et disponible à la réservation. Le partage ne contourne ni les affectations de travail habituelles ni les restrictions des ordres de fabrication.

<!-- l10n:handoff -->
Un objet inactif est dissocié de son travailleur et de son ordre de fabrication d’origine afin qu’un ordre compatible puisse le reprendre. Tant que l’objet inachevé de cet ordre est en attente ou en cours de transport, l’ordre ne lance pas la fabrication d’un doublon. Un travail en cours n’est pas interrompu simplement pour le confier à quelqu’un d’autre.

<!-- l10n:reserved -->
Partager les premières étapes du travail est différent de réserver la touche finale. Un objet géré mis en attente juste avant son achèvement affiche **Auteur : réservé (Quality Jobs)**. Il attend un [finisseur qualifié](topic:finishers) ; un travailleur ordinaire ne peut pas l’achever par le partage.

<!-- l10n:off -->
Désactivez le partage pour rétablir l’attribution habituelle des travaux partagés. Les ordres gérés peuvent toujours réserver la touche finale. Désactiver **{k:QJ_ManageBill}** libère les travaux de cet ordre en attente de finition pour leur qualité ; cela ne désactive pas le partage pour la colonie.

