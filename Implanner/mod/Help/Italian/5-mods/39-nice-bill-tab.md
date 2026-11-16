---
title: Nice Bill Tab
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **Verdetto** | ![Pienamente compatibile](verdict-blue.png|16) Pienamente compatibile (supporto dedicato) |
| **Prove** | Analisi del codice sorgente, test automatico in gioco |
| **Verificato** | 2026-11-16 |
| **Versioni** | Implanner 1.3.0, Nice Bill Tab [Workshop 3520130671](https://steamcommunity.com/sharedfiles/filedetails/?id=3520130671), file 2026-11-13 |

<!-- l10n:summary -->
Un banco Implanner mostra la sua coda di produzione e la casella del banco ha una riga propria.

<!-- l10n:toggle -->
Su un banco che può fabbricare impianti, la casella **Banco Implanner** occupa una riga propria in cima all'elenco degli ordini di Nice Bill Tab, sopra il campo di ricerca. I banchi che non fabbricano impianti mostrano Nice Bill Tab senza modifiche.

<!-- l10n:vanilla-tab -->
Un banco contrassegnato come banco Implanner mostra la coda di produzione di Implanner al posto della disposizione di Nice Bill Tab, la stessa scheda che mostra senza Nice Bill Tab (vedi [opzioni di produzione](topic:production-options)). L'interruttore e il pulsante delle impostazioni di Nice Bill Tab non sono mostrati in quella scheda. Disattiva la casella e la disposizione di Nice Bill Tab torna per quel banco.

<!-- l10n:stray-bills -->
Nice Bill Tab può creare un compito su un banco disponibile per un ingrediente mancante. I banchi Implanner vengono saltati, quindi il compito va all'altro banco più vicino in grado di fabbricare l'oggetto. Quando solo i banchi Implanner possono fabbricarlo, non viene creato alcun compito.
