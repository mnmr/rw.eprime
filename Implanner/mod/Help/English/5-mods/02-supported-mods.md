---
title: Supported mods
---
Every implant mod that uses the game's surgery data works without any
setup. Players have confirmed Expanded Prosthetics and Organ Engineering
and CeleTech, whose implants are read exactly like vanilla content. The
mods below were checked one by one against their data.

![A picked mechanite strain in the Torso list](mechanites-picked.png)

![The other strain rows showing an overrides caption naming the picked strain](mechanites-overrides.png)

**[FSF] Advanced Bionics Expansion**: mechanite strains exclude each
other. Injecting one strain removes any other strain in the body, so the
picker treats every strain as one slot and the overrides caption names
the strain being replaced. Immunity-enhancing mechanites are not a strain
and coexist with every strain. Its advanced bionic bladder and advanced
hygiene enhancer are covered by the mod compatibility options together
with Dubs Bad Hygiene.

**Dubs Bad Hygiene**: the bionic bladder and the hygiene enhancer are
torso implants that the game lets a colonist combine with the advanced
versions from [FSF] Advanced Bionics Expansion. The two mod compatibility
options decide whether Implanner allows that as well.

![The plain and advanced bionic eye rows showing an overrides caption naming the picked modular bionic eye](modular-eye.png)

**Integrated Implants**: extra arms allow one per side, and picking a
second kind of extra arm on the same side overrides the first. Subdermal
armour and its prestige version, the shoulder turrets, the tails and the
implanted explosives each form a group where only one can be installed.
The modular bionics are upgrades in place: pick a modular bionic eye and
automation installs a bionic eye first if the colonist has none, then the
upgrade surgery, which consumes one component and no second bionic eye.
These upgrades need EBSG Framework and are not offered while Medical
System Expansion 2 is loaded. The modules themselves are inserted from
the inventory without any surgery, so they are not listed.

![The requirement prompt for a leg module](requirement-prompt.png)

![The plan after Add to plan: the bionic leg and the module are both picked](requirement-added.png)

**Bionic modularity**: every module occupies a slot on its body part, so
one combat module and one work module fit on the same arm while two
combat modules do not. Modules for limbs install onto a bionic limb and
share the part with it, so a planned bionic arm and a planned arm module
coexist. Ticking a limb module while the plan has no bionic on that part
opens a prompt that lists the bionics able to host it, with the least
advanced host selected by default. **Add to plan** adds your choice and
the module together. **Cancel** adds nothing.
