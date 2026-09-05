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

RimWorld keeps a separate list for emergency jobs: firefighting, urgent
tending and going to bed for emergency treatment. Jobs on that list
interrupt sleep, meals and any other job. By default every emergency job
covered by an enabled role goes on that list, regardless of where the
role sits in the row.

The Options tab offers vanilla's rule instead: "Emergency jobs interrupt
only at top priority". With it on, an emergency job interrupts everything
only when its work type's number in the priority grid (the 0-4 mapping)
is as good as or better than the colonist's best number for ordinary
work. Otherwise the job runs in its normal position. A colonist whose
grid shows Firefighter and Hauling at 1 and Doctor at 3 still interrupts
any job for a fire, but treats urgent tending as ordinary doctor work at
its position in the row.
