---
title: Recommendation order
---
The chip list at the top of the Recommendations tab defines which roles
the engine considers and hints at where they land in a proposed row:

![The recommendation order](recommendation-order.png)

Drag chips to reorder; the order breaks ties when two roles score the
same. Roles missing from the list can still be recommended; they slot in
by rule:

- A [training role](topic:training-paths) lands directly after its
path's target, so Nurse and Medic always follow wherever Doctor was
placed.
- A role fully covered by another sits right after the role covering it,
the way Hauler follows Grunt.
- Hunter places dynamically by shooting skill: better shots hunt later,
because their time is worth more elsewhere. Adding Hunter as a chip
locks it to that spot instead; remove the chip to let it roam again.

Add Role also unlocks a role's [tuning options](topic:role-options)
below.
