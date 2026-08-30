---
title: Assignment ordering
---
A colonist works their row from left to right, counting enabled roles
only:

![Ordered role chips on a colonist](colonist-row.png)

1. Earlier roles outrank later ones, job for job.
2. Inside a role, earlier jobs come first.
3. A job listed in several roles counts at its earliest position.

Colonists will only perform jobs from the assigned roles. Not assigning a
role is equivalent to leaving the vanilla priority grid slot without a
number.

Changing priority is a drag:

@demo:chip-drag

Drops work across rows too: drag a chip onto another colonist to move the
assignment. Click the trash can icon inside a chip to remove the
assignment; the chip leaves the row.

Note that RimWorld has a separate priority list for emergency jobs
(firefighting and urgent tending). These will run first whenever an
enabled role covers them, regardless of where the role assignment sits.
