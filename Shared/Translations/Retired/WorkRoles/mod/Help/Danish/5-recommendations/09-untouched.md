---
title: Hvad der aldrig ændres
---
Gør det, Ret min koloni og Automatisk optimering bevarer normalt fire
slags tildelinger:

- [Fastgjorte](topic:pinning) tildelinger, dit udtrykkelige "lad stå".
- [Blokeringsroller](topic:blocker-roles), fordi forbuddet var et valg.
- [Betingede roller](topic:conditional-roles), som allerede styres af
deres regler.
- Alle roller hos en kolonist, der ikke kan rejse sig.

**Fastgjorte tildelinger beskyttes, selv når de er udelukket.** Hvis en
ikke-fastgjort rolle eller en af dens medlemsroller udelukkes,
fjernes tildelingen, når anbefalingerne anvendes. Se
[Anbefalingernes rækkefølge](topic:recommendation-order).

En kolonist, der ikke kan rejse sig, har en rød streg gennem sin række:

![Rækken for en kolonist, der ikke kan rejse sig](downed-row.png)

Kolonistens ikke-udelukkede roller og fastgjorte tildelinger bliver som
de er, indtil vedkommende kommer sig. Ingen af rollerne tæller med i
koloniens behov, så systemet dækker behovene med andre kolonister.
Hvert mærke i listen Anbefalede roller forklarer dette:

![Værktøjstip om en kolonist, der ikke kan rejse sig](downed-tooltip.png)
