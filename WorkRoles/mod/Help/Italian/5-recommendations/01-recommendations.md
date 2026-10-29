---
title: Come funzionano i consigli
---
<!-- l10n:b-07818cd37217 -->
Il sistema di consigli analizza i punti di forza dei coloni e i bisogni della colonia, poi propone una riga di ruoli per ciascuno.

<!-- l10n:b-d0c69b8a87f5 -->
I suoi indizi si chiamano segnali: passioni ardenti, attitudini genetiche, tratti, specializzazioni nelle abilità e migliori abilità della colonia. Inoltre soddisfa il fabbisogno della colonia, così ruoli essenziali come Medico e Cuoco vengono sempre assegnati a qualcuno, idealmente dotato del talento adatto.

<!-- l10n:b-45031c5441da -->
I precetti contrari a un lavoro ne abbassano il punteggio. È più probabile che lo riceva un colono adatto e disponibile a svolgerlo, ma un colono contrario può comunque coprire attività essenziali. Un rifiuto verificato o una capacità mancante esclude quel lavoro. Le valutazioni delle abilità descrivono ancora l'attitudine; Più adatti elenca questa attitudine pura tra i coloni capaci, mentre i consigli per la colonia considerano anche avversioni e fabbisogno.

<!-- l10n:b-e112aed1a354 -->
Questi sono dati per i consigli. I ruoli condizionali continuano a controllare quando e dove un ruolo assegnato è attivo.

<!-- l10n:b-2a25543f495f -->
Nelle versioni supportate di Vivi, i consigli di raccolta usano la velocità e la resa effettive del lavoratore. Un umano con una cannuccia Vivi può raccogliere miele e polline; una spazzola multiuso abilita la raccolta delle piante. Equipaggiare o rimuovere questi strumenti aggiorna la capacità anche in pausa. Un lavoratore capace di eseguire alcune ricette di raccolta resta idoneo alla raccolta; le ricette indisponibili rimangono escluse dalle scelte formative. Versioni Vivi sconosciute o regole di raccolta personalizzate conservano il comportamento esistente.

<!-- l10n:b-fce545e1aa79 -->
Il sistema considera tutti i ruoli idonei e li ordina automaticamente per ogni colono. Opzioni dei ruoli nella scheda Consigli elenca tutti i ruoli tranne quelli esclusi. Per impedire che un ruolo venga mai consigliato, aggiungilo a **Ruoli esclusi** in cima alla scheda. Le esclusioni prevalgono sul fabbisogno e sulle regole speciali, mentre le assegnazioni già fissate restano protette. Vedi [ordine ed esclusioni](topic:recommendation-order).

<!-- l10n:b-4cefba49c974 -->
Puoi vedere il giudizio del sistema ovunque. I suggerimenti della palette elencano i più adatti a ciascun ruolo:

<!-- l10n:b-0dd35388dc64 -->
![I più adatti nel suggerimento della palette](palette-tooltip.png)

<!-- l10n:b-f1a1a1196f55 -->
Due ruoli hanno regole speciali. Cacciatore è consigliato a chiunque porti un'arma a distanza, perché cacciare allena Tiro. Viene collocato prima per i tiratori inesperti e dopo per quelli eccellenti, il cui tempo vale di più altrove. Anche l'avversione alla caccia lo sposta più indietro. Meccanitor viene consigliato solo ai coloni con un mechlink, indipendentemente dall'abilità degli altri artigiani; anche il suo elenco Più adatti segue questa regola.

<!-- l10n:b-b241ec72dcda -->
Il sistema propone, ma decidi tu cosa applicare, [un ruolo alla volta](topic:cherry-picking) oppure tutti insieme con [Ottimizza la colonia](topic:fix-my-colony). Fa eccezione l'[Ottimizzazione automatica](topic:auto-optimize): attivandola, Ottimizza la colonia viene eseguito ogni ora di gioco in tutte le colonie.

<!-- l10n:mechanical-suggestions -->
Quando le righe di robot e mech sono abilitate, i consigli si basano sulle loro abilità attuali e sui lavori che possono svolgere, senza passioni umane o percorsi di apprendimento. La loro mansione principale accettata resta invariata, anche se si tratta di un ruolo escluso assegnato esplicitamente; per cambiarla, intervieni manualmente. I consigli per lavori aggiuntivi rispettano le esclusioni. Decidi tu le loro assegnazioni; l'Ottimizzazione automatica continua ad applicare solo i consigli per gli umani.

<!-- l10n:mechanical-coverage -->
**Considera la copertura di robot e mech nei consigli per gli umani** è un'opzione separata per ogni salvataggio, anch'essa disattivata per impostazione predefinita. Conteggia le assegnazioni meccaniche accettate nel fabbisogno di personale: una mansione interamente coperta può così fare a meno dell'ultimo specialista umano. Ogni lavoratore copre una sola mansione principale; i ruoli di riserva non contano come personale aggiuntivo. Aree ristrette, lavori bloccati, abilità insufficienti e restrizioni di Quality Jobs sul lavoro dei robot impediscono la copertura completa. Un robot capace solo di pulire non sostituisce un ruolo che combina pulizia e trasporto.

<!-- l10n:mechanical-learning -->
L'addestramento selezionato per gli umani resta protetto finché non raggiunge l'obiettivo, così come le assegnazioni fissate. La normale ricarica mantiene la copertura; la disattivazione, la perdita di controllo o l'uscita dalla modalità Lavoro la rimuovono. I consigli aggiornati propongono sostituti umani, mentre l'Ottimizzazione automatica, se attiva, applica le sostituzioni alla consueta scadenza oraria. Con l'integrazione disattivata, le righe di robot e mech non influiscono sui consigli per gli umani.
