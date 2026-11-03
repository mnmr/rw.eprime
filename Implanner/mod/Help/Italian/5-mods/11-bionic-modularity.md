---
title: Bionic modularity
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **Verdetto** | ![Pienamente compatibile](verdict-blue.png|16) Pienamente compatibile (supporto dedicato) |
| **Prove** | Analisi del codice sorgente, test automatico in gioco |
| **Verificato** | 2026-09-05 |
| **Versioni** | Implanner 1.1.0, Bionic modularity [Workshop 3589154549](https://steamcommunity.com/sharedfiles/filedetails/?id=3589154549), file 2026-09-05 |

<!-- l10n:summary -->
I moduli condividono la parte con un arto bionico, e un modulo per arto richiede il proprio ospite.

<!-- l10n:b-c7cd0a958354 -->
![La richiesta del requisito per un modulo per gamba](requirement-prompt.png)

<!-- l10n:b-71ab3bae9608 -->
![Il piano dopo Aggiungi al piano: la gamba bionica e il modulo sono entrambi selezionati](requirement-added.png)

<!-- l10n:b-1a485abf9e8c -->
Ogni modulo occupa uno slot sulla sua parte del corpo, quindi un modulo da combattimento e uno da lavoro stanno sullo stesso braccio, mentre due moduli da combattimento no. I moduli per gli arti si installano su un arto bionico e condividono la parte con esso, quindi un braccio bionico pianificato e un modulo per braccio coesistono. Spuntando un modulo per arto quando il piano non ha un bionico su quella parte si apre una finestra che elenca i bionici in grado di ospitarlo, con l'ospite meno avanzato selezionato per impostazione predefinita. **Aggiungi al piano** aggiunge insieme la tua scelta e il modulo. **Annulla** non aggiunge nulla.

