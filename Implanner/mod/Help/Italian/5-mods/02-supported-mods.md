---
title: Mod supportate
---
Ogni mod di impianti che usa i dati chirurgici del gioco funziona senza configurazione. I giocatori hanno confermato Expanded Prosthetics and Organ Engineering e CeleTech, i cui impianti vengono letti come quelli del gioco base. Le mod seguenti sono state controllate singolarmente nei loro dati.

![Un ceppo di meccaniti selezionato nell'elenco Torso](mechanites-picked.png)

![Le altre righe dei ceppi indicano che sostituiranno quello selezionato](mechanites-overrides.png)

**[FSF] Advanced Bionics Expansion**: i ceppi di meccaniti si escludono a vicenda. Iniettarne uno rimuove ogni altro ceppo dal corpo, quindi il selettore li tratta come un unico alloggiamento e indica il ceppo sostituito. I meccaniti che rafforzano l'immunità non sono un ceppo e convivono con tutti. La vescica bionica avanzata e il potenziatore igienico avanzato sono coperti dalle opzioni di compatibilità insieme a Dubs Bad Hygiene.

**Dubs Bad Hygiene**: la vescica bionica e il potenziatore igienico sono impianti del torso che il gioco permette di combinare con le versioni avanzate di [FSF] Advanced Bionics Expansion. Le due opzioni di compatibilità decidono se anche Implanner lo consente.

![Gli occhi bionici semplici e avanzati indicano la sostituzione dell'occhio modulare selezionato](modular-eye.png)

**Integrated Implants**: è consentito un braccio aggiuntivo per lato; sceglierne un secondo tipo sullo stesso lato sostituisce il primo. L'armatura sottocutanea e la sua versione prestigiosa, le torrette sulle spalle, le code e gli esplosivi impiantati formano ciascuno un gruppo di cui si può installare un solo elemento. I bionici modulari sono potenziamenti sul posto: scegliendo un occhio bionico modulare, l'automazione installa prima un occhio bionico se manca, poi esegue il potenziamento, che consuma un componente e non un secondo occhio. Questi potenziamenti richiedono EBSG Framework e non vengono proposti con Medical System Expansion 2 caricato. I moduli stessi si inseriscono dall'inventario senza intervento, quindi non compaiono nell'elenco.

**Vanilla Genetics Expanded**: gli impianti ibridi per coloni compaiono insieme agli altri. Quelli per animali no, perché Implanner pianifica soltanto i coloni.

Alcuni ibridi sostituiscono una parte del corpo: il modulo di ibernazione sostituisce il cervello. Altri si appoggiano alla parte: il rinforzo neuronale si colloca sul cervello. Gli impianti normali possono essere aggiunti a una parte che ospita un ibrido, ma non a una parte sostituita.

Installare un impianto ibrido espelle tutto ciò che è già installato su quella parte. Gli impianti espulsi cadono accanto al medico.

- Implanner installa prima l'ibrido, così non occorre espellere nulla.
- Se il colono possiede già impianti lì, Implanner li lascia cadere, li riserva a quel colono e li reinstalla subito dopo.
- Implanner non installa mai un ibrido se qualcosa su quella parte andrebbe perso definitivamente.

Gli ibridi ereditano la qualità dal genotelaio usato per produrli; vedi [qualità minima](topic:minimum-quality).

**Quality Bionics Remastered**: i bionici hanno qualità; quelli migliori funzionano meglio e hanno più punti salute. Il suggerimento mostra l'efficienza della parte dalla qualità pessima alla leggendaria e ogni piano stabilisce la qualità minima installabile; vedi [qualità minima](topic:minimum-quality).

**Elite Bionics Framework**: gli impianti che aumentano o riducono i punti salute di una parte mostrano la variazione nel suggerimento.

![La richiesta del componente necessario per un modulo della gamba](requirement-prompt.png)

![Il piano dopo Aggiungi al piano, con gamba bionica e modulo selezionati](requirement-added.png)

**Bionic modularity**: ogni modulo occupa un alloggiamento sulla propria parte del corpo. Un modulo da combattimento e uno da lavoro possono condividere un braccio, ma due da combattimento no. I moduli per arti si installano su un arto bionico e ne condividono la parte, quindi un braccio bionico e un modulo pianificati possono convivere. Se selezioni un modulo senza un bionico previsto su quella parte, una finestra elenca i bionici capaci di ospitarlo, scegliendo per impostazione predefinita quello meno avanzato. **Aggiungi al piano** aggiunge insieme la scelta e il modulo. **Annulla** non aggiunge nulla.
