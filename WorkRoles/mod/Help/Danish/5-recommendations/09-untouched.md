---
title: Hvad der aldrig ændres
---
Gør det, Ret min koloni og Automatisk optimering bevarer normalt fire
slags tildelinger:

- [Fastgjorte](topic:pinning) tildelinger, dit udtrykkelige "lad stå".
- [Blokeringsroller](topic:blocker-roles), fordi forbuddet var et valg.
- [Betingede roller](topic:conditional-roles), som allerede styres af
deres regler.
- Alle roller hos en nedlagt kolonist.

**Fastgjorte tildelinger beskyttes, selv når de er udelukket.** Hvis en
ikke-fastgjort rolle eller et af dens sammensatte medlemmer udelukkes,
fjernes tildelingen, når anbefalingerne anvendes. Se
[Anbefalingernes rækkefølge](topic:recommendation-order).

En nedlagt kolonist har en rød streg gennem sin række:

![En nedlagt kolonists række](downed-row.png)

Kolonistens ikke-udelukkede roller og fastgjorte tildelinger bliver som
de er, indtil vedkommende kommer sig. Ingen af rollerne tæller med i
koloniens behov, så systemet dækker behovene med andre kolonister.
Hvert mærke i listen Anbefalede roller forklarer dette:

![Forklaring om nedlagt kolonist i et værktøjstip](downed-tooltip.png)
