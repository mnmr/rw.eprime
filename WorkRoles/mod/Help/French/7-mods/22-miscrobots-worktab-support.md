---
title: MiscRobots WorkTab support
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **Verdict** | ![Entièrement compatible](verdict-blue.png|16) Entièrement compatible (prise en charge dédiée) |
| **Preuve** | Analyse du code source, test automatisé en jeu |
| **Vérifié le** | 2026-11-02 |
| **Versions** | WorkRoles 1.7.9, MiscRobots WorkTab support [Workshop 2271338001](https://steamcommunity.com/sharedfiles/filedetails/?id=2271338001), fichiers du 2026-11-02 |

<!-- l10n:summary -->
Les robots suivent leur onglet Travail, et WorkRoles y lit ce que chaque modèle
peut faire.

<!-- l10n:detail -->
Avec cet add-on, un robot de Misc. Robots choisit son travail comme un colon,
d'après les priorités de l'onglet Travail, et chaque type de travail déclaré par son modèle est considéré comme
possible, quelle que soit sa priorité déclarée. WorkRoles applique cette règle pour les
vérifications de capacité, les recommandations et les pastilles de rôle : ainsi, un robot Artisan ou Omni de Misc. Robots++
peut faire de la recherche et tous les modèles peuvent combattre les incendies via
leurs rôles, au lieu d'afficher une pastille critique.

