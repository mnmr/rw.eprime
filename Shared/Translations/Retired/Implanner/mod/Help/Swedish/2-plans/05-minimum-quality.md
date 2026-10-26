---
title: Lägsta kvalitet
---
Med Quality Bionics Remastered eller Vanilla Genetics Expanded inläst har implantatföremål kvalitet från avskyvärd till legendarisk, vilket påverkar hur väl implantatet fungerar. Planen visar då knappen **Lägsta kvalitet** bredvid **Ta bort plan**.

![Planrubriken med knappen Lägsta kvalitet](plan-min-quality.png)

Alla implantat i planen, även ärvda, installeras och tillverkas med den kvaliteten eller bättre. Implantat utan kvalitet påverkas inte. En ny plan som bygger på en annan börjar med samma minimum.

Implanner ersätter aldrig en frisk kroppsdel med en sämre. En avskyvärd bionisk arm fungerar sämre än en naturlig, så den väntar på ett bättre föremål om kolonistens arm inte saknas eller är svårt skadad. Om flera föremål duger får kolonisten som står näst i tur det bästa.

En kolonist som redan har det planerade implantatet behåller det, även under minimikvaliteten, om inte alternativet nedan är aktiverat.

## Bättre implantat till högt prioriterade kolonister

![Alternativet på fliken Automatik](upgrade-by-priority.png)

Med **Ge de bästa implantaten till högt prioriterade kolonister** aktiverat i operationsalternativen på fliken Automatik följer kvaliteten kolonisternas prioritet:

- Nya operationer använder den lägsta kvalitet planen godtar, så de bättre föremålen hålls lediga.
- Ett bättre föremål går till den högst prioriterade kolonisten vars implantat av samma slag är sämre. Det gamla tas först bort, sedan installeras det bättre, och det uttagna implantatet går till nästa kolonist i kön.
- Även ett föremål reserverat för en lägre prioriterad kolonist lämnas över, så länge dennes operation inte har planerats ännu.

Varje byte är en extra operation. En kolonist får bara ett byte när ingen annan Implanner-operation väntar och gränsen för samtidiga operationer har plats. Ett implantat byts aldrig om borttagningen också skulle ta bort något annat från kroppsdelen.

När Implanner tillverkar ett implantat till planen:

- Med Quality Jobs inläst har tillverkningsbeställningen minimikvaliteten som mål, så avslutningen går till en hantverkare som kan nå den.
- Ett hybridimplantat från Vanilla Genetics Expanded får sin kvalitet från genramen. Beställningen använder bara genramar med minimikvaliteten eller bättre.
- Utan någon av dessa får alla hantverkare slutföra implantatet, och ett som hamnar under minimikvaliteten tillverkas igen. Lagergränserna (se [Behålla lager](topic:keeping-stock)) stoppar det innan materialet blir knappt.
