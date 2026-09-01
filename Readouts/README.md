# EPrime's Readouts

EPrime's Readouts replaces [RimWorld](https://rimworldgame.com/)'s resource readout with customizable groups that render resource counts as a stylish, modern band with up to three display tiers.

## Key features

- **Display Groups:** each group is a band of resource icons with counters, rendered as transparent stripes with a small colored group marker so counters stay readable against the map. Groups can be enabled/disabled and reordered.
- **Tiers:** put your must-see resources in tier 1 and details in tiers 2-3, then click a band's triangle markers to cycle how many tiers are shown.
- **Resource Pools:** combine resources into pools, so you can render "Meats" (or all leathers, all stone blocks) as a single icon with the summed count. Tooltips then provide the details.
- **Beyond Resources:** any storable item can be tracked, not just resources — weapons, apparel, bionics and more can be added to groups and pools.
- **Easy Setup:** ships with predefined groups (Food, Raw, Medicine, Drugs, Textiles, Materials, Wealth) and resource pools covering every vanilla and DLC resource. A Restore Defaults button brings them back anytime.
- **Material Debt:** options to subtract resources needed for planned work (bills, buildables), with support for [EPrime's Quality Jobs](https://steamcommunity.com/sharedfiles/filedetails/?id=3776722051).
- **Ignore Missing:** per-item option to skip rendering if count is 0 (to keep readouts minimal). Thresholded resources stay visible at zero.
- **Warning Thresholds:** per-item low/critical thresholds, so counts tint yellow/orange when running low.
- **Search:** quick search that highlights matches and lists every matching resource on the map, including ones not in any group. Can be configured or turned off in options.
- **No Map Scrolling:** map scrolling is disabled when the mouse hovers over the readouts, so you can interact without force-panning the map left.
- **Options:** fully customizable groups, tiers and resource pools in the built-in settings dialog, with drag-and-drop editing and a live per-tier preview that renders exactly like the readout does in-game.
- **Import/Export:** easily share or backup your readout setup.
- **Designed for Performance:** cached rendering, no per-frame work, event- and interval-driven logic (updates counters every 204 ticks).

## Compatibility

- Requires [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077).
- Safe to add to or remove from existing saves. Removing a content mod cleans its resources from your groups automatically.
- Multiplayer compatible ([RimWorld Multiplayer](https://steamcommunity.com/sharedfiles/filedetails/?id=2606448745)): group definitions are shared and synced; what each player shows is their own choice.
- MultiFloors mod support: readouts show combined resource counts across all floors of a level stack. Other multi-floor mods (Strata, 'As above, so below 2') do not need special support to work.
- Not compatible with other mods that replace the resource readout (Readouts+, Custom Resource Readout, Toggleable Readouts, and similar).

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
- [EPrime's Implanner](https://steamcommunity.com/sharedfiles/filedetails/?id=3793988932): plan, track and optionally automate the rollouts of bionic implants.

## Disclaimer

Created with the help of Claude Code.

## License

See [LICENSE](../LICENSE).
