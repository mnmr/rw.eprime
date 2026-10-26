---
title: Dietro le quinte
---
I ruoli ordinati di ogni colono vengono tradotti in un unico ordine rigoroso di attività, fornito al gioco tramite gli stessi elenchi letti dalla selezione originale del lavoro. Il comportamento sul lavoro corrisponde esattamente a quello ottenuto con una griglia di priorità regolata a mano, senza patch a ogni tick né lavoro a ogni fotogramma.

Tutto viene ricalcolato quando cambiano ruoli o assegnazioni, poi conservato in cache. I ruoli condizionali si aggiornano anche allo scoccare delle ore e quando il colono cambia luogo. Le emergenze, come spegnere incendi, passano dal controllo d'emergenza del gioco quando sono coperte da uno dei primi cinque ruoli attivi; le altre rimangono nella posizione normale. Con “Solo i primi 5 ruoli interrompono il lavoro per le emergenze” disattivo, ogni emergenza coperta usa quel controllo.

WorkRoles mantiene aggiornate le priorità del gioco base a partire dai ruoli, convertendole nel familiare intervallo da 0 a 4 quando l'apposita opzione è attiva, come da impostazione predefinita. Per questo rimuovere la mod è sicuro: torna la scheda Lavoro originale, già popolata con i ruoli convertiti in priorità. Aggiungere la mod a un salvataggio esistente esegue la conversione inversa.
