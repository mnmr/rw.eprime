# Changelog

## 1.2.9 — 2026-11-03

- Added: Help pages for mods to show compatibility verdicts and other relevant information.
- Added: You can now create plans targeting ghouls (Anomaly DLC required).

## 1.2.8 — 2026-11-02

- Changed: Multi-level mods no longer switch automation off; a warning explains the limits instead.
- Fixed: Saurid claws (Vanilla Races Expanded) no longer stop planned bionic arms from being installed.

## 1.2.7 — 2026-11-01

- Changed: Plans for the human body now also cover races with the same or more body parts.
- Changed: Races with the same body parts as humans are no longer listed as a separate body.
- Fixed: Combat Bionics' swarm wrist implant can no longer be planned alongside a bionic arm.

## 1.2.6 — 2026-11-01

- Added: Option to select body anatomy target for plans (Any, Human or race from mods, e.g. Android). Targets plans allow for better UI validation and feedback.
- Added: Plan exports carry the body; older exports and saves read as plans for Any body.
- Added: Main bar icon to open the window along with an option to choose which buttons to keep.
- Added: A body filter in the plan editor when a mod adds races with their own bodies.
- Fixed: Toolbar icon is now rendered via standard methods to improve compatibility with toolbar mods like UI Rethemed.
- Fixed: A modded race whose body nests organs differently (such as the liver under a kidney) no longer makes those implants exclusive in everyone's plans. 
- Fixed: An issue where multiple implants targeting the same body part could stall a plan.

## 1.2.4 — 2026-11-01

- Fixed: Multiplayer desyncs with Quality Bionics Remastered when players had different quality multiplier settings. All players now use one shared set of multipliers, taken from the most recent player whose settings changed.

## 1.2.3 — 2026-10-31

- Added: Support for Android Tiers Continued. Android implants can be planned for each side separately, and android operations go to the colonist with the best Crafting skill.
- Added: Implanner follows the Android Tiers setting that lets crafters operate on androids.
- Fixed: Implants for races with their own body parts now list every part with its side, instead of only the left one.

## 1.2.2 — 2026-10-30

- Fixed: Surgery holdbacks, concurrency, and doctor requirements now stay valid when options or patient conditions change.
- Fixed: Quality upgrades avoid fatal intermediate removals.
- Fixed: Healing limbs are protected from permanently weaker implants, including surgeries already underway and custom implant quality settings.
- Fixed: Production respects updated bench and skill limits, budgets variable ingredients and prerequisites, and reports coverage per required quality.
- Fixed: Aborted ranking drags preserve order, shared XML merges duplicate implant slots, and new adults appear in the overview immediately.

## 1.2.1 — 2026-10-30

- Changed: Clarified some help texts and tooltips.

## 1.2.0 — 2026-10-26

- Added: Built-in translations. Available languages: English, French, German, Italian, Japanese, Korean, Brazilian Portuguese, Spanish (European and Latin American), Simplified Chinese, Traditional Chinese, Turkish, Ukrainian, and Vietnamese.
- Added: Welcome warnings when multi-level mods disable automation, clarifying that planning and tracking remain available.
- Fixed: Clarified how equipment affects automation in Help.
- Fixed: Improved CJK help wrapping, Turkish searches, accented plan names, and translated option labels.

## 1.1.9 — 2026-10-18

- Added: Support for alternate production routes (for mod 'EPOE-Forked: Allow direct crafting').

## 1.1.8 — 2026-10-12

- Changed: RimWorld 1.7 support.

## 1.1.7 — 2026-10-04

- Changed: Extracted common logic to shared library for easier maintenance.
- Fixed: Welcome dialogs now wait for any opening dialog to be closed before showing.

## 1.1.6 — 2026-10-03

- Changed: Suppress welcome dialog when dev mode is enabled.

## 1.1.5 — 2026-09-27

- Added: Support for Quality Bionics Remastered (quality in plans and production bills).
- Added: Support for Vanilla Genetics Expanded (including workarounds for its bugs, to ensure surgeries never destroy existing implants).
- Added: Additional options to control surgery/rollout order (and who gets quality items, if quality is enabled through mods like QBR).
- Changed: Implanner now schedules production in the order items are needed, with 1 item per bill and at most two bills per bench.
- Changed: If resource counts drop below the configured thresholds, queued bills are cancelled to preserve the reserved materials.
- Fixed: Implants carried by pawns were not counted as in-stock.
  
## 1.1.4 — 2026-09-23

- Changed: Increased bench limit to 50 since it turns out some folks have more than 10 benches - I salute you.

## 1.1.3 — 2026-09-13

- Added: Option to configure what the upper skill limit of the game is (for mod compatibility).
- Fixed: Removed the upper skill limit for bills (fixes an incompatibility with mods like Endless Growth).

## 1.1.2 — 2026-09-07

- Fixed: Production bills were created incorrectly.

## 1.1.1 — 2026-09-07

- Added: Search input filter for implants.
- Fixed: Hospitalized pawns now only includes the colony's own members (slaves included). Prisoners, guests and patients are not included.
- Fixed: Surgery for the best doctor now gets assigned to the 2nd best doctor (preventing a soft-lock due to self-surgery).
- Fixed: CJK text wrapping support for help content.

## 1.1.0 — 2026-09-05

- Added: Support for (FSF) Advanced Bionics Expansion, Integrated Implants and Bionic Modularity mods.
- Added: Options to control mutual exclusivity of bladder implants and hygiene enhancers (DBH, FSF ABE)
- Changed: Option to allow purchase-only items (archotech implants) in plans.
- Fixed: Plans layout makes better use of window space and provides tooltips for text that might truncate.

## 1.0.3 — 2026-09-02

- Added: Surgery gained an iteration strategy (ASAP) that doesn't batch implant surgeries.
- Changed: Updated help to account for the new iteration option.

## 1.0.2 — 2026-09-02

- Added: Colony panel tooltips for production and surgery that provide detailed breakdowns per implant type.
- Fixed: Performance improvements.

## 1.0.1 — 2026-09-01

- Fixed: A minor UI header alignment fix.

## 1.0.0 — 2026-09-01

- Initial release.
