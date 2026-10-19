---
title: How recommendations work
---
The recommendation engine reads what your colonists are good at and what
the colony needs, then proposes a role row for each of them.

Its evidence is called signals: burning passions, gene aptitudes, traits,
skill expertise, and the best skills in the colony. On top of that it
enforces colony needs, so essential roles like Doctor and Cook always
land on somebody, ideally the somebody with the talent.

Precepts that oppose work lower its recommendation score. A suitable willing
colonist is more likely to get it, while an opposed colonist can still cover
essential work. A verified refusal or missing capability makes that work
ineligible. Skill ratings still describe aptitude; Best fits lists that raw
aptitude among capable colonists, while the colony recommendation also accounts
for aversions and demand.

These are recommendation inputs. Conditional roles continue to control when
and where an assigned role is active.

The engine considers all eligible roles and orders them automatically for
each colonist. Role Options on the Recommendations tab lists every role
except those you have excluded. To prevent a role from ever being
recommended, add it to **Excluded roles** at the top of that tab.
Exclusions take precedence over colony needs and special handling, while
existing pinned assignments stay protected. See
[Ordering and exclusions](topic:recommendation-order) for details.

You can see its opinion everywhere. Palette tooltips list the best fits
for every role:

![Best fits in a palette tooltip](palette-tooltip.png)

Two roles get special handling. Hunter is recommended to anyone carrying
a ranged weapon (hunting trains Shooting), placed earlier for poor shots
and later for sharpshooters whose time is worth more elsewhere.
An aversion to hunting also places Hunter later in the row.
Mechanitor is recommended only to colonists with a mechlink, however
good the other crafters are, and its Best fits list follows the same
rule.

The engine suggests; applying is yours, one role at a time
([cherry-picking](topic:cherry-picking)) or wholesale
([Fix My Colony](topic:fix-my-colony)). The exception is
[Auto-optimize](topic:auto-optimize): switch it on and Fix My Colony
runs automatically every in-game hour, across all your colonies.
