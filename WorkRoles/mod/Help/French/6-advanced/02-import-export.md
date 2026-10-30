---
title: Importer et exporter
---
<!-- l10n:b-63a50794a977 -->
Les boutons du coin de l'onglet Rôles permettent de transférer des configurations entre sauvegardes et amis :

<!-- l10n:b-4f44ec3259f6 -->
![Boutons Importer, Exporter et Rétablir les valeurs par défaut](io-buttons.png)

<!-- l10n:b-fcca50ae80e8 -->
Exporter écrit la configuration complète des rôles dans un fichier ou le presse-papiers : rôles, groupes et leur ordre, icônes personnalisées, couleurs de la palette (y compris les couleurs inutilisées), règles, parcours de formation, réglages des recommandations et options de la sauvegarde. Vos préférences d'affichage sont aussi incluses dans une section dont l'importation est facultative. Les affectations des colons, les associations aux ordres de travail et l'état temporaire des fenêtres restent propres à la sauvegarde ou à la session en cours.

<!-- l10n:b-0420bcf88983 -->
![Fenêtre d'exportation](export-dialog.png)

<!-- l10n:b-9a255dfe5c80 -->
Importer lit un fichier ou le presse-papiers :

<!-- l10n:b-8a2c8b9bcf40 -->
![Fenêtre d'importation](import-dialog.png)

<!-- l10n:b-c5c6fbb19706 -->
Un aperçu propose ensuite Fusionner ou Remplacer pour chaque section. Fusionner ajoute et actualise sans supprimer. Remplacer rend votre configuration identique au fichier, en listant précisément les rôles qui seraient créés, modifiés ou retirés avant toute action. Remplacer rétablit aussi l'ordre des rôles du fichier. Les options de la sauvegarde et vos préférences d'affichage disposent de cases à cocher distinctes ; les préférences d'affichage sont décochées par défaut. Les anciens fichiers restent compatibles et les sections absentes d'un fichier laissent les paramètres correspondants inchangés.

<!-- l10n:b-67c0a3e8f921 -->
Pour réutiliser une configuration dans vos futures colonies, ouvrez **Rôles → Définir les valeurs par défaut** et choisissez **Utiliser cette configuration**. Les nouvelles parties commencent avec ces rôles et options ; les sauvegardes existantes conservent leur propre configuration lors du chargement. Effacer votre configuration par défaut personnelle rétablit la configuration initiale fournie pour les futures parties.

<!-- l10n:b-3fe7c915a062 -->
Vous pouvez activer les mises à jour automatiques lors de l'enregistrement d'une configuration par défaut. Après modification d'une configuration approuvée, fermer l'éditeur enregistre ces changements dans la configuration par défaut. Si vous ouvrez l'éditeur dans une sauvegarde dont la configuration diffère, vous devrez choisir entre utiliser la configuration de cette sauvegarde, conserver la configuration par défaut enregistrée ou arrêter les mises à jour automatiques. Conserver la configuration par défaut la protège des modifications ultérieures dans cette sauvegarde. Commencer une nouvelle partie ne remplace pas vos préférences d'affichage actuelles.

<!-- l10n:b-b8d402e619ac -->
Les tâches, compétences et icônes de mods indisponibles restent dans les configurations importées afin de pouvoir être exportées à nouveau. Une restriction de lieu visant une colonie ou un vaisseau nommé introuvable reste inactive ; elle ne devient jamais automatiquement « Partout ». Modifiez cette restriction dans l'éditeur de rôles lorsque vous passez à une nouvelle colonie. Les importations contenant des références invalides à des rôles composites ou à des parcours de formation sont rejetées avant toute modification de la configuration.
