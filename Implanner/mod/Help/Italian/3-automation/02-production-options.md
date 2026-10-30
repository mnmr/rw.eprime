---
title: Opzioni di produzione
---
<!-- l10n:b-bdd2b459773e -->
Con **Aggiungi automaticamente ordini per gli impianti mancanti** attivo, Implanner aggiunge ordini ai banchi capaci di produrli e rimuove i propri ordini quando il bisogno scompare. Non rimuove mai gli ordini creati da te. Ogni ordine produce un oggetto, nell'ordine previsto per l'installazione: con Set completi, le gambe di un colono precedono le braccia se sono classificate più in alto nelle classifiche condivise degli impianti.

<!-- l10n:b-8effc9e9b828 -->
Quando più ricette producono lo stesso impianto, Implanner preferisce un percorso disponibile che possa pagare subito. Un impianto base libero può essere potenziato; altrimenti una ricetta diretta può produrre quello avanzato dai materiali. Se è abilitato solo il potenziamento, la produzione di intermedi può fabbricare prima la base. Le ricette devono essere ricercate e supportate da un banco idoneo.

<!-- l10n:b-0aa3ded1d655 -->
Per dedicare un banco agli impianti, attiva **Banco Implanner** nella sua scheda Ordini. La casella appare solo sui banchi capaci di fabbricare impianti. Gli ordini già presenti vengono sospesi e Implanner aggiunge i propri a questo banco prima che agli altri. Finché la casella è attiva, non puoi aggiungere, incollare, sospendere o riprendere ordini su quel banco.

<!-- l10n:b-a4d04c1bceb2 -->
![Un banco di fabbricazione affidato a Implanner](implanner-bench.png)

<!-- l10n:b-a89a880dcac5 -->
Disattivala per riprendere il controllo del banco. Gli ordini già sospesi prima dell'attivazione restano sospesi; gli altri riprendono. Se rimuovi Implanner mentre il banco è dedicato, questo continua a funzionare, ma devi riprenderne manualmente gli ordini.

<!-- l10n:b-c9f0dad1a5c5 -->
![Le opzioni di produzione](production-options.png)

<!-- l10n:b-f6f358878993 -->
- **Banchi che possono ricevere ordini** limita quanti banchi per colonia lavorano contemporaneamente agli impianti. Ogni banco contiene fino a due ordini di Implanner, così il successivo attende già quando termina il primo. Anche i banchi dedicati contano nel limite.
- **Crea ordini solo sui banchi inattivi** evita di aggiungere impianti a banchi con lavori in attesa, mantenendo prioritarie le tue code.
- **Usa solo banchi Implanner** limita gli ordini ai banchi dedicati: una colonia senza questi banchi non produce impianti. Se disattivo, gli altri banchi vengono usati quando ogni banco Implanner contiene due ordini.
- **Abilità Artigianato richiesta per gli ordini di produzione** tiene i materiali costosi lontani dagli artigiani inesperti.
- **Consenti ordini per i prodotti intermedi mancanti** segue le carenze lungo la catena: se manca un componente avanzato per la prossima gamba bionica, viene ordinato prima quello. Mentre un impianto attende una risorsa grezza, come il plastacciaio, i banchi liberi producono i componenti avanzati necessari. Sono ammessi i prodotti fabbricati, come componenti e impianti base. Le risorse grezze, come l'acciaio, non vengono mai prodotte, neppure quando una ricetta di fusione potrebbe fornirle: devi comunque raccoglierle tu.

<!-- l10n:b-24089426e9a9 -->
Ogni ordine rispetta anche le scorte minime; vedi [conservare le scorte](topic:keeping-stock).
