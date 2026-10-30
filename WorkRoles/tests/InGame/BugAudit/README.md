# Native bug-audit reproductions

Build WorkRoles 1.7 and the shared automation runtime first, then run `run.ps1`.
The script creates, captures, stops and removes its own copied-save run. It never
writes the player's save or deploys to the player installation.

The current probe verifies **WR22** at the production drag mouse-up boundary.
It uses normal commands to create a pinned, disabled, excluded cleaning
assignment on Takeo, initializes the drag state for a valid drop on Hysott,
and invokes `RoleDrag.ResolveMouseUp` with a native MouseUp event. Waylith
requires both `type` and `rawType`, as in the shared automation runtime.
It then invokes the real Make It So handler and requires the pinned assignment
to survive. Duplicate targets and stale sources must be no-ops; moving the
last source assignment must preserve its forced state and pin while releasing
the source's managed set.
This isolates assignment transfer; it does not test pointer hit geometry.

`SUCCESS` means all regression assertions passed.
The successful original reproduction is
`temp/bug-audit/drag-pin-raw-event-fd4c778103e242d3941a099c41c2dd7d`.
