---
title: Hoe implantaten uit mods worden gelezen
---
Implanner heeft geen lijst met ondersteunde mods nodig. Het leest alle geladen operaties uit het basisspel, uitbreidingen en mods. Elke operatie die een implantaat op een lichaamsdeel plaatst, wordt een vermelding in de kiezer. Dezelfde gegevens bepalen welke implantaten kunnen samengaan:

- **Eén vervanging per lichaamsdeel**: een bionische arm vervangt de schouder. Twee vervangingen delen dus nooit een lichaamsdeel. Een vervanging verwijdert alle implantaten op dat deel of de onderliggende delen.
- **Implantaten op één lichaamsdeel**: die kunnen samengaan tenzij de operatie ze als onverenigbaar markeert. Zo sluiten huidklieren elkaar uit in het basisspel en markeren mods moduleplaatsen.
- **Verwijdering bij plaatsing**: sommige implantaten verwijderen bij plaatsing een concurrent, waar die ook zit. Twee implantaten die elkaar verwijderen kunnen nooit samengaan.
- **Operaties die een lichaamsdeel leegmaken**: sommige verwijderen alles op een deel voordat ze een implantaat toevoegen dat het deel niet vervangt. Implantaten die daarna worden geplaatst blijven zitten. Implanner plaatst daarom zo'n implantaat eerst en zet terug wat het verdringt.
- **Modules op bionica**: een operatie die bedoeld is voor een kunstmatig deel deelt dat deel met de bionica in plaats van die te vervangen.
- **Verbetering ter plaatse**: een operatie die één implantaat verwijdert en een ander toevoegt verbetert het geplaatste onderdeel. De verbeterde versie staat apart in de lijst.
- **Niet-maakbare voorwerpen**: een implantaat dat geen werkbank kan maken blijft verborgen totdat de bijbehorende optie aanstaat.

Twee dingen zijn niet uitleesbaar. Modules die een kolonist zonder operatie vanuit de inventaris plaatst worden nooit getoond. Regels die alleen in modcode bestaan en niet in gegevens, blijven ook onzichtbaar. Alles wat een mod via zijn operaties uitdrukt werkt zonder wijzigingen aan Implanner.
