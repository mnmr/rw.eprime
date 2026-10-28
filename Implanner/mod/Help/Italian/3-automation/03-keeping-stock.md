---
title: Conservare le scorte
---
<!-- l10n:b-2d4a360fb110 -->
Due impostazioni impediscono all'automazione di svuotare i magazzini.

<!-- l10n:b-f53aef22895d -->
**Conserva in magazzino** stabilisce le scorte minime per la produzione. Un ordine viene aggiunto soltanto se la colonia può pagarne i materiali, oltre a quelli ancora necessari agli ordini già in coda, conservando almeno la quantità impostata di ogni ingrediente. Acciaio a 2000 significa che la produzione di impianti non porta mai l'acciaio sotto 2000. Se consumi materiali e un ordine non ancora iniziato non rientra più nel budget, Implanner lo rimuove e lo aggiunge di nuovo quando le scorte bastano.

<!-- l10n:b-96bbe1392879 -->
![Le quantità minime da conservare in magazzino](keep-in-stock.png)

<!-- l10n:b-5864119cb6f1 -->
**Scorte di impianti accantonate** trattengono impianti finiti per l'uso manuale. Accantonando un braccio bionico, l'automazione assegna solo quelli oltre il primo, lasciandone uno libero per interventi urgenti o commercio. La produzione considera comunque mancante un oggetto accantonato, così le scorte vengono reintegrate.
Anche gli ordini di potenziamento lasciano intatti gli impianti accantonati e quelli necessari agli interventi pianificati. Solo gli impianti in eccesso possono essere usati come ingredienti di produzione.

<!-- l10n:b-04d4c3acbddc -->
![Una scorta accantonata trattiene un braccio bionico](implant-reservations.png)
