---
title: Come funziona l'automazione
---
<!-- l10n:b-44e89be1e85f -->
L'automazione controlla la colonia circa due volte per ora di gioco. Dopo una modifica, esegue un nuovo controllo non appena il tempo di gioco avanza. Riprendi il gioco per applicare le modifiche all'automazione. Ogni controllo segue questi passaggi:

<!-- l10n:b-6eb19bb9f96f -->
1. Conta ciò che manca ancora ai coloni assegnati ai piani.
2. Accoda ordini per gli impianti che la colonia può produrre; vedi [opzioni di produzione](topic:production-options).
3. Assegna gli oggetti finiti in magazzino a coloni specifici, seguendo priorità e fasce.
4. Quando tutto ciò che serve a un colono per la prossima fase è assegnato e sul posto, aggiunge gli interventi in un unico blocco al suo elenco, dopo quelli già in coda.

<!-- l10n:b-a747349bea45 -->
Il riepilogo della colonia in cima alla scheda Panoramica mostra tutto questo: se l'automazione è attiva, cosa sta producendo o aspettando, quanti impianti sono disponibili e ancora da produrre, e chi è il prossimo a ricevere interventi. Passa il puntatore sui dati di produzione o chirurgia per un dettaglio per impianto, compreso ciò che ogni oggetto attende.

<!-- l10n:production-counts -->
Nel suggerimento della produzione, **Ancora da produrre** conta gli impianti necessari non ancora coperti dalle scorte, compresi quelli senza un ordine di produzione. **Negli ordini di produzione** conta gli impianti previsti dagli ordini Implanner esistenti, in attesa o in corso.

<!-- l10n:b-8fea0b171c65 -->
![Il riepilogo della colonia con stato della produzione e della chirurgia](colony-next.png)

<!-- l10n:b-26d1852e16fa -->
Ogni impianto richiede comunque un intervento distinto. Il raggruppamento rinvia la chirurgia finché si possono installare più impianti durante un'unica anestesia, riducendo il numero di anestesie separate. Un gruppo comprende tutto ciò che serve al colono nella prossima fase: la fascia attuale oppure l'intero piano, secondo la strategia di avanzamento; vedi [opzioni chirurgiche](topic:surgery-options). Una strategia evita del tutto i gruppi e programma ogni impianto appena è assegnato e sul posto.

<!-- l10n:b-6b9f966652bc -->
Gli interventi attendono che il colono sia in condizioni adatte. Le colonie non prendono mai oggetti dalle scorte altrui: gli impianti vengono assegnati e installati dove vive il colono.
