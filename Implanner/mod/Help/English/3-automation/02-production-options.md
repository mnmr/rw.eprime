---
title: Production options
---
<!-- l10n:b-bdd2b459773e -->
With **Automatically add crafting bills for missing implants** switched
on, Implanner places bills for missing implants at benches that can make
them, and removes its own bills when the need disappears. Bills you
created yourself are never removed. Each bill runs one craft, in the
order the surgeries will install them: with Full sets, a colonist's legs
come before their arms if legs rank higher in the plan.

<!-- l10n:b-8effc9e9b828 -->
When several recipes make the same implant, Implanner prefers an available
route it can pay for now. A free base implant can be upgraded; otherwise,
a direct recipe can make the advanced implant from materials. If only the
upgrade route is enabled, intermediary production can make the base first.
Recipes must be researched and supported by an eligible bench.

<!-- l10n:b-0aa3ded1d655 -->
To give implant crafting a bench of its own, check **Implanner bench** on
the bench's Bills tab. The checkbox appears only on benches that can craft
implants. The bills already on the bench are suspended, and Implanner
places its crafting bills on it before any other bench. While it is
checked, you cannot add, paste, suspend or resume bills on it.

<!-- l10n:b-a4d04c1bceb2 -->
![A fabrication bench handed over to Implanner](implanner-bench.png)

<!-- l10n:b-a89a880dcac5 -->
Uncheck it to take the bench back. Bills that were already suspended when
you checked it stay suspended, and the rest resume. If you remove
Implanner while a bench is checked, the bench keeps working, but you have
to resume its bills yourself.

<!-- l10n:b-c9f0dad1a5c5 -->
![The production options](production-options.png)

<!-- l10n:b-f6f358878993 -->
- **Benches that may hold bills** caps how many benches per colony work on
  implants at once. A bench holds up to two Implanner bills, so the next
  bill is already waiting when the first is done. Implanner benches count toward
  it.
- **Only allow bill creation at idle benches** keeps implant bills off
  benches that already have work waiting, so your own queues keep priority.
- **Only use Implanner benches** keeps implant bills on Implanner benches,
  so a colony without one crafts no implants. Switched off, other benches
  are used once every Implanner bench holds two bills.
- **Required crafting skill for production bills** keeps expensive
  materials away from low-skill crafters.
- **Allow production bills for missing intermediaries** follows shortfalls
  down the chain: one advanced component short for the next bionic leg
  means an advanced component bill first. While an implant waits for a raw
  resource such as plasteel, spare benches build the advanced components
  it will need. Manufactured items such as components and base implants qualify. Raw resources
  like steel are never crafted, even when a smelting recipe could produce
  them; you still have to gather them yourself.

<!-- l10n:b-24089426e9a9 -->
Every bill also respects the keep-in-stock floors (see
[keeping stock](topic:keeping-stock)).
