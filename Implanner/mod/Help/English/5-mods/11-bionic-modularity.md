---
title: Bionic modularity
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **Verdict** | ![Fully compatible](verdict-blue.png|16) Fully compatible (custom support) |
| **Evidence** | Source analysis, automated in-game test |
| **Checked** | 2026-09-05 |
| **Versions** | Implanner 1.1.0, Bionic modularity [Workshop 3589154549](https://steamcommunity.com/sharedfiles/filedetails/?id=3589154549), files 2026-09-05 |

<!-- l10n:summary -->
Modules share a bionic limb with it, and a limb module asks for its host.

<!-- l10n:b-c7cd0a958354 -->
![The requirement prompt for a leg module](requirement-prompt.png)

<!-- l10n:b-71ab3bae9608 -->
![The plan after Add to plan: the bionic leg and the module are both picked](requirement-added.png)

<!-- l10n:b-1a485abf9e8c -->
Every module occupies a slot on its body part, so one combat module and one
work module fit on the same arm while two combat modules do not. Modules for
limbs install onto a bionic limb and share the part with it, so a planned
bionic arm and a planned arm module coexist. Ticking a limb module while the
plan has no bionic on that part opens a prompt that lists the bionics able
to host it, with the least advanced host selected by default. **Add to
plan** adds your choice and the module together. **Cancel** adds nothing.
