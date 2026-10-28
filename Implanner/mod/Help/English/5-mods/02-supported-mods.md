---
title: Supported mods
---
<!-- l10n:b-3d8630b508cc -->
Every implant mod that uses the game's surgery data works without any
setup. Players have confirmed Expanded Prosthetics and Organ Engineering
and CeleTech, whose implants are read exactly like vanilla content. The
mods below were checked one by one against their data.

<!-- l10n:b-fc34121549e7 -->
![A picked mechanite strain in the Torso list](mechanites-picked.png)

<!-- l10n:b-8e0b09ad2325 -->
![The other strain rows showing an overrides caption naming the picked strain](mechanites-overrides.png)

<!-- l10n:b-8115e58333ac -->
**[FSF] Advanced Bionics Expansion**: mechanite strains exclude each
other. Injecting one strain removes any other strain in the body, so the
picker treats every strain as one slot and the overrides caption names
the strain being replaced. Immunity-enhancing mechanites are not a strain
and coexist with every strain. Its advanced bionic bladder and advanced
hygiene enhancer are covered by the mod compatibility options together
with Dubs Bad Hygiene.

<!-- l10n:b-951da050479a -->
**Dubs Bad Hygiene**: the bionic bladder and the hygiene enhancer are
torso implants that the game lets a colonist combine with the advanced
versions from [FSF] Advanced Bionics Expansion. The two mod compatibility
options decide whether Implanner allows that as well.

<!-- l10n:b-41bdd507c8fd -->
![The plain and advanced bionic eye rows showing an overrides caption naming the picked modular bionic eye](modular-eye.png)

<!-- l10n:b-d5d8d7431467 -->
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

<!-- l10n:b-261a620cdad5 -->
**Vanilla Genetics Expanded**: its hybrid implants for colonists are listed
with the other implants. Its animal implants are not, since Implanner plans
colonists only.

<!-- l10n:b-8b2ffcc18549 -->
Some hybrid implants replace a body part: the hibernation module replaces
the brain. Others sit on the body part: neuron reinforcement sits on the
brain. Regular implants can go on a body part that has a hybrid implant
sitting on it, but not on one that has been replaced.

<!-- l10n:b-cd881c381961 -->
Installing any hybrid implant pushes out everything already installed on
that body part. The pushed-out implants drop next to the doctor.

<!-- l10n:b-ad1a61d0da90 -->
- Implanner installs the hybrid implant first, so nothing needs to come out.
- If the colonist already has implants there, Implanner lets them drop out,
  holds them for that colonist, and installs them again right after.
- Implanner never installs a hybrid implant while something on that body
  part would be lost for good.

<!-- l10n:b-c282558e897d -->
Hybrid implants take their quality from the genoframe they are made with
(see [minimum quality](topic:minimum-quality)).

<!-- l10n:b-721ad339d2c2 -->
**Quality Bionics Remastered**: bionics come in qualities, and a better
bionic works better and has more hit points. The implant tooltip shows the
part efficiency from awful to legendary, and each plan sets the lowest
quality it installs (see [minimum quality](topic:minimum-quality)).

<!-- l10n:b-7a2e3799838c -->
**Elite Bionics Framework**: implants that raise or lower a body part's hit
points show the change in their tooltip.

<!-- l10n:b-c7cd0a958354 -->
![The requirement prompt for a leg module](requirement-prompt.png)

<!-- l10n:b-71ab3bae9608 -->
![The plan after Add to plan: the bionic leg and the module are both picked](requirement-added.png)

<!-- l10n:b-1a485abf9e8c -->
**Bionic modularity**: every module occupies a slot on its body part, so
one combat module and one work module fit on the same arm while two
combat modules do not. Modules for limbs install onto a bionic limb and
share the part with it, so a planned bionic arm and a planned arm module
coexist. Ticking a limb module while the plan has no bionic on that part
opens a prompt that lists the bionics able to host it, with the least
advanced host selected by default. **Add to plan** adds your choice and
the module together. **Cancel** adds nothing.
