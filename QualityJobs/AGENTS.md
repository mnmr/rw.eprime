# QualityJobs Engineering Contract

The general contract in the repository root `AGENTS.md` (one level up) applies
in full. This file records only what is specific to QualityJobs: project names,
canonical cache dependencies, approved refresh boundaries, and verification
commands. Where this file is silent, the root contract governs.

## Project boundaries

- `src/QualityJobs.Core` must remain deterministic and independent of RimWorld, Verse, Unity, Harmony, and Multiplayer APIs.
- `src/QualityJobs` owns game integration, persistence, patches, rendering, and UI.
- `src/QualityJobs.Core.Tests` owns executable behavioral and regression tests.

## Canonical refresh boundaries

- Time-driven invalidation must fire on computed game-tick boundaries, never on per-frame or per-tick polling.
- The canonical boundary is the 2500-tick hour flip via `FixedTickBoundaryGate`.
- The explicitly named responsiveness fallback interval is `ResponsivenessInterval(250)`.

## Canonical cache dependency matrix

| Cached artifact | Required invalidation inputs |
|---|---|
| Store settings presentation (`StoreSettingsSnapshot`) | The fourteen per-save bill, construction, cap, and sharing default fields; command and load/seed invalidation |
| Construction plan presentation (`PlanPresentationSnapshot`) | Plan target identity/map, configuration, and state; immediate command/lifecycle invalidation |
| Sparkle overlay maps (`SparkleOverlay.MapSnapshot`) | Plan membership and target identity/map/position/rotation/footprint; immediate structural invalidation plus the 2500-tick audit fallback |
| Bill dialog status | `QualityJobsStore.BillStatusRevision`; entry/count/sharing/configuration changes and external pawn facts |
| Construction dialog status | `QualityJobsStore.PlanStatusRevision`; plan configuration/state/map changes and external pawn facts |
| Expected-attempt API memos | Complete configuration in `AttemptsKey`; external pawn-facts revision only for auto-best keys; store identity teardown |
| External pawn facts | Immediate work-priority, skill-level, inspiration, ideology, and role events; 250-tick `ResponsivenessInterval` fallback for XP progress and unpatched facts |
| Text fit widths (`WrText.FitWidth`) | `(font, text)` key; cleared when `UiVersion.Current` moves or on language change |
| Stock-cap counts | UFT spawn/despawn events keyed by map identity; `FixedTickBoundaryGate(2500)` audit fallback |
| Idle-UFT pooling and dispatch health | Spawn/despawn-maintained UFT index; immediate/next-component-tick reconcile for commands and pause events; explicitly named `ResponsivenessInterval(250)` fallback |
| Time-driven full audits | `FixedTickBoundaryGate(2500)` hour boundary, game ticks only |

Changes to these dependencies require updated behavioral tests in the same change.

## Text and layout measurement

- The shared measurement cache is `WrText.FitWidth`, keyed by `(font, text)` and cleared when `UiVersion.Current` moves or the language changes.
- Fractional UI-scale glyph drift is absorbed by `FitWidth` padding, not by re-measuring per frame.

## Authoritative state

- `QualityJobsStore` is authoritative per-save state.
- Only `Commands` and deterministic `QualityJobsStore` lifecycle code may mutate the shared model.

## Verification

Canonical verification commands:

```powershell
dotnet build -c Release src/QualityJobs.slnx --no-restore
dotnet test src/QualityJobs.Core.Tests --no-restore
```

Building never deploys: in-game verification requires `pwsh scripts/deploy.ps1` and a game restart.
