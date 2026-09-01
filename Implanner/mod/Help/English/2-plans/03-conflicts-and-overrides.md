---
title: Conflicts and overrides
---
Two implants cannot always share a body. One part holds one replacement,
and some implants exclude each other outright. Implanner applies the same
rules as the game and resolves collisions when you click:

![Rows warning that selecting them overrides the planned bionic arm](picker-overrides.png)

- Selecting a slot that collides with one of the plan's own picks
  deselects the existing pick. The row warns with an "overrides" caption
  before you click.
- In an extending plan, a colliding inherited slot is suppressed instead:
  the base plan keeps its choice, your plan replaces it locally.

Slots covered by a base plan show an "inherited" caption. Ticking an
inherited slot re-includes it as this plan's own pick.
