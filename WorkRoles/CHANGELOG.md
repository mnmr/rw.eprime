# Changelog

## 1.2.1 — 2026-07-21

- Added: More Than Capable integration. Hated work is treated as an Awful recommendation signal and assigned roles containing hated work now show a red warning marker.
- Changed: Added a red/yellow lightning icon decoration on role assignments containing jobs that a colonist will never do. Red when colonist will do none of the jobs, yellow otherwise.
- Fixed: Reorganized code to make project more maintainable and testable.

## 1.2.0 — 2026-07-20

- Added: Recommendation Tuning (under Options and per-role). Specify the default recommendation order for roles, customize training paths (role progression definitions) and set assignment counts.
- Added: Import/export extended to include role groups and recommendation tuning options.
- Changed: Restore Roles is now Restore Defaults and supports roles, groups, colors and recommendation tuning options.
- Changed: Recommendation engine has been fully rewritten to use training paths and configurable per-role assignment ranges, so it can provide suggestions that reflect how you want them to be.
- Changed: Tooltips on colonist skills and role recommendations now show recommendation signals/verdict and assignment reasoning.
- Changed: Lots of UI polish, with improved tooltips and help messages.
- Changed: Split Basics role (added new Core role, kept Basics for the rest), so Doctor/Medic/Nurse can slot in before less important tasks.
- Fixed: Recommendation signals from Vanilla Skills Expanded and Alpha Skills were incomplete. Coverage should be much improved now.
- Fixed: Additional performance fixes and internal housekeeping.

## 1.1.4 — 2026-07-16

- Fixed: Performance fixes.

## 1.1.3 — 2026-07-15

- Added: Configurable training goals, skill gates and promotion targets. Flag roles as "Training role" to adjust options.
- Added: Customizable template to set the default order of assignments that the recommendation engine uses.
- Changed: Reworked how the recommendation engine finds training roles, so it can provide better suggestions and work with player-made roles.
- Changed: Improved the seeded/default roles, so they include training roles with sensible defaults.
- Fixed: Role editor fixes and UI polish, as well as support for training roles.
- Fixed: Fixed an issue where all pawn priorities were reset when no roles were assigned, which could have affected mechs. Loading should now self-heal this.

## 1.1.2 — 2026-07-14

- Added: An Options tab to control how priorities are provided to other mods that don't know about WorkRoles (e.g. Numbers and RimHUD).
- Added: You can now open a priority grid viewer to see what priorities WorkRoles generates from the role assignments, so you can verify that everything works as expected.

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
- Changed: Made the Odd Jobs role visible (a container for invisible modded jobs that can come and go, depending on the mod).
- Added: Blocker roles, as a mechanism to explicitly exclude pawns from doing the jobs it lists. Configure via the "Blocker role" checkbox in the role editor. Seed now includes a "No Firefighting" blocker role that recommendations place it above Basics for pawns genetically terrified of fire.
- Added: Render slave names using vanilla color and add a filter option for colonist type (colonists and slaves, colonists only, slaves only).
- Added: Vanilla Skills Expanded / Alpha Skills integration (in the colonist stats panel and recommendation engine, where expertise now outranks burning passion).

## 1.0.2 — 2026-07-09

- Fixed: Role seeding could fail due to bad mod interactions, causing the save to be marked as seeded without actually creating any roles.
- Fixed: One corrupt pawn could abort the priorities-to-roles migration for all remaining pawns.
- Added: A "Restore Roles" button on the Roles tab (top-right corner) that re-creates missing pre-made roles (with preview).

## 1.0.1 — 2026-07-09

- Fixed: The main window didn't enforce a min width, so when colonists only had few roles assigned, the roles tab ended up with not enough space.

## 1.0.0 — 2026-07-08

- Initial release.
