---
title: Produktionsindstillinger
---
Med **Tilføj automatisk fremstillingsordrer for manglende implantater** opretter Implanner ordrer ved egnede bænke og fjerner sine egne ordrer, når behovet forsvinder. Dine egne ordrer fjernes aldrig. Hver ordre fremstiller ét eksemplar i operationsrækkefølgen: Med Hele sæt kommer en kolonists ben før armene, hvis benene rangerer højere.

Hvis flere opskrifter fremstiller samme implantat, foretrækker Implanner en tilgængelig vej, der kan betales nu. Et frit grundimplantat kan opgraderes; ellers kan en direkte opskrift fremstille det avancerede implantat af materialer. Hvis kun opgraderingsvejen er aktiv, kan mellemproduktion fremstille grundimplantatet først. Opskrifter skal være udforsket og understøttet af en egnet bænk.

Markér **Implanner-bænk** på bænkens ordrefane for at give implantatfremstilling sin egen bænk. Feltet vises kun ved bænke, der kan fremstille implantater. Eksisterende ordrer suspenderes, og Implanner bruger bænken før andre. Mens feltet er markeret, kan du ikke tilføje, indsætte, suspendere eller genoptage ordrer.

![En fabrikationsbænk overdraget til Implanner](implanner-bench.png)

Fjern markeringen for at tage bænken tilbage. Ordrer, der allerede var suspenderet før markeringen, forbliver suspenderet; resten genoptages. Fjerner du Implanner, mens bænken er markeret, fungerer bænken stadig, men du må selv genoptage ordrerne.

![Produktionsindstillingerne](production-options.png)

- **Bænke, der må have ordrer** begrænser antallet af samtidige bænke pr. koloni. En bænk rummer op til to Implanner-ordrer, så den næste venter, når den første er færdig. Implanner-bænke tæller med.
- **Opret kun ordrer ved ledige bænke** holder implantatordrer væk fra bænke med ventende arbejde, så dine egne køer har forrang.
- **Brug kun Implanner-bænke** begrænser implantatordrer til disse bænke. En koloni uden en sådan fremstiller intet. Slået fra bruges andre bænke, når alle Implanner-bænke har to ordrer.
- **Krævet håndværksfærdighed til produktionsordrer** holder dyre materialer væk fra uøvede håndværkere.
- **Tillad produktionsordrer for manglende mellemprodukter** følger mangler ned gennem kæden: Mangler næste bioniske ben en avanceret komponent, oprettes komponentordren først. Mens et implantat venter på en råressource som plaststål, fremstiller ledige bænke de avancerede komponenter, det skal bruge. Fremstillede genstande som komponenter og grundimplantater er egnede. Råressourcer som stål fremstilles aldrig, selv med en mulig smelteopskrift; dem må du selv skaffe.

Alle ordrer respekterer også minimumslagrene (se [Bevar lagerbeholdningen](topic:keeping-stock)).
