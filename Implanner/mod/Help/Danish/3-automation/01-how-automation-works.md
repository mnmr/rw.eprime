---
title: Sådan virker automatisering
---
Automatikken gennemgår kolonien et par gange pr. spiltime. Hver ændring, du foretager, udløser straks en ekstra gennemgang:

1. Tæl det, tilmeldte kolonister stadig mangler.
2. Sæt fremstillingsordrer i kø for implantater, kolonien kan bygge (se [Produktionsindstillinger](topic:production-options)).
3. Reserver færdige lagergenstande til bestemte kolonister efter prioriteter og trin.
4. Når alt det næste nødvendige er reserveret og på stedet, tilføjes operationerne som én blok efter kolonistens eksisterende ordrer.

Kolonioversigten øverst på Oversigt viser det hele: om automatikken er slået til, hvad produktionen fremstiller eller venter på, antal implantater på lager og i kø samt næste operationspatient. Hold markøren over produktions- eller operationstallene for detaljer pr. implantat, inklusive hvad hver genstand venter på.

![Kolonioversigt med produktions- og operationsstatus](colony-next.png)

Gruppering udsætter operationer, så flere implantater kan indsættes under én bedøvelse. Gruppen omfatter det næste, kolonisten mangler: enten det aktuelle trin eller hele planen, afhængigt af strategien (se [Operationsindstillinger](topic:surgery-options)). Én strategi springer gruppering over og planlægger hvert implantat, så snart det er reserveret og på stedet.

Operationer venter, til kolonisten er rask nok. Kolonier tager aldrig genstande fra hinandens lagre: Reservation og indsættelse sker, hvor kolonisten bor.
