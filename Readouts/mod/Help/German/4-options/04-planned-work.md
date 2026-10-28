---
title: Geplante Arbeiten
---
<!-- l10n:b-7aa1a59c7379 -->
Diese Optionen ziehen Material ab, das Aufträge oder Baupläne bereits beanspruchen.

<!-- l10n:b-ee40b81e925d -->
- Ressourcen für Auftragsprodukte reservieren zieht die Zutaten ab, die offene Aufträge noch verbrauchen müssen. Eine Zutat wird nur reserviert, wenn sich ihr Bedarf auf genau eine Weise decken lässt. „Beliebiges Leder“ und ähnliche offene Optionen reservieren nichts. „X-mal herstellen“ reserviert X Durchläufe, „Herstellen bis X vorhanden“ die nötigen Durchläufe zum Ziel und „Endlos herstellen“ einen Durchlauf. Ausgesetzte Aufträge reservieren nichts.
- Ressourcen für Bauvorhaben reservieren zieht den Restbedarf von Bauplänen und begonnenen Rahmen ab. Bereits abgeliefertes oder von Kolonisten zum Abliefern aufgenommenes Material gilt als ausgegeben und wird nicht erneut abgezogen. Verbotene Baupläne werden ignoriert.
- Fehlmengen als negative Zahlen anzeigen zeigt bei höherem Bedarf als Vorrat die Fehlmenge, statt bei null aufzuhören.
- Unterstützung für Quality Jobs schätzt die nötigen Versuche für eine Zielqualität und reserviert für alle. Ohne EPrime's Quality Jobs oder bei veralteter Version ist die Zeile abgeblendet; der Grund erscheint beim Darüberfahren.

<!-- l10n:b-a4ac1a2f64dc -->
Bei aktivierter Fehlmengenanzeige zeigt ein durch geplante Arbeiten unter null gedrückter Zähler den Mangel orange:

<!-- l10n:b-b209bc9139c1 -->
![Materialien mit aktivierten negativen Zahlen](negatives-on.png)

<!-- l10n:b-5325cc201c17 -->
Ausgeschaltet enden dieselben Felder bei null:

<!-- l10n:b-a997f13279d8 -->
![Dieselben Felder ohne negative Zahlen](negatives-off.png)

<!-- l10n:b-f017c3669f5c -->
Geplante Arbeiten werden etwa alle siebzehn Sekunden Spielzeit neu erfasst. So lange kann es dauern, bis ein neuer Auftrag in den Anzahlen erscheint.
