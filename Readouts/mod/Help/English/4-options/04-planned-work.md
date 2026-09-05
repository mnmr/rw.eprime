---
title: Planned work
---
Planned work options subtract material that bills or blueprints have already claimed.

- Reserve resources for bill items subtracts the ingredients your outstanding bills still have to consume. A bill reserves an ingredient only when there is exactly one way to satisfy that requirement. "Any leather" and similar open choices reserve nothing. A "do X times" bill reserves X runs, a "do until you have X" bill reserves the runs needed to reach the target, and a "do forever" bill reserves one run. Suspended bills reserve nothing.
- Reserve resources for buildables subtracts the materials your blueprints and part-built frames still need. Material already delivered to a frame, or picked up by a colonist to deliver, counts as spent and is not subtracted a second time. Forbidden blueprints are ignored.
- Show overruns as negative numbers displays how far short you are when planned work needs more than you have, instead of stopping at zero.
- Quality Jobs support estimates how many attempts a quality target will take and reserves for all of them. The row is greyed out when EPrime's Quality Jobs is not installed or too old, and the reason shows on hover.

With Show overruns as negative numbers on, a counter that planned work pushes below zero shows the shortfall in orange:

![Materials with negative numbers on](negatives-on.png)

With the option off, the same slots stop at zero:

![The same slots with negative numbers off](negatives-off.png)

Planned work is rescanned about every seventeen seconds of game time, so a new bill can take that long to show in the counts.
