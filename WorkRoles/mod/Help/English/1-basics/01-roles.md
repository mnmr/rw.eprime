---
title: Key concepts
---
<!-- l10n:b-0b4f02eca3c3 -->
WorkRoles replaces the vanilla priority grid system with a role system.
Instead of assigning priority numbers to work types, you assign a set of
named roles to colonists. Behind the scenes, both systems produce
an ordered set of jobs that colonists should perform.

<!-- l10n:b-ebb5bd5b5197 -->
![A colonist and their role chips](colonist-row.png)

<!-- l10n:b-717543003bf6 -->
Think of a role as a container for jobs. You can edit roles to choose the
order of jobs within a role, or stick with the defaults (which closely
mirror vanilla behavior).

<!-- l10n:b-4ff0b4222fb8 -->
Most pre-made roles match the columns of the vanilla priority grid, one
role per kind of work colonists can do. The rest are extras the mod adds
for more precise assignments: narrower roles like Butcher or Plant
Cutter, and bundles like Basics.

<!-- l10n:b-2ea070047d6c -->
A work type is one vanilla grid column; a job is one specific activity
inside it. The Doctoring work type bundles jobs like tending patients and
performing surgery:

<!-- l10n:b-9414ea963bdd -->
![The Doctoring work type and its jobs](worktype-jobs.png)

<!-- l10n:b-60869dfe29f5 -->
Roles hold either whole work types or single jobs: the pre-made
![Doctor chip](chip-doctor.png) role carries the complete Doctoring work
type, while Butcher carries only the butchering jobs from Cooking.

<!-- l10n:b-9af931f46521 -->
The stars on a chip are a suitability verdict: how well this colonist
fits the role, from red (bad) through grey (neutral) to white and gold
(good). [Verdicts](topic:verdicts) has the full scale.

<!-- l10n:b-9bfd05d9475f -->
Chip order decides what gets done first
([Assignment ordering](topic:ordering)), and each chip switches on and
off without losing its place ([Role states](topic:states)).
