---
title: En coulisses
---
Les rôles ordonnés de chaque colon sont compilés en un ordre strict de tâches, transmis au jeu par les listes que sa sélection de travail lit déjà. Le comportement au travail est identique à celui obtenu avec une grille réglée manuellement, sans correctifs à chaque tick ni travail à chaque image.

Tout est recalculé lorsque les rôles ou affectations changent, puis conservé en cache. Les rôles conditionnels s'actualisent aussi aux changements d'heure et de lieu. Les urgences comme les incendies passent par le traitement d'urgence du jeu si l'un des cinq premiers rôles activés les couvre ; les autres restent à leur place normale. Désactiver « Seuls les 5 premiers rôles interrompent le travail en cas d'urgence » fait passer toutes les urgences couvertes par ce traitement.

WorkRoles remplit les priorités du jeu à partir des rôles, converties dans la plage 0–4 si l'option correspondante est activée, ce qui est le cas par défaut. C'est pourquoi retirer le mod est sûr : l'onglet Travail revient avec vos rôles convertis en priorités. Ajouter le mod à une sauvegarde existante réalise la conversion inverse.
