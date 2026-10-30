---
title: Condividere i gruppi
---
<!-- l10n:b-a06a00109adf -->
I quattro pulsanti in alto nell'editor permettono di condividere, salvare o reimpostare la configurazione:

<!-- l10n:b-3abef8172f5c -->
![Importa, Esporta, Imposta valori predefiniti e Ripristina i valori predefiniti](editor-buttons.png)

<!-- l10n:b-d2699fb79187 -->
## Esportare una configurazione

<!-- l10n:b-e4537f800fee -->
Esporta salva gruppi, insiemi di risorse, soglie, eccezioni di conteggio e impostazioni del giocatore. Scegli un percorso e un nome per il file, poi fai clic su Salva oppure usa Copia negli appunti.

<!-- l10n:b-355662a5828e -->
![Anteprima e comandi di esportazione](export-setup.png)

<!-- l10n:b-fe372ca48bad -->
Le impostazioni del giocatore includono i gruppi attivi e le opzioni globali di visualizzazione e conteggio. Sono esclusi il numero di livelli mostrati, il testo di ricerca, le posizioni di scorrimento e lo stato temporaneo dell'editor.

<!-- l10n:b-cf6b7284c676 -->
## Scegliere cosa importare

<!-- l10n:b-5c19c2c6b9e4 -->
Importa legge un file salvato o il contenuto degli appunti e mostra un'anteprima prima di modificare qualsiasi cosa. Seleziona le categorie da sostituire. La casella Impostazioni del giocatore e gruppi attivi è inizialmente deselezionata, così puoi mantenere le tue preferenze di visualizzazione.

<!-- l10n:b-537b3af44d4d -->
![Anteprima dell'importazione con selezione delle categorie](import-selection.png)

<!-- l10n:b-1c0ab7a10c14 -->
Puoi selezionare ogni categoria presente nel file. Puoi cambiare le categorie correlate una alla volta, anche se una scelta intermedia è incompleta. Importa resta disabilitato finché la configurazione scelta non contiene riferimenti validi a insiemi e gruppi; passa il puntatore sul pulsante disabilitato per vedere cosa manca.

<!-- l10n:import-dependency-recovery -->
Se le soglie o le regole di conteggio delle risorse richiedono un insieme mancante, importa queste impostazioni insieme ai relativi Insiemi di risorse, oppure lascia invariate queste categorie. Se le impostazioni del giocatore richiedono un gruppo mancante, importa insieme **Gruppi** e **Impostazioni del giocatore e gruppi attivi**, oppure lascia invariati entrambi.

<!-- l10n:b-3081ef3ab31d -->
Le categorie assenti dalle vecchie esportazioni restano invariate. Le voci che richiedono contenuti di mod non disponibili vengono ignorate.

<!-- l10n:b-84361107e203 -->
In multigiocatore, gruppi, insiemi, soglie ed eccezioni di conteggio importati cambiano la configurazione per tutti. Le impostazioni del giocatore importate valgono solo per te.

<!-- l10n:b-f4e74689b23c -->
## Salvare o ripristinare i valori predefiniti

<!-- l10n:b-6181f2c14286 -->
Imposta valori predefiniti salva una configurazione personale per le colonie future (vedi [Valori predefiniti per nuove partite](topic:new-game-defaults)). Ripristina i valori predefiniti chiede conferma, poi sostituisce tutti i gruppi e gli insiemi di risorse con quelli predefiniti inclusi nella mod e cancella tutte le soglie e le eccezioni di conteggio. In multigiocatore, il ripristino modifica la configurazione condivisa da tutti.
