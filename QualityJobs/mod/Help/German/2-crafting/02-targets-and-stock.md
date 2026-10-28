---
title: Zielqualität und Bestandsobergrenzen
---
<!-- l10n:target -->
Mit **{k:QJ_MinQualityLabel}** wird ein Auftrag mit fester Wiederholungszahl so lange wiederholt, bis genügend Gegenstände dieser oder einer höheren Qualität hergestellt wurden. Ein Gegenstand unterhalb der Zielqualität wird behalten, verbraucht aber keine der verbleibenden Wiederholungen des Auftrags. Jeder zusätzliche Versuch benötigt weiteres Material. **{k:QJ_AnyQuality}** akzeptiert jedes Ergebnis.

<!-- l10n:repeat-modes -->
Diese zusätzlichen Versuche gelten für **X-mal herstellen**. Bei **Herstellen bis X** legst du mit dem normalen Qualitätsbereich des Auftrags fest, was zum gewünschten Bestand zählt. **Endlos herstellen** läuft wie gewohnt weiter. Eine Zielqualität allein wählt keinen besseren Arbeiter für die Fertigstellung aus; lege auch die Anforderungen für die Fertigstellung fest.

<!-- l10n:stock -->
Die **Bestandsobergrenze** begrenzt den Beginn neuer Arbeiten an einem Produkt auf dieser Karte. Seine unfertigen Gegenstände werden über alle Aufträge und Werkbänke hinweg zusammengezählt, aber nur verwaltete Aufträge halten die Obergrenze ein. **0 bedeutet unbegrenzt.** Fertige Gegenstände im Lager zählen nicht mit.

<!-- l10n:cap-behavior -->
Ist die Obergrenze erreicht, beginnen verwaltete Aufträge keine neuen Gegenstände mehr. Bereits begonnene Gegenstände dürfen weiterhin bearbeitet oder fertiggestellt werden, sodass die Warteschlange abgearbeitet werden kann. Erfüllt niemand die Anforderungen für die Fertigstellung, kannst du diese senken oder dafür sorgen, dass ein geeigneter Arbeiter wieder verfügbar ist.

