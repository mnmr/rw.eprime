---
title: Planlagt arbejde
---
Indstillingerne trækker materialer fra, som ordrer eller byggeplaner allerede har lagt beslag på.

- Reserver ressourcer til ordrer fratrækker de ingredienser, udestående ordrer stadig skal forbruge. En ingrediens reserveres kun, når behovet kan dækkes på præcis én måde. »Alle lædertyper« og lignende åbne valg reserverer intet. »Udfør X gange« reserverer X fremstillinger, »Udfør indtil du har X« reserverer nok til målet, og »Udfør for evigt« reserverer én fremstilling. Suspenderede ordrer reserverer intet.
- Reserver ressourcer til byggeri fratrækker restbehovet for byggeplaner og delvist byggede rammer. Materialer, som allerede er leveret eller samlet op til levering, regnes som brugt og trækkes ikke fra igen. Forbudte byggeplaner ignoreres.
- Vis underskud som negative tal viser manglen, når planlagt arbejde kræver mere, end du har, i stedet for at stoppe ved nul.
- Understøttelse af Quality Jobs anslår antallet af forsøg for en målkvalitet og reserverer til alle. Rækken er grå, hvis EPrime's Quality Jobs mangler eller er for gammelt; hold markøren over for årsagen.

Med negative tal slået til vises underskud fra planlagt arbejde i orange:

![Materialer med negative tal slået til](negatives-on.png)

Slået fra stopper de samme felter ved nul:

![Samme felter uden negative tal](negatives-off.png)

Planlagt arbejde genoptælles cirka hvert syttende sekund af spilletiden. En ny ordre kan derfor tage så længe om at påvirke tallene.
