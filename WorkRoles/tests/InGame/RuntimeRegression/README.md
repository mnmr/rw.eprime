# Native runtime regressions

Build WorkRoles 1.7 and the shared automation runtime, then run `run.ps1`.
It creates a single-use copied-save run, preserves evidence, stops that exact
run and removes it. `SUCCESS WR23 WR24 WR29` means every assertion passed.

- WR23 uses an existing cooking bill's actual `PawnAllowedToStartAnew` gate.
  Direct and combined membership are checked with global/member toggles,
  disabled and force-on assignments, blockers, outer hours and locations.
  It also verifies priority attribution remains on the outer assignment.
- WR24 creates a native encounter and attached undercave, settles the surface
  through the game's Settle method, and checks both holders' real compiled
  work immediately while paused. An unrelated map retains its cached identity.
- WR29 keeps the Work tab open across native ghoul conversion and revert,
  with automatic assignment disabled. Published rows must update for managed
  and roleless pawns without reopening the window.

Original failing native evidence is preserved in `temp/audit-runtime` under
`0b6ee7780215432fb344b194e47d9971` (WR23/24) and
`3ee62a03773348c2aee5e0d55140dad6` (WR29).
This verifies the changed runtime boundaries on 1.7; it does not exercise a
native multiplayer session or complete a production bill at the bench.
