---
title: Travaux prévus
---
<!-- l10n:b-7aa1a59c7379 -->
Les options de travaux prévus déduisent des quantités affichées le coût estimé en matériaux des ordres de production ou des plans de construction.

<!-- l10n:b-ee40b81e925d -->
- Réserver les ressources des ordres de production déduit les ingrédients nécessaires aux ordres de production en attente. Un ingrédient n'est comptabilisé que si l'ordre n'autorise qu'un seul type de ressource pour cet ingrédient. Par exemple, autoriser n'importe quel cuir ne réserve rien ; n'autoriser qu'un seul type de cuir inclut le coût de ce cuir. Un ordre « faire X fois » réserve X fabrications ; « faire jusqu'à X » réserve celles nécessaires à la cible ; « faire indéfiniment » en réserve une. Les ordres suspendus ne réservent rien.
- Réserver les ressources des constructions déduit les matériaux encore nécessaires aux plans et structures inachevées. Ceux déjà livrés, ou ramassés par un colon pour livraison, sont considérés comme dépensés et ne sont pas déduits à nouveau. Les plans interdits sont ignorés.
- Afficher les déficits en nombres négatifs indique le manque lorsque les travaux prévus dépassent vos stocks, au lieu de s'arrêter à zéro.
- Prise en charge de Quality Jobs ajuste les réservations activées pour les ordres et les constructions aux qualités cibles gérées par Quality Jobs. Estime les matériaux nécessaires aux tentatives supplémentaires et tient compte des matériaux récupérés lors des déconstructions. Activez l’option de réservation correspondante ci-dessus pour l’utiliser. La ligne est grisée si EPrime's Quality Jobs est absent ou trop ancien ; le survol en donne la raison.

<!-- l10n:planned-work-tooltip-columns -->
Survolez une ressource ou un ensemble de ressources faisant l'objet de déductions pour les travaux prévus pour afficher le tableau Travaux prévus. Travail restant indique le nombre de fabrications ou de constructions à réaliser. Coût unitaire indique le coût en matériaux d'un essai. Déduit indique la quantité soustraite au total affiché, en tenant compte des matériaux déjà livrés ou en cours de transport vers les constructions, ainsi que des essais supplémentaires estimés pour atteindre la qualité cible si la prise en charge de Quality Jobs est activée.

<!-- l10n:b-a4ac1a2f64dc -->
Avec les nombres négatifs activés, un compteur poussé sous zéro par le travail prévu affiche le déficit en orange :

<!-- l10n:b-b209bc9139c1 -->
![Matériaux avec nombres négatifs activés](negatives-on.png)

<!-- l10n:b-5325cc201c17 -->
Sans cette option, les mêmes cases s'arrêtent à zéro :

<!-- l10n:b-a997f13279d8 -->
![Mêmes cases avec nombres négatifs désactivés](negatives-off.png)

<!-- l10n:b-f017c3669f5c -->
Les travaux prévus sont actualisés avec les quantités de ressources toutes les quelques secondes de temps de jeu. Les ordres nouveaux ou modifiés apparaissent à la prochaine actualisation ; reprenez le jeu pour laisser le temps avancer.
