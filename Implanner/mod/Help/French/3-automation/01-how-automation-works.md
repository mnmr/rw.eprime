---
title: Fonctionnement de l'automatisation
---
<!-- l10n:b-44e89be1e85f -->
L'automatisation examine la colonie quelques fois par heure de jeu ; chaque modification de votre part déclenche un passage supplémentaire immédiat :

<!-- l10n:b-6eb19bb9f96f -->
1. Compter les implants manquants des colons affectés.
2. Programmer des ordres pour les implants fabricables (voir [Options de production](topic:production-options)).
3. Réserver des objets terminés du stock pour des colons précis, selon les priorités et les niveaux.
4. Quand tout ce dont un colon a besoin ensuite est réservé et sur place, ajouter les opérations en un bloc après les ordres déjà présents dans sa liste.

<!-- l10n:b-a747349bea45 -->
Le résumé de colonie en haut de Vue d'ensemble indique si l'automatisation est active, ce que la production fabrique ou attend, les implants en stock et encore en attente, et le prochain colon à opérer. Survolez les chiffres de production ou de chirurgie pour les détails par implant, y compris ce qui bloque chaque objet.

<!-- l10n:b-8fea0b171c65 -->
![Résumé de colonie avec production et chirurgie](colony-next.png)

<!-- l10n:b-26d1852e16fa -->
Les lots retardent la chirurgie afin de poser plusieurs implants sous une même anesthésie. Un lot est tout ce dont le colon a besoin ensuite : son niveau actuel ou son plan complet, selon la stratégie (voir [Options de chirurgie](topic:surgery-options)). Une stratégie ignore les lots et programme chaque implant dès qu'il est réservé et sur place.

<!-- l10n:b-6b9f966652bc -->
Les opérations attendent que le colon soit apte à la chirurgie. Les colonies ne prennent jamais dans les stocks les unes des autres : les implants sont réservés et posés là où vit le colon.
