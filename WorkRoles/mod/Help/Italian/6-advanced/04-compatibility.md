---
title: Compatibilità con le mod
---
<!-- l10n:b-6110204fbb19 -->
I lavori delle altre mod vengono rilevati automaticamente; alcune mod hanno un'integrazione più profonda. Le specializzazioni di Vanilla Skills Expanded e Alpha Skills alimentano i segnali dei consigli; i lavori odiati di More Than Capable diventano un segnale Pessimo; Colony Groups appare tra i raggruppamenti; le mod con più piani trattano i piani sovrapposti come un unico luogo.

<!-- l10n:b-c7669aaaba7d -->
Le mod opzionali di lavoro e abilità possono fornire profili aggiuntivi delle attività. Questi identificano le abilità utilizzate e allenate e possono precisare l'abilità principale usata nei consigli quando le mod pertinenti sono installate. WorkRoles lascia alle mod fornitrici idoneità, designazioni ed esecuzione delle attività. Queste integrazioni non introducono dipendenze obbligatorie.

<!-- l10n:b-27d73501db9c -->
I consigli leggono anche categorie di lavoro dichiarate, capacità richieste, opposizione dei precetti e rifiuti verificati dell'estrazione ordinaria. Per le versioni controllate di Humanoid Alien Races su RimWorld 1.6 e 1.7, usano le restrizioni effettive del framework su fornitori di lavoro e ricette. Specie diverse che adottano quelle regole beneficiano della stessa integrazione. Una ricetta bloccata non rimuove altre ricette utilizzabili dal ruolo e non conta come modo per allenarne l'abilità.

<!-- l10n:b-15011508cb81 -->
Le versioni sconosciute dei framework conservano l'idoneità esistente e segnalano che le loro regole non sono supportate. Restrizioni nascoste nel codice di attività arbitrarie non possono essere rilevate in sicurezza provando le attività durante la raccolta dei dati per i consigli. Le versioni supportate di Vivi forniscono velocità, resa e idoneità alla raccolta dipendenti dall'equipaggiamento. Versioni Vivi sconosciute, regole di raccolta personalizzate e regole Ratkin non verificate conservano il comportamento esistente; non si deducono restrizioni dal nome della specie o dello xenotipo.

<!-- l10n:b-cc8167cf5cfc -->
Le estensioni che modificano fornitori di lavoro esistenti continuano a usare gli stessi ruoli e lo stesso ordine. Per esempio, i ruoli di gestione degli animali continuano a usare Animali per i consigli. I guadagni condizionali di esperienza in un'altra abilità dipendono dalle regole di idoneità dell'estensione; non creano un percorso formativo generale per ogni colono assegnato a quel lavoro.

<!-- l10n:b-f6d4800368e1 -->
Due famiglie di mod entrano in conflitto. Le altre sostituzioni della scheda Lavoro, come varianti di Work Tab e Better Work Tab, competono per la stessa interfaccia. Le mod che impostano priorità, come Free Will e PriorityMaster, competono per gli stessi dati: WorkRoles controlla le priorità dei coloni gestiti, quindi blocca quelle scritture. Quando accade, un avviso mostrato una sola volta nomina la mod e suggerisce come ottenere lo stesso effetto con i ruoli.

<!-- l10n:b-3e9c0a143130 -->
Il controllo delle priorità riguarda soltanto i personaggi umanoidi. Le mod che gestiscono le priorità dei meccanoidi, come Mech Work Tab, sono quindi pienamente compatibili.

<!-- l10n:b-3df0d9a43535 -->
Alcune mod richiedono priorità da 0 a 4. In caso di problemi, verifica che “Comunica alle altre mod priorità da 0 a 4” sia attivo.

<!-- l10n:b-99382dfb0e0e -->
Le mod che si limitano a leggere le priorità funzionano: vedono i valori calcolati da WorkRoles.
