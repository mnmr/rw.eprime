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
| Options/Help window tabs | Window-owned translated captions and delegates; language revision rebuilds the list, selection uses cached predicates, PostClose releases the list. Shared HelpTabView owns topic, image and measurement caches keyed by language, UI metric revision, content generation and width; PostClose flushes personal reading history then releases window data. No new game polling or simulation cache. |
| Settings enable/disable button bounds | UI metric revision and translated enable/disable captions; Small-font width and line height. Immediate gated refresh, unchanged inputs reuse scalars; SettingsLabels.Reset clears stamps and caption references. |
| Robot-preparation option geometry | Translated caption identity, Small font, available checkbox width and UI metric revision; wrapped height and measured line height built only after the key changes. Unchanged keys reuse the row height; SettingsLabels.Reset clears stamps and caption references. No model/cache revisions depend on these metrics. |
| Reserved unfinished-item author display (`UftAuthor`) | Current language revision only; one immutable translated string, equal rebuilds retain identity. The vanilla inspector reads the presentation marker without changing invariant `creatorName` save data. Game disposal resets the cached string. |
| Store settings presentation (`StoreSettingsSnapshot`) | Per-save bill, construction, cap, sharing and priority default fields, plus `allowRobotPreparation`; command and load/seed invalidation. A no-op setting command retains snapshot identity and unrelated bill/plan revisions. |
| Construction plan presentation (`PlanPresentationSnapshot`) | Plan target identity/map, configuration, and state; immediate command/lifecycle invalidation |
| Completed-building facts (`completedBuildings`) | Store identity, spawned target identity/map, player faction, CompQuality value, and construction blueprint/frame defs; immediate spawn/despawn, faction and quality events, load and definition reload; equal facts retain identity; map removal/game disposal release entries |
| Construction dialog geometry | Window's fixed content width, translated note text, Small/Medium fonts, and shared UI metric revision; measured only behind the revision gate; metric-only changes preserve pawn eligibility data, language changes refresh labels; window-owned |
| Sparkle overlay maps (`SparkleOverlay.MapSnapshot`) | Plan membership and target identity/map/position/rotation/footprint; immediate structural invalidation plus the 2500-tick audit fallback |
| Structured tips (shared `RimShared.UiLib` `WrTips`/`StructuredTipPresenter`/`WrTipUI` in `Shared/UiLib/Tips`, per-assembly statics; Quality Jobs' window id, table inset and revisions in `UI/TipHost.cs`) | Stable key + continuous-hover session (0.45s delay); content and geometry frozen per session; registries and geometry cleared when `UiRevision.Current` moves; Begin/EndSuppression pairs from popup owners reset the session |
| Bill dialog status | `QualityJobsStore.BillStatusRevision`; entry/count/sharing/configuration changes and external pawn facts |
| Bill status row geometry | Panel-owned status-string identities, available column width and `UiRevision.Current`; Small-font wrapped heights are built only after that gate changes. Stable reads retain the scalar geometry; panel/window release drops all strings and measurements. Wrapped rows advance by their measured height while preserving the existing two-pixel single-line descender allowance. |
| Bill window attachment | Existing window reference (weak key), bill identity and the panel's existing presentation dependencies; attach at factory/open events, reuse on repeated attachment, remove on successful close and reset on game disposal. The existing virtual content method receives its original width; the QJ panel owns the added right column. |
| Welcome conflict notice (shared `WelcomeConflictNotice`) | Caller-supplied translated conflict catalog and active package IDs, resolved once by `WelcomeDialog.FindConflicts` at welcome admission. Immutable selection shared with the window; empty selection adds no geometry. Shared default heading/advice and optional host guidance resolve once per `PreOpen`, measured with the welcome's existing width/UI-metric gate. Window lifetime; no process cache. Per-player `compatibilityWarningsSeen` acknowledges each persistent save/package pair, allowing a newly detected conflict to reopen an already-seen welcome. |
| Construction dialog status | `QualityJobsStore.PlanStatusRevision`; plan configuration/state/map changes and external pawn facts |
| Expected-attempt API memos | Complete configuration in `AttemptsKey`; external pawn-facts revision only for auto-best keys; store identity teardown |
| External pawn facts | Immediate work-priority, skill-level, inspiration, ideology, role, colony-worker spawn/despawn/death, and player-faction changes; 250-tick `ResponsivenessInterval` fallback for XP progress, health/overseer availability, final VSE quality stat values (expertise levels/settings/modifiers), and other unpatched facts; no additional polling while paused |
| Managed recipe set (`ManagedRecipes`) | Definition database: recipe `unfinishedThingDef` and product, product `CompQuality`, and the ingredient filters' declared things/categories (a `GeneticRim.DefExtension_Quality` ingredient excludes the recipe: the ingredient, not the crafter, decides quality; `IngredientQuality`, shared with finisher work-giver generation); startup build queued to run after every mod's static constructor (Quality Bionics Remastered adds `CompQuality` in its own, and the game runs them in no guaranteed order; finisher work-giver generation precedes it, so it also counts `isTechHediff` products), `Invalidate` on definition reload; every load releases unmanaged recipes' gate-locked items and drops per-bill configuration only for bills unmanaged for another reason (e.g. Quality Bionics Remastered removed), keeping it for ingredient-quality bills |
| Optional VSE stat definitions (`QualityBonusStats`) | The three quality StatDef identities; startup and `ManagedRecipes.Invalidate` definition reload; no pawn/map/world references retained |
| Optional Misc. Robots bindings and station index (`MiscRobotSupport`) | AIRobot types/method/field identities bound once per process to immutable delegates. A private game-owned station identity index registers spawn and PostLoadInit events, retaining identity through minification/inventory transfers; duplicate registration is a no-op, destruction/map removal release entries and report whether membership changed, game disposal/owner change releases all references. `WorkerCapabilities` owns pawn-fact invalidation, colony-population filtering and scheduler admission; existing auto-best pool builds read the native index and current containers/holders. Freshly loaded unspawned robots use declared work-type priorities/givers until AIRobot initializes its native definition at SpawnSetup. No inventory polling, new interval, derived pawn cache or render access. |
| Pawn-specific odds (`OddsRows`) | Skill, inspiration, role offset, and normalized post-roll quality bonus; bonus values observed through the external pawn-facts revision; equal keys preserve row identity |
| Text fit widths (shared `RimShared.UiLib.WrText.FitWidth`, per-assembly static) | `(font, text)` key; cleared when `UiRevision.Current` (shared `RimShared.UiLib.UiRevision`: UI scale, tiny-font preference, language) moves; `WrText.Reset` on game dispose (`Patch_GameDispose`) |
| Stock-cap counts | UFT spawn/despawn events keyed by map identity; `FixedTickBoundaryGate(2500)` audit fallback |
| Idle-UFT pooling and dispatch health | Spawn/despawn-maintained UFT index; immediate/next-component-tick reconcile for commands and pause events; explicitly named `ResponsivenessInterval(250)` fallback |
| Welcome dialog assets and layout (shared `RimShared.UiLib.WelcomeDialog`, built in `QualityJobsStore.QueueWelcome`) | Constructor-resolved translated text; window-owned About/Preview.png loaded in `PreOpen`, destroyed in `PostClose` including suspension. Cached geometry depends on compact tile width/height, the maximum vertical row-band height (screen dimensions and welcome membership), `UiRevision.Current`, constructor text, pre-open adornments/warning and preview aspect; equal keys retain geometry. Content grows the window within its row band before scrolling; artwork keeps the compact viewport footprint, and conflicts remain first. Membership and logical screen-dimension changes tile all loaded assembly copies without overlap; metric changes rebuild in `WindowUpdate`. Overflow closes normally and queues the released instance per assembly and persistent save id until capacity returns; disposal, suppression and save changes clear pending references. All assembly copies stop queue admission before disposal closes live welcomes and releases previews; the next Game.FinalizeInit resumes admission. Each PreOpen starts fresh geometry, and WindowUpdate observes UI metrics. Initially unshown welcomes remain unclaimed while capacity is full or the scenario opening is active; retries run on window removal, dimension changes or `Notify_GameStartDialogClosed`. `QualityJobsSettings.welcomeShownSaves` records the persistent world id only on first display, and suspension never removes it; initial opening remains queued after the existing-bill migration prompt |
| Time-driven full audits | `FixedTickBoundaryGate(2500)` hour boundary, game ticks only |

Changes to these dependencies require updated behavioral tests in the same change.

## Text and layout measurement

- The shared measurement cache is `RimShared.UiLib.WrText.FitWidth`, keyed by `(font, text)` and cleared when `UiRevision.Current` moves (UI scale, tiny-font preference or language).
- Fractional UI-scale glyph drift is absorbed by `FitWidth` padding, not by re-measuring per frame.

## Authoritative state

- `QualityJobsStore` is authoritative per-save state.
- Only `Commands` and deterministic `QualityJobsStore` lifecycle code may mutate the shared model.
- `allowRobotPreparation` is a synced per-save setting, enabled by default on
  both new and legacy saves. Its no-op command preserves settings-snapshot
  identity and does not interrupt jobs or advance unrelated revisions.
- Shared-item preparation provenance (`preparationSources`) retains the original
  live source bill across handover, repeated sharing and save/load. Ordinary
  robot preparation observes both destination and source management; explicitly
  assigned finishing remains allowed. Setting/management changes and load
  enforce active-job admission once, with no per-tick polling. UFT destruction,
  bill removal, giver destruction, map removal and store teardown release the
  provenance references.

## Verification

Canonical verification commands:

```powershell
dotnet build -c Release src/QualityJobs.slnx --no-restore
dotnet test src/QualityJobs.Core.Tests --no-restore
```

Completed-building integration verification lives in
`tests/InGame/CompletedBuildings`. Build `Probe.csproj` after the mod, then run
its `run.ps1 -Version 1.6 -Renderer Software` and `run.ps1 -Version 1.7` in
parallel. These batches click the real UI, observe pawns completing rebuilds,
exercise cancellation and full save/reload, and retain evidence under
`temp/completed-buildings`. Use `-Clean` for the final load without the probe.

Building never deploys. Automated verification requires creating a fresh managed
run after building, following the root contract; creation deploys its copies.
Player installation deployment uses `pwsh scripts/deploy.ps1` and a game restart.
