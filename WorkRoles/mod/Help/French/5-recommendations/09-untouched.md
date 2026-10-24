---
title: Ce qui reste inchangé
---
Appliquer les recommandations, Optimiser ma colonie et l'optimisation automatique conservent normalement quatre types d'affectations :

- Les affectations [épinglées](topic:pinning), votre instruction explicite de ne pas y toucher.
- Les [rôles bloqueurs](topic:blocker-roles), car interdire une tâche était une décision.
- Les [rôles conditionnels](topic:conditional-roles), déjà gérés par leurs règles.
- Tous les rôles d'un colon à terre.

**Les affectations épinglées restent protégées même en cas d'exclusion.** Si un rôle non épinglé ou l'un de ses membres composites est exclu, appliquer les recommandations retire cette affectation. Voir [Ordre des recommandations](topic:recommendation-order).

La ligne d'un colon à terre est barrée de rouge :

![Ligne d'un colon à terre](downed-row.png)

Ses rôles non exclus et ses affectations épinglées restent inchangés jusqu'à son rétablissement. Rien de ce qu'il possède ne compte pour les besoins de la colonie ; le moteur les couvre donc avec d'autres colons. Chaque pastille de sa liste de Rôles recommandés l'explique :

![Explication pour un colon à terre dans l'infobulle](downed-tooltip.png)
