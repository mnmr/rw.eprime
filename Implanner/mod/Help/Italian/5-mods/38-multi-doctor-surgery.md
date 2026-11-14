---
title: Multi-Doctor Surgery
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **Verdetto** | ![Pienamente compatibile](verdict-blue.png|16) Pienamente compatibile (supporto dedicato) |
| **Prove** | Analisi del codice sorgente, test automatico in gioco |
| **Verificato** | 2026-11-14 |
| **Versioni** | Implanner 1.2.9, Multi-Doctor Surgery [Workshop 3356249192](https://steamcommunity.com/sharedfiles/filedetails/?id=3356249192) |

<!-- l10n:summary -->
Le operazioni pianificate da Implanner possono essere eseguite da una squadra chirurgica.

<!-- l10n:teams -->
L'opzione **Squadre chirurgiche** nella scheda Opzioni sceglie chi esegue le operazioni di Implanner. **Singolo**, l'impostazione predefinita, fa operare un solo medico, come senza Multi-Doctor Surgery. **Squadra predefinita** usa la squadra chirurgica predefinita impostata nella scheda Operazioni di Multi-Doctor Surgery. **Squadra migliore** fa guidare il medico disponibile più abile, assistito dai medici che aumentano di più la probabilità di successo, fino al limite di medici di Multi-Doctor Surgery. I medici meccanoidi possono unirsi alla squadra quando Multi-Doctor Surgery li elenca.

<!-- l10n:rules -->
Implanner assegna la squadra quando pianifica un'operazione, che poi compare nella scheda Operazioni di Multi-Doctor Surgery. Un'operazione di squadra attende il chirurgo capo, che può operare anche quando la selezione automatica del medico richiederebbe un'abilità più alta. Se il capo predefinito non può operare su quel colono, ad esempio perché è lui stesso il paziente o si trova in un'altra colonia, un solo medico esegue l'operazione senza squadra. Le operazioni escluse nelle impostazioni di Multi-Doctor Surgery restano singole. Le squadre non vengono usate in multigiocatore.

<!-- l10n:manual -->
Le operazioni aggiunte a mano ottengono comunque la finestra della squadra e i bonus di Multi-Doctor Surgery, anche sui coloni che seguono un piano di Implanner.

