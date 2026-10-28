---
title: Travaux prévus
---
<!-- l10n:b-7aa1a59c7379 -->
Ces options déduisent les matériaux déjà réclamés par les ordres ou les plans de construction.

<!-- l10n:b-ee40b81e925d -->
- Réserver les ressources des ordres de production déduit les ingrédients que les ordres en attente doivent encore consommer. Un ingrédient n'est réservé que s'il existe exactement une façon de satisfaire son besoin. « N'importe quel cuir » et les autres choix ouverts ne réservent rien. Un ordre « faire X fois » réserve X fabrications ; « faire jusqu'à X » réserve celles nécessaires à la cible ; « faire indéfiniment » en réserve une. Les ordres suspendus ne réservent rien.
- Réserver les ressources des constructions déduit les matériaux encore nécessaires aux plans et structures inachevées. Ceux déjà livrés, ou ramassés par un colon pour livraison, sont considérés comme dépensés et ne sont pas déduits à nouveau. Les plans interdits sont ignorés.
- Afficher les déficits en nombres négatifs indique le manque lorsque les travaux prévus dépassent vos stocks, au lieu de s'arrêter à zéro.
- Prise en charge de Quality Jobs estime le nombre d'essais requis pour atteindre la qualité cible et réserve pour tous. La ligne est grisée si EPrime's Quality Jobs est absent ou trop ancien ; le survol en donne la raison.

<!-- l10n:b-a4ac1a2f64dc -->
Avec les nombres négatifs activés, un compteur poussé sous zéro par le travail prévu affiche le déficit en orange :

<!-- l10n:b-b209bc9139c1 -->
![Matériaux avec nombres négatifs activés](negatives-on.png)

<!-- l10n:b-5325cc201c17 -->
Sans cette option, les mêmes cases s'arrêtent à zéro :

<!-- l10n:b-a997f13279d8 -->
![Mêmes cases avec nombres négatifs désactivés](negatives-off.png)

<!-- l10n:b-f017c3669f5c -->
Les travaux prévus sont réexaminés environ toutes les dix-sept secondes de temps de jeu : un nouvel ordre peut donc attendre ce délai avant d'apparaître dans les quantités.
