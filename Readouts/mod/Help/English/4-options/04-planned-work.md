---
title: Planned work
---
<!-- l10n:b-7aa1a59c7379 -->
Planned work options subtract the estimated material costs of bills or blueprints from the displayed counts.

<!-- l10n:b-ee40b81e925d -->
- Reserve resources for bill items subtracts the ingredients needed by outstanding production bills. An ingredient is counted only when the bill allows exactly one resource type for it. For example, allowing any leather reserves nothing; allowing only one leather type includes that leather's cost. A "do X times" bill reserves X runs, a "do until you have X" bill reserves the runs needed to reach the target, and a "do forever" bill reserves one run. Suspended bills reserve nothing.
- Reserve resources for buildables subtracts the materials your blueprints and part-built frames still need. Material already delivered to a frame, or picked up by a colonist to deliver, counts as spent and is not subtracted a second time. Forbidden blueprints are ignored.
- Show overruns as negative numbers displays how far short you are when planned work needs more than you have, instead of stopping at zero.
- Quality Jobs support adjusts enabled bill and buildable reservations for quality targets managed by Quality Jobs. It estimates materials for repeat attempts and accounts for construction refunds. Enable the corresponding reservation option above to use it. The row is greyed out when EPrime's Quality Jobs is not installed or too old, and the reason shows on hover.

<!-- l10n:planned-work-tooltip-columns -->
Hover a resource or pool with planned-work deductions to see the Planned Work table. Remaining work counts recipe runs or construction targets. Cost each is the material cost of one attempt. Deducted is the amount subtracted from the displayed count, allowing for materials already delivered or being hauled to construction and estimated quality retries when Quality Jobs support is enabled.

<!-- l10n:b-a4ac1a2f64dc -->
With Show overruns as negative numbers on, a counter that planned work pushes below zero shows the shortfall in orange:

<!-- l10n:b-b209bc9139c1 -->
![Materials with negative numbers on](negatives-on.png)

<!-- l10n:b-5325cc201c17 -->
With the option off, the same slots stop at zero:

<!-- l10n:b-a997f13279d8 -->
![The same slots with negative numbers off](negatives-off.png)

<!-- l10n:b-f017c3669f5c -->
Planned work refreshes with resource counts every few seconds of game time. New or changed bills appear on the next refresh; unpause to let game time advance.
