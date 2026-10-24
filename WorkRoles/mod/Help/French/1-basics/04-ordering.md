---
title: Ordre des affectations
---
Un colon parcourt sa ligne de gauche à droite, en ne considérant que les rôles activés :

![Pastilles de rôles ordonnées sur un colon](colonist-row.png)

1. Les premiers rôles passent avant les suivants, tâche par tâche.
2. Dans un rôle, les premières tâches sont prioritaires.
3. Une tâche présente dans plusieurs rôles compte à sa première position.

Les colons n'effectuent que les tâches de leurs rôles affectés. Ne pas affecter de rôle équivaut à laisser vide la case correspondante de la grille de priorités du jeu de base.

Changez la priorité par glissement :

@demo:chip-drag

Vous pouvez aussi déplacer une pastille vers la ligne d'un autre colon pour transférer l'affectation. Cliquez sur la corbeille d'une pastille pour retirer l'affectation ; elle quitte la ligne.

RimWorld conserve une liste distincte de tâches urgentes : combattre les incendies, prodiguer des soins urgents et se coucher pour recevoir des soins urgents. Ces tâches interrompent le sommeil, les repas et toute autre activité. Par défaut, une tâche urgente n'entre dans cette liste que si l'un des cinq premiers rôles activés du colon la contient. Les tâches urgentes des rôles suivants s'exécutent à leur place normale. Un colon ayant Urgences en premier et Médecin en sixième interrompt toujours son travail pour un incendie, mais traite les soins urgents comme du travail médical ordinaire à leur place dans la ligne. Déplacer Urgences au-delà de la cinquième position fait aussi de la lutte contre les incendies un travail ordinaire pour ce colon.

Le réglage « Seuls les 5 premiers rôles interrompent le travail en cas d'urgence » de l'onglet Options permet de désactiver cette limite. Chaque tâche urgente couverte par un rôle activé entre alors dans la liste, quelle que soit la place du rôle.
