---
title: So werden Modimplantate eingelesen
---
Implanner benötigt keine Liste unterstützter Mods. Es liest alle geladenen Operationen aus Grundspiel, Erweiterungen und Mods. Jede Operation, die ein Implantat an einem Körperteil einsetzt, wird zur Auswahl. Dieselben Daten zeigen, welche Implantate gemeinsam bestehen können:

- **Ein Ersatz pro Körperteil**: Ein bionischer Arm ersetzt die Schulter. Zwei Ersatzteile teilen daher nie denselben Platz; ein Ersatz entfernt auch alle Implantate darauf und an untergeordneten Körperteilen.
- **Implantate an einem Körperteil**: Sie bestehen nebeneinander, sofern die Operation sie nicht als unvereinbar markiert. So schließen sich Hautdrüsen im Grundspiel aus und kennzeichnen Mods Modulplätze.
- **Entfernung beim Einbau**: Manche Implantate entfernen beim Einsetzen einen Rivalen irgendwo am Körper. Zwei sich gegenseitig entfernende Implantate können nie gemeinsam bestehen.
- **Operationen, die ein Körperteil leeren**: Manche entfernen dort alles, bevor sie ein Implantat hinzufügen, das das Körperteil nicht ersetzt. Spätere Implantate bleiben. Implanner setzt solche Implantate zuerst ein und setzt Verdrängtes wieder ein.
- **Module auf Bionik**: Eine Operation, deren Ausführung ausdrücklich für künstliche Körperteile geschrieben wurde, teilt das Körperteil mit der Bionik, statt sie zu ersetzen.
- **Direkte Verbesserungen**: Eine Operation, die ein Implantat entfernt und ein anderes hinzufügt, verbessert das eingebaute Teil. Die verbesserte Version erscheint als eigenes Implantat.
- **Nicht herstellbare Gegenstände**: Implantate, die keine Werkbank fertigen kann, bleiben bis zur entsprechenden Option verborgen.

Zwei Dinge sind nicht lesbar: Module, die Kolonisten ohne Operation aus dem Inventar einsetzen, werden nie aufgelistet. Regeln, die nur im Modcode und nicht in Daten stehen, sind ebenfalls unsichtbar. Alles, was eine Mod durch Operationsdaten ausdrückt, funktioniert ohne Änderung an Implanner.
