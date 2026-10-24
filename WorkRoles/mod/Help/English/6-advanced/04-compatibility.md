---
title: Mod compatibility
---
Work and jobs from other mods are picked up automatically, and several
mods get deeper integration: Vanilla Skills Expanded and Alpha Skills
expertise feed the recommendation signals, More Than Capable's hated work
becomes an Awful signal, Colony Groups appears as a grouping option, and
multi-floor mods treat stacked floors as one location.

Optional work and skill mods can supply additional job profiles. These
profiles identify the skills a job uses and trains, and may refine its
primary recommendation skill when the relevant mods are installed.
WorkRoles leaves job eligibility, designations and execution to the mods
that provide them. These integrations add no required mod dependencies.

Recommendations also read declared work tags, required capacities, precept
opposition, and verified ordinary-mining refusals. For audited Humanoid Alien
Races builds on RimWorld 1.6 and 1.7, they use the framework's actual work-giver
and recipe restrictions. Different races using those rules benefit from the
same integration. A blocked recipe does not remove other usable recipes from
the role or count as a way to train its skill.

Unknown framework builds preserve existing eligibility and report that their
rules are unsupported. Restrictions hidden inside arbitrary jobs cannot be
detected safely by trying jobs during recommendation capture. Supported Vivi
versions provide equipment-dependent gathering speed, yield and eligibility.
Unknown Vivi builds, custom gathering rules and unverified Ratkin-specific
rules retain existing behavior; no race or xenotype name is used to guess a
restriction.

Add-ons that patch existing work givers still use the same roles and job
ordering. Animal-handling roles, for example, continue to use Animals for
recommendations. Conditional experience gains in another skill depend on
the add-on's eligibility rules; they do not create a general training path
for every colonist assigned to that work.

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
