---
title: Lavori pianificati
---
<!-- l10n:b-7aa1a59c7379 -->
Le opzioni dei lavori pianificati sottraggono i materiali già richiesti da ordini o progetti.

<!-- l10n:b-ee40b81e925d -->
- Riserva risorse per gli ordini sottrae gli ingredienti ancora necessari agli ordini in sospeso. Un ordine riserva un ingrediente solo quando c'è esattamente un modo per soddisfare il requisito. “Qualsiasi pelle” e altre scelte aperte non riservano nulla. Un ordine “esegui X volte” riserva X esecuzioni; “produci fino ad averne X” riserva quelle necessarie a raggiungere l'obiettivo; “esegui per sempre” ne riserva una. Gli ordini sospesi non riservano nulla.
- Riserva risorse per le costruzioni sottrae i materiali ancora necessari a progetti e strutture parzialmente costruite. Il materiale già consegnato o raccolto da un colono per la consegna conta come speso e non viene sottratto di nuovo. I progetti vietati vengono ignorati.
- Mostra le carenze come numeri negativi indica quanto manca quando il lavoro pianificato richiede più di quanto possiedi, invece di fermarsi a zero.
- Supporto per Quality Jobs stima quanti tentativi richiederà una qualità obiettivo e riserva risorse per tutti. La riga è disabilitata se EPrime's Quality Jobs manca o è troppo vecchio; passando il puntatore appare il motivo.

<!-- l10n:b-a4ac1a2f64dc -->
Con Mostra le carenze come numeri negativi attivo, un contatore portato sotto zero dai lavori pianificati mostra la carenza in arancione:

<!-- l10n:b-b209bc9139c1 -->
![Materiali con numeri negativi attivi](negatives-on.png)

<!-- l10n:b-5325cc201c17 -->
Con l'opzione disattivata, le stesse caselle si fermano a zero:

<!-- l10n:b-a997f13279d8 -->
![Le stesse caselle con numeri negativi disattivati](negatives-off.png)

<!-- l10n:b-f017c3669f5c -->
I lavori pianificati vengono riesaminati circa ogni diciassette secondi di gioco: un nuovo ordine può richiedere quel tempo per apparire nei conteggi.
