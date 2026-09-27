# Changelog

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
