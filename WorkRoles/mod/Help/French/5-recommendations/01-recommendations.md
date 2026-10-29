---
title: Fonctionnement des recommandations
---
<!-- l10n:b-07818cd37217 -->
Le moteur de recommandations examine les aptitudes des colons et les besoins de la colonie, puis propose une ligne de rôles à chacun.

<!-- l10n:b-d0c69b8a87f5 -->
Ses indices sont appelés signaux : passions ardentes, aptitudes génétiques, traits, expertises et meilleures compétences de la colonie. Il impose aussi les besoins de la colonie, afin que les rôles essentiels comme Médecin et Cuisinier reviennent toujours à quelqu'un, idéalement doué pour cela.

<!-- l10n:b-45031c5441da -->
Les préceptes opposés à un travail réduisent son score de recommandation. Un colon apte et volontaire a plus de chances de le recevoir, mais un colon opposé peut encore couvrir un besoin essentiel. Un refus vérifié ou une capacité manquante rend ce travail inadmissible. Les notes de compétences décrivent toujours l'aptitude ; Meilleurs candidats liste cette aptitude brute parmi les colons capables, tandis que les recommandations de colonie tiennent aussi compte des aversions et des besoins.

<!-- l10n:b-e112aed1a354 -->
Ce sont les données des recommandations. Les rôles conditionnels continuent de déterminer quand et où un rôle affecté est actif.

<!-- l10n:b-2a25543f495f -->
Avec les versions prises en charge de Vivi, les recommandations de récolte utilisent la vitesse et le rendement réels du travailleur. Un humain équipé d'une paille Vivi peut récolter du miel et du pollen ; une brosse multifonction permet la récolte de plantes. Équiper ou retirer ces outils actualise la capacité même en pause. Un travailleur capable de réaliser certaines recettes de récolte reste admissible à ce travail ; les recettes indisponibles restent exclues des choix de formation. Les versions inconnues de Vivi et les règles de récolte personnalisées conservent leur comportement existant.

<!-- l10n:b-fce545e1aa79 -->
Le moteur considère tous les rôles admissibles et les ordonne automatiquement pour chaque colon. Les Options des rôles de l'onglet Recommandations listent tous les rôles sauf ceux exclus. Pour interdire toute recommandation d'un rôle, ajoutez-le aux **Rôles exclus** en haut de l'onglet. Les exclusions priment sur les besoins et les traitements spéciaux, mais les affectations épinglées restent protégées. Voir [Ordre et exclusions](topic:recommendation-order).

<!-- l10n:b-4cefba49c974 -->
Son avis est visible partout. Les infobulles de la palette listent les meilleurs candidats :

<!-- l10n:b-0dd35388dc64 -->
![Meilleurs candidats dans l'infobulle de la palette](palette-tooltip.png)

<!-- l10n:b-f1a1a1196f55 -->
Deux rôles ont un traitement spécial. Chasseur est recommandé à toute personne portant une arme à distance : la chasse entraîne le Tir. Il est placé plus tôt pour les mauvais tireurs, et plus tard pour les tireurs d'élite, dont le temps est plus utile ailleurs. Une aversion pour la chasse le repousse aussi dans la ligne. Méchanitor n'est recommandé qu'aux colons possédant un mécalien, même si les autres artisans sont excellents ; sa liste Meilleurs candidats respecte cette règle.

<!-- l10n:b-b241ec72dcda -->
Le moteur suggère ; vous appliquez, [rôle par rôle](topic:cherry-picking) ou [pour toute la colonie](topic:fix-my-colony). L'exception est l'[optimisation automatique](topic:auto-optimize) : activez-la pour lancer Optimiser ma colonie à chaque heure du jeu dans toutes vos colonies.

<!-- l10n:mechanical-suggestions -->
Lorsque les lignes des robots et des mécanoïdes sont activées, leurs recommandations reposent sur leurs compétences actuelles et les travaux qu'ils peuvent effectuer, sans passions humaines ni parcours d'apprentissage. Leur tâche principale acceptée reste en place, même s'il s'agit d'un rôle exclu explicitement affecté ; modifiez cette tâche manuellement. Les recommandations de travaux supplémentaires respectent les exclusions. Vous choisissez leurs affectations ; l'optimisation automatique continue d'appliquer uniquement les recommandations destinées aux humains.

<!-- l10n:mechanical-coverage -->
**Tenir compte du travail des robots et des mécanoïdes dans les recommandations humaines** est une option distincte propre à chaque sauvegarde, également désactivée par défaut. Elle comptabilise les affectations mécaniques acceptées dans les effectifs, permettant de remplacer le dernier spécialiste humain d'une tâche entièrement prise en charge. Chaque travailleur assure une seule tâche principale ; les rôles de repli ne comptent pas comme des effectifs supplémentaires. Les zones restreintes, les travaux bloqués, les compétences insuffisantes et les restrictions de Quality Jobs sur le travail des robots empêchent une prise en charge complète. Un robot capable uniquement de nettoyer ne remplace pas un rôle combinant nettoyage et transport.

<!-- l10n:mechanical-learning -->
Les formations sélectionnées pour les humains dont l'objectif n'est pas encore atteint et les affectations épinglées restent protégées.
La recharge habituelle maintient la couverture des tâches ; la désactivation, la perte de contrôle ou la sortie du mode Travail la suppriment. Les recommandations actualisées proposent des remplaçants humains, et l'optimisation automatique les applique à son échéance horaire habituelle si elle est activée. Lorsque l'intégration est désactivée, les lignes des robots et des mécanoïdes n'influencent pas les recommandations pour les humains.
