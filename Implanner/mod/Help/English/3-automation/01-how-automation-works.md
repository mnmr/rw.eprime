---
title: How automation works
---
Automation checks the colony a couple of times per in-game hour, and
every edit you make triggers an immediate extra pass:

1. Count what enlisted colonists still miss.
2. Queue crafting bills for implants the colony can build (see
   [production options](topic:production-options)).
3. Reserve finished items from storage for specific colonists, following
   priorities and tiers.
4. When everything a colonist needs next is reserved and on site, append
   the operations to their bill list in one block, after any bills
   already queued.

The colony summary at the top of the Overview tab shows all of this:
whether automation is on, what production is crafting or waiting for,
how many implants are in stock and still queued, and who is next for
surgery. Hover the production or surgery figures for a breakdown per
implant, including what each item is waiting for.

![The colony summary with production and surgery status](colony-next.png)

Batching delays surgery until several implants can be installed in one
anesthetic sleep. A batch is everything the
colonist needs next, either their current tier or their whole plan,
depending on the iteration strategy (see
[surgery options](topic:surgery-options)). One strategy skips batching
entirely and schedules each implant as soon as it is reserved and on
site.

Operations wait until the colonist is fit for surgery, and colonies never
take items from each other's stock: items are reserved and installed
where the colonist lives.
