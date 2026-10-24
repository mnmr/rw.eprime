---
title: Ordine delle assegnazioni
---
Un colono percorre la propria riga da sinistra a destra, considerando soltanto i ruoli attivi:

![Tessere ordinate sulla riga di un colono](colonist-row.png)

1. I ruoli precedenti prevalgono sui successivi, attività per attività.
2. All'interno di un ruolo, le attività precedenti vengono prima.
3. Un'attività presente in più ruoli conta nella sua prima posizione.

I coloni svolgono soltanto attività dei ruoli assegnati. Non assegnare un ruolo equivale a lasciare senza numero la casella della griglia originale.

Per cambiare priorità basta trascinare:

@demo:chip-drag

Puoi anche trascinare tra righe: rilascia una tessera su un altro colono per spostare l'assegnazione. Fai clic sul cestino nella tessera per rimuoverla dalla riga.

RimWorld mantiene un elenco separato per le emergenze: spegnere incendi, prestare cure urgenti e andare a letto per ricevere cure d'emergenza. Queste attività interrompono sonno, pasti e qualsiasi altro lavoro. Per impostazione predefinita, un'attività urgente entra nell'elenco solo se è coperta da uno dei primi cinque ruoli attivi del colono. Le emergenze dei ruoli successivi vengono svolte nella posizione normale. Un colono con Essenziale al primo posto e Medico al sesto interrompe comunque tutto per un incendio, ma tratta le cure urgenti come normale lavoro medico nella posizione della riga. Spostare Essenziale oltre il quinto posto rende anche lo spegnimento incendi un lavoro ordinario per quel colono.

Il comando “Solo i primi 5 ruoli interrompono il lavoro per le emergenze” nella scheda Opzioni può disattivare questo limite. In tal caso, ogni emergenza coperta da un ruolo attivo entra nell'elenco, indipendentemente dalla posizione.
