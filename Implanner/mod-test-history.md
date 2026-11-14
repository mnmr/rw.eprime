# Mod test history

Verification runs of Implanner against third-party mods, newest first, at most
one row per mod per day (see the root `AGENTS.md`, "Mod test history").
`Version` is the mod's `modVersion`, or its Workshop id and newest file date
when About.xml has none. `Tested` points at the evidence under `temp/`.

| Date | Mod | Version | Implanner | Tested | Outcome |
|---|---|---|---|---|---|
| 2026-11-14 | Multi-Doctor Surgery | Workshop 3356249192, files 2026-11-14 | 1.2.9 | Surgery teams option: Solo, Best team and Default team (lead below a doctor floor of 25) bionic eye operations reserved, bound and installed by the game's doctor job, the mod's exclusion list, Options tab captures, plus a control run without the mod on Fisso 1.7 (`tests/InGame/MultiDoctor`, `temp/multidoctor/run5-72e351a410b94910bbd4b0a8f8550593`, `temp/multidoctor/control2-f583dc01ec1445e59a7610ab6ce72d3d`) | Passed: no errors; Solo stays a plain bill, team modes get the mod's team with an assistant joining, excluded operations stay plain, and without the mod the option is locked to Solo. |
