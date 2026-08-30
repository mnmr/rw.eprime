---
title: Key concepts
---
WorkRoles replaces the vanilla priority grid system with a role system.
Instead of assigning priority numbers to work types, you assign a set of
named roles to colonists. Behind the scenes, both systems produce
an ordered set of jobs that colonists should perform.

![A colonist and their role chips](colonist-row.png)

Think of a role as a container for jobs. You can edit roles to choose the
order of jobs within a role, or stick with the defaults (which closely
mirror vanilla behavior).

Most pre-made roles match the columns of the vanilla priority grid, one
role per kind of work colonists can do. The rest are extras the mod adds
for more precise assignments: narrower roles like Butcher or Plant
Cutter, and bundles like Basics.

A work type is one vanilla grid column; a job is one specific activity
inside it. The Doctoring work type bundles jobs like tending patients and
performing surgery:

![The Doctoring work type and its jobs](worktype-jobs.png)

Roles hold either whole work types or single jobs: the pre-made
![Doctor chip](chip-doctor.png) role carries the complete Doctoring work
type, while Butcher carries only the butchering jobs from Cooking.

The stars on a chip are a suitability verdict: how well this colonist
fits the role, from red (bad) through grey (neutral) to white and gold
(good). [Verdicts](topic:verdicts) has the full scale.

Chip order decides what gets done first
([Assignment ordering](topic:ordering)), and each chip switches on and
off without losing its place ([Role states](topic:states)).
