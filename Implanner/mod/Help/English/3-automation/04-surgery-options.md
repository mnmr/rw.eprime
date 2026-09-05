---
title: Surgery options
---
![The surgery options](surgery-options.png)

**Iteration strategy** decides the order in which implants are delivered.
Colonist priority on the Overview tab always decides who goes first.

- **Tier batching**: every colonist gets the implants in their best tier
  first, then work moves down the tiers. A colonist goes into surgery
  once their whole tier is reserved. Each colonist needs more surgeries,
  but every colonist benefits sooner.
- **Full sets**: finish one colonist's whole plan before starting the
  next. Surgery waits until the whole plan is reserved. Each colonist
  needs fewer surgeries, but the last colonist in priority order waits
  the longest.
- **ASAP**: every implant in stock is scheduled right away for the
  colonist with the highest priority who is missing it. At equal
  priority, legs go to the slowest colonist, and arms go to colonists
  carrying a melee weapon first, then to whoever has the highest Crafting
  and Intellectual skills combined. Nothing waits for a batch: a single
  bionic leg is scheduled as soon as it is reserved and on site, even if
  that means one surgery per implant.

**Concurrent surgeries** caps how many colonists per colony can have
operations scheduled at the same time. It starts at one per ten
colonists. With **Count hospitalized pawns** on, pawns lying in medical
beds or downed and awaiting treatment take up slots too, so new implant
surgeries wait until the hospital has room.

**Assign surgery bills to best available doctor** (on by default)
restricts Implanner surgery bills to the most skilled doctor at each
colony. The restriction follows the best doctor as colonists arrive and
leave. Switch it off to set a fixed minimum Medicine skill instead.
