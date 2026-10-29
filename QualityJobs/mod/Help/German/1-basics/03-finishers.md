---
title: Arbeiter für die Fertigstellung auswählen
---
<!-- l10n:requirements -->
Diese Einstellungen sind bei Aufträgen und Gebäuden verfügbar. Andere geeignete Arbeiter können die Vorarbeit übernehmen; nur für die Fertigstellung müssen deine Anforderungen erfüllt sein.

<!-- l10n:controls -->
- **Fertigstellen ab Stufe** legt die Mindestfertigkeitsstufe fest.
- **{k:QJ_AutoBest}** wählt automatisch den besten geeigneten Arbeiter. Die manuelle Reglereinstellung bleibt erhalten, falls du diese Option deaktivierst.
- **{k:QJ_RequireInspired}** wartet auf diese Inspiration. Die Fertigstellung verbraucht sie wie üblich.
- **{k:QJ_RequireSpecialist}** setzt eine Ideology-Rolle voraus, die einen Bonus auf die Produktionsqualität gewährt. Diese Option wird mit Ideology angezeigt.

<!-- l10n:odds-image -->
![Anforderungen an Fertigsteller und Vorschau der Qualitätschancen](finisher-options.png)

<!-- l10n:best -->
Die automatische Anpassung vergleicht geeignete Kolonisten, Mechs der Kolonie und unterstützte Roboter und wählt den Arbeiter mit der höchsten erwarteten Qualität. Dabei zählen die Fertigkeit, Inspirierte Kreativität, Boni von Produktionsspezialisten und Qualitätsboni unterstützter Mods. So wird die durchschnittliche Qualität maximiert; das Ergebnis eines einzelnen Qualitätswurfs ist nicht garantiert.

<!-- l10n:availability -->
Arbeiter, die ansonsten die Voraussetzungen erfüllen, bleiben im Vergleich, während sie abwesend, niedergeschlagen, eingeschlafen, in einem Nervenzusammenbruch oder beim Aufladen sind. Das gilt auch für Roboter in ihren Ladestationen. Kann keiner der bestplatzierten Arbeiter auf dieser Karte fertigstellen, wartet die Arbeit. Beispielsweise kann ein besserer Handwerker auf einer Karawane verhindern, dass ein schlechter platzierter Arbeiter zu Hause fertigstellt. Es gibt keine Wartefrist, nach der die Qualitätsanforderung sinkt.

<!-- l10n:manual-threshold -->
Ist die automatische Anpassung deaktiviert, kann jeder zur Arbeit fähige Arbeiter fertigstellen, der deine manuell festgelegte Mindestfertigkeitsstufe und die übrigen Anforderungen erfüllt. Ein besserer Arbeiter anderswo in der Kolonie blockiert ihn nicht.

<!-- l10n:robot-finishers -->
Mechs und unterstützte Roboter müssen dieselben Anforderungen für die Fertigstellung erfüllen wie Kolonisten. Wird die Inspiration „Inspirierte Kreativität“ oder eine Rolle als Produktionsspezialist vorausgesetzt, bleiben Arbeiter ohne diese weiterhin ausgeschlossen. Deaktivierst du **{k:QJ_SettingsAllowRobotPreparation}**, bleibt die Vorarbeit Kolonisten vorbehalten; geeignete Mechs und Roboter können weiterhin Fertigstellungsaufträge von Quality Jobs übernehmen.

<!-- l10n:odds -->
**{k:QJ_OddsHeader}** vergleicht in den Spalten **{k:QJ_OddsColConfig}** und **{k:QJ_OddsColBest}** deine Einstellungen mit dem derzeit besten Kandidaten. Dies sind Wahrscheinlichkeiten, keine Garantien. **Geeignete Fertigsteller** listet Arbeiter auf, die die Anforderungen derzeit auf dieser Karte erfüllen können; abwesende, niedergeschlagene Arbeiter und solche mit einem Nervenzusammenbruch werden nicht aufgeführt.

<!-- l10n:priority -->
**{k:QJ_SettingsHighPriorityFinish}** sorgt dafür, dass zugewiesene Fertigstellungsarbeiten vor anderen Arbeiten erledigt werden, ausgenommen Brandbekämpfung, medizinische Versorgung, Rettung und Bettruhe. Die aktuelle Tätigkeit wird dabei nicht unterbrochen. Ist die Option deaktiviert, hat die Fertigstellung innerhalb ihrer Arbeitsart Vorrang, nach allen höher priorisierten Arbeitsarten. Der Arbeiter muss die normale Arbeitsart des Gegenstands ausführen können.

