# Changelog

## 1.0.2 — 2026-07-09

- Fixed: role seeding could fail due to bad mod interactions, causing the save to be marked as seeded without actually creating any roles.
- Fixed: one corrupt pawn could abort the priorities-to-roles migration for all remaining pawns.
- Added: a "Restore Roles" button on the Roles tab (top-right corner) that re-creates missing pre-made roles (with preview).

## 1.0.1 — 2026-07-09

- Fixed: the main window didn't enforce a min width, so when colonists only had few roles assigned, the roles tab ended up with not enough space.

## 1.0.0 — 2026-07-08

- Initial release.
