---
title: Come vengono letti gli impianti delle mod
---
Implanner non ha bisogno di un elenco di mod supportate per leggere gli impianti. Legge tutti gli interventi caricati dal gioco base, dalle espansioni e dalle mod; ogni intervento che installa un impianto su una parte del corpo diventa una voce del selettore. Gli stessi dati indicano quali impianti possono convivere:

- **Una sostituzione per parte**: un braccio bionico sostituisce la spalla, quindi due sostituzioni non condividono mai una parte. Una sostituzione rimuove tutti gli impianti montati su quella parte o sulle parti sottostanti.
- **Impianti sulla stessa parte**: convivono, salvo che l'intervento li dichiari incompatibili. È così che le ghiandole cutanee si escludono nel gioco base e che le mod definiscono gli alloggiamenti dei moduli.
- **Rimozione all'installazione**: alcuni impianti rimuovono un concorrente ovunque sia nel corpo appena vengono installati. Due impianti che si rimuovono a vicenda non possono convivere.
- **Interventi che svuotano una parte del corpo**: alcuni rimuovono tutto da una parte prima di aggiungere un impianto che non la sostituisce. Gli impianti aggiunti dopo restano, quindi Implanner installa prima quello e rimette ciò che espelle.
- **Moduli montati sui bionici**: un intervento il cui esecutore è scritto per installare su una parte artificiale condivide quella parte con il bionico invece di sostituirlo.
- **Potenziamenti sul posto**: un intervento che rimuove un impianto e ne aggiunge un altro migliora la parte installata. La versione potenziata compare come impianto distinto.
- **Oggetti non fabbricabili**: un impianto che nessun banco può produrre resta nascosto finché attivi l'opzione corrispondente nella scheda Opzioni.

Due cose non sono leggibili. I moduli inseriti dal colono dall'inventario senza intervento non compaiono mai; le regole presenti soltanto nel codice di una mod, senza dati corrispondenti, non sono visibili. Tutto ciò che una mod esprime tramite i propri interventi funziona senza modificare Implanner.
