---
title: Mod compatibility
---
Work and jobs from other mods are picked up automatically, and several
mods get deeper integration: Vanilla Skills Expanded and Alpha Skills
expertise feed the recommendation signals, More Than Capable's hated work
becomes an Awful signal, Colony Groups appears as a grouping option, and
multi-floor mods treat stacked floors as one location.

RJW's Brothel Colony add-on supplies Brothel work, which appears in roles
automatically. Its recommendations use Social. If Sexperience is also
installed, Sex becomes the primary recommendation skill and Social stays
secondary; the job profile shows both as used and trained skills.
Service designation and visitor eligibility still come from those mods.
All three mods are optional, and RJW by itself adds no work type to manage.

Two families do clash. Other Work tab replacements (Work Tab forks,
Better Work Tab and friends) fight over the same UI. Priority-setting
mods (Free Will, PriorityMaster and similar) fight over the same data:
WorkRoles owns priorities for managed colonists, so their writes are
blocked. When that happens a one-time notice names the mod and suggests
the role-based way to get the same effect.

Priority ownership applies to humanlike pawns only, so mods that set
priorities for other pawn kinds, like Mech Work Tab for mechanoids, are
fully compatible.

Some mods require priorities in the range 0 to 4, so if you have issues
with other mods make sure the "Report vanilla 0-4 priorities to other
mods" option is enabled.

Mods that only read priorities are fine; they see the values WorkRoles
computed.
