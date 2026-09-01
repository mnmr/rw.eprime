---
title: Production options
---
With **Automatically add crafting bills** switched on, Implanner places
bills for missing implants at benches that can make them, and removes its
own bills when the need disappears. Bills you created yourself are never
touched.

![The production options](production-options.png)

- **Benches that may hold bills** caps how many benches per colony work on
  implants at once, one Implanner bill per bench.
- **Only allow bill creation at idle benches** keeps implant bills off
  benches that already have work waiting, so your own queues keep priority.
- **Required crafting skill** keeps expensive materials away from
  low-skill crafters.
- **Allow production bills for missing intermediaries** follows shortfalls
  down the chain: no components for the bionic leg means a components bill
  first. Only manufactured items such as components qualify. Raw resources
  like steel are never crafted, even when a smelting recipe could produce
  them; you still have to gather them yourself.

Every bill also respects the keep-in-stock floors (see
[keeping stock](topic:keeping-stock)).
