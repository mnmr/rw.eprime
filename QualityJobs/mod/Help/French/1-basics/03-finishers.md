---
title: Choisir les finisseurs
---
<!-- l10n:requirements -->
Utilisez ces commandes sur un ordre de fabrication ou un bâtiment. D'autres travailleurs admissibles peuvent effectuer les premières étapes ; seule la touche finale doit respecter vos exigences.

<!-- l10n:controls -->
- **Compétence du finisseur** définit un niveau de compétence minimum.
- **{k:QJ_AutoBest}** choisit automatiquement le meilleur travailleur admissible. Le réglage manuel du curseur est conservé pour lorsque vous désactivez cette option.
- **{k:QJ_RequireInspired}** attend cette inspiration. Achever le travail la consomme comme d’habitude.
- **{k:QJ_RequireSpecialist}** exige un rôle d’Ideology accordant un bonus de qualité de production. Cette option apparaît avec Ideology.

<!-- l10n:odds-image -->
![Critères des finisseurs et aperçu des probabilités de qualité](finisher-options.png)

<!-- l10n:best -->
L’ajustement automatique compare les colons admissibles, les mécanoïdes de la colonie et les robots pris en charge, puis choisit le travailleur dont la qualité attendue est la plus élevée. La compétence, l’inspiration de créativité, les bonus des spécialistes de la production et les bonus de qualité des mods pris en charge sont tous pris en compte. Cela maximise la qualité moyenne, sans garantir le résultat d’un tirage individuel.

<!-- l10n:availability -->
Les travailleurs qui remplissent les autres conditions restent dans la comparaison lorsqu’ils sont absents, à terre, endormis, en crise mentale ou en recharge, y compris les robots dans leurs stations. Si aucun des travailleurs les mieux classés ne peut terminer le travail sur cette carte, celui-ci attend. Par exemple, un meilleur artisan parti en caravane peut empêcher un travailleur moins bien classé resté à la colonie de terminer. Aucun délai d’attente ne réduit l’exigence de qualité.

<!-- l10n:manual-threshold -->
Lorsque l’ajustement automatique est désactivé, tout travailleur capable d’effectuer le travail et respectant votre seuil de compétence manuel ainsi que les autres exigences peut le terminer. Un meilleur travailleur ailleurs dans la colonie ne le bloque pas.

<!-- l10n:robot-finishers -->
Les mécanoïdes et les robots pris en charge doivent respecter les mêmes critères de finition que les colons. Exiger l’inspiration de créativité ou un rôle de spécialiste de la production exclut toujours les travailleurs qui n’en disposent pas. Désactiver **{k:QJ_SettingsAllowRobotPreparation}** réserve les premières étapes aux colons ; les mécanoïdes et robots admissibles peuvent toujours effectuer les tâches de finition de Quality Jobs.

<!-- l10n:odds -->
**{k:QJ_OddsHeader}** compare votre **{k:QJ_OddsColConfig}** au **{k:QJ_OddsColBest}** candidat actuel. Ce sont des probabilités, pas des garanties. **Finisseurs admissibles** liste les travailleurs pouvant remplir les critères sur cette carte à l’heure actuelle ; les travailleurs absents, à terre ou en crise mentale en sont exclus.

<!-- l10n:priority -->
**{k:QJ_SettingsHighPriorityFinish}** fait passer les travaux de finition attribués avant les autres travaux, sauf la lutte contre les incendies, les soins médicaux, le sauvetage et le repos au lit. Cette option n'interrompt pas la tâche en cours. Si elle est désactivée, les finitions passent en premier au sein de leur type de travail, après les types de travail plus prioritaires. Le travailleur doit être capable d'effectuer le type de travail habituel de l'objet.

