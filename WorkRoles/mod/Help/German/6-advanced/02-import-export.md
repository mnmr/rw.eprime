---
title: Import und Export
---
<!-- l10n:b-63a50794a977 -->
Mit den Schaltflächen in der Ecke des Reiters „Rollen“ kannst du Rollenkonfigurationen zwischen Spielständen und mit Freunden austauschen:

<!-- l10n:b-4f44ec3259f6 -->
![Die Schaltflächen Importieren, Exportieren und Standards wiederherstellen](io-buttons.png)

<!-- l10n:b-fcca50ae80e8 -->
Der Export schreibt die vollständige Rollenkonfiguration in eine Datei oder die Zwischenablage: Rollen, Gruppen und deren Reihenfolge, benutzerdefinierte Symbole, Palettenfarben (auch ungenutzte Farben), Regeln, Ausbildungspfade, Einstellungen für Empfehlungen und spielstandweite Optionen. Deine Anzeigeeinstellungen sind ebenfalls enthalten und können optional importiert werden. Kolonistenzuweisungen, Auftragsbindungen und der vorübergehende Fensterzustand bleiben beim aktuellen Spielstand bzw. der aktuellen Sitzung.

<!-- l10n:b-0420bcf88983 -->
![Der Exportdialog](export-dialog.png)

<!-- l10n:b-9a255dfe5c80 -->
Der Import liest eine Datei oder die Zwischenablage:

<!-- l10n:b-8a2c8b9bcf40 -->
![Der Importdialog](import-dialog.png)

<!-- l10n:b-c5c6fbb19706 -->
Eine Vorschau bietet anschließend für jeden Abschnitt „Zusammenführen“ oder „Überschreiben“ an. Zusammenführen ergänzt und aktualisiert, ohne etwas zu löschen. Überschreiben gleicht deine Konfiguration vollständig an die Datei an. Bevor etwas geschieht, zeigt die Vorschau genau, welche Rollen erstellt, geändert oder entfernt würden. Überschreiben stellt auch die Rollenreihenfolge aus der Datei wieder her. Spielstandweite Optionen und deine Anzeigeeinstellungen haben separate Kontrollkästchen; Anzeigeeinstellungen sind standardmäßig abgewählt. Ältere Dateien werden weiterhin unterstützt. Fehlen Abschnitte in einer Datei, bleiben die entsprechenden Einstellungen unverändert.

<!-- l10n:b-67c0a3e8f921 -->
Um eine Konfiguration in künftigen Kolonien wiederzuverwenden, öffne **Rollen → Standards festlegen** und wähle **Diese Konfiguration verwenden**. Neue Spiele beginnen mit diesen Rollen und Optionen; beim Laden eines bestehenden Spielstands bleibt dessen eigene Konfiguration erhalten. Wenn du die persönliche Standardkonfiguration löschst, wird für künftige Spiele die mitgelieferte Ausgangskonfiguration wiederhergestellt.

<!-- l10n:b-3fe7c915a062 -->
Beim Speichern einer Standardkonfiguration kannst du automatische Aktualisierungen aktivieren. Wenn du eine dafür bestätigte Konfiguration bearbeitest, werden die Änderungen beim Schließen des Editors in die Standardkonfiguration übernommen. Öffnest du den Editor in einem Spielstand mit abweichender Konfiguration, wirst du gefragt, ob du dessen Konfiguration übernehmen, die gespeicherte Standardkonfiguration beibehalten oder automatische Aktualisierungen beenden möchtest. Wenn du die Standardkonfiguration beibehältst, bleibt sie vor späteren Änderungen in diesem Spielstand geschützt. Der Start eines neuen Spiels ersetzt deine aktuellen Anzeigeeinstellungen nicht.

<!-- l10n:b-b8d402e619ac -->
Nicht verfügbare Mod-Tätigkeiten, Fähigkeiten und Symbole bleiben in importierten Konfigurationen erhalten, damit sie erneut exportiert werden können. Eine benannte Siedlung oder ein Schiff, das nicht zugeordnet werden kann, bleibt als inaktive Ortsbeschränkung erhalten; diese wird niemals automatisch zu „Überall“. Passe diese Beschränkung beim Wechsel zu einer neuen Kolonie im Rolleneditor an. Importe mit ungültigen Verweisen auf zusammengesetzte Rollen oder Trainingspfade werden abgelehnt, bevor die Konfiguration geändert wird.
