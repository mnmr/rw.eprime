---
title: Obiettivi e limiti di scorte
---
<!-- l10n:target -->
La **{k:QJ_MinQualityLabel}** fa sì che un ordine con un numero fisso di ripetizioni continui a tentare finché non produce abbastanza oggetti di quella qualità o superiore. Un oggetto sotto l'obiettivo viene conservato, ma non consuma una delle ripetizioni rimanenti dell'ordine. Ogni tentativo aggiuntivo consuma altri materiali. **{k:QJ_AnyQuality}** accetta ogni risultato.

<!-- l10n:repeat-modes -->
Questi tentativi aggiuntivi si applicano a **Ripeti X volte**. Per **Ripeti finché non ne hai X**, usa l'intervallo di qualità dell'ordine normale per decidere cosa conteggiare ai fini delle scorte desiderate. **Ripeti per sempre** prosegue come al solito. Una qualità obiettivo non seleziona da sola un addetto al completamento migliore: imposta anche i requisiti per il completamento.

<!-- l10n:stock -->
Il **Limite di scorte** limita l'avvio di nuovi lavori per un prodotto su questa mappa. I suoi oggetti incompleti vengono conteggiati insieme tra tutti gli ordini e i banchi di lavoro, ma solo gli ordini gestiti rispettano il limite. **0 significa nessun limite.** Gli oggetti completati in magazzino non vengono conteggiati.

<!-- l10n:cap-behavior -->
Al raggiungimento del limite, gli ordini gestiti smettono di avviare nuovi oggetti. È comunque possibile riprendere o completare quelli esistenti, così la coda può smaltirsi. Se nessun addetto al completamento soddisfa i requisiti, abbassarli o rendere nuovamente disponibile un lavoratore idoneo può sbloccare la coda.

