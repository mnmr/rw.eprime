---
title: Unter der Haube
---
Die geordneten Rollen jedes Kolonisten werden in eine eindeutige Reihenfolge von Arbeiten übersetzt. Das Spiel erhält diese über dieselben Listen, die schon die normale Arbeitsauswahl verwendet. Die KI der Bauern verhält sich genau wie bei einer von Hand abgestimmten Prioritätentabelle, ohne Tick-Patches oder Berechnungen in jedem Bild.

Bei Änderungen an Zuweisungen oder Rollen wird alles neu berechnet und anschließend zwischengespeichert. Bedingte Rollen werden zusätzlich zu vollen Stunden und bei einem Standortwechsel des Kolonisten aktualisiert. Notfallarbeiten wie Feuerlöschen werden über den Notfalldurchlauf des Spiels ausgeführt, wenn eine der ersten fünf aktivierten Rollen des Kolonisten sie abdeckt. Andernfalls bleiben sie an ihrer normalen Position. Ist „Nur die ersten 5 Rollen unterbrechen Tätigkeiten bei Notfällen“ ausgeschaltet, verwendet jede abgedeckte Notfallarbeit diesen Durchlauf.

WorkRoles befüllt den normalen Prioritätenspeicher des Spiels anhand deiner Rollen. Solange die standardmäßig aktivierte Option für den normalen Wertebereich eingeschaltet ist, werden diese auf den vertrauten Bereich von 0 bis 4 abgebildet. Deshalb lässt sich die Mod sicher entfernen: Der normale Arbeitsreiter kehrt zurück und enthält deine in Prioritäten umgewandelten Rollen. Beim Hinzufügen der Mod zu einem bestehenden Spielstand erfolgt die Umwandlung in die andere Richtung.
