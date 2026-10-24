---
title: So funktioniert die Automatisierung
---
Die Automatik prüft die Kolonie einige Male pro Spielstunde. Jede Bearbeitung löst zusätzlich sofort einen Durchlauf aus:

1. Zählen, was zugewiesenen Kolonisten noch fehlt.
2. Fertigungsaufträge für herstellbare Implantate einreihen (siehe [Produktionsoptionen](topic:production-options)).
3. Fertige Lagergegenstände nach Priorität und Stufe für bestimmte Kolonisten reservieren.
4. Sobald alles als Nächstes Benötigte reserviert und vor Ort ist, die Operationen als Block hinter bestehende Aufträge des Kolonisten setzen.

Die Zusammenfassung oben in der Übersicht zeigt dies alles: Automatisierungszustand, laufende Produktion oder Wartegründe, vorhandene und eingereihte Implantate sowie den nächsten Operationspatienten. Fahre über Produktions- oder Operationszahlen für Details je Implantat einschließlich Wartegrund.

![Koloniezusammenfassung mit Produktions- und Operationsstatus](colony-next.png)

Gruppenbildung verzögert Operationen, damit mehrere Implantate während einer Narkose eingesetzt werden. Eine Gruppe umfasst je nach Strategie die aktuelle Stufe oder den ganzen Plan des Kolonisten (siehe [Operationsoptionen](topic:surgery-options)). Eine Strategie verzichtet ganz darauf und plant jedes Implantat sofort ein, sobald es reserviert und vor Ort ist.

Operationen warten, bis der Kolonist dafür gesund genug ist. Kolonien nehmen nie Gegenstände aus fremden Kolonievorräten: Reservierung und Einbau erfolgen am Wohnort des Kolonisten.
