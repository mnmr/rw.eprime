# EPrime's Readouts

EPrime's Readouts replaces [RimWorld](https://rimworldgame.com/)'s resource readout with customizable groups that render resource counts as a stylish, modern band with up to three display tiers.

## Key features

- **Resource Pools:** combine resources into pools, so you can render "Meats" as a single counter. Tooltips then provide the details.
- **Easy Setup:** ships with predefined groups and resource pools.
- **Ignore Missing:** per-item option to skip rendering if count is 0 (to keep readouts minimal).
- **Warning Thresholds:** per-item option to set thresholds, so counts render in different colors when running low.
- **Modern UI:** renders as transparent stripes with a small group marker.
- **Search:** quick search and inline result display. Can be turned off in options.
- **No Map Scrolling:** map scrolling is disabled when the mouse hovers over the readouts, so you can interact without force-panning the map left.
- **Tiered:** click to cycle how many tiers are rendered.
- **Options:** fully customizable groups, tiers and resource pools in the built-in settings dialog.
- **Import/Export:** easily share or backup your readout setup.
- **Designed for Performance:** cached rendering, no per-frame work, event- and interval-driven logic (updates counters every 204 ticks).

## Compatibility

- Requires [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077).
- Safe to add to or remove from existing saves.
- Multiplayer compatible.
- MultiFloors mod support: readouts show combined resource counts across all floors of a level stack. Other multi-floor mods (Strata, "As above, so below" I/II) do not need special support to work.

## Building from source

```
dotnet build -c Release src\EPrimeReadouts.slnx
```

Three projects: `EPrimeReadouts.Core` (pure logic, netstandard2.0, unit-tested), `EPrimeReadouts` (net472 game assembly; game refs via the `Krafs.Rimworld.Ref` NuGet package), and `EPrimeReadouts.Core.Tests` (.NET 10, TUnit).

Building never deploys the mod. After building, mirror the current `mod/` folder to your local RimWorld installation with:

```
pwsh scripts/deploy.ps1
```

Override the Mods directory with `pwsh scripts/deploy.ps1 -RimWorldMods <path>`.

Run the tests with:

```
dotnet test src\EPrimeReadouts.slnx
```

## Check out my other mods

- [EPrime's Quality Jobs](https://steamcommunity.com/sharedfiles/filedetails/?id=3776722051): manage quality crafting and construction, so your items get the best possible quality.
- [WorkRoles](https://steamcommunity.com/sharedfiles/filedetails/?id=3760146134): easily and intuitively manage work priorities by assigning named roles to colonists.

## Disclaimer

This was created with the help of Claude Code (see WorkRoles for my longer take on this). The mod is free, made in my spare time, and without AI it likely would not exist.

## License

See [LICENSE](LICENSE).
