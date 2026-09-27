---
title: Keeping stock
---
Two settings stop automation from draining your stockpiles.

**Keep in stock** sets resource floors for production. A crafting bill is
only added when the colony can pay for it, on top of the materials its
queued bills still need, and keep at least the configured amount of every
ingredient. Steel at 2000 means implant crafting never drags your steel
below 2000. If you spend materials and a bill nobody has started no longer
fits, Implanner removes it and adds it again once the stock is back.

![The keep-in-stock resource floors](keep-in-stock.png)

**Implant reservations** hold finished implants back for manual use.
Reserve one bionic arm and automation only assigns arms beyond the first,
keeping one free for emergency surgery or trade. Production still counts a
held-back item as missing, so the stock is replenished.

![An implant reservation holding one bionic arm back](implant-reservations.png)
