---
title: How recommendations work
---
The recommendation engine reads what your colonists are good at and what
the colony needs, then proposes a role row for each of them.

Its evidence is called signals: burning passions, gene aptitudes, traits,
skill expertise, and the best skills in the colony. On top of that it
enforces colony needs, so essential roles like Doctor and Cook always
land on somebody, ideally the somebody with the talent.

You can see its opinion everywhere. Palette tooltips list the best fits
for every role:

![Best fits in a palette tooltip](palette-tooltip.png)

Hunter gets special handling: recommended to anyone carrying a ranged
weapon (hunting trains Shooting), placed earlier for poor shots and later
for sharpshooters whose time is worth more elsewhere.

The engine suggests; applying is yours, one role at a time
([cherry-picking](topic:cherry-picking)) or wholesale
([Fix My Colony](topic:fix-my-colony)). The exception is
[Auto-optimize](topic:auto-optimize): switch it on and Fix My Colony
runs automatically every in-game hour, across all your colonies.
