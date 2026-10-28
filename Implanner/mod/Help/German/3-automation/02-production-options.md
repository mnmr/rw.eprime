---
title: Produktionsoptionen
---
<!-- l10n:b-bdd2b459773e -->
Mit **Fertigungsaufträge für fehlende Implantate automatisch hinzufügen** erstellt Implanner Aufträge an geeigneten Werkbänken und entfernt eigene Aufträge, sobald der Bedarf entfällt. Deine selbst erstellten Aufträge werden nie entfernt. Jeder Auftrag fertigt ein Exemplar in der späteren Operationsreihenfolge: Bei Vollständige Sätze kommen Beine vor Armen, wenn sie im Plan höher stehen.

<!-- l10n:b-8effc9e9b828 -->
Gibt es mehrere Rezepte für ein Implantat, bevorzugt Implanner eine verfügbare und sofort bezahlbare Möglichkeit. Ein freies Basisimplantat kann verbessert werden; andernfalls kann ein direktes Rezept das fortgeschrittene Implantat aus Materialien herstellen. Ist nur die Verbesserung verfügbar, kann Zwischenproduktion zuerst das Basisimplantat fertigen. Rezepte müssen erforscht sein und von einer geeigneten Werkbank unterstützt werden.

<!-- l10n:b-0aa3ded1d655 -->
Aktiviere **Implanner-Werkbank** im Auftragsreiter einer Bank, um sie der Implantatherstellung zu widmen. Das Kästchen erscheint nur an implantatfähigen Werkbänken. Bestehende Aufträge werden ausgesetzt; Implanner verwendet diese Bank vor allen anderen. Währenddessen kannst du keine Aufträge hinzufügen, einfügen, aussetzen oder fortsetzen.

<!-- l10n:b-a4d04c1bceb2 -->
![Ein an Implanner übergebener Fabrikationstisch](implanner-bench.png)

<!-- l10n:b-a89a880dcac5 -->
Deaktiviere das Kästchen, um die Bank zurückzunehmen. Zuvor bereits ausgesetzte Aufträge bleiben ausgesetzt, alle anderen laufen weiter. Entfernst du Implanner bei aktivem Kästchen, funktioniert die Bank weiter, aber du musst ihre Aufträge selbst fortsetzen.

<!-- l10n:b-c9f0dad1a5c5 -->
![Die Produktionsoptionen](production-options.png)

<!-- l10n:b-f6f358878993 -->
- **Werkbänke mit erlaubten Aufträgen** begrenzt die Zahl gleichzeitig verwendeter Bänke je Kolonie. Jede hält bis zu zwei Implanner-Aufträge, damit der nächste schon wartet. Implanner-Werkbänke zählen mit.
- **Aufträge nur an freien Werkbänken erstellen** hält Implantataufträge von Bänken mit wartender Arbeit fern, damit deine Listen Vorrang haben.
- **Nur Implanner-Werkbänke verwenden** beschränkt Implantataufträge auf diese Bänke. Ohne eine solche wird nichts gefertigt. Deaktiviert werden andere Bänke genutzt, sobald jede Implanner-Werkbank zwei Aufträge hat.
- **Erforderliche Handwerksstufe für Produktionsaufträge** hält teure Materialien von unerfahrenen Handwerkern fern.
- **Produktionsaufträge für fehlende Zwischenprodukte erlauben** verfolgt Fehlmengen durch die Kette: Fehlt dem nächsten bionischen Bein eine fortgeschrittene Komponente, kommt zuerst deren Auftrag. Während ein Implantat auf Rohstoffe wie Plastahl wartet, fertigen freie Bänke die benötigten fortgeschrittenen Komponenten. Hergestellte Gegenstände wie Komponenten und Basisimplantate sind zulässig. Rohstoffe wie Stahl werden nie gefertigt, selbst wenn ein Schmelzrezept sie liefern könnte; du musst sie selbst beschaffen.

<!-- l10n:b-24089426e9a9 -->
Jeder Auftrag beachtet außerdem Mindestbestände (siehe [Vorräte erhalten](topic:keeping-stock)).
