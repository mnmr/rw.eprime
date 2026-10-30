---
title: Choosing finishers
---
<!-- l10n:requirements -->
Use these controls on a bill or a building. Other eligible workers may do the earlier work; only the finishing touch must meet your requirements.

<!-- l10n:controls -->
- **Finisher skill** sets a minimum skill level.
- **{k:QJ_AutoBest}** selects workers by expected quality instead of your manual minimum skill. The manual slider is kept for when you turn this off.
- **{k:QJ_RequireInspired}** waits for that inspiration. Completing the work consumes it as usual.
- **{k:QJ_RequireSpecialist}** requires an Ideology role that grants a production quality bonus. This option appears with Ideology.

<!-- l10n:odds-image -->
![Finisher requirements and the quality-odds preview](finisher-options.png)

<!-- l10n:best -->
Automatic selection compares eligible colonists, colony mechs and supported robots and chooses the worker with the highest expected quality. Skill, Inspired Creativity, production-specialist bonuses and supported mod quality bonuses all count. This maximizes average quality; it does not guarantee the result of an individual roll.

<!-- l10n:availability -->
Otherwise eligible workers remain in the comparison while away, downed, asleep, mentally broken or recharging, including robots docked in their stations. If no top-ranked worker can finish on this map, the work waits. For example, a better crafter away in a caravan can prevent a lower-ranked worker at home from finishing. There is no timeout that lowers the quality requirement.

<!-- l10n:manual-threshold -->
With **{k:QJ_AutoBest}** off, any capable worker meeting your manual skill threshold and other requirements can finish. A better worker elsewhere in the colony does not block them.

<!-- l10n:robot-finishers -->
Mechs and supported robots must meet the same finisher requirements as colonists. Requiring Inspired Creativity or a production-specialist role still excludes workers without it. Turning off **{k:QJ_SettingsAllowRobotPreparation}** reserves the earlier work for colonists; eligible mechs and robots can still take Quality Jobs finishing assignments.

<!-- l10n:odds -->
In a bill's **{k:QJ_OddsHeader}** table, **{k:QJ_OddsColConfig}** estimates the odds for your minimum finisher skill and required bonuses. With **{k:QJ_AutoBest}** enabled and an eligible candidate found, it instead shows that colony-wide worker's odds. If no candidate exists, it falls back to your manual settings; this does not mean a finisher is available.

<!-- l10n:odds-best -->
**{k:QJ_OddsColBest}** shows the odds for the highest-ranked capable worker on this map, ignoring your extra finisher requirements. The two columns can therefore describe different workers. These are chances, not promises; the worker who actually finishes determines the result.

<!-- l10n:eligible-finishers -->
**Eligible finishers** lists workers who can qualify on this map now; away, downed or mentally broken workers are excluded from that list. For bills, the list uses the selected bill's requirements, even though the queue counts include other bills making the same product on this map.

<!-- l10n:priority -->
**{k:QJ_SettingsHighPriorityFinish}** makes assigned finishing work come before other work except firefighting, medical care, rescue and bed rest. It does not interrupt the current job. With it off, finishing comes first within its work type, after any higher-ranked work types. The worker must be able to perform the item's normal work type.
