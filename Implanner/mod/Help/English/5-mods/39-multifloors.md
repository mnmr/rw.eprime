---
title: MultiFloors
icon: verdict-yellow.png
---
<!-- l10n:verdict -->
| **Verdict** | ![Partially compatible](verdict-yellow.png|16) Partially compatible |
| **Evidence** | Source analysis |
| **Checked** | 2026-11-02 |
| **Versions** | Implanner 1.2.8, MultiFloors 1.6.1.3, [Workshop 3384660931](https://steamcommunity.com/sharedfiles/filedetails/?id=3384660931) |

<!-- l10n:summary -->
Automation cannot carry an implant or an operation across a floor boundary.

<!-- l10n:detail -->
Planning, tracking and automation all work, but a medical bill only looks
for its implant on the patient's own floor; MultiFloors' hauling helps
production bills, not medical ones. An operation whose reserved implant
sits on another floor waits until you move it, and a warning beside the
master switch says so (see
[When automation stands down](topic:when-automation-stands-down)). Keep
implants, benches and patients on one floor for full automation.
