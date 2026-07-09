# Changelog

## 1.0.3 — 2026-07-09

- Fixed: Some UI elements didn't scale properly, so would render oddly depending on the interface UI scale setting.
- Changed: made the Odd Jobs role visible (a container for invisible modded jobs that can come and go, depending on the mod).
- Added: Blocker roles, as a mechanism to explicitly exclude pawns from doing the jobs it lists. Configure via the "Blocker role" checkbox in the role editor. Seed now includes a "No Firefighting" blocker role that recommendations place it above Basics for pawns genetically terrified of fire.
- Added: Render slave names using vanilla color and add a filter option for colonist type (colonists and slaves, colonists only, slaves only).
- Added: Vanilla Skills Expanded / Alpha Skills integration (in the colonist stats panel and recommendation engine, where expertise now outranks burning passion).

## 1.0.2 — 2026-07-09

- Fixed: role seeding could fail due to bad mod interactions, causing the save to be marked as seeded without actually creating any roles.
- Fixed: one corrupt pawn could abort the priorities-to-roles migration for all remaining pawns.
- Added: a "Restore Roles" button on the Roles tab (top-right corner) that re-creates missing pre-made roles (with preview).

## 1.0.1 — 2026-07-09

- Fixed: the main window didn't enforce a min width, so when colonists only had few roles assigned, the roles tab ended up with not enough space.

## 1.0.0 — 2026-07-08

- Initial release.
