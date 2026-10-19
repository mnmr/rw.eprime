---
title: Ordering and exclusions
---
Recommendations are ordered automatically for each colonist. The engine
uses its built-in order alongside suitability, colony importance, time
demand, training paths, and coverage. Custom roles fit into that order
using the game's work priorities. The former colony-wide ordering panel
has been removed, and saved custom orders no longer affect recommendations.

- A [training role](topic:training-paths) lands directly after its
path's target, so Nurse and Medic follow Doctor.
- A role fully covered by another sits right after the role covering it,
the way Hauler follows Grunt.
- **Hunter keeps its shooting-skill placement:** poor shots hunt earlier
and better shots hunt later, because their time is worth more elsewhere.
An individually pinned assignment remains protected.

The **Excluded roles** panel at the top of the Recommendations tab controls
which roles must never be recommended. Use Add Role to exclude a role,
or click its chip's remove icon to allow recommendations again.

Rescuer, Firefighter, Patient, Bedrest, and Laborer are excluded by default
because Core and Basics already cover their work. Existing saves receive
these defaults once when updated. Removing a role from the panel is respected
on later loads. To add the defaults back, choose **Default excluded roles**
in [Restore Defaults](topic:restore-defaults).

[Role Options](topic:role-options) below lists all non-excluded roles.
Excluding a role hides its options without erasing them; removing the
exclusion makes those options available again.

Exclusions apply across the save, including [Fix My Colony](topic:fix-my-colony)
and [Auto-optimize](topic:auto-optimize). They override colony demand,
training paths, automatic and special roles, composite substitution, and
unpinned retained assignments, including those on incapacitated pawns.
Excluding a composite leaves its eligible member roles available; excluding
a member prevents it from returning through a recommended composite.

You can still assign excluded roles manually. Editing the exclusion list
does not alter current assignments. Applying recommendations removes unpinned
excluded roles from the affected colonists. **Pinned assignments remain
protected**, even if their role or a composite member is excluded. A kept
pinned assignment is your existing choice, not a new recommendation.
