---
title: Einen Auftrag einrichten
---
<!-- l10n:open -->
Öffne **Details** bei einem Auftrag für einen Gegenstand mit Qualitätsstufe. Bei unterstützten Rezepten, die einen unfertigen Gegenstand hinterlassen, erscheint der Bereich **{k:QJ_QualityPanelTitle}** neben den normalen Auftragseinstellungen. Rezepte ohne Qualitätsstufe oder unfertige Gegenstände behalten ihr normales Verhalten.

<!-- l10n:bill-image -->
![Ein Werkbankauftrag mit dem Bereich von Quality Jobs](crafting-bill.png)

<!-- l10n:setup -->
Aktiviere **{k:QJ_ManageBill}** und wähle dann die [Anforderungen für die Fertigstellung](topic:finishers). Gewöhnliche Arbeiter können den Großteil der Herstellung übernehmen. Erfüllt der aktuelle Arbeiter die Anforderungen, stellt er den Gegenstand direkt fertig. Andernfalls wird der fast fertige Gegenstand beiseitegelegt, und über den Auftrag kann innerhalb seiner Bestandsobergrenze mit der Herstellung weiterer Gegenstände begonnen werden.

<!-- l10n:dispatch -->
Wenn ein Arbeiter die Anforderungen für die Fertigstellung erfüllt, erstellt Quality Jobs einen temporären Auftrag **Fertigstellen …** und weist ihn diesem Arbeiter zu. Der Arbeiter holt den unfertigen Gegenstand und stellt ihn fertig. Anschließend wird der temporäre Auftrag automatisch entfernt. Wartende Gegenstände können wie andere unfertige Gegenstände transportiert und eingelagert werden.

<!-- l10n:finisher-bill-restrictions -->
Die maximale Fertigkeitsstufe und die Zuweisung an einen bestimmten Arbeiter im ursprünglichen Auftrag gelten, bis der Gegenstand zur Fertigstellung beiseitegelegt wird. Du kannst die Arbeit einem Anfänger zuweisen, während ein erfahrenerer Kolonist, ein Mech der Kolonie oder ein unterstützter Roboter den zugehörigen Fertigstellungsauftrag übernimmt. Arbeiter für die Fertigstellung müssen weiterhin das Rezept ausführen können und die Anforderungen von Quality Jobs erfüllen.

<!-- l10n:status -->
Der Abschnitt **{k:QJ_StatusHeader}** zeigt gemeinsam nutzbare **Unfertige Arbeiten**, Gegenstände, die auf die Fertigstellung **Warten**, und Gegenstände, die bereits zur **Fertigstellung** zugewiesen sind. Bewege den Mauszeiger über den Status und die Liste der Arbeiter für die Fertigstellung, um weitere Details zu sehen. Wenn nichts vorangeht, siehe [wenn die Arbeit wartet](topic:waiting).

<!-- l10n:disable -->
Deaktiviere **{k:QJ_ManageBill}**, damit der Auftrag wieder normal abgeschlossen wird. Seine gemeinsam nutzbaren unfertigen Gegenstände können weiterhin von anderen Arbeitern bearbeitet werden, wenn die gemeinsame Nutzung aktiviert ist.

