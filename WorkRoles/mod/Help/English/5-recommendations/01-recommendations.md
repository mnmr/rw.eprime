---
title: How recommendations work
---
<!-- l10n:b-07818cd37217 -->
The recommendation engine reads what your colonists are good at and what
the colony needs, then proposes a role row for each of them.

<!-- l10n:b-d0c69b8a87f5 -->
Its evidence is called signals: burning passions, gene aptitudes, traits,
skill expertise, and the best skills in the colony. On top of that it
enforces colony needs, so essential roles like Doctor and Cook always
land on somebody, ideally the somebody with the talent.

<!-- l10n:b-45031c5441da -->
Precepts that oppose work lower its recommendation score. A suitable willing
colonist is more likely to get it, while an opposed colonist can still cover
essential work. A verified refusal or missing capability makes that work
ineligible. Skill ratings still describe aptitude; Best fits lists that raw
aptitude among capable colonists, while the colony recommendation also accounts
for aversions and demand.

<!-- l10n:b-e112aed1a354 -->
These are recommendation inputs. Conditional roles continue to control when
and where an assigned role is active.

<!-- l10n:b-2a25543f495f -->
With supported Vivi versions, gathering recommendations use the worker's actual
gathering speed and yield. A human with a Vivi straw can gather honey and pollen;
a multiuse brush enables plant gathering. Equipping or removing these tools
updates capability while paused. A worker who can perform some gathering recipes
remains eligible for gathering; unavailable recipes stay excluded from training
choices. Unknown Vivi builds or custom gathering rules retain existing behavior.

<!-- l10n:b-fce545e1aa79 -->
The engine considers all eligible roles and orders them automatically for
each colonist. Role Options on the Recommendations tab lists every role
except those you have excluded. To prevent a role from ever being
recommended, add it to **Excluded roles** at the top of that tab.
Exclusions take precedence over colony needs and special handling, while
existing pinned assignments stay protected. See
[Ordering and exclusions](topic:recommendation-order) for details.

<!-- l10n:b-4cefba49c974 -->
You can see its opinion everywhere. Palette tooltips list the best fits
for every role:

<!-- l10n:b-0dd35388dc64 -->
![Best fits in a palette tooltip](palette-tooltip.png)

<!-- l10n:b-f1a1a1196f55 -->
Two roles get special handling. Hunter is recommended to anyone carrying
a ranged weapon (hunting trains Shooting), placed earlier for poor shots
and later for sharpshooters whose time is worth more elsewhere.
An aversion to hunting also places Hunter later in the row.
Mechanitor is recommended only to colonists with a mechlink, however
good the other crafters are, and its Best fits list follows the same
rule.

<!-- l10n:b-b241ec72dcda -->
The engine suggests; applying is yours, one role at a time
([cherry-picking](topic:cherry-picking)) or wholesale
([Fix My Colony](topic:fix-my-colony)). The exception is
[Auto-optimize](topic:auto-optimize): switch it on and Fix My Colony
runs automatically every in-game hour, across all your colonies.

<!-- l10n:mechanical-suggestions -->
When robot and mech rows are enabled, their suggestions use current skills and
executable work, without human passions or learning paths. Their accepted
primary duty stays in place, including an explicitly assigned excluded role;
change that duty manually. Suggestions for additional work respect exclusions.
You choose their assignments; Auto-optimize continues to apply human
recommendations only.

<!-- l10n:mechanical-coverage -->
**Use robot and mech coverage in human recommendations** is a separate per-save
option, also off by default. It counts accepted mechanical assignments toward
staffing, allowing a fully covered duty to replace its last human specialist.
Each worker supplies one primary duty; fallback roles add no independent
staffing credit. Restricted areas, blocked work, insufficient skills and
Quality Jobs restrictions on robot work prevent full coverage. A robot that can
only clean does not replace a combined cleaning-and-hauling role.

<!-- l10n:mechanical-learning -->
Selected human training below its target and pinned assignments stay protected.
Routine charging retains coverage; deactivation, lost control or leaving Work
mode removes it. Updated recommendations propose human replacements, while
Auto-optimize applies them at its normal hourly boundary when enabled. With
integration off, robot and mech rows do not affect human recommendations.
