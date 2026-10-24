---
title: Produksjonsvalg
---
Med **Opprett produksjonsbestillinger automatisk for manglende implantater** på legger Implanner bestillinger på manglende implantater ved benker som kan lage dem. Den fjerner sine egne bestillinger når behovet forsvinner, men aldri bestillinger du opprettet selv. Hver bestilling utfører én produksjon, i samme rekkefølge som operasjonene skal installere implantatene: med Fullstendige sett kommer kolonistens bein før armene hvis bein rangeres høyere i planen.

Når flere oppskrifter lager samme implantat, foretrekker Implanner en tilgjengelig vei den kan betale for nå. Et ledig grunnimplantat kan oppgraderes; ellers kan en direkte oppskrift lage det avanserte implantatet av materialer. Er bare oppgraderingsveien aktivert, kan produksjon av mellomprodukter lage grunnimplantatet først. Oppskrifter må være forsket på og støttet av en egnet benk.

Gi implantatproduksjonen en egen benk ved å krysse av **Implanner-benk** på benkens Bestillinger-fane. Boksen vises bare på benker som kan lage implantater. Eksisterende bestillinger settes på pause, og Implanner bruker denne benken før andre. Mens valget er avkrysset, kan du ikke legge til, lime inn, pause eller gjenoppta bestillinger der.

![En fabrikasjonsbenk overlevert til Implanner](implanner-bench.png)

Fjern krysset for å ta benken tilbake. Bestillinger som allerede var på pause da du krysset av, forblir på pause; resten fortsetter. Fjerner du Implanner mens benken er avkrysset, virker benken fortsatt, men du må gjenoppta bestillingene selv.

![Produksjonsvalgene](production-options.png)

- **Benker som kan ha bestillinger** begrenser hvor mange benker per koloni som arbeider med implantater samtidig. Hver benk har opptil to Implanner-bestillinger, så neste venter når den første er ferdig. Implanner-benker teller med.
- **Opprett bare bestillinger ved ledige benker** holder implantatbestillinger unna benker med ventende arbeid, så dine egne køer beholder prioriteten.
- **Bruk bare Implanner-benker** holder implantatbestillinger på disse benkene. En koloni uten en slik benk lager da ingen implantater. Når valget er av, brukes andre benker når hver Implanner-benk har to bestillinger.
- **Påkrevd håndverksferdighet for produksjonsbestillinger** holder dyre materialer unna lite erfarne håndverkere.
- **Tillat produksjonsbestillinger for manglende mellomprodukter** følger mangler nedover kjeden: én avansert komponent for lite til neste bioniske bein betyr en komponentbestilling først. Mens et implantat venter på en råressurs som plaststål, lager ledige benker de avanserte komponentene det trenger. Industrivarer som komponenter og grunnimplantater kvalifiserer. Råressurser som stål produseres aldri, selv om en smelteoppskrift kunne laget dem; dem må du fortsatt samle selv.

Alle bestillinger respekterer også minstelageret (se [beholde lager](topic:keeping-stock)).
