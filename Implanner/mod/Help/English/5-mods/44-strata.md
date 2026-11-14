---
title: Strata
icon: verdict-yellow.png
---
<!-- l10n:verdict -->
| **Verdict** | ![Partially compatible](verdict-yellow.png|16) Partially compatible |
| **Evidence** | Source analysis, automated in-game test |
| **Checked** | 2026-11-02 |
| **Versions** | Implanner 1.2.8, Strata 3.11.6, [Workshop 3762844292](https://steamcommunity.com/sharedfiles/filedetails/?id=3762844292) |

<!-- l10n:summary -->
Automation cannot carry an implant or an operation across a level boundary.

<!-- l10n:detail -->
Planning, tracking and automation all work, but a medical bill only looks
for its implant on the patient's own level; Strata's shortfall hauling
serves production benches, not medical bills. An operation whose reserved
implant sits on another level waits until you move it, and a warning beside
the master switch says so (see
[When automation stands down](topic:when-automation-stands-down)). Keep
implants, benches and patients on one level for full automation.
