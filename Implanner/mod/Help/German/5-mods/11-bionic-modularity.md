---
title: Bionic modularity
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **Urteil** | ![Voll kompatibel](verdict-blue.png|16) Voll kompatibel (eigene Unterstützung) |
| **Nachweis** | Quellcode-Analyse, automatisierter Test im Spiel |
| **Geprüft** | 2026-09-05 |
| **Versionen** | Implanner 1.1.0, Bionic modularity [Workshop 3589154549](https://steamcommunity.com/sharedfiles/filedetails/?id=3589154549), Dateien 2026-09-05 |

<!-- l10n:summary -->
Module teilen sich eine bionische Gliedmaße mit ihr, und ein Gliedmaßenmodul fragt nach seinem Träger.

<!-- l10n:b-c7cd0a958354 -->
![Die Abfrage der Voraussetzung für ein Beinmodul](requirement-prompt.png)

<!-- l10n:b-71ab3bae9608 -->
![Der Plan nach „Zum Plan hinzufügen“: Das bionische Bein und das Modul sind beide ausgewählt](requirement-added.png)

<!-- l10n:b-1a485abf9e8c -->
Jedes Modul belegt einen Platz an seinem Körperteil. Ein Kampfmodul und ein Arbeitsmodul passen daher an denselben Arm, zwei Kampfmodule dagegen nicht. Module für Gliedmaßen werden auf eine bionische Gliedmaße montiert und teilen sich das Körperteil mit ihr, sodass ein geplanter bionischer Arm und ein geplantes Armmodul nebeneinander bestehen. Wenn du ein Gliedmaßenmodul anhakst, obwohl der Plan an diesem Körperteil kein Bionik-Implantat enthält, öffnet sich eine Abfrage mit den Bionik-Implantaten, die es aufnehmen können. Standardmäßig ist das am wenigsten fortschrittliche ausgewählt. **Zum Plan hinzufügen** fügt deine Auswahl zusammen mit dem Modul hinzu. **Abbrechen** fügt nichts hinzu.

