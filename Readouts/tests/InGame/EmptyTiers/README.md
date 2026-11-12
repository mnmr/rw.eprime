# Empty leading tiers regression (RD3)

The fixture removes gold and silver from the active map, supplies steel, and
imports three hide-when-zero tiers through `ReadoutCommands`. Real scoped mouse
input must cycle through the empty gold/silver tiers to reveal stocked steel.
The probe observes the published panel and configured depth; it never invokes
the click handler or sets the target depth to simulate a successful click.

Build Readouts and the shared 1.7 automation runtime, then build `Probe.csproj`.
From the repository root:

```powershell
pwsh Readouts/tests/InGame/EmptyTiers/run.ps1
pwsh Readouts/tests/InGame/EmptyTiers/run.ps1 -Vertical -Scale 1.5
```

Both runs cover hover disabled, expand-on-hover, and expand-on-hover with idle
collapse; actual clicks through depths 1–3; leave/reentry; and wrapping to depth
1. Each uses a separate managed 1.7 run, stops it, preserves logs/captures and
manifests in `Readouts/temp/empty-tiers/<run-id>`, and removes the stopped run.

Verified 2026-11-11:

- Horizontal, 125%: `ccbbd320ba1648e291652f3bffde3916`.
- Vertical, 150%: `0fa9cb1d0f0d461e816321d43d2e5c75`.

All interaction assertions passed. Captures were inspected for compact empty
bands, fixed arrow alignment, selected/hover depth tints, and visible tier-3
stock. Copied Readouts 1.7 assembly hashes matched the Release build, with no
Readouts errors found in these logs. Production assemblies contain no probe
instrumentation. Runtime verification covers 1.7; 1.6 was build-verified.

The Core regression failed before the fix because empty leading tiers had no
marker hit target. The complete Readouts suite passes after the fix, as do the
other repository executable test suites. Both Readouts game versions build with
zero warnings and errors.
