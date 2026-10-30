---
title: Colonist details
---
<!-- l10n:b-46b4919bcee3 -->
Select a colonist in the table and the details panel shows every implant in
their plan with its current status:

<!-- l10n:b-1cb82bc13e58 -->
![The details panel listing each implant with its status](colonist-details.png)

<!-- l10n:b-4d5159744d80 -->
- **Missing**: still needed, nothing reserved yet.
- **Reserved**: an item in storage is claimed for this colonist. Surgery
  starts once everything the colonist needs next is reserved and on site
  (see [how automation works](topic:how-automation-works)).
- **Ready**: the reserved item has arrived at the colonist's colony.
- **Surgery scheduled**: the operation is on the colonist's bill list.
- **Recovering**: surgery waits while the colonist is bleeding, has
  untreated wounds, or is down. Anesthetic sleep does not count.
- **Waiting for a doctor with Medical ...+**: no eligible doctor meets this
  operation's skill requirement. Check doctor availability or adjust the
  minimum on the Automation tab (see [surgery options](topic:surgery-options)).
- **Complete**: installed.

<!-- l10n:b-7d5d4531ac6c -->
A plan describes an end state: implants the colonist already has count
as complete.
