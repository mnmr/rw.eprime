# Editing and Restore Defaults regressions

Build WorkRoles and the shared automation runtime, then build `Probe.csproj`.
Run `pwsh tests/InGame/EditRestore/run.ps1` from WorkRoles. Every invocation
creates and stops its own managed run using the native saved colony.

The scenario exercises actual commands, production export/import, the complete
native recommendation adapter and the pawn's compiled executable work:

- WR6: swap two named custom colors using distinct temporary edits, export the
  result, then import the palette alone with Merge and Overwrite.
- WR7: duplicate native training, preserve/rebind its bands and independent
  collection ownership, and compare final full-colony assignments with the
  deliberately untrained control.
- WR8: delete a trained owner, check separate preview choices, test partial
  selections, restore owner and training together, and verify actual trainee
  recommendations without reopening Restore Defaults.
- WR9: turn native Doctor into a hauling composite, restore its defaults and
  verify that the pawn receives every original executable Doctor job. Repeating
  the same restoration must preserve revisions.
- WR10: restore an edited Doctor blocker without adding a Builder assignment or
  Construction work; then verify that ordinary positive Construction coverage
  still recovers through a template role.

The runner captures the real missing-owner training preview at the managed
1920x1080 / 1.25 default. Review that image before claiming visual verification.
Results, the exported setup, configuration metadata and capture are copied to
`temp/edit-restore/<run-id>` after stopping the game.
