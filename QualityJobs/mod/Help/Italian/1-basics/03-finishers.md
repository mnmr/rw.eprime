---
title: Scegliere gli addetti alla rifinitura
---
<!-- l10n:requirements -->
Usa questi controlli su un ordine di produzione o un edificio. Altri lavoratori idonei possono svolgere le fasi iniziali; solo il tocco finale deve soddisfare i requisiti scelti.

<!-- l10n:controls -->
- **Abilità per il completamento** imposta un livello minimo di abilità.
- **{k:QJ_AutoBest}** sceglie automaticamente il lavoratore idoneo migliore. Il cursore manuale viene conservato per quando disattivi questa opzione.
- **{k:QJ_RequireInspired}** attende quell'ispirazione. Completare il lavoro la consuma come di consueto.
- **{k:QJ_RequireSpecialist}** richiede un ruolo di Ideology che conferisca un bonus alla qualità di produzione. Questa opzione compare con Ideology.

<!-- l10n:odds-image -->
![Requisiti per il completamento e anteprima delle probabilità di qualità](finisher-options.png)

<!-- l10n:best -->
La regolazione automatica confronta i coloni idonei, i mech della colonia e i robot supportati e sceglie il lavoratore con la qualità attesa più alta. Tiene conto dell’abilità, di Creatività ispirata, dei bonus degli specialisti della produzione e dei bonus alla qualità delle mod supportate. Questo massimizza la qualità media, ma non garantisce il risultato di un singolo tiro.

<!-- l10n:availability -->
I lavoratori che soddisfano gli altri requisiti restano nel confronto anche quando sono assenti, a terra, addormentati, in crisi mentale o in ricarica, compresi i robot nelle loro stazioni. Se nessuno dei lavoratori in cima alla classifica può completare il lavoro su questa mappa, il lavoro attende. Ad esempio, un artigiano migliore in viaggio con una carovana può impedire a un lavoratore meno qualificato rimasto alla colonia di completarlo. Non esiste un tempo d’attesa dopo il quale il requisito di qualità viene abbassato.

<!-- l10n:manual-threshold -->
Con la regolazione automatica disattivata, qualsiasi lavoratore capace di svolgere il lavoro può completarlo se soddisfa la soglia di abilità impostata manualmente e gli altri requisiti. Un lavoratore migliore altrove nella colonia non lo blocca.

<!-- l10n:robot-finishers -->
I meccanoidi e i robot supportati devono soddisfare gli stessi requisiti di completamento dei coloni. Richiedere Creatività ispirata o un ruolo di specialista della produzione esclude comunque i lavoratori che ne sono privi. Disattivare **{k:QJ_SettingsAllowRobotPreparation}** riserva le fasi iniziali ai coloni; i meccanoidi e i robot idonei possono comunque ricevere incarichi di rifinitura da Quality Jobs.

<!-- l10n:odds -->
**{k:QJ_OddsHeader}** confronta la tua **{k:QJ_OddsColConfig}** con il candidato **{k:QJ_OddsColBest}** attuale. Sono probabilità, non garanzie. **Lavoratori idonei al completamento** elenca i lavoratori che possono soddisfare i requisiti su questa mappa in questo momento; i lavoratori assenti, a terra o in crisi mentale sono esclusi dall'elenco.

<!-- l10n:priority -->
**{k:QJ_SettingsHighPriorityFinish}** dà precedenza al lavoro di rifinitura assegnato rispetto agli altri lavori, tranne spegnere incendi, prestare cure mediche, soccorrere e riposare a letto. Non interrompe l'incarico in corso. Se l'opzione è disattivata, la rifinitura ha la precedenza all'interno del proprio tipo di lavoro, dopo i tipi di lavoro con priorità superiore. Il lavoratore deve essere in grado di svolgere il normale tipo di lavoro dell'oggetto.

