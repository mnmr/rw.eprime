---
title: Unfertige Arbeiten teilen
---
<!-- l10n:sharing -->
Wenn **{k:QJ_SettingsShareWork}** in den Optionen aktiviert ist, kann ein anderer geeigneter Arbeiter einen derzeit nicht bearbeiteten unfertigen Gegenstand weiterbearbeiten, selbst wenn dessen ursprünglicher Hersteller gestorben ist. Das Teilen funktioniert unabhängig von **{k:QJ_ManageBill}**.

<!-- l10n:matching -->
Der nächste Arbeiter benötigt einen ausführbaren Auftrag für dasselbe Rezept. Die Zutaten des Gegenstands müssen in diesem Auftrag erlaubt sein, und sein Stil muss passen. Der Gegenstand muss außerdem erreichbar, erlaubt und reservierbar sein. Das Teilen umgeht weder die normale Arbeitszuweisung noch die Beschränkungen eines Auftrags.

<!-- l10n:handoff -->
Ein Gegenstand, an dem gerade niemand arbeitet, wird von seinem ursprünglichen Arbeiter und Auftrag gelöst, damit ein passender Auftrag ihn übernehmen kann. Solange der unfertige Gegenstand dieses Auftrags wartet oder transportiert wird, beginnt der Auftrag keinen zweiten Gegenstand. Laufende Arbeit wird nicht unterbrochen, nur um sie jemand anderem zu übergeben.

<!-- l10n:reserved -->
Das Teilen früher Arbeitsschritte unterscheidet sich vom Reservieren des letzten Arbeitsschritts. Bei einem verwalteten Gegenstand, dessen Bearbeitung kurz vor Abschluss pausiert, steht **Autor: reserviert (Quality Jobs)**. Er wartet auf einen [geeigneten Fertigsteller](topic:finishers); ein gewöhnlicher Arbeiter kann ihn auch durch Teilen nicht fertigstellen.

<!-- l10n:robot-work -->
Wenn **{k:QJ_SettingsAllowRobotPreparation}** deaktiviert ist, bleibt ein geteilter unfertiger Gegenstand für die Bearbeitung durch Kolonisten reserviert, solange sein ursprünglicher Auftrag noch besteht und verwaltet wird. Auch über einen nicht verwalteten Auftrag kann kein Mech oder Roboter diese Arbeit übernehmen. Geeignete Mechs und Roboter können weiterhin Fertigstellungsaufträge von Quality Jobs ausführen.

<!-- l10n:off -->
Deaktiviere das Teilen, damit gewöhnliche unfertige Arbeiten bei ihrem ursprünglichen Hersteller bleiben. Verwaltete Aufträge können den letzten Arbeitsschritt weiterhin reservieren und an einen geeigneten Fertigsteller übergeben. Wenn du **{k:QJ_ManageBill}** deaktivierst, entfallen die Anforderungen dieses Auftrags an die Fertigstellung und seine darauf wartenden Arbeiten werden freigegeben; das Teilen bleibt für die Kolonie aktiviert.

