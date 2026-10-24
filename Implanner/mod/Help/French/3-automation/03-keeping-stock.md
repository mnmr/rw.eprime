---
title: Conserver du stock
---
Deux réglages empêchent l'automatisation de vider vos réserves.

**Conserver en stock** fixe les minima de ressources pour la production. Un ordre n'est ajouté que si la colonie peut le payer en plus des matériaux encore nécessaires aux ordres en attente, tout en gardant au moins la quantité configurée de chaque ingrédient. Acier à 2000 signifie que la fabrication d'implants ne descend jamais sous 2000. Si vous dépensez des matériaux et qu'un ordre non commencé ne respecte plus ce budget, Implanner le retire et le remet lorsque le stock revient.

![Stocks minimaux de ressources](keep-in-stock.png)

**Implants mis de côté** conserve des implants terminés pour un usage manuel. Mettez un bras bionique de côté : l'automatisation n'affecte que les bras supplémentaires, en gardant un exemplaire libre pour les urgences ou le commerce. La production considère toujours un objet mis de côté comme manquant, afin de renouveler le stock.
Les ordres d'amélioration préservent aussi les implants mis de côté et ceux nécessaires aux opérations prévues. Seuls les implants excédentaires peuvent servir d'ingrédients.

![Mise de côté d'un bras bionique](implant-reservations.png)
