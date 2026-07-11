# Changelog

## 1.0.4 — 2026-07-11

- Fixed: adding the mod to an existing save could drop whole work types during priority migration (Doctor, Builder, Cook and Smith no longer matched their roles), leaving colonists without that work. Migration is now covered by tests against the shipped role definitions, and self-checks at seeding time (a dropped work type logs an error).
- Fixed: roles built on a work type (e.g. Doctor) silently lost jobs when a mod (e.g. Complex Jobs) moved those jobs to a different work type. Roles now remember the jobs seen under their work types and keep them even after they move.
- Added: Restore Roles can now also recover vanilla jobs that were moved by mods installed before WorkRoles, and its preview lets you select which items to restore instead of all-or-nothing.
- Added: Shift-clicking a role in the top panel assigns it to the selected colonist.
- Added: An Export button on the Roles tab shows role definitions and the custom color palette as human-editable XML, with copy-to-clipboard or save-to-file (pick a location and filename). Assignments are not included.
- Fixed: recommendations could keep a colonist's Firefighter/Hauler-style singles next to Basics or Grunt, which already include those jobs. Covered singles are now dropped (pinned ones stay).
- Changed: every passion, expertise and aptitude now produces a role recommendation — overlapping specialists order by their own skill, instead of skipping roles that two better colonists already hold.
- Added: A filter row on the Roles tab — search roles by name or pick a specific job to see every role carrying it, with an X to clear.
- Added: An Import button loads role definitions from the export file or the clipboard, with a preview where palette and roles can each be merged (pick individual items) or overwritten wholesale.
- Changed: Role dropdowns (role filter and the + menu on colonist rows) now list roles alphabetically.
- Added: The colonist stats panel lists pawn traits under the portrait, and skill cells show a tooltip with trait bonuses and aptitudes.
- Changed: Recommendation tooltips for training roles (Butcher, Brewer, Medic, Handyman) now name the role they train toward and the required skill level.
- Added: Display options behind a gear button in the filter row — chip rendering (full names, unique initials, or color squares; abbreviated chips show the full name on hover) and colonist order (colonist bar order or A-Z). Both persist across saves.
- Added: Skill columns — a Skills button adds up to 3 skill columns to the colonist table (fractional levels with passion/aptitude/expertise icons and trait tooltips). Adding a column sorts by it; column headers switch the sort, X removes, and the Colonist header restores the default order. Column selection and sort are remembered, so the window reopens as it was closed.

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
