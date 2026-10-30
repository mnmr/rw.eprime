# Native lead-retirement comparison

Build WorkRoles 1.7 and the shared automation runtime first. Run `run.ps1` with
`-Mode baseline -ModSourceRoot <immutable baseline>` and then `-Mode final`
against the changed production artifacts. Use `-SkipProbeBuild` on the second
run to retain the identical probe binary. Every invocation owns and cleans one
copied-save run, preserving logs, captures and ordered outcomes.

The probe retains all 27 native Fisso colonists and their original assignments,
states, pins, rules, blockers and training paths. For saved and nondefault legacy
lead settings it records the production adapter/planner's ordered recommendations
and training paths, actual Fix My Colony targets and actual Apply outcomes.
Compare `lead-outcomes.txt` exactly across baseline and final. The ordinary
portable export/import APIs must preserve both nondefault legacy lead values.

Native clicks expand the remaining Hunter section, reset its thresholds to
4/8/12/16, then collapse it. UI snapshots must contain five sections before the
retirement and four afterward, with unchanged-read identity reuse. Screenshots
and `global-sections-bounds.txt` supply the actual section-stack pixel bounds
for reviewed help-artwork cropping; the probe does not edit images.
