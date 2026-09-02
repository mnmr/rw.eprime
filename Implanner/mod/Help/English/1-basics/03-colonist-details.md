---
title: Colonist details
---
Select a colonist in the table and the details panel shows every implant in
their plan with its current status:

![The details panel listing each implant with its status](colonist-details.png)

- **Missing**: still needed, nothing reserved yet.
- **Reserved**: an item in storage is claimed for this colonist. Surgery
  starts once everything the colonist needs next is reserved and on site
  (see [how automation works](topic:how-automation-works)).
- **Ready**: the reserved item has arrived at the colonist's colony.
- **Surgery scheduled**: the operation is on the colonist's bill list.
- **Recovering**: surgery waits while the colonist is bleeding, has
  untreated wounds, or is down. Anesthetic sleep does not count.
- **Blocked by doctor floor**: no doctor at the colony meets the required
  Medicine skill (see [surgery options](topic:surgery-options)).
- **Complete**: installed.

A plan describes an end state: implants the colonist already has count
as complete.
