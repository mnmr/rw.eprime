---
title: Mod options
---
![The Options tab with the Mod compatibility and Catalog sections](options-tab.png)

The Options tab holds the settings that shape plans. Automation settings
live on the Automation tab. All of them are shared by everyone in a
multiplayer game.

![Hovering an option lists the implants it affects and their mods](options-bladder-tip.png)

**Allow multiple bladder implants** and **Allow multiple hygiene
enhancers** are on by default, which matches the game: a colonist can
carry the version from Dubs Bad Hygiene and the version from [FSF]
Advanced Bionics Expansion at the same time, and their effects stack. Switch one
off to treat that pair as one slot:

- Picking one version in a plan overrides the other, with the usual
  caption in the picker.
- A colonist who already has either version counts as done for that
  slot, so automation never installs the second one.
- A colonist who already carries both keeps both. Nothing is ever
  removed.
- Picks made while the option was on stay stored and return when it is
  switched back on.

Each tooltip lists the affected implants that are loaded and the mod each
one comes from.

![Hovering the option shows the affected implants](options-purchase-tip.png)

**Show purchase-only implants** is off by default. Switched on, it lists
implants whose item no workbench can craft, such as archotech parts,
which only arrive through trade, quests or salvage. The tooltip names every affected implant
that is loaded.

![The Limbs picker listing archotech legs and arms](picker-purchase-only.png)

With the option on, the picker shows them like any other implant. A
planned purchase-only implant never holds anything else back: the rest of
the batch goes ahead without it, and once its item is in stock it is
installed with the colonist's next batch, or right away under ASAP.
Switching the option off hides the rows again and removes these implants
from the Implant reservations menu, but picks already made stay in the
plan and keep working. A mod that adds a crafting recipe for the item
makes the implant an ordinary one automatically, regardless of this
option.
