---
title: Produktionsalternativ
---
Med **Skapa tillverkningsbeställningar automatiskt för saknade implantat** aktiverat lägger Implanner beställningar på lämpliga bänkar och tar bort sina egna när behovet försvinner. Dina egna beställningar tas aldrig bort. Varje beställning tillverkar ett implantat, i operationsordning: med Fullständiga uppsättningar kommer kolonistens ben före armarna om benen har högre nivå i planen.

När flera recept ger samma implantat föredrar Implanner en tillgänglig väg som den kan betala för nu. Ett ledigt grundimplantat kan uppgraderas; annars kan ett direktrecept tillverka det avancerade implantatet av material. Om bara uppgraderingsvägen är aktiverad kan produktion av mellanprodukter tillverka grundimplantatet först. Recept måste vara utforskade och stödjas av en lämplig bänk.

För att ge implantattillverkningen en egen bänk markerar du **Implanner-bänk** på bänkens flik Beställningar. Kryssrutan visas bara på bänkar som kan tillverka implantat. Befintliga beställningar pausas, och Implanner väljer bänken före andra. Medan den är markerad kan du inte lägga till, klistra in, pausa eller återuppta beställningar på den.

![En fabrikationsbänk överlämnad till Implanner](implanner-bench.png)

Avmarkera för att ta tillbaka bänken. Beställningar som redan var pausade när du markerade den förblir pausade; resten återupptas. Tar du bort Implanner medan bänken är markerad fungerar bänken fortfarande, men du måste själv återuppta beställningarna.

![Produktionsalternativen](production-options.png)

- **Bänkar som får ha beställningar** begränsar antalet bänkar per koloni som arbetar med implantat samtidigt. Varje bänk har upp till två Implanner-beställningar, så nästa väntar när den första blir klar. Implanner-bänkar räknas med.
- **Skapa bara beställningar vid lediga bänkar** undviker bänkar där arbete redan väntar, så dina egna köer behåller förtur.
- **Använd bara Implanner-bänkar** håller implantatbeställningar på dessa bänkar; en koloni utan en sådan tillverkar inga implantat. Med alternativet av används andra bänkar när varje Implanner-bänk har två beställningar.
- **Krävd hantverksfärdighet för produktionsbeställningar** håller dyra material borta från oerfarna hantverkare.
- **Tillåt produktionsbeställningar för saknade mellanprodukter** följer brister genom kedjan: saknas en avancerad komponent till nästa bioniska ben beställs komponenten först. Medan ett implantat väntar på en råvara som plaststål tillverkar lediga bänkar de avancerade komponenter som behövs. Tillverkade föremål som komponenter och grundimplantat kan användas. Råvaror som stål tillverkas aldrig, även om ett smältrecept skulle kunna ge dem; dem måste du samla själv.

Varje beställning respekterar också lagergränserna (se [Behålla lager](topic:keeping-stock)).
