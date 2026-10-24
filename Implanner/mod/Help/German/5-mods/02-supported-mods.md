---
title: Unterstützte Mods
---
Jede Implantatmod mit Operationsdaten des Spiels funktioniert ohne Einrichtung. Spieler haben Expanded Prosthetics and Organ Engineering und CeleTech bestätigt; ihre Implantate werden wie Grundspielinhalte gelesen. Die folgenden Mods wurden einzeln anhand ihrer Daten geprüft.

![Ein ausgewählter Mechanitenstamm in der Rumpfliste](mechanites-picked.png)

![Andere Stämme zeigen die Überschreibung des gewählten Stamms](mechanites-overrides.png)

**[FSF] Advanced Bionics Expansion**: Mechanitenstämme schließen sich aus. Ein eingespritzter Stamm entfernt jeden anderen im Körper. Die Auswahl behandelt daher alle Stämme als einen Platz; die Überschreibungsbeschriftung nennt den ersetzten. Immunitätssteigernde Mechaniten sind kein Stamm und können neben jedem bestehen. Die fortgeschrittene bionische Blase und der fortgeschrittene Hygieneverbesserer unterliegen zusammen mit Dubs Bad Hygiene den Kompatibilitätsoptionen.

**Dubs Bad Hygiene**: Bionische Blase und Hygieneverbesserer sind Rumpfimplantate, die das Spiel gleichzeitig mit den fortgeschrittenen Versionen von [FSF] Advanced Bionics Expansion erlaubt. Die beiden Kompatibilitätsoptionen bestimmen, ob Implanner das ebenfalls zulässt.

![Einfaches und fortgeschrittenes bionisches Auge überschreiben das gewählte modulare Auge](modular-eye.png)

**Integrated Implants**: Zusätzliche Arme erlauben einen pro Seite; eine zweite Art derselben Seite überschreibt die erste. Unterhautpanzerung und Prestigeversion, Schultertürme, Schwänze und implantierte Sprengstoffe bilden jeweils Gruppen mit nur einer möglichen Auswahl. Modulare Bionik verbessert bestehende Teile: Bei einem modularen bionischen Auge setzt die Automatik zunächst ein gewöhnliches ein, falls keines vorhanden ist, dann folgt die Verbesserung mit einer Komponente ohne zweites Auge. Diese Verbesserungen benötigen EBSG Framework und werden bei geladenem Medical System Expansion 2 nicht angeboten. Die Module selbst werden ohne Operation aus dem Inventar eingesetzt und erscheinen daher nicht.

**Vanilla Genetics Expanded**: Hybridimplantate für Kolonisten stehen neben den anderen Implantaten. Tierimplantate fehlen, da Implanner nur Kolonisten plant.

Manche Hybridimplantate ersetzen ein Körperteil: Das Winterschlafmodul ersetzt das Gehirn. Andere sitzen darauf, etwa die Neuronenverstärkung. Gewöhnliche Implantate passen auf Körperteile mit aufgesetztem Hybridimplantat, aber nicht auf ersetzte Körperteile.

Jedes eingesetzte Hybridimplantat verdrängt alles bereits auf diesem Körperteil Eingesetzte. Die Gegenstände fallen neben den Arzt.

- Implanner setzt das Hybridimplantat zuerst ein, damit nichts herausmuss.
- Sind bereits Implantate vorhanden, lässt Implanner sie herausfallen, reserviert sie für denselben Kolonisten und setzt sie direkt danach wieder ein.
- Implanner setzt nie ein Hybridimplantat ein, wenn dabei etwas auf dem Körperteil dauerhaft verloren ginge.

Hybridimplantate erhalten ihre Qualität vom verwendeten Genorahmen (siehe [Mindestqualität](topic:minimum-quality)).

**Quality Bionics Remastered**: Bionische Teile besitzen Qualität; bessere Teile arbeiten besser und haben mehr Trefferpunkte. Der Tooltip zeigt die Effizienz von miserabel bis legendär. Jeder Plan legt seine Mindestqualität fest (siehe [Mindestqualität](topic:minimum-quality)).

**Elite Bionics Framework**: Implantate, die Trefferpunkte eines Körperteils verändern, zeigen dies im Tooltip.

![Anforderungsabfrage für ein Beinmodul](requirement-prompt.png)

![Nach Zum Plan hinzufügen sind Bein und Modul ausgewählt](requirement-added.png)

**Bionic modularity**: Jedes Modul belegt einen Platz auf seinem Körperteil. Ein Kampf- und ein Arbeitsmodul passen auf denselben Arm, zwei Kampfmodule nicht. Gliedmaßenmodule werden auf Bionik montiert und teilen deren Körperteil. Geplanter bionischer Arm und Armmodul können daher zusammen bestehen. Bei Auswahl ohne passende Bionik zeigt eine Abfrage mögliche Träger, standardmäßig den am wenigsten fortgeschrittenen. **Zum Plan hinzufügen** ergänzt Auswahl und Modul gemeinsam; **Abbrechen** fügt nichts hinzu.
