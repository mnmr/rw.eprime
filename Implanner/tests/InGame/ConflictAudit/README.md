# Conflict-rule oracle audit

A mod-agnostic check of `ImplantConflicts.Conflicts` against the game's own
surgery workers. For every pair of catalog kinds that can meet on one anatomy
instance (same record, or one kind's record under the other's when either
clears its part), the probe installs one through its real worker on a test
pawn, asks the other kind's worker whether its record is still offered,
installs it too and checks both hediffs survived; in both orders. The game
"coexists" when some order keeps both. Implanner must say conflict exactly
when no order does.

- STRICT: Implanner excludes a pair the game lets coexist in some order.
- LAX: Implanner allows a pair the game never keeps together.
- ORDER: the game keeps the pair in one order only (informational; the
  failing step is named: not offered, surgery threw, first removed, second
  not added).

Pairs of two plain implants installed by the unmodified vanilla implant
worker with no incompatibility or removal tags cannot be refused by the game
and are counted as assumed. Per body the probe also reports the catalog slots
missing or extra versus human, the pawn kind it generated, and the kinds whose
surgery a fresh pawn of that body is not offered (in-place upgrades and
race-restricted recipes land here).

Run from the repository root after building Implanner for RimWorld 1.6:

```powershell
dotnet build Implanner/tests/InGame/ConflictAudit/Probe.csproj -c Release -p:NuGetAudit=false
Implanner/tests/InGame/ConflictAudit/run.ps1 -ModSet implanner-conflict-audit-all -Bodies Human -Shard 0/6
Implanner/tests/InGame/ConflictAudit/run.ps1 -ModSet implanner-conflict-audit-all -Bodies 'Milira_Body,Milian_Body'
```

One body, or one pair shard of a large catalog, per run keeps the sweep in
the 60 s budget (about 45,000 pairs per run). The sweep runs in 200 ms slices
per frame with time running, because the run guard needs frames and ticks.
Results land in `Implanner/temp/conflicts/audit/<modset>-<bodies>-<shard>-<run-id>`.

Mod sets: `implanner-conflict-audit` (FSF bionics, Integrated Implants,
Archotech Bionics, Dubs Bad Hygiene, Vanilla Genetics Expanded, Beyond Our
Reach, Android Tiers, Milira) and `implanner-conflict-audit-all` (those plus
every implant mod installed on 2026-11-01: Anima Bionics, Astraltech,
CeleTech MKIII and its organs, Combat Bionics, Crypto Bionics, Epochs
Pyrinth bionics, Greyscythe, EPOE-Forked, KEP Toolbox, lem's, Protogen,
RBSE, The Cosmic Odyssey, Progression: Bionics, Quality Bionics Remastered,
and the Integrated Implants compatibility patches).

Pitfalls met while writing it: a sweep inside one frame freezes the game and
the run guard stops it; `RestorePart` keeps `keepOnBodyPartRestoration`
hediffs (FSF's immortality core under Anomaly), which then poison every later
pair, so the reset removes part hediffs by hand; a surgery worker that drops
items (Android Tiers) logs "spawn in a null map" for the unspawned pawn, which
is harmless.

Results 2026-11-01 (793 catalog kinds, 51 mods; logs under
`temp/conflicts/audit`):

- Human, all six shards, about 125,000 related pairs compared: LAX 0 after
  the Combat Bionics fix (before it, 100 pairs: the swarm wrist implant beside
  every arm replacement at the shoulder; its worker checks for missing parts
  and parts under an added part are missing). STRICT 16, all The Cosmic
  Odyssey: its skinweaves declare `ArmorGland` incompatible without carrying
  the tag, and its blood implants declare lower tiers incompatible one way,
  so the game accepts the pair in one order while Implanner excludes it
  (policy question, left as is). ORDER: part-clearing kinds (wipers such as
  neuron reinforcement and RBSE's neurostimulator, every bionic arm, leg and
  eye) must precede the implants that mount on them; expected.
- Android4Tier and AndroidM7Mech: no disagreements; anatomy differs from
  human in 62 missing and 16 to 19 extra slots.
- Milira_Body: human slots plus two wing slots, every human kind offered on a
  Milira pawn, same results as human on the compared pairs. Milian_Body has
  one catalog slot and no pawn kind that generates it; skipped.
