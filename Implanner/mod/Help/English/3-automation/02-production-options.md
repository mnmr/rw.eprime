---
title: Production options
---
With **Automatically add crafting bills for missing implants** switched
on, Implanner places bills for missing implants at benches that can make
them, and removes its own bills when the need disappears. Bills you
created yourself are never removed. Each bill crafts one implant, in the
order the surgeries will install them: with Full sets, a colonist's legs
come before their arms if legs rank higher in the plan.

To give implant crafting a bench of its own, check **Implanner bench** on
the bench's Bills tab. The checkbox appears only on benches that can craft
implants. The bills already on the bench are suspended, and Implanner
places its crafting bills on it before any other bench. While it is
checked, you cannot add, paste, suspend or resume bills on it.

![A fabrication bench handed over to Implanner](implanner-bench.png)

Uncheck it to take the bench back. Bills that were already suspended when
you checked it stay suspended, and the rest resume. If you remove
Implanner while a bench is checked, the bench keeps working, but you have
to resume its bills yourself.

![The production options](production-options.png)

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
  it will need. Only manufactured items such as components qualify. Raw resources
  like steel are never crafted, even when a smelting recipe could produce
  them; you still have to gather them yourself.

Every bill also respects the keep-in-stock floors (see
[keeping stock](topic:keeping-stock)).
