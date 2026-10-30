---
title: Keeping stock
---
<!-- l10n:b-2d4a360fb110 -->
Two settings stop automation from draining your stockpiles.

<!-- l10n:b-f53aef22895d -->
**Keep in stock** sets resource floors for production. A crafting bill is
only added when the colony can pay for it, on top of the materials its
queued bills still need, and keep at least the configured amount of every
ingredient. Steel at 2000 means implant crafting never drags your steel
below 2000. If you spend materials and a bill nobody has started no longer
fits, Implanner removes it and adds it again once the stock is back.

<!-- l10n:b-96bbe1392879 -->
![The keep-in-stock resource floors](keep-in-stock.png)

<!-- l10n:b-5864119cb6f1 -->
**Implant reservations** hold up to the configured number of existing implants
back for manual use. Reserve one bionic arm and automation only assigns arms
beyond the first, leaving that arm free for emergency surgery or trade.
Automatic production makes additional implants while assigned plans need them.
To replenish emergency stock when no plan needs an implant, add a crafting bill
yourself.
Upgrade bills also leave held-back implants and implants needed for planned
surgeries untouched. Only spare implants can be used as production ingredients.

<!-- l10n:b-04d4c3acbddc -->
![An implant reservation holding one bionic arm back](implant-reservations.png)
