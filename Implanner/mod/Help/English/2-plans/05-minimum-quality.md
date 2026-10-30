---
title: Minimum quality
---
<!-- l10n:b-d8fd3884b5cc -->
With Quality Bionics Remastered or Vanilla Genetics Expanded loaded, implant
items come in qualities from awful to legendary, and the quality changes how
well the implant works. A plan then shows a **Minimum quality** button beside
**Delete plan**.

<!-- l10n:b-118873404542 -->
![The plan header with the Minimum quality button](plan-min-quality.png)

<!-- l10n:b-84db128338c2 -->
The minimum applies to new installations for this plan, including inherited
choices. Automatic crafting targets this quality and may need repeated
attempts to reach it. Implants without a quality are not affected. A new plan
that extends another starts with the same minimum.

<!-- l10n:b-93af25d8591d -->
Implanner never replaces a healthy body part with a worse one. An awful
bionic arm works worse than a natural arm, so it waits for a better item,
unless the colonist's arm is missing or badly damaged. When several items
qualify, the best one goes to the colonist whose turn comes first.

<!-- l10n:b-0e7cf20ad9dc -->
An already-installed implant of the planned kind still counts as complete,
even below the minimum. Enable **Give the best implants to high-priority
colonists** to allow better-quality replacements, as described below.

<!-- l10n:b-b163fd8a6054 -->
## Better implants for high-priority colonists

<!-- l10n:b-0c6c2d002995 -->
![The option on the Automation tab](upgrade-by-priority.png)

<!-- l10n:b-8937cc3e8047 -->
With **Give the best implants to high-priority colonists** switched on (in
the surgery options on the Automation tab), quality follows colonist
priority:

<!-- l10n:b-0d3ec0233e11 -->
- New surgeries use the lowest quality the plan accepts, so the better
  items stay free.
- A better item goes to the highest-priority colonist whose implant of the
  same kind is worse. Their old implant is removed first, then the better
  one is installed, and the removed implant goes to the next colonist in
  line.
- An item already reserved for a lower-priority colonist is handed over
  too, as long as their surgery is not scheduled yet.

<!-- l10n:b-e246867de4e8 -->
Replacing an installed implant with a better-quality one requires two
operations: removal and installation. A replacement only starts while no
other Implanner surgery is waiting for that colonist and the concurrent
surgeries limit has room. An implant is never exchanged when removing it
would also take something else off the body part.

<!-- l10n:b-37015122025b -->
When Implanner crafts an implant for the plan:

<!-- l10n:b-51f50276fd20 -->
- With Quality Jobs loaded, the crafting bill targets the minimum, so the
  finishing step goes to a crafter who can reach it.
- A hybrid implant from Vanilla Genetics Expanded takes its quality from the
  genoframe. The bill uses only genoframes of the minimum quality or better.
- Without either, any crafter may finish the implant, and one that comes out
  below the minimum is crafted again. The keep-in-stock floors (see
  [keeping stock](topic:keeping-stock)) stop this before your materials run
  low.
