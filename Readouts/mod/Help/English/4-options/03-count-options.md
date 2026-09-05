---
title: Count options
---
Count options decide which stacks are counted in every counter the panel shows. Both are on when you install the mod.

Only count items in storage counts stacks in stockpile zones and in storage buildings such as shelves. Loose items lying around the map are left out:

![Raw resources with storage only on](storage-only-on.png)

With the option off, everything on the map counts:

![The same slots with storage only off](storage-only-off.png)

Ignore forbidden items leaves forbidden stacks out of the count.

Some items are never counted: rotten items, items in parts of the map you have not explored yet, and items a colonist is carrying or keeps in an inventory or a container.

Because carried items are not counted, the counter normally drops the moment a colonist picks up a stack. Material for a blueprint is the exception. With Reserve resources for buildables turned on (see [Planned work](topic:planned-work)), the counter already subtracted what the blueprint needs when you placed it, so picking that material up and delivering it does not lower the counter a second time.

The panel refreshes its counts every few seconds of game time. Changing a count option refreshes them at once, even while the game is paused. A resource or pool can override both options for every slot that shows it (see [Slot options](topic:slot-options)).
