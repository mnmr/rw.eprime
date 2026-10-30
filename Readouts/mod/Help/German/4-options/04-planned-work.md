---
title: Geplante Arbeiten
---
<!-- l10n:b-7aa1a59c7379 -->
Diese Optionen für geplante Arbeiten ziehen die geschätzten Materialkosten von Aufträgen oder Bauplänen von den angezeigten Anzahlen ab.

<!-- l10n:b-ee40b81e925d -->
- Ressourcen für Auftragsprodukte reservieren zieht die Zutaten ab, die offene Produktionsaufträge benötigen. Eine Zutat wird nur berücksichtigt, wenn der Auftrag dafür genau einen Ressourcentyp zulässt. Ist beispielsweise beliebiges Leder erlaubt, wird nichts reserviert; ist nur eine Lederart erlaubt, wird deren Bedarf berücksichtigt. „X-mal herstellen“ reserviert X Durchläufe, „Herstellen bis X vorhanden“ die nötigen Durchläufe zum Ziel und „Endlos herstellen“ einen Durchlauf. Ausgesetzte Aufträge reservieren nichts.
- Ressourcen für Bauvorhaben reservieren zieht den Restbedarf von Bauplänen und begonnenen Rahmen ab. Bereits abgeliefertes oder von Kolonisten zum Abliefern aufgenommenes Material gilt als ausgegeben und wird nicht erneut abgezogen. Verbotene Baupläne werden ignoriert.
- Fehlmengen als negative Zahlen anzeigen zeigt bei höherem Bedarf als Vorrat die Fehlmenge, statt bei null aufzuhören.
- Unterstützung für Quality Jobs passt aktivierte Reservierungen für Aufträge und Bauvorhaben an die von Quality Jobs verwalteten Qualitätsziele an. Schätzt den Materialbedarf für weitere Versuche und berücksichtigt beim Abbau zurückgewonnene Materialien. Aktiviere dazu die entsprechende Reservierungsoption weiter oben. Ohne EPrime's Quality Jobs oder bei veralteter Version ist die Zeile abgeblendet; der Grund erscheint beim Darüberfahren.

<!-- l10n:planned-work-tooltip-columns -->
Fahre mit der Maus über eine Ressource oder einen Ressourcenpool mit Abzügen für geplante Arbeiten, um die Tabelle „Geplante Arbeiten“ anzuzeigen. „Verbleibende Arbeit“ zählt Rezeptdurchläufe oder Bauvorhaben. „Kosten je Versuch“ gibt den Materialbedarf eines Versuchs an. „Abgezogen“ ist die vom angezeigten Bestand abgezogene Menge. Dabei werden bereits zur Baustelle gelieferte oder dorthin transportierte Materialien sowie bei aktivierter Unterstützung für Quality Jobs die geschätzten Wiederholungsversuche zum Erreichen der Zielqualität berücksichtigt.

<!-- l10n:b-a4ac1a2f64dc -->
Bei aktivierter Fehlmengenanzeige zeigt ein durch geplante Arbeiten unter null gedrückter Zähler den Mangel orange:

<!-- l10n:b-b209bc9139c1 -->
![Materialien mit aktivierten negativen Zahlen](negatives-on.png)

<!-- l10n:b-5325cc201c17 -->
Ausgeschaltet enden dieselben Felder bei null:

<!-- l10n:b-a997f13279d8 -->
![Dieselben Felder ohne negative Zahlen](negatives-off.png)

<!-- l10n:b-f017c3669f5c -->
Geplante Arbeiten werden zusammen mit den Ressourcenbeständen alle paar Sekunden Spielzeit aktualisiert. Neue oder geänderte Aufträge erscheinen bei der nächsten Aktualisierung. Hebe die Pause auf, damit die Spielzeit weiterläuft.
