---
title: Laskenta-asetukset
---
Laskenta-asetukset määrittävät, mitkä pinot lasketaan mukaan paneelin näyttämiin määriin. Molemmat ovat käytössä, kun asennat modin.

Laske vain varastoidut esineet laskee pinot varastoalueilta ja varastointirakennuksista, kuten hyllyistä. Muualla kartalla lojuvat esineet jätetään pois:

![Raaka-aineet, kun vain varastoidut esineet lasketaan](storage-only-on.png)

Kun asetus ei ole käytössä, esineitä lasketaan kaikkialta kartalta:

![Samat paikat, kun laskentaa ei rajata varastoihin](storage-only-off.png)

Ohita kielletyt esineet jättää kielletyt pinot laskennan ulkopuolelle.

Joitakin esineitä ei lasketa koskaan: pilaantuneita esineitä, tutkimattomissa kartan osissa olevia esineitä sekä esineitä, joita siirtolainen kantaa tai pitää inventaariossa tai säiliössä.

Koska kannettavia esineitä ei lasketa, määrä yleensä pienenee heti, kun siirtolainen nostaa pinon. Rakennussuunnitelmaan tarkoitetut materiaalit ovat poikkeus. Kun Varaa resurssit rakennuskohteille on käytössä (katso [Suunnitellut työt](topic:planned-work)), laskuri vähensi suunnitelman tarvitsemat materiaalit jo sitä sijoittaessasi. Materiaalien nostaminen ja toimittaminen ei siksi vähennä määrää toistamiseen.

Paneeli päivittää määrät muutaman peliaikasekunnin välein. Laskenta-asetuksen muuttaminen päivittää ne heti, myös pelin ollessa tauolla. Resurssi tai kooste voi ohittaa molemmat asetukset kaikissa sitä näyttävissä paikoissa (katso [Paikan asetukset](topic:slot-options)).
