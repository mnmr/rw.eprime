---
title: Assignment ordering
---
<!-- l10n:b-32c8a631526a -->
A colonist works their row from left to right, counting enabled roles
only:

<!-- l10n:b-75c264ac5015 -->
![Ordered role chips on a colonist](colonist-row.png)

<!-- l10n:b-617ab83d7eb0 -->
1. Earlier roles outrank later ones, job for job.
2. Inside a role, earlier jobs come first.
3. A job listed in several roles counts at its earliest position.

<!-- l10n:b-be52fb18f81e -->
Colonists will only perform jobs from the assigned roles. Not assigning a
role is equivalent to leaving the vanilla priority grid slot without a
number.

<!-- l10n:b-db1bdd5d561c -->
Changing priority is a drag:

<!-- l10n:b-90462aacb6d4 -->
@demo:chip-drag

<!-- l10n:b-be64d575edc7 -->
Drops work across rows too: drag a chip onto another colonist to move the
assignment. Click the trash can icon inside a chip to remove the
assignment; the chip leaves the row.

<!-- l10n:b-6bbb9dc33edb -->
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

<!-- l10n:b-a52fa63b5067 -->
Turn off "Only the first 5 roles interrupt for emergencies" on the Options
tab to remove this limit. Then every emergency job covered by an enabled
role goes on the list, regardless of where the role sits in the row.
