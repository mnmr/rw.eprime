---
title: Compatibilité avec les mods
---
<!-- l10n:b-6110204fbb19 -->
Le travail et les tâches des autres mods sont détectés automatiquement. Certains bénéficient d'une intégration plus poussée : les expertises de Vanilla Skills Expanded et Alpha Skills alimentent les signaux ; le travail détesté de More Than Capable devient un signal Désastreux ; Colony Groups fournit un regroupement ; les mods à plusieurs étages traitent les étages superposés comme un seul lieu.

<!-- l10n:b-c7669aaaba7d -->
Des mods facultatifs de travail et de compétences peuvent fournir des profils de tâches supplémentaires. Ces profils identifient les compétences utilisées et entraînées, et peuvent préciser la compétence principale de recommandation lorsque les mods concernés sont installés. WorkRoles laisse l'admissibilité, les désignations et l'exécution aux mods qui fournissent les tâches. Ces intégrations n'ajoutent aucune dépendance obligatoire.

<!-- l10n:b-27d73501db9c -->
Les recommandations lisent aussi les catégories de travail déclarées, les capacités requises, l'opposition des préceptes et les refus vérifiés d'extraction minière ordinaire. Pour les versions vérifiées de Humanoid Alien Races sous RimWorld 1.6 et 1.7, elles utilisent les véritables restrictions de tâches et de recettes du framework. Toutes les races utilisant ces règles profitent de l'intégration. Une recette bloquée ne retire pas les autres recettes utilisables du rôle et ne compte pas comme moyen d'entraîner la compétence.

<!-- l10n:b-15011508cb81 -->
Les versions inconnues du framework conservent l'admissibilité existante et signalent que leurs règles ne sont pas prises en charge. Il n'est pas possible de détecter sûrement les restrictions cachées dans des tâches arbitraires en essayant ces tâches pendant la collecte des recommandations. Les versions prises en charge de Vivi fournissent la vitesse, le rendement et l'admissibilité de récolte dépendant de l'équipement. Les versions inconnues de Vivi, les règles de récolte personnalisées et les règles propres à Ratkin non vérifiées conservent le comportement existant ; aucun nom de race ou de xénotype ne sert à deviner une restriction.

<!-- l10n:b-cc8167cf5cfc -->
Les extensions modifiant des fournisseurs de tâches existants utilisent toujours les mêmes rôles et le même ordre. Les rôles de dressage, par exemple, continuent d'utiliser Animaux pour les recommandations. Les gains conditionnels d'expérience dans une autre compétence dépendent des règles d'admissibilité de l'extension ; ils ne créent pas un parcours général pour tous les colons affectés à ce travail.

<!-- l10n:b-f6d4800368e1 -->
Deux familles entrent en conflit. Les autres remplacements de l'onglet Travail, comme les variantes de Work Tab et Better Work Tab, se disputent la même interface. Les mods fixant les priorités, comme Free Will et PriorityMaster, se disputent les mêmes données : WorkRoles contrôle les priorités des colons gérés, donc bloque leurs modifications. Un avertissement unique nomme alors le mod et propose une méthode par rôles pour obtenir le même effet.

<!-- l10n:b-3e9c0a143130 -->
Les mods réglant les priorités des mécanoïdes, comme Mech Work Tab, peuvent gérer les travailleurs sans affectation WorkRoles. Si vous activez la prise en charge des robots et des mécanoïdes, WorkRoles contrôle aussi l'ordre des tâches des travailleurs mécaniques ayant des rôles attribués. Désactivez cette prise en charge dans les options pour rétablir leur ordre de travail natif à tous ; leurs rôles enregistrés restent inactifs.

<!-- l10n:mechanical-workers -->
**Afficher les robots et mécanoïdes dans Colons** est une option propre à chaque sauvegarde, désactivée par défaut.
Elle ajoute les mécanoïdes de Biotech, Misc. Robots et Misc. Robots++ au tableau existant.
Attribuez-y des rôles ; utilisez leurs onglets habituels pour les groupes de contrôle, les zones, l'activation
et la recharge. Les rôles ordonnent uniquement les tâches que le travailleur peut réellement accomplir. Les androïdes
humanoïdes continuent de suivre les règles ordinaires des colons.

<!-- l10n:mechanical-recharge -->
La recharge reste gérée par le jeu ou le mod d'origine et ne nécessite aucun rôle Recharge. Biotech propose des limites de charge pour chaque groupe de contrôle. Misc. Robots utilise ses propres seuils de retour automatique ; cliquer sur le pourcentage de charge dans son onglet Robots demande une recharge immédiate. WorkRoles ne modifie pas ces seuils.

<!-- l10n:b-3df0d9a43535 -->
Certains mods exigent des priorités de 0 à 4. En cas de problème, vérifiez que « Transmettre aux autres mods des priorités de 0 à 4 » est activé.

<!-- l10n:b-99382dfb0e0e -->
Les mods qui lisent seulement les priorités fonctionnent : ils voient les valeurs calculées par WorkRoles.
