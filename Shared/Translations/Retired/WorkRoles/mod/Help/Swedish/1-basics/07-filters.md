---
title: Filter och sökning
---
Filterraden begränsar tabellen utan att ändra några tilldelningar:

![Filterraden](filter-row.png)

- **Sökning** matchar kolonistnamn. Alla blankstegsavgränsade termer måste
matcha. Prefix breddar sökningen: r: eller role: söker i tilldelade
rollnamn, j: eller job: i jobben som rollerna ger. "br r:läk" hittar
kolonister med *br* i namnet och en läkarroll.
- **Tilldelad roll** visar kolonister med vald roll eller en annan roll
som täcker alla dess jobb och inte är
[blockerande](topic:blocker-roles).
- **Täckt jobb** visar kolonister vars icke-blockerande roller
innehåller det valda jobbet.
- **Plats** väljer vilken koloni tabellen visar: aktuell karta, överallt,
en bosättning eller skeppet. Kolonister i karavan räknas till platsen
de lämnade.

Filtren kombineras. Rensningsknappen som visas när något är aktivt
återställer sökning samt roll- och jobbfilter men behåller plats,
gruppering och visningsval.

Ett filter döljer aldrig en vald kolonist. Om det annars skulle ske
rensas markeringen och den första kolonisten som fortfarande visas
väljs i stället.
