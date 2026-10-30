# Native runtime authority regression

After coordinated WorkRoles and shared automation runtime builds, restore the
probe once and run its six independent single-use managed scenarios:

```powershell
dotnet restore WorkRoles/tests/InGame/RuntimeAuthority/Probe.csproj
pwsh WorkRoles/tests/InGame/RuntimeAuthority/run.ps1
```

Each mode stays below the shared 60-second post-ready host cap. `-Mode all`
runs bill/fish enabled, revoked and forced controls in separate fresh runs.
For individual runs, use `-Mode fish-enabled` (or `bill-enabled`), then supply
its recorded `CONTROL ... ticks=N` to the matching revoked mode through
`-ControlTicks N`. Forced modes require no prior control duration.

The 1.7 probe uses the source save's capable adult, native bill/job factories,
ordinary scheduling, unmodified work durations and game ticks. When the save
has only an orbital map, it generates an ordinary native river map for fishing.
It proves native production/catches with roles enabled, immediate interruption
and no completion after revocation, and successful native player-forced work
after complete revocation. Queue fixtures separately prove exact giver cleanup
while valid siblings, forced jobs and unknown origins retain instance/order;
these factory-created short waits never run and do not supply production proof.

The fixture retains the complete source colony. Native `TimeSpeed.Ultrafast`
lets each full-duration session finish within the shared run cap; job work,
toil durations and game ticks are never shortened or fabricated. Two-second
progress records show actual ticks, stage, native speed/rate and pause state.
Current-job checks capture native `loadID` and `JobDef`: native cleanup returns
jobs to a pool before finding replacement work, so a replacement can reuse the
same object. Revocation diagnostics include both identities, original/replacement
giver and driver, the original driver's ended flag, and pooled-reference reuse.

Results and manifests are retained under `WorkRoles/temp/runtime-authority`.
The runner stops its exact run before collecting evidence and removes it after.
The probe is never shipped or deployed to the player's mods.

The observed pre-fix failures are preserved in
`WorkRoles/temp/player-bug-review-2026-10-30/runtime-0c159e44071544bf9c529862d50cac65/runtime-review-results.txt`
(revoked stonecutting consumed its ingredient and produced 20 blocks) and
`WorkRoles/temp/player-bug-review-2026-10-30/fishing-bd2a1dd36e564b62ad6b57e64475015c/runtime-review-results.txt`
(an unattributed native 7619-tick fishing session completed after full revocation).
These failures are at a RimWorld-only executable boundary; no production test
seam or source-text assertion is introduced.
