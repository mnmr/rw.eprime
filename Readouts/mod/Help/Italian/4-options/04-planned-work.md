---
title: Lavori pianificati
---
<!-- l10n:b-7aa1a59c7379 -->
Le opzioni dei lavori pianificati sottraggono dai conteggi visualizzati i costi stimati in materiali degli ordini o dei progetti.

<!-- l10n:b-ee40b81e925d -->
- Riserva risorse per gli ordini sottrae gli ingredienti necessari agli ordini di produzione in sospeso. Un ingrediente viene conteggiato solo quando l'ordine ammette un unico tipo di risorsa per soddisfare quel requisito. Ad esempio, consentire qualsiasi pelle non riserva nulla; consentire un solo tipo di pelle ne include il costo. Un ordine “esegui X volte” riserva X esecuzioni; “produci fino ad averne X” riserva quelle necessarie a raggiungere l'obiettivo; “esegui per sempre” ne riserva una. Gli ordini sospesi non riservano nulla.
- Riserva risorse per le costruzioni sottrae i materiali ancora necessari a progetti e strutture parzialmente costruite. Il materiale già consegnato o raccolto da un colono per la consegna conta come speso e non viene sottratto di nuovo. I progetti vietati vengono ignorati.
- Mostra le carenze come numeri negativi indica quanto manca quando il lavoro pianificato richiede più di quanto possiedi, invece di fermarsi a zero.
- Supporto per Quality Jobs adegua le riserve attivate per ordini e costruzioni agli obiettivi di qualità gestiti da Quality Jobs. Stima i materiali necessari per i tentativi aggiuntivi e tiene conto dei materiali recuperati smantellando le costruzioni. Per usarlo, attiva la corrispondente opzione di riserva qui sopra. La riga è disabilitata se EPrime's Quality Jobs manca o è troppo vecchio; passando il puntatore appare il motivo.

<!-- l10n:planned-work-tooltip-columns -->
Passa il puntatore su una risorsa o un insieme di risorse con detrazioni per i lavori pianificati per visualizzare la tabella Lavori pianificati. Lavoro rimanente indica il numero di esecuzioni di una ricetta o di costruzioni da completare. Costo per tentativo indica il costo in materiali di un singolo tentativo. Sottratto indica la quantità sottratta dal conteggio visualizzato, tenendo conto dei materiali già consegnati o in trasporto verso le costruzioni e dei tentativi aggiuntivi stimati per raggiungere la qualità richiesta quando il supporto per Quality Jobs è attivo.

<!-- l10n:b-a4ac1a2f64dc -->
Con Mostra le carenze come numeri negativi attivo, un contatore portato sotto zero dai lavori pianificati mostra la carenza in arancione:

<!-- l10n:b-b209bc9139c1 -->
![Materiali con numeri negativi attivi](negatives-on.png)

<!-- l10n:b-5325cc201c17 -->
Con l'opzione disattivata, le stesse caselle si fermano a zero:

<!-- l10n:b-a997f13279d8 -->
![Le stesse caselle con numeri negativi disattivati](negatives-off.png)

<!-- l10n:b-f017c3669f5c -->
I lavori pianificati si aggiornano insieme ai conteggi delle risorse ogni pochi secondi di gioco. Gli ordini nuovi o modificati compaiono al prossimo aggiornamento; riprendi il gioco per far avanzare il tempo.
