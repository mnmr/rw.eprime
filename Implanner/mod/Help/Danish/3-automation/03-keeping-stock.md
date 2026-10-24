---
title: Bevar lagerbeholdningen
---
To indstillinger forhindrer automatikken i at tømme dine lagre.

**Behold på lager** angiver minimumsbeholdninger for produktionsressourcer. En fremstillingsordre tilføjes kun, når kolonien har råd til den ud over de materialer, som ordrerne i kø stadig har brug for, og bagefter har mindst den angivne mængde af hver ingrediens tilbage. Stål sat til 2000 betyder, at fremstilling af implantater aldrig bringer dit stållager under 2000. Hvis du bruger materialer, så der ikke længere er råd til en ordre, som ingen er begyndt på, fjerner Implanner den og tilføjer den igen, når der er nok på lager.

![Minimumsbeholdningerne under »Behold på lager«](keep-in-stock.png)

**Implantatreserver** holder færdige implantater tilbage til manuel brug. Hvis du holder én bionisk arm tilbage, tildeler automatikken kun arme ud over den første, så én er fri til akutte operationer eller handel. Produktionen regner stadig et tilbageholdt implantat som manglende, så lageret bliver fyldt op igen.
Ordrer til opgradering lader også tilbageholdte implantater og implantater, der skal bruges til planlagte operationer, være urørte. Kun overskydende implantater må bruges som ingredienser i produktionen.

![En implantatreserve holder én bionisk arm tilbage](implant-reservations.png)
