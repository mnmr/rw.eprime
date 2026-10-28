---
title: What is never touched
---
<!-- l10n:b-fa6609cf86a3 -->
Make It So, Fix My Colony, and Auto-optimize normally preserve four kinds
of assignment:

<!-- l10n:b-4596c00b561b -->
- [Pinned](topic:pinning) assignments, your explicit "hands off".
- [Blocker roles](topic:blocker-roles), because forbidding something was
a decision.
- [Conditional roles](topic:conditional-roles), which their rules manage
already.
- Every role of a downed colonist.

<!-- l10n:b-fceae01af917 -->
**Pinned assignments remain protected even when excluded.** If an unpinned
role or one of its composite members is excluded, applying recommendations
removes that assignment. See [Recommendation order](topic:recommendation-order).

<!-- l10n:b-d0e464673ca2 -->
A downed colonist shows a red line through their row:

<!-- l10n:b-45b61ed9f1e9 -->
![A downed colonist's row](downed-row.png)

<!-- l10n:b-5710ea821624 -->
Their non-excluded roles and pinned assignments stay as they are until they recover, and nothing
they hold counts toward colony needs, so the engine covers those needs
with other colonists. Every chip in their Recommended Roles list says so:

<!-- l10n:b-6de430f0562e -->
![The downed explanation in a chip tooltip](downed-tooltip.png)
