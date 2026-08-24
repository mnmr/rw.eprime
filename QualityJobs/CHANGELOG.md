# Changelog

## 1.0.8 — 2026-08-24

- Changed: Improved API surface and some internal optimizations.

## 1.0.7 — 2026-08-18

- Fixed: Added two more log guards so that all logging sites are guarded.

## 1.0.6 — 2026-08-18

- Fixed: Delayed init so mod load order isn't important and added a log error guard so the same error isn't logged repeatedly.

## 1.0.5 — 2026-08-16

- Added: API methods allowing other mods to interact with Quality Jobs. EPrime's Readouts uses this to estimate resource material needs, and EPrime's Pawn Planner uses this to schedule new quality work bills.
- Fixed: Performance improvements.
 
## 1.0.4 — 2026-08-10

- Added: Status panel showing item stats (unfinished, waiting, finishing) and eligible finishers for a selected bill or buildable.

## 1.0.3 — 2026-08-08

- Added: Migration dialog to allow users to enable Quality Jobs on existing bills.
- Fixed: Added tooltips for settings that didn't have one already.

## 1.0.2 — 2026-08-06

- Fixed: Source bill was not properly decremented when items were finished (because the mod creates a separate finisher bill for each item to complete).

## 1.0.1 — 2026-08-06

- Added: Option to automatically allow the best available colonist to finish jobs (this overrides the skill requirement and makes it follow the best available colonist).
- Added: Toolbar icon for quick access to settings. Can be disabled.
- Added: Option to specify target quality also for fixed-amount bills; if the produced quality was below the desired amount, bump count on the bill so one more is queued.
- Fixed: Buildables waiting for a finisher would sometimes render an overflowing "work square".

## 1.0.0 — 2026-08-03

- Initial release.
