---
title: Mod-Kompatibilität
---
<!-- l10n:b-6110204fbb19 -->
Arbeitsarten und Arbeiten aus anderen Mods werden automatisch übernommen. Einige Mods sind tiefer eingebunden: Expertise aus Vanilla Skills Expanded und Alpha Skills fließt in die Empfehlungssignale ein; gehasste Arbeit aus More Than Capable erzeugt ein Signal der Stufe Miserabel; Colony Groups erscheint als Gruppierungsoption; Mods für mehrere Stockwerke behandeln übereinanderliegende Etagen als einen Standort.

<!-- l10n:b-c7669aaaba7d -->
Optionale Arbeits- und Fertigkeitsmods können zusätzliche Arbeitsprofile liefern. Diese geben an, welche Fertigkeiten eine Arbeit verwendet und trainiert. Sind die entsprechenden Mods installiert, können sie auch die primäre Fertigkeit für die Empfehlung genauer bestimmen. WorkRoles überlässt Zulässigkeit, Arbeitsmarkierungen und Ausführung der Arbeit den Mods, die sie bereitstellen. Durch diese Integrationen entstehen keine zwingenden Mod-Abhängigkeiten.

<!-- l10n:b-27d73501db9c -->
Empfehlungen berücksichtigen außerdem deklarierte Arbeitskennzeichen, erforderliche Fähigkeiten, entgegenstehende Glaubenssätze und bestätigte Verweigerungen gewöhnlicher Bergbauarbeit. Bei geprüften Versionen von Humanoid Alien Races für RimWorld 1.6 und 1.7 verwenden sie die tatsächlichen Beschränkungen des Frameworks für Arbeitsgeber und Rezepte. Verschiedene Spezies, die diese Regeln nutzen, profitieren von derselben Integration. Ein gesperrtes Rezept entfernt weder andere nutzbare Rezepte aus der Rolle noch zählt es als Möglichkeit, deren Fertigkeit zu trainieren.

<!-- l10n:b-15011508cb81 -->
Bei unbekannten Framework-Versionen bleibt die bisherige Zulässigkeit erhalten und es wird gemeldet, dass ihre Regeln nicht unterstützt werden. Beschränkungen, die in beliebigem Arbeitscode verborgen sind, lassen sich nicht sicher erkennen, indem während der Erfassung von Empfehlungen Arbeiten ausprobiert werden. Unterstützte Vivi-Versionen liefern ausrüstungsabhängige Sammelgeschwindigkeit, Ausbeute und Zulässigkeit. Bei unbekannten Vivi-Versionen, eigenen Sammelregeln und nicht bestätigten Ratkin-spezifischen Regeln bleibt das bisherige Verhalten erhalten. Beschränkungen werden niemals aus dem Namen einer Spezies oder eines Xenotyps abgeleitet.

<!-- l10n:b-cc8167cf5cfc -->
Erweiterungen, die vorhandene Arbeitsgeber patchen, verwenden weiterhin dieselben Rollen und dieselbe Arbeitsreihenfolge. Tierbetreuungsrollen nutzen beispielsweise weiterhin Tiere für Empfehlungen. Bedingter Erfahrungsgewinn in einer anderen Fertigkeit hängt von den Zulässigkeitsregeln der Erweiterung ab. Daraus entsteht kein allgemeiner Trainingspfad für jeden Kolonisten, dem diese Arbeit zugewiesen ist.

<!-- l10n:b-f6d4800368e1 -->
Zwei Gruppen von Mods stehen im Konflikt. Andere Ersatzoberflächen für den Arbeitsreiter, etwa Work-Tab-Abspaltungen oder Better Work Tab, beanspruchen dieselbe Oberfläche. Mods, die Prioritäten setzen, etwa Free Will oder PriorityMaster, beanspruchen dieselben Daten: WorkRoles verwaltet die Prioritäten betreuter Kolonisten, deshalb werden solche Schreibzugriffe blockiert. In diesem Fall nennt ein einmaliger Hinweis die Mod und erklärt, wie sich derselbe Effekt mit Rollen erreichen lässt.

<!-- l10n:b-3e9c0a143130 -->
Die Prioritätsverwaltung gilt nur für menschenähnliche Charaktere. Mods, die Prioritäten für Mechanoiden festlegen, etwa Mech Work Tab, sind daher vollständig kompatibel.

<!-- l10n:b-3df0d9a43535 -->
Manche Mods benötigen Prioritäten im Bereich von 0 bis 4. Falls Probleme mit anderen Mods auftreten, prüfe deshalb, ob „Anderen Mods Grundspielprioritäten von 0–4 melden“ eingeschaltet ist.

<!-- l10n:b-99382dfb0e0e -->
Mods, die Prioritäten nur lesen, funktionieren problemlos. Sie sehen die von WorkRoles berechneten Werte.
