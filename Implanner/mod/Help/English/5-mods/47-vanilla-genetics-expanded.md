---
title: Vanilla Genetics Expanded
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **Verdict** | ![Fully compatible](verdict-blue.png|16) Fully compatible (custom support) |
| **Evidence** | Source analysis, automated in-game test |
| **Checked** | 2026-11-01 |
| **Versions** | Implanner 1.2.7, Vanilla Genetics Expanded [Workshop 2801160906](https://steamcommunity.com/sharedfiles/filedetails/?id=2801160906), files 2026-10-14 |

<!-- l10n:b-261a620cdad5 -->
Its hybrid implants for colonists are listed with the other implants. Its
animal implants are not, since Implanner plans colonists only.

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
