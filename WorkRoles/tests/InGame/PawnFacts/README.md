# Native pawn-facts regressions

Build WorkRoles and the shared automation runtime before running. Each command
creates, deploys, launches, stops and removes its own managed run, preserving
results under `WorkRoles/temp/pawn-facts`. It uses the newest read-only player
`Fisso-NAM*.rws`, including its actual orbital map, DLCs, pawn names and skills.

```powershell
pwsh WorkRoles/tests/InGame/PawnFacts/run.ps1 -Mode ui
pwsh WorkRoles/tests/InGame/PawnFacts/run.ps1 -Mode external
pwsh WorkRoles/tests/InGame/PawnFacts/run.ps1 -Mode birthday
```

- **WR14/WR15, `ui`:** actual Colonists window and palette action. Removing
  unrelated filtered membership retains Takeo/Hysott multi-selection; collapse
  and repeated repaints retain it. Removing selected Takeo's filter role hides
  and unselects him, and the real subsequent palette assignment reaches only
  the visible fallback pawn. Equivalent job coverage also qualifies for the
  role filter. Native `SetIdeo` moves name-searched Takeo into the correct
  ideology group while paused, preserving skill facts. Inspect `wr14-before`,
  `wr14-after`, `wr15-before` and `wr15-after` captures: Takeo is absent from the
  role filter after removal, and his ideology header changes after conversion.
- **WR16/WR17, `external`:** native Inhumanized changes Hysott's Animals 16 to 4
  and the cached training recommendation from Handler to Herder; removing it
  restores both. A duplicate native hediff merge and unrelated health changes
  retain cache identities. Native mechlink add/remove updates mechanitor
  qualification and recommendations without replacing skill captures. Native
  Flow ranged drop removes Hunter and re-equipping restores it. Melee changes,
  a rejected existing-primary equip and another pawn's equipment changes
  preserve Flow's unaffected identities and narrow revisions. The rejected
  native equip deliberately produces one annotated game error in Player.log.
- **WR18, `birthday`:** the actual game tick advances a native pawn across
  eighteen. Its standing adult-only Cleaning role persists, the actual window's
  too-young warning clears, and age context refresh preserves skill identity.
  Inspect `wr18-before` and `wr18-after`: the warning marker disappears with
  the same assignment present. This never calls `DebugForceBirthday`.

Repeated warm reads check actual external, skill and recommendation snapshot
identity and narrow revisions. Internal identity assertions protect independent
cache ownership contracts that cannot be established from pixels alone; all
player-facing assertions use native mutations and the real production cache
and window. Captures are evidence to inspect, not automatic visual approval.
The test hides automatic log popups only; recoverable save errors remain logged.

For an independently coordinated launch, build `Probe.csproj`, pass its `Probe`
folder as `create-run.ps1 -AdditionalModSource`, and write the mode to the run's
`pawn-facts-mode.txt` before launch. On `pawn-facts-stage.txt = CAPTURE NAME`,
capture the exact run and create `pawn-facts-captured-NAME.txt` in its profile.
Each mode must reach `SUCCESS MODE ...`; `FAILED` terminates immediately. The
runner allows 50 seconds after shared launch readiness and always stops its
exact run before copying or inspecting evidence.
