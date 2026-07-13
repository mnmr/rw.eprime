# Changelog

## 1.1.1 — 2026-07-13

  - Changed: Changed job assignment to treat work types and jobs independently. When all sub-jobs for a work type are selected, the UI no longer collapses to the work type. To also make the role cover any future jobs appearing under the work type (from mods), you must have the work type in the role as well.
  
## 1.1.0 — 2026-07-12

### Added
  - Colonist table: allows for flat or grouped rendering; location filters; role chip display options; ability to add skill columns; sort order changed to tab order (A-Z also available).
  - Role groups: organize roles into player-defined groups; show roles by skill or group in the role palette.
  - Import/Export: roles, groups and palette colors as human-editable XML. Export to clipboard or file; import with a preview that merges (pick individual items) or overwrites wholesale.
  - Keyboard/mouse: navigate the table (cursor, home/end, pgup/down), copy/paste role sets (ctrl-c/v), focus search (ctrl+f). Shift-click adds role from palette; mouse wheel scrolls the colonist table.
  - Priority watchdog: when another mod tries to set work priorities on role-managed colonists, a dialog explains which mod, what it touched, and the role-based way to get the same effect. Once per mod per save.
  - Set-up summary: after seeding, a dialog shows what was created and migrated, plus any failures (a new game only reports failures).
  - Odd Jobs can be deleted (with an explanation of what it does) if none of your mods add hidden jobs; Restore Roles brings it back, jobs and assignments included.

  ### Changed
  - Recommendations: every passion, expertise and aptitude now produces a suggestion, overlapping specialists spread by their own skill, and redundant single-job roles are dropped. Tooltips provide reasoning.
  - Role list editing: dragging now organizes only (reorder roles and groups, move between groups). Right-click roles to modify parent-child relationships. 
  - Auto Roles: full support for named settlements; better icons (custom clock and location pin to show what rules apply); organized into a separate Auto Roles group (hidden when empty).
  - Shipped roles reference named palette colors (PaletteDef, Tailwind names) instead of inline RGB — retheme every role using a color by patching one def.
  - Restore Roles can also recover vanilla jobs that mods moved to other work types, and its preview lets you pick individual items.
  
  ### Fixed
  - Roles built on a work type (e.g. Doctor) silently lost jobs when a mod (e.g. Complex Jobs) moved those jobs to a different work type. Roles now remember the jobs seen under their work types and keep them.
  - Priority migration could drop whole work types (player fixable, but annoying).
  - Colonists present at seeding never received the Odd Jobs role, so invisible modded work (e.g. Allow Tool's Finish Off) silently stopped working. Restore Roles restores the role and assignments for existing saves.

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
