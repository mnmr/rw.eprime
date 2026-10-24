---
title: Så läses modimplantat
---
Implanner behöver ingen lista över stödda moddar för att läsa implantat. Den läser alla operationer spelet har laddat från grundspel, expansioner och moddar. Varje operation som installerar ett implantat på en kroppsdel blir en post i väljaren. Samma data berättar vilka implantat som kan dela kropp:

- **En ersättningsdel per kroppsdel**: en bionisk arm ersätter axeln. Två ersättningsdelar delar därför aldrig samma del, och en ersättning tar bort alla implantat på delen och underliggande delar.
- **Implantat på samma del**: de kan samexistera om inte operationen markerar dem som oförenliga. Så utesluter hudkörtlar varandra i grundspelet, och så markerar moddar modulplatser.
- **Borttagning vid installation**: vissa implantat tar bort en konkurrent var som helst i kroppen när de sätts in. Två implantat som tar bort varandra kan aldrig samexistera.
- **Operationer som rensar en kroppsdel**: vissa operationer tar bort allt från en del innan de installerar ett implantat som inte ersätter den. Implantat som installeras efteråt blir kvar, så Implanner installerar ett sådant implantat först och sätter tillbaka det som trängs undan.
- **Moduler monterade på bionik**: en operation vars kod installerar på en konstgjord del delar den med bioniken i stället för att ersätta den.
- **Uppgradering på plats**: en operation som tar bort ett implantat och lägger till ett annat uppgraderar den installerade delen. Uppgraderingen listas som ett eget implantat.
- **Föremål som bara kan köpas**: ett implantat som ingen arbetsbänk kan tillverka är dolt tills motsvarande alternativ på fliken Alternativ aktiveras.

Två saker går inte att läsa. Moduler som kolonisten sätter in från inventariet utan operation listas aldrig. Regler som bara finns i en mods kod och inte i dess data kan inte ses. Allt en mod uttrycker genom sina operationer fungerar utan ändringar i Implanner.
