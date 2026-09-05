---
title: How modded implants are read
---
Implanner does not need a list of supported mods to read implants. It
reads every surgery the game has loaded, from vanilla, the expansions and
every mod, and any surgery that installs an implant on a body part
becomes an entry in the picker. The same data tells Implanner which
implants can share a body:

- **One replacement per part**: a bionic arm swaps the shoulder out, so
  two replacements never share a part, and a replacement clears every
  implant mounted on it or on the parts below it.
- **Implants on one part**: they coexist unless the surgery marks them as
  incompatible. This is how skin glands exclude each other in the base
  game, and how mods mark module slots.
- **Removal on install**: some implants remove a rival anywhere on the
  body the moment they go in. Two implants that remove each other can
  never coexist.
- **Modules that mount on bionics**: a surgery whose worker is written to
  install onto an artificial part shares that part with the bionic
  instead of replacing it.
- **Upgrades in place**: a surgery that removes one implant and adds
  another upgrades the installed part. The upgraded version is listed as
  its own implant.
- **Purchase-only items**: an implant whose item no workbench can craft
  is hidden until the matching option on the Options tab is switched on.

Two things cannot be read. Modules that a colonist inserts from the
inventory without any surgery are never listed, and rules that exist only
in a mod's code, with nothing in its data, cannot be seen. Everything a
mod expresses through its surgeries works without any change to
Implanner.
