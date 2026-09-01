---
title: Progress and states
---
The State column condenses each colonist into one word:

- **Waiting**: implants are missing, but no item is reserved for this
  colonist yet.
- **Preparing**: items are reserved and being collected for the
  colonist's next surgery batch.
- **Operating**: surgery is scheduled on the colonist's bill list.
- **Done**: every planned implant is installed.
- **Away**: the colonist is off traveling; automation waits.

Progress always reflects what is installed right now. A planned implant
the colonist's body cannot take at all (the required part does not exist
on it) is left out of their target and counts neither as installed nor as
missing. A harvested or
destroyed implant counts as missing again, and automation pursues a
replacement. If you want to harvest an implant to sell or pass along,
switch **Enable automation** off first, or the part will be reserved and
scheduled straight back in.

Manually installed implants count too. A superior part installed where the
plan wanted a lesser one satisfies that slot: an archotech leg covers a
bionic-leg goal, and Implanner never schedules a downgrade. Implants that
can share a part, like brain implants, stay separate goals.
