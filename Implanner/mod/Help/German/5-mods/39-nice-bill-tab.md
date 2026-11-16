---
title: Nice Bill Tab
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **Urteil** | ![Vollständig kompatibel](verdict-blue.png|16) Voll kompatibel (eigene Unterstützung) |
| **Nachweis** | Quellcode-Analyse, automatisierter Test im Spiel |
| **Geprüft** | 2026-11-16 |
| **Versionen** | Implanner 1.3.0, Nice Bill Tab [Workshop 3520130671](https://steamcommunity.com/sharedfiles/filedetails/?id=3520130671), Dateien 2026-11-13 |

<!-- l10n:summary -->
Eine Implanner-Werkbank zeigt ihre Produktionswarteschlange, und das Kontrollkästchen der Werkbank hat eine eigene Zeile.

<!-- l10n:toggle -->
An einer Werkbank, die Implantate herstellen kann, steht das Kontrollkästchen **Implanner-Werkbank** in einer eigenen Zeile oben in der Auftragsliste von Nice Bill Tab, über dem Suchfeld. Werkbänke, die keine Implantate herstellen, zeigen Nice Bill Tab unverändert.

<!-- l10n:vanilla-tab -->
Eine als Implanner-Werkbank markierte Werkbank zeigt statt der Ansicht von Nice Bill Tab die Implanner-Produktionswarteschlange, denselben Reiter wie ohne Nice Bill Tab (siehe [Produktionsoptionen](topic:production-options)). Der eigene Schalter und die Einstellungsschaltfläche von Nice Bill Tab werden in diesem Reiter nicht angezeigt. Deaktiviere das Kontrollkästchen, und die Ansicht von Nice Bill Tab kehrt für diese Werkbank zurück.

<!-- l10n:stray-bills -->
Nice Bill Tab kann für eine fehlende Zutat einen Auftrag an einer verfügbaren Werkbank anlegen. Implanner-Werkbänke werden dabei übergangen, sodass der Auftrag an die nächstgelegene andere Werkbank geht, die den Gegenstand herstellen kann. Können ihn nur Implanner-Werkbänke herstellen, wird kein Auftrag angelegt.
