---
title: Så fungerar automatiken
---
Automatiken kontrollerar kolonin ett par gånger per speltimme, och varje ändring du gör utlöser en extra genomgång direkt:

1. Räkna vad anslutna kolonister fortfarande saknar.
2. Köa tillverkningsbeställningar för implantat kolonin kan bygga (se [Produktionsalternativ](topic:production-options)).
3. Reservera färdiga föremål i lager för bestämda kolonister, efter prioritet och nivå.
4. När allt en kolonist behöver härnäst är reserverat och på plats läggs operationerna till som ett sammanhängande block sist i beställningslistan, efter befintliga beställningar.

Kolonisammanfattningen överst på fliken Översikt visar allt detta: om automatiken är på, vad produktionen tillverkar eller väntar på, hur många implantat som finns i lager och kö, och vem som står näst i tur för operation. Håll pekaren över produktions- eller operationssiffrorna för en uppdelning per implantat, inklusive vad varje föremål väntar på.

![Kolonisammanfattningen med produktions- och operationsstatus](colony-next.png)

Omgångar skjuter upp operationen tills flera implantat kan installeras under samma bedövning. En omgång är allt kolonisten behöver härnäst: antingen den aktuella nivån eller hela planen, beroende på strategi (se [Operationsalternativ](topic:surgery-options)). En strategi hoppar över omgångar helt och planerar varje implantat så snart det är reserverat och på plats.

Operationer väntar tills kolonisten är i skick att opereras. Kolonier tar aldrig föremål från varandras lager: föremål reserveras och installeras där kolonisten bor.
