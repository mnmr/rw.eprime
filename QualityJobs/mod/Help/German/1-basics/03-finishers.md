---
title: Arbeiter für die Fertigstellung auswählen
---
<!-- l10n:requirements -->
Diese Einstellungen sind bei Aufträgen und Gebäuden verfügbar. Andere geeignete Arbeiter können die Vorarbeit übernehmen; nur für die Fertigstellung müssen deine Anforderungen erfüllt sein.

<!-- l10n:controls -->
- **Fertigstellen ab Stufe** legt die Mindestfertigkeitsstufe fest.
- **{k:QJ_AutoBest}** wählt Arbeiter anhand der erwarteten Qualität statt deiner manuell festgelegten Mindestfertigkeitsstufe aus. Die manuelle Reglereinstellung bleibt erhalten, falls du diese Option deaktivierst.
- **{k:QJ_RequireInspired}** wartet auf diese Inspiration. Die Fertigstellung verbraucht sie wie üblich.
- **{k:QJ_RequireSpecialist}** setzt eine Ideology-Rolle voraus, die einen Bonus auf die Produktionsqualität gewährt. Diese Option wird mit Ideology angezeigt.

<!-- l10n:odds-image -->
![Anforderungen an Fertigsteller und Vorschau der Qualitätschancen](finisher-options.png)

<!-- l10n:best -->
Die automatische Auswahl vergleicht geeignete Kolonisten, Mechs der Kolonie und unterstützte Roboter und wählt den Arbeiter mit der höchsten erwarteten Qualität. Dabei zählen die Fertigkeit, Inspirierte Kreativität, Boni von Produktionsspezialisten und Qualitätsboni unterstützter Mods. So wird die durchschnittliche Qualität maximiert; das Ergebnis eines einzelnen Qualitätswurfs ist nicht garantiert.

<!-- l10n:availability -->
Arbeiter, die ansonsten die Voraussetzungen erfüllen, bleiben im Vergleich, während sie abwesend, niedergeschlagen, eingeschlafen, in einem Nervenzusammenbruch oder beim Aufladen sind. Das gilt auch für Roboter in ihren Ladestationen. Kann keiner der bestplatzierten Arbeiter auf dieser Karte fertigstellen, wartet die Arbeit. Beispielsweise kann ein besserer Handwerker auf einer Karawane verhindern, dass ein schlechter platzierter Arbeiter zu Hause fertigstellt. Es gibt keine Wartefrist, nach der die Qualitätsanforderung sinkt.

<!-- l10n:manual-threshold -->
Ist **{k:QJ_AutoBest}** deaktiviert, kann jeder zur Arbeit fähige Arbeiter fertigstellen, der deine manuell festgelegte Mindestfertigkeitsstufe und die übrigen Anforderungen erfüllt. Ein besserer Arbeiter anderswo in der Kolonie blockiert ihn nicht.

<!-- l10n:robot-finishers -->
Mechs und unterstützte Roboter müssen dieselben Anforderungen für die Fertigstellung erfüllen wie Kolonisten. Wird die Inspiration „Inspirierte Kreativität“ oder eine Rolle als Produktionsspezialist vorausgesetzt, bleiben Arbeiter ohne diese weiterhin ausgeschlossen. Deaktivierst du **{k:QJ_SettingsAllowRobotPreparation}**, bleibt die Vorarbeit Kolonisten vorbehalten; geeignete Mechs und Roboter können weiterhin Fertigstellungsaufträge von Quality Jobs übernehmen.

<!-- l10n:odds -->
In der Tabelle **{k:QJ_OddsHeader}** eines Auftrags schätzt **{k:QJ_OddsColConfig}** die Wahrscheinlichkeiten anhand deiner Mindestfertigkeitsstufe für die Fertigstellung und der vorausgesetzten Boni. Ist **{k:QJ_AutoBest}** aktiviert und ein geeigneter Kandidat vorhanden, zeigt die Spalte stattdessen die Wahrscheinlichkeiten für diesen kolonieweit ausgewählten Arbeiter. Gibt es keinen Kandidaten, werden deine manuellen Einstellungen verwendet; das bedeutet nicht, dass ein Fertigsteller verfügbar ist.

<!-- l10n:odds-best -->
**{k:QJ_OddsColBest}** zeigt die Wahrscheinlichkeiten für den bestplatzierten arbeitsfähigen Arbeiter auf dieser Karte, ohne deine zusätzlichen Anforderungen an Fertigsteller zu berücksichtigen. Die beiden Spalten können sich daher auf unterschiedliche Arbeiter beziehen. Dies sind Wahrscheinlichkeiten, keine Garantien; der Arbeiter, der tatsächlich fertigstellt, bestimmt das Ergebnis.

<!-- l10n:eligible-finishers -->
**Geeignete Fertigsteller** listet Arbeiter auf, die die Anforderungen derzeit auf dieser Karte erfüllen können; abwesende, niedergeschlagene Arbeiter und solche mit einem Nervenzusammenbruch werden nicht aufgeführt. Bei Aufträgen gelten für diese Liste die Anforderungen des ausgewählten Auftrags, obwohl die Warteschlangenzahlen auch andere Aufträge für dasselbe Produkt auf dieser Karte umfassen.

<!-- l10n:priority -->
**{k:QJ_SettingsHighPriorityFinish}** sorgt dafür, dass zugewiesene Fertigstellungsarbeiten vor anderen Arbeiten erledigt werden, ausgenommen Brandbekämpfung, medizinische Versorgung, Rettung und Bettruhe. Die aktuelle Tätigkeit wird dabei nicht unterbrochen. Ist die Option deaktiviert, hat die Fertigstellung innerhalb ihrer Arbeitsart Vorrang, nach allen höher priorisierten Arbeitsarten. Der Arbeiter muss die normale Arbeitsart des Gegenstands ausführen können.

