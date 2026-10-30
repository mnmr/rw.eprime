---
title: Choisir les finisseurs
---
<!-- l10n:requirements -->
Utilisez ces commandes sur un ordre de fabrication ou un bâtiment. D'autres travailleurs admissibles peuvent effectuer les premières étapes ; seule la touche finale doit respecter vos exigences.

<!-- l10n:controls -->
- **Compétence du finisseur** définit un niveau de compétence minimum.
- **{k:QJ_AutoBest}** sélectionne les travailleurs selon la qualité attendue plutôt que selon votre seuil de compétence manuel. Le réglage manuel du curseur est conservé pour lorsque vous désactivez cette option.
- **{k:QJ_RequireInspired}** attend cette inspiration. Achever le travail la consomme comme d’habitude.
- **{k:QJ_RequireSpecialist}** exige un rôle d’Ideology accordant un bonus de qualité de production. Cette option apparaît avec Ideology.

<!-- l10n:odds-image -->
![Critères des finisseurs et aperçu des probabilités de qualité](finisher-options.png)

<!-- l10n:best -->
La sélection automatique compare les colons admissibles, les mécanoïdes de la colonie et les robots pris en charge, puis choisit le travailleur dont la qualité attendue est la plus élevée. La compétence, l’inspiration de créativité, les bonus des spécialistes de la production et les bonus de qualité des mods pris en charge sont tous pris en compte. Cela maximise la qualité moyenne, sans garantir le résultat d’un tirage individuel.

<!-- l10n:availability -->
Les travailleurs qui remplissent les autres conditions restent dans la comparaison lorsqu’ils sont absents, à terre, endormis, en crise mentale ou en recharge, y compris les robots dans leurs stations. Si aucun des travailleurs les mieux classés ne peut terminer le travail sur cette carte, celui-ci attend. Par exemple, un meilleur artisan parti en caravane peut empêcher un travailleur moins bien classé resté à la colonie de terminer. Aucun délai d’attente ne réduit l’exigence de qualité.

<!-- l10n:manual-threshold -->
Lorsque **{k:QJ_AutoBest}** est désactivé, tout travailleur capable d’effectuer le travail et respectant votre seuil de compétence manuel ainsi que les autres exigences peut le terminer. Un meilleur travailleur ailleurs dans la colonie ne le bloque pas.

<!-- l10n:robot-finishers -->
Les mécanoïdes et les robots pris en charge doivent respecter les mêmes critères de finition que les colons. Exiger l’inspiration de créativité ou un rôle de spécialiste de la production exclut toujours les travailleurs qui n’en disposent pas. Désactiver **{k:QJ_SettingsAllowRobotPreparation}** réserve les premières étapes aux colons ; les mécanoïdes et robots admissibles peuvent toujours effectuer les tâches de finition de Quality Jobs.

<!-- l10n:odds -->
Dans le tableau **{k:QJ_OddsHeader}** d’un ordre de fabrication, **{k:QJ_OddsColConfig}** estime les probabilités selon votre compétence minimale de finition et les bonus exigés. Lorsque **{k:QJ_AutoBest}** est activé et qu’un candidat admissible est trouvé, cette colonne affiche plutôt les probabilités de ce travailleur sélectionné à l’échelle de la colonie. Sans candidat admissible, elle utilise vos réglages manuels ; cela ne signifie pas qu’un finisseur est disponible.

<!-- l10n:odds-best -->
**{k:QJ_OddsColBest}** indique les probabilités pour le travailleur le mieux classé capable d'effectuer le travail sur cette carte, sans tenir compte de vos exigences supplémentaires de finition. Les deux colonnes peuvent donc concerner des travailleurs différents. Ce sont des probabilités, pas des garanties ; le résultat dépend du travailleur qui termine effectivement le travail.

<!-- l10n:eligible-finishers -->
**Finisseurs admissibles** liste les travailleurs pouvant remplir les critères sur cette carte à l’heure actuelle ; les travailleurs absents, à terre ou en crise mentale en sont exclus. Pour les ordres, cette liste utilise les exigences de l'ordre sélectionné, même si les décomptes de la file incluent d'autres ordres fabriquant le même produit sur cette carte.

<!-- l10n:priority -->
**{k:QJ_SettingsHighPriorityFinish}** fait passer les travaux de finition attribués avant les autres travaux, sauf la lutte contre les incendies, les soins médicaux, le sauvetage et le repos au lit. Cette option n'interrompt pas la tâche en cours. Si elle est désactivée, les finitions passent en premier au sein de leur type de travail, après les types de travail plus prioritaires. Le travailleur doit être capable d'effectuer le type de travail habituel de l'objet.

