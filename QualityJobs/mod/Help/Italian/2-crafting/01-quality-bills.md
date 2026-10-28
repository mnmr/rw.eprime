---
title: Configurare un ordine
---
<!-- l10n:open -->
Apri **Dettagli** in un ordine di produzione per un oggetto dotato di qualità. Il pannello **{k:QJ_QualityPanelTitle}** appare accanto ai normali controlli dell'ordine per le ricette supportate che lasciano un oggetto incompleto. Le ricette senza qualità o lavoro incompleto mantengono il comportamento normale.

<!-- l10n:bill-image -->
![Un ordine di produzione a un banco da lavoro con il pannello Quality Jobs](crafting-bill.png)

<!-- l10n:setup -->
Attiva **{k:QJ_ManageBill}**, poi scegli i [requisiti per la rifinitura](topic:finishers). I normali lavoratori possono svolgere gran parte della produzione. Se il lavoratore attuale soddisfa i requisiti, completa direttamente l'oggetto; altrimenti l'oggetto quasi finito viene messo da parte e l'ordine può preparare altro lavoro entro il proprio limite di scorta.

<!-- l10n:dispatch -->
Quando un addetto alla rifinitura soddisfa i requisiti, Quality Jobs crea un ordine temporaneo **Completa …** assegnato a lui. L'addetto raccoglie l'oggetto incompleto e lo completa. L'ordine temporaneo viene poi rimosso automaticamente. Gli oggetti in attesa possono essere trasportati e immagazzinati come gli altri oggetti incompleti.

<!-- l10n:status -->
La sezione **{k:QJ_StatusHeader}** mostra i lavori **Incompleti** condivisi, gli oggetti **In attesa** di un addetto al completamento e quelli già assegnati per il **Completamento**. Passa il puntatore sullo stato e sull'elenco degli addetti al completamento per maggiori dettagli. Se il lavoro non procede, consulta [quando il lavoro resta in attesa](topic:waiting).

<!-- l10n:disable -->
Deseleziona **{k:QJ_ManageBill}** per tornare a completarlo normalmente. Se la condivisione è attiva, gli altri lavoratori possono comunque proseguirne il lavoro condiviso.

