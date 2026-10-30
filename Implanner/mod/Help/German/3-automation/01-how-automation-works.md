---
title: So funktioniert die Automatisierung
---
<!-- l10n:b-44e89be1e85f -->
Die Automatik prüft die Kolonie ungefähr zweimal pro Spielstunde. Nach einer Bearbeitung prüft sie erneut, sobald die Spielzeit weiterläuft. Hebe die Pause auf, damit die Automatik die Änderungen umsetzt. Jeder Durchlauf umfasst diese Schritte:

<!-- l10n:b-6eb19bb9f96f -->
1. Zählen, was zugewiesenen Kolonisten noch fehlt.
2. Fertigungsaufträge für herstellbare Implantate einreihen (siehe [Produktionsoptionen](topic:production-options)).
3. Fertige Lagergegenstände nach Priorität und Stufe für bestimmte Kolonisten reservieren.
4. Sobald alles als Nächstes Benötigte reserviert und vor Ort ist, die Operationen als Block hinter bestehende Aufträge des Kolonisten setzen.

<!-- l10n:b-a747349bea45 -->
Die Zusammenfassung oben in der Übersicht zeigt dies alles: Automatisierungszustand, laufende Produktion oder Wartegründe, vorhandene und noch herzustellende Implantate sowie den nächsten Operationspatienten. Fahre über Produktions- oder Operationszahlen für Details je Implantat einschließlich Wartegrund.

<!-- l10n:production-counts -->
Im Produktions-Tooltip zählt **Noch herzustellen** die benötigten Implantate, die noch nicht durch den Vorrat abgedeckt sind, auch wenn dafür noch kein Fertigungsauftrag besteht. **In Fertigungsaufträgen** zählt die erwartete Ausgabemenge bestehender Implanner-Aufträge, unabhängig davon, ob sie warten oder bereits bearbeitet werden.

<!-- l10n:b-8fea0b171c65 -->
![Koloniezusammenfassung mit Produktions- und Operationsstatus](colony-next.png)

<!-- l10n:b-26d1852e16fa -->
Jedes Implantat benötigt weiterhin eine eigene Operation. Gruppenbildung verzögert Operationen, damit mehrere Implantate während einer Narkose eingesetzt werden können und weniger einzelne Narkosen nötig sind. Eine Gruppe umfasst alles, was der Kolonist als Nächstes benötigt: je nach Strategie die aktuelle Stufe oder den ganzen Plan (siehe [Operationsoptionen](topic:surgery-options)). Eine Strategie verzichtet ganz darauf und plant jedes Implantat sofort ein, sobald es reserviert und vor Ort ist.

<!-- l10n:b-6b9f966652bc -->
Operationen warten, bis der Kolonist dafür gesund genug ist. Kolonien nehmen nie Gegenstände aus fremden Kolonievorräten: Reservierung und Einbau erfolgen am Wohnort des Kolonisten.
