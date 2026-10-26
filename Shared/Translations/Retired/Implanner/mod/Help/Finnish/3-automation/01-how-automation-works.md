---
title: Automaation toiminta
---
Automaatio tarkistaa siirtokunnan pari kertaa pelitunnissa. Jokainen tekemäsi muutos käynnistää lisäksi välittömän tarkistuksen:

1. Lasketaan, mitä mukaan otetuilta siirtolaisilta vielä puuttuu.
2. Lisätään valmistustilaukset implanteille, joita siirtokunta voi valmistaa (katso [valmistusasetukset](topic:production-options)).
3. Varataan valmiit varastossa olevat implantit tietyille siirtolaisille prioriteettien ja tasojen mukaan.
4. Kun kaikki siirtolaisen seuraavaksi tarvitsemat implantit on varattu ja tuotu paikalle, leikkaukset lisätään yhtenä ryhmänä hänen toimenpideluettelonsa loppuun, jo jonossa olevien tilausten jälkeen.

Yleiskatsaus-välilehden yläosan siirtokuntayhteenveto näyttää kaiken tämän: onko automaatio käytössä, mitä tuotannossa valmistetaan tai odotetaan, kuinka monta implanttia on varastossa ja jonossa sekä kuka pääsee seuraavaksi leikkaukseen. Vie osoitin tuotannon tai leikkausten lukujen päälle, niin näet implanttikohtaisen erittelyn ja sen, mitä kukin odottaa.

![Siirtokuntayhteenveto sekä tuotannon ja leikkausten tila](colony-next.png)

Eräkäsittely lykkää leikkausta, kunnes useita implantteja voidaan asentaa saman nukutuksen aikana. Erään kuuluu kaikki, mitä siirtolainen tarvitsee seuraavaksi: joko nykyisen tason implantit tai koko suunnitelma toteutusjärjestyksen mukaan (katso [leikkausasetukset](topic:surgery-options)). Yksi vaihtoehto ohittaa eräkäsittelyn kokonaan ja ajoittaa jokaisen implantin asennuksen heti, kun implantti on varattu ja paikalla.

Leikkaukset odottavat, kunnes siirtolainen on leikkauskunnossa. Siirtokunnat eivät koskaan ota tavaroita toistensa varastoista: implantit varataan ja asennetaan siirtolaisen kotisiirtokunnassa.
