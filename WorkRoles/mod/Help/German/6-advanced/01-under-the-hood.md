---
title: Unter der Haube
---
<!-- l10n:b-5dd60ada4d2e -->
Die geordneten Rollen jedes Kolonisten werden in eine eindeutige Reihenfolge von Arbeiten übersetzt. Das Spiel erhält diese über dieselben Listen, die schon die normale Arbeitsauswahl verwendet. Das Arbeitsverhalten entspricht einer von Hand abgestimmten Prioritätentabelle, ohne Tick-Patches oder Berechnungen in jedem Bild.

<!-- l10n:b-03b6b823d064 -->
Bei Änderungen an Zuweisungen oder Rollen wird alles neu berechnet und anschließend zwischengespeichert. Bedingte Rollen werden zusätzlich zu vollen Stunden und bei einem Standortwechsel des Kolonisten aktualisiert. Notfallarbeiten wie Feuerlöschen werden über den Notfalldurchlauf des Spiels ausgeführt, wenn eine der ersten fünf aktivierten Rollen des Kolonisten sie abdeckt. Andernfalls bleiben sie an ihrer normalen Position. Ist „Nur die ersten 5 Rollen unterbrechen Tätigkeiten bei Notfällen“ ausgeschaltet, verwendet jede abgedeckte Notfallarbeit diesen Durchlauf.

<!-- l10n:b-29b79c3efacf -->
WorkRoles befüllt den normalen Prioritätenspeicher des Spiels anhand deiner Rollen. Solange die standardmäßig aktivierte Option für den normalen Wertebereich eingeschaltet ist, werden diese auf den vertrauten Bereich von 0 bis 4 abgebildet. Deshalb lässt sich die Mod sicher entfernen: Der normale Arbeitsreiter kehrt zurück und enthält deine in Prioritäten umgewandelten Rollen. Beim Hinzufügen der Mod zu einem bestehenden Spielstand erfolgt die Umwandlung in die andere Richtung.
