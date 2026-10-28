---
title: Unterstützte Mods
---
<!-- l10n:b-3d8630b508cc -->
Jede Implantatmod mit Operationsdaten des Spiels funktioniert ohne Einrichtung. Spieler haben Expanded Prosthetics and Organ Engineering und CeleTech bestätigt; ihre Implantate werden wie Grundspielinhalte gelesen. Die folgenden Mods wurden einzeln anhand ihrer Daten geprüft.

<!-- l10n:b-fc34121549e7 -->
![Ein ausgewählter Mechanitenstamm in der Rumpfliste](mechanites-picked.png)

<!-- l10n:b-8e0b09ad2325 -->
![Andere Stämme zeigen die Überschreibung des gewählten Stamms](mechanites-overrides.png)

<!-- l10n:b-8115e58333ac -->
**[FSF] Advanced Bionics Expansion**: Mechanitenstämme schließen sich aus. Ein eingespritzter Stamm entfernt jeden anderen im Körper. Die Auswahl behandelt daher alle Stämme als einen Platz; die Überschreibungsbeschriftung nennt den ersetzten. Immunitätssteigernde Mechaniten sind kein Stamm und können neben jedem bestehen. Die fortgeschrittene bionische Blase und der fortgeschrittene Hygieneverbesserer unterliegen zusammen mit Dubs Bad Hygiene den Kompatibilitätsoptionen.

<!-- l10n:b-951da050479a -->
**Dubs Bad Hygiene**: Bionische Blase und Hygieneverbesserer sind Rumpfimplantate, die das Spiel gleichzeitig mit den fortgeschrittenen Versionen von [FSF] Advanced Bionics Expansion erlaubt. Die beiden Kompatibilitätsoptionen bestimmen, ob Implanner das ebenfalls zulässt.

<!-- l10n:b-41bdd507c8fd -->
![Einfaches und fortgeschrittenes bionisches Auge überschreiben das gewählte modulare Auge](modular-eye.png)

<!-- l10n:b-d5d8d7431467 -->
**Integrated Implants**: Zusätzliche Arme erlauben einen pro Seite; eine zweite Art derselben Seite überschreibt die erste. Unterhautpanzerung und Prestigeversion, Schultertürme, Schwänze und implantierte Sprengstoffe bilden jeweils Gruppen mit nur einer möglichen Auswahl. Modulare Bionik verbessert bestehende Teile: Bei einem modularen bionischen Auge setzt die Automatik zunächst ein gewöhnliches ein, falls keines vorhanden ist, dann folgt die Verbesserung mit einer Komponente ohne zweites Auge. Diese Verbesserungen benötigen EBSG Framework und werden bei geladenem Medical System Expansion 2 nicht angeboten. Die Module selbst werden ohne Operation aus dem Inventar eingesetzt und erscheinen daher nicht.

<!-- l10n:b-261a620cdad5 -->
**Vanilla Genetics Expanded**: Hybridimplantate für Kolonisten stehen neben den anderen Implantaten. Tierimplantate fehlen, da Implanner nur Kolonisten plant.

<!-- l10n:b-8b2ffcc18549 -->
Manche Hybridimplantate ersetzen ein Körperteil: Das Winterschlafmodul ersetzt das Gehirn. Andere sitzen darauf, etwa die Neuronenverstärkung. Gewöhnliche Implantate passen auf Körperteile mit aufgesetztem Hybridimplantat, aber nicht auf ersetzte Körperteile.

<!-- l10n:b-cd881c381961 -->
Jedes eingesetzte Hybridimplantat verdrängt alles bereits auf diesem Körperteil Eingesetzte. Die Gegenstände fallen neben den Arzt.

<!-- l10n:b-ad1a61d0da90 -->
- Implanner setzt das Hybridimplantat zuerst ein, damit nichts herausmuss.
- Sind bereits Implantate vorhanden, lässt Implanner sie herausfallen, reserviert sie für denselben Kolonisten und setzt sie direkt danach wieder ein.
- Implanner setzt nie ein Hybridimplantat ein, wenn dabei etwas auf dem Körperteil dauerhaft verloren ginge.

<!-- l10n:b-c282558e897d -->
Hybridimplantate erhalten ihre Qualität vom verwendeten Genorahmen (siehe [Mindestqualität](topic:minimum-quality)).

<!-- l10n:b-721ad339d2c2 -->
**Quality Bionics Remastered**: Bionische Teile besitzen Qualität; bessere Teile arbeiten besser und haben mehr Trefferpunkte. Der Tooltip zeigt die Effizienz von übel bis legendär. Jeder Plan legt seine Mindestqualität fest (siehe [Mindestqualität](topic:minimum-quality)).

<!-- l10n:b-7a2e3799838c -->
**Elite Bionics Framework**: Implantate, die Trefferpunkte eines Körperteils verändern, zeigen dies im Tooltip.

<!-- l10n:b-c7cd0a958354 -->
![Anforderungsabfrage für ein Beinmodul](requirement-prompt.png)

<!-- l10n:b-71ab3bae9608 -->
![Nach Zum Plan hinzufügen sind Bein und Modul ausgewählt](requirement-added.png)

<!-- l10n:b-1a485abf9e8c -->
**Bionic modularity**: Jedes Modul belegt einen Platz auf seinem Körperteil. Ein Kampf- und ein Arbeitsmodul passen auf denselben Arm, zwei Kampfmodule nicht. Gliedmaßenmodule werden auf Bionik montiert und teilen deren Körperteil. Geplanter bionischer Arm und Armmodul können daher zusammen bestehen. Bei Auswahl ohne passende Bionik zeigt eine Abfrage mögliche Träger, standardmäßig den am wenigsten fortgeschrittenen. **Zum Plan hinzufügen** ergänzt Auswahl und Modul gemeinsam; **Abbrechen** fügt nichts hinzu.
