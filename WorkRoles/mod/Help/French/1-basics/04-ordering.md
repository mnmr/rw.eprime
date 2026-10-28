---
title: Ordre des affectations
---
<!-- l10n:b-32c8a631526a -->
Un colon parcourt sa ligne de gauche à droite, en ne considérant que les rôles activés :

<!-- l10n:b-75c264ac5015 -->
![Pastilles de rôles ordonnées sur un colon](colonist-row.png)

<!-- l10n:b-617ab83d7eb0 -->
1. Les premiers rôles passent avant les suivants, tâche par tâche.
2. Dans un rôle, les premières tâches sont prioritaires.
3. Une tâche présente dans plusieurs rôles compte à sa première position.

<!-- l10n:b-be52fb18f81e -->
Les colons n'effectuent que les tâches de leurs rôles affectés. Ne pas affecter de rôle équivaut à laisser vide la case correspondante de la grille de priorités du jeu de base.

<!-- l10n:b-db1bdd5d561c -->
Changez la priorité par glissement :

<!-- l10n:b-90462aacb6d4 -->
@demo:chip-drag

<!-- l10n:b-be64d575edc7 -->
Vous pouvez aussi déplacer une pastille vers la ligne d'un autre colon pour transférer l'affectation. Cliquez sur la corbeille d'une pastille pour retirer l'affectation ; elle quitte la ligne.

<!-- l10n:b-6bbb9dc33edb -->
RimWorld conserve une liste distincte de tâches urgentes : combattre les incendies, prodiguer des soins urgents et se coucher pour recevoir des soins urgents. Ces tâches interrompent le sommeil, les repas et toute autre activité. Par défaut, une tâche urgente n'entre dans cette liste que si l'un des cinq premiers rôles activés du colon la contient. Les tâches urgentes des rôles suivants s'exécutent à leur place normale. Un colon ayant Urgences en premier et Médecin en sixième interrompt toujours son travail pour un incendie, mais traite les soins urgents comme du travail médical ordinaire à leur place dans la ligne. Déplacer Urgences au-delà de la cinquième position fait aussi de la lutte contre les incendies un travail ordinaire pour ce colon.

<!-- l10n:b-a52fa63b5067 -->
Désactivez « Seuls les 5 premiers rôles interrompent le travail en cas d'urgence » dans l'onglet Options pour supprimer cette limite. Chaque tâche urgente couverte par un rôle activé entre alors dans la liste, quelle que soit la place du rôle.
