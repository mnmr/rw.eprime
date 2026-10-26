---
title: Roolien tilat
---
Roolin voi poistaa käytöstä yleisesti koko siirtokunnalta, yksittäiseltä siirtolaiselta tai molemmilta.

Valikoiman roolin napsauttaminen vaihtaa sen yleistä tilaa: ![käytössä oleva valikoimatunniste](palette-chip-on.png) tai ![käytöstä poistettu valikoimatunniste](palette-chip-off.png).

Kun rooli on yleisesti pois käytöstä, sen tunnisteessa näkyy kaksinkertainen yliviivaus ![kaksinkertaisesti yliviivattu tunniste](chip-state-global-off.png) kaikilla roolin saaneilla siirtolaisilla.

Siirtolaisen rivin tunnisteen napsauttaminen kiertää tilat käytössä ![käytössä oleva tunniste](chip-state-on.png), pois käytöstä tältä siirtolaiselta ![kerran yliviivattu tunniste](chip-state-off.png) ja pakotettu käyttöön ![lipulla merkitty tunniste](chip-state-forced.png). Sekä yleisesti että siirtolaiselta käytöstä poistaminen yhdistää viivat ![päällekkäiset yliviivaukset](chip-state-both-off.png). Pakotettu käyttö toimii, vaikka rooli olisi yleisesti pois käytöstä.

Tilan vaihtaminen ei koskaan poista määritystä: tunniste säilyttää paikkansa rivillä.

Tästä on hyötyä varalla pidettäville rooleille. Anna Kasvienleikkaaja puolelle siirtokuntaa, poista se yleisesti käytöstä ja ota se takaisin käyttöön yhdellä valikoiman napsautuksella kasvitautien iskiessä.

Etkö ole varma tunnisteen tilasta? Työkaluvihje nimeää sen kehystetyssä kulmassa:

![Roolitunnisteen työkaluvihjeen tilakulma kehystettynä](chip-tooltip-state.png)
