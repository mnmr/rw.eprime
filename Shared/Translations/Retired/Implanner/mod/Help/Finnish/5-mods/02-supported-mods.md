---
title: Tuetut modit
---
Jokainen pelin leikkaustietoja käyttävä implanttimodi toimii ilman erillistä määritystä. Pelaajat ovat vahvistaneet Expanded Prosthetics and Organ Engineeringin ja CeleTechin toimivan; niiden implantit luetaan aivan kuten peruspelin sisältö. Alla olevien modien tiedot on tarkistettu yksitellen.

![Valittu mekaniittikanta Vartalo-luettelossa](mechanites-picked.png)

![Muiden kantojen ohittaa-tekstit nimeävät valitun kannan](mechanites-overrides.png)

**[FSF] Advanced Bionics Expansion**: mekaniittikannat sulkevat toisensa pois. Yhden kannan ruiskuttaminen poistaa kehosta kaikki muut kannat, joten valitsin käsittelee niitä yhtenä paikkana ja ohittaa-teksti nimeää korvattavan kannan. Vastustuskykyä parantavat mekaniitit eivät ole kanta, ja ne sopivat yhteen kaikkien kantojen kanssa. Kehittynyt bioninen virtsarakko ja kehittynyt hygieniaparannus kuuluvat modiyhteensopivuusasetusten piiriin yhdessä Dubs Bad Hygienen kanssa.

**Dubs Bad Hygiene**: bioninen virtsarakko ja hygieniaparannus ovat vartaloimplantteja, jotka peli sallii yhdistää [FSF] Advanced Bionics Expansionin kehittyneisiin versioihin. Kaksi modiyhteensopivuusasetusta ratkaisee, salliiko Implanner saman.

![Tavallisen ja kehittyneen bionisen silmän ohittaa-tekstit nimeävät valitun modulaarisen bionisen silmän](modular-eye.png)

**Integrated Implants**: lisäkäsivarsia sallitaan yksi kummallekin puolelle. Toisen lisäkäsivarsityypin valinta samalle puolelle ohittaa ensimmäisen. Ihonalainen panssari ja sen arvoversio, olkapäätornit, hännät sekä istutetut räjähteet muodostavat kukin ryhmän, josta voi asentaa vain yhden. Modulaariset bioniset osat päivitetään paikallaan: kun valitset modulaarisen bionisen silmän, automaatio asentaa ensin bionisen silmän, jos siirtolaisella ei ole sellaista, ja tekee sitten päivitysleikkauksen. Se kuluttaa yhden komponentin, ei toista bionista silmää. Päivitykset vaativat EBSG Frameworkin, eikä niitä tarjota Medical System Expansion 2:n ollessa ladattuna. Itse moduulit lisätään tavaraluettelosta ilman leikkausta, joten niitä ei luetella.

**Vanilla Genetics Expanded**: siirtolaisten hybridi-implantit luetellaan muiden implanttien kanssa. Eläinten implantteja ei näytetä, sillä Implanner suunnittelee vain siirtolaisille.

Jotkin hybridi-implantit korvaavat ruumiinosan: horrosmoduuli korvaa aivot. Toiset asennetaan osan päälle: hermosoluvahvistus asennetaan aivoihin. Tavallisia implantteja voi asentaa ruumiinosaan, jonka päällä on hybridi-implantti, mutta ei sen korvaamaan osaan.

Jokaisen hybridi-implantin asentaminen syrjäyttää kaikki ruumiinosassa jo olevat implantit. Syrjäytetyt implantit putoavat lääkärin viereen.

- Implanner asentaa hybridi-implantin ensin, jotta mitään ei tarvitse poistaa.
- Jos siirtolaisella on jo implantteja samassa kohdassa, Implanner antaa niiden pudota, varaa ne tälle siirtolaiselle ja asentaa ne heti takaisin.
- Implanner ei koskaan asenna hybridi-implanttia, jos jokin saman ruumiinosan implantti menetettäisiin lopullisesti.

Hybridi-implantit saavat laatunsa niiden valmistukseen käytetystä genorungosta (katso [vähimmäislaatu](topic:minimum-quality)).

**Quality Bionics Remastered**: bionisilla osilla on laatu. Parempi osa toimii tehokkaammin ja kestää enemmän vaurioita. Implantin työkaluvihje näyttää osan tehokkuuden hirveästä legendaariseen laatuun, ja jokainen suunnitelma määrittää alimman asennettavan laadun (katso [vähimmäislaatu](topic:minimum-quality)).

**Elite Bionics Framework**: implantit, jotka suurentavat tai pienentävät ruumiinosan kestävyyttä, näyttävät muutoksen työkaluvihjeessään.

![Raajamoduulin vaatimuksen valintaikkuna](requirement-prompt.png)

![Lisää suunnitelmaan -valinnan jälkeen sekä bioninen jalka että moduuli ovat valittuina](requirement-added.png)

**Bionic modularity**: jokainen moduuli vie yhden paikan ruumiinosassaan. Sama käsivarsi voi siksi sisältää yhden taistelumoduulin ja yhden työmoduulin, mutta ei kahta taistelumoduulia. Raajamoduulit asennetaan bioniseen raajaan ja jakavat sen kanssa saman osan, joten suunniteltu bioninen käsivarsi ja käsivarsimoduuli sopivat yhteen. Jos valitset raajamoduulin eikä suunnitelmassa ole bionista osaa siihen kohtaan, avautuu valintaikkuna, joka luettelee sopivat bioniset osat. Vähiten kehittynyt vaihtoehto on oletuksena valittu. **Lisää suunnitelmaan** lisää sekä valitsemasi osan että moduulin. **Peruuta** ei lisää mitään.
