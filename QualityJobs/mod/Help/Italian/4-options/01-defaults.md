---
title: Opzioni e impostazioni predefinite
---
<!-- l10n:options-image -->
![La scheda Opzioni con le impostazioni predefinite degli ordini e delle costruzioni](options.png)

<!-- l10n:scope -->
Con una partita caricata, la scheda Opzioni modifica le impostazioni di quella colonia. Dal menu principale, le impostazioni della mod definiscono i valori predefiniti per le nuove partite. **{k:QJ_SettingsShowToolbarButton}** è una preferenza di visualizzazione personale; nasconderlo non disabilita la mod. Puoi comunque aprire le impostazioni della mod dal menu Opzioni del gioco.

<!-- l10n:bill-defaults -->
Le **{k:QJ_SettingsBillDefaults}** regolano gli ordini che seguono le impostazioni predefinite. Le impostazioni dei singoli ordini hanno la precedenza. **{k:QJ_SettingsManageNewBills}** influisce sui nuovi ordini e su quelli esistenti che seguono ancora questa impostazione predefinita di gestione. Disattivandola, gli oggetti in attesa di quegli ordini vengono sbloccati e possono essere completati normalmente. Anche le altre impostazioni predefinite degli ordini possono influire sugli ordini esistenti per i quali la relativa impostazione non è stata personalizzata. Il limite delle scorte incompiute è condiviso per prodotto, con un conteggio separato per ogni mappa.

<!-- l10n:construction-defaults -->
Le **{k:QJ_SettingsConstructionDefaults}** vengono applicate quando si abilita una costruzione o la si gestisce automaticamente. Gli edifici già gestiti mantengono le proprie impostazioni. La qualità desiderata per la costruzione usa **{k:QJ_NoRetries}**; quella per la produzione usa **{k:QJ_AnyQuality}**.

<!-- l10n:global -->
**{k:QJ_SettingsShareWork}** e **{k:QJ_SettingsHighPriorityFinish}** si applicano a tutta la colonia. Consulta [condivisione](topic:sharing-work) e [completamento](topic:finishers) per conoscerne gli effetti. **{k:QJ_DisableButton}** serve per la [rimozione sicura](topic:removing), non per sospendere un singolo ordine.

<!-- l10n:robot-preparation -->
**{k:QJ_SettingsAllowRobotPreparation}** viene salvata per la colonia attuale ed è inizialmente attiva, anche nei salvataggi esistenti. Disattivala per riservare ai coloni il lavoro sugli ordini di produzione e sui progetti di costruzione gestiti, così che acquisiscano l'esperienza nelle abilità derivante dal lavoro. Questo interrompe anche il lavoro dei mech e dei robot che se ne stanno già occupando. I mech idonei e i robot supportati possono comunque completare i lavori assegnati da Quality Jobs. I normali lavori non gestiti non sono interessati; gli [oggetti incompiuti condivisi](topic:sharing-work) mantengono la restrizione dell'ordine gestito originale. Questa opzione è disponibile con una partita caricata ed è sincronizzata in Multiplayer.

