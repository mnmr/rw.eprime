---
title: Qualità minima
---
<!-- l10n:b-d8fd3884b5cc -->
Con Quality Bionics Remastered o Vanilla Genetics Expanded caricati, gli impianti hanno qualità da terribile a leggendaria, che ne modifica l'efficacia. In questo caso il piano mostra un pulsante **Qualità minima** accanto a **Elimina piano**.

<!-- l10n:b-118873404542 -->
![L'intestazione del piano con il pulsante Qualità minima](plan-min-quality.png)

<!-- l10n:b-84db128338c2 -->
Ogni impianto del piano, compresi quelli ereditati, viene installato e prodotto con quella qualità o superiore. Gli impianti privi di qualità non sono interessati. Un nuovo piano derivato parte dallo stesso minimo del piano base.

<!-- l10n:b-93af25d8591d -->
Implanner non sostituisce mai una parte sana del corpo con una peggiore. Un braccio bionico di qualità terribile funziona peggio di uno naturale, quindi attende un oggetto migliore, salvo che il braccio del colono sia mancante o gravemente danneggiato. Quando più oggetti sono idonei, il migliore va al colono il cui turno arriva prima.

<!-- l10n:b-0e7cf20ad9dc -->
Un colono che possiede già l'impianto pianificato lo conserva anche sotto il minimo, a meno che sia attiva l'opzione seguente.

<!-- l10n:b-b163fd8a6054 -->
## Impianti migliori ai coloni con priorità alta

<!-- l10n:b-0c6c2d002995 -->
![L'opzione nella scheda Automazione](upgrade-by-priority.png)

<!-- l10n:b-8937cc3e8047 -->
Con **Dai gli impianti migliori ai coloni con priorità alta** attivo nelle opzioni chirurgiche della scheda Automazione, la qualità segue la priorità del colono:

<!-- l10n:b-0d3ec0233e11 -->
- I nuovi interventi usano la qualità minima accettata dal piano, lasciando liberi gli oggetti migliori.
- Un oggetto migliore va al colono con priorità più alta che ne possiede uno peggiore dello stesso tipo. Il vecchio impianto viene prima rimosso, poi si installa quello migliore e quello rimosso passa al colono successivo.
- Viene ceduto anche un oggetto già assegnato a un colono con priorità inferiore, purché il suo intervento non sia ancora programmato.

<!-- l10n:b-e246867de4e8 -->
Ogni scambio richiede un intervento aggiuntivo. Un colono lo riceve solo se non ha già altri interventi di Implanner in attesa e c'è spazio nel limite degli interventi contemporanei. Un impianto non viene mai scambiato se la sua rimozione toglierebbe anche qualcos'altro dalla parte del corpo.

<!-- l10n:b-37015122025b -->
Quando Implanner produce un impianto per il piano:

<!-- l10n:b-51f50276fd20 -->
- Con Quality Jobs caricato, l'ordine mira alla qualità minima, così l'ultima fase spetta a un artigiano in grado di raggiungerla.
- Un impianto ibrido di Vanilla Genetics Expanded eredita la qualità dal genotelaio. L'ordine usa soltanto genotelai con qualità minima o superiore.
- Senza nessuna delle due integrazioni, qualsiasi artigiano può completare l'impianto; se risulta sotto il minimo, viene prodotto di nuovo. Le scorte minime, descritte in [conservare le scorte](topic:keeping-stock), fermano questo processo prima che i materiali scarseggino.
