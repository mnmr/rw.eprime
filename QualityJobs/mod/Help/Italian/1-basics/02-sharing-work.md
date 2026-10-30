---
title: Condivisione dei lavori incompiuti
---
<!-- l10n:sharing -->
Con **{k:QJ_SettingsShareWork}** attivato nelle Opzioni, un oggetto incompiuto su cui nessuno sta lavorando può essere ripreso da un altro lavoratore idoneo, anche se l'artigiano originale è morto. La condivisione funziona indipendentemente da **{k:QJ_ManageBill}**.

<!-- l10n:matching -->
Il lavoratore che subentra deve disporre di un ordine utilizzabile per la stessa ricetta. Gli ingredienti dell'oggetto devono essere consentiti da quell'ordine e lo stile deve corrispondere. L'oggetto deve inoltre essere raggiungibile, consentito e disponibile per essere riservato. La condivisione non aggira le normali assegnazioni di lavoro né le restrizioni degli ordini.

<!-- l10n:handoff -->
Un oggetto su cui nessuno sta lavorando viene svincolato dal lavoratore e dall'ordine originali, così che un ordine compatibile possa riprenderlo. Finché l'oggetto incompiuto di quell'ordine è in attesa o viene trasportato, l'ordine non avvia un duplicato. Un lavoro in corso non viene interrotto solo per passarlo a qualcun altro.

<!-- l10n:reserved -->
Condividere il lavoro iniziale è diverso dal riservare il tocco finale. Un oggetto gestito, sospeso poco prima del completamento, mostra **Autore: riservato (Quality Jobs)**. Attende un [lavoratore idoneo al completamento](topic:finishers); un lavoratore comune non può completarlo tramite la condivisione.

<!-- l10n:robot-work -->
Quando **{k:QJ_SettingsAllowRobotPreparation}** è disattivato, un oggetto incompiuto condiviso resta riservato al lavoro dei coloni finché il suo ordine originale esiste ed è gestito. Riprenderlo tramite un ordine non gestito non consente a un mech o a un robot di subentrare nel lavoro. I mech e i robot idonei possono comunque svolgere gli incarichi di completamento assegnati da Quality Jobs.

<!-- l10n:off -->
Disattiva la condivisione per lasciare il normale lavoro incompiuto all'artigiano che l'ha iniziato. Gli ordini gestiti possono ancora riservare l'ultimo tocco e affidarlo a un lavoratore idoneo al completamento. Disattivare **{k:QJ_ManageBill}** rimuove i requisiti di completamento di quell'ordine e ne sblocca i lavori in attesa di essere completati; non disattiva la condivisione per la colonia.

