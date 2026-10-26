---
title: Filtres et recherche
---
La ligne de filtres réduit la table sans modifier les affectations :

![Ligne de filtres](filter-row.png)

- **Recherche** cherche dans les noms des colons. Tous les termes séparés par des espaces doivent correspondre. Les préfixes élargissent la recherche : r: ou role: cherche les noms des rôles affectés, j: ou job: les tâches qu'ils fournissent. « br r:méd » trouve les colons dont le nom contient *br* et ayant un rôle dont le nom contient *méd*.
- **Rôle affecté** liste les colons ayant le rôle choisi, ou un rôle non [bloqueur](topic:blocker-roles) couvrant toutes ses tâches.
- **Tâche couverte** liste les colons dont les rôles non bloqueurs incluent la tâche choisie.
- **Lieu** choisit la provenance des colons : carte actuelle, partout, colonie ou vaisseau. Les colons en caravane comptent sous le lieu dont ils sont partis.

Les filtres se combinent. Le bouton d'effacement qui apparaît lorsqu'un filtre est actif réinitialise la recherche et les filtres de rôle et de tâche, sans toucher au lieu, au regroupement ni aux choix d'affichage.

Un filtre ne masque jamais un colon sélectionné : s'il devait le faire, la sélection est effacée et le premier colon encore listé est sélectionné à sa place.
