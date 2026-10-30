# WR30 native preview regression

Build production WorkRoles and the shared automation runtime first, then run
`run.ps1` (optionally `-UiScale 1.0`). The probe keeps the original colony and
uses normal role commands and the actual Fix My Colony preview. Its legal
30-character CJK role name has blocker, time and location markers.

The pre-fix reproduction is retained in
`temp/audit-ui/1eb4e8f44b484d97a020d6dcc2570402`: the chip ended at 524 in
a 508-wide row. This runtime-only layout defect is verified against native
font measurements, rather than a synthetic text metric test.

Assertions cover bounded chips, full-name tooltip content, ordinary chip
widths, wider dialog geometry, paused marker edits and steady/no-op snapshot
identity. Scoped hover captures verify that the full role name is readable;
review every capture before accepting the run. The runner stops and removes
its exact managed run, retaining logs, captures and manifests under `temp`.
