---
title: Under motorhjelmen
---
Hver kolonists ordnede roller omsættes til én entydig opgaverækkefølge,
som gives til spillet gennem de samme lister, grundspillets opgavevalg
allerede læser. Bondens AI opfører sig præcis som med et håndindstillet
prioritetsgitter, uden tick-patches eller arbejde for hvert skærmbillede.

Alt genberegnes, når tildelinger eller roller ændres, og ligger derefter
i en cache. Betingede roller opdateres desuden ved timeskift, og når en
kolonist skifter sted. Akutte opgaver som brandslukning går gennem
spillets behandling af akut arbejde, når en af kolonistens første fem
aktiverede roller dækker dem. Resten udføres på deres normale plads.
Når "Kun de første 5 roller afbryder det igangværende arbejde ved
nødsituationer" er slået fra, bruges akutbehandlingen til alle dækkede
akutte opgaver.

WorkRoles holder grundspillets gemte prioriteter opdateret ud fra dine
roller. De omsættes til den velkendte 0–4-skala, når valget for
grundspillets skala er slået til, som det er som standard. Derfor kan
moddet fjernes sikkert: grundspillets Arbejde-fane vender tilbage med
dine roller omsat til prioriteter. Når moddet føjes til et eksisterende
spil, sker den modsatte omregning.
