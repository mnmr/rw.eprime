---
title: Progress and states
---
<!-- l10n:b-92da28b4ac7d -->
The State column condenses each colonist into one word:

<!-- l10n:b-03986aa19f4e -->
- **Waiting**: implants are missing, but no item is reserved for this
  colonist yet.
- **Preparing**: items are reserved and being collected for the
  colonist's next surgery.
- **Operating**: surgery is scheduled on the colonist's bill list.
- **Done**: every planned implant is installed.
- **Away**: the colonist is off traveling; automation waits.

<!-- l10n:b-1d48ea8e126e -->
Progress always reflects what is installed right now. A planned implant
the colonist's body cannot take at all (the required part does not exist
on it) is left out of their target and counts neither as installed nor as
missing. A harvested or
destroyed implant counts as missing again, and automation pursues a
replacement. If you want to harvest an implant to sell or pass along,
switch **Enable automation** off first, or the part will be reserved and
scheduled straight back in.

<!-- l10n:b-c804648dcb55 -->
Manually installed implants count too. A superior part installed where the
plan wanted a lesser one satisfies that slot: an archotech leg covers a
bionic-leg goal, and Implanner never schedules a downgrade. Implants that
can share a part, like brain implants, stay separate goals.
