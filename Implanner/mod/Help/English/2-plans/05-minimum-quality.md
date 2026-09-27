---
title: Minimum quality
---
With Quality Bionics Remastered or Vanilla Genetics Expanded loaded, implant
items come in qualities from awful to legendary, and the quality changes how
well the implant works. A plan then shows a **Minimum quality** button beside
**Delete plan**.

![The plan header with the Minimum quality button](plan-min-quality.png)

Every implant of the plan, inherited ones included, is
installed and crafted at that quality or better. Implants without a quality
are not affected. A new plan that extends another starts with the same
minimum.

Implanner never replaces a healthy body part with a worse one. An awful
bionic arm works worse than a natural arm, so it waits for a better item,
unless the colonist's arm is missing or badly damaged. When several items
qualify, the best one goes to the colonist whose turn comes first.

A colonist who already has the planned implant keeps it, even below the
minimum, unless the option below is switched on.

## Better implants for high-priority colonists

![The option on the Automation tab](upgrade-by-priority.png)

With **Give the best implants to high-priority colonists** switched on (in
the surgery options on the Automation tab), quality follows colonist
priority:

- New surgeries use the lowest quality the plan accepts, so the better
  items stay free.
- A better item goes to the highest-priority colonist whose implant of the
  same kind is worse. Their old implant is removed first, then the better
  one is installed, and the removed implant goes to the next colonist in
  line.
- An item already reserved for a lower-priority colonist is handed over
  too, as long as their surgery is not scheduled yet.

Every exchange is an extra surgery. A colonist only gets one while no
other Implanner surgery is waiting for them and the concurrent surgeries
limit has room. An implant is never exchanged when removing it would also
take something else off the body part.

When Implanner crafts an implant for the plan:

- With Quality Jobs loaded, the crafting bill targets the minimum, so the
  finishing step goes to a crafter who can reach it.
- A hybrid implant from Vanilla Genetics Expanded takes its quality from the
  genoframe. The bill uses only genoframes of the minimum quality or better.
- Without either, any crafter may finish the implant, and one that comes out
  below the minimum is crafted again. The keep-in-stock floors (see
  [keeping stock](topic:keeping-stock)) stop this before your materials run
  low.
