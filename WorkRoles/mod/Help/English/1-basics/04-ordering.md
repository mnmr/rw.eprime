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
interrupt sleep, meals and any other job. By default an emergency job
goes on that list only when one of the colonist's first five enabled
roles covers it. Emergency jobs from later roles run in their normal
position. A colonist with Core first and Doctor as their sixth role still
interrupts any job for a fire, but treats urgent tending as ordinary
doctor work at its position in the row. Moving Core down the row past the
fifth position turns firefighting into ordinary work for that colonist
too.

The Options tab switch "Only the first 5 roles interrupt for emergencies"
turns this off. Then every emergency job covered by an enabled role goes
on the list, regardless of where the role sits in the row.
