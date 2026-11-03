---
title: EPrime's Quality Jobs
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **Verdict** | ![Entièrement compatible](verdict-blue.png|16) Entièrement compatible (prise en charge dédiée) |
| **Preuves** | Analyse du code source, test automatisé en jeu, tests en conditions de jeu |
| **Vérifié le** | 2026-09-27 |
| **Versions** | Implanner 1.1.5, Quality Jobs 1.1.4, [Workshop 3776722051](https://steamcommunity.com/sharedfiles/filedetails/?id=3776722051) |

<!-- l10n:summary -->
Les ordres de production peuvent porter la qualité minimale d'un plan.

<!-- l10n:detail -->
Lorsqu'un plan définit une [qualité minimale](topic:minimum-quality) supérieure à Épouvantable et que la qualité de l'implant n'est pas déterminée par un ingrédient, Implanner transmet l'ordre de production à Quality Jobs avec cet objectif : l'établi continue donc d'essayer jusqu'à obtenir un implant suffisamment bon pour le plan. Sans Quality Jobs, un implant fabriqué sous le minimum ne comble simplement pas l'emplacement et il est fabriqué de nouveau.

