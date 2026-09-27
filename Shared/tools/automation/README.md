# Background game automation

The shared commands run RimWorld on a private Windows desktop. They never switch
desktops, focus the game on your desktop, move the hardware cursor, or send global
keyboard input. Keep using your normal applications while a run is in progress.
Screenshots come directly from the game renderer, including when it is invisible.
The automation profile starts with master volume zero. Its runtime keeps the
game's audio listener muted even when a settings test changes volume; the
player's profile and other applications' audio are unaffected.

## Prepare and run

Use PowerShell 7 from `D:\Code\RimWorld`. Build the mods under test first. Each
run copies their build artifacts and the host, so creating the run is its
**deployment** step; no deployment into the player installation is needed.

```powershell
& Shared/tools/automation/build-runtime.ps1
$run = & Shared/tools/automation/create-run.ps1 -Purpose 'Readouts search regression'
$env:RIMWORLD_AUTOMATION_RUN_ID = $run.runId
try {
    & Shared/tools/automation/launch.ps1 -RunId $run.runId
    & Shared/tools/automation/run-sequence.ps1 -RunId $run.runId -File Readouts/temp/check.actions
} finally {
    & Shared/tools/automation/stop.ps1 -RunId $run.runId
}
# Preserve evidence first, then remove the stopped run:
& Shared/tools/automation/remove-run.ps1 -RunId $run.runId
```

Independent runs have no configured instance limit. Each has its own writable
profile, mod copies, logs, captures, private desktop, token and process-tree job.
Large installed game assets and Workshop content are shared; leave them unchanged.
Concurrent functional tests are supported. Hardware contention makes concurrent
runs unsuitable for isolated performance comparisons.

`create-run.ps1` reads the newest player `Fisso-NAM*.rws` without modifying it,
verifies the save/hash and ordered mod list, and applies the always-on
`automation-runtime` mod set. Use `-ModSet readouts-modern-dev-tools` for that
compatibility scenario. Use
`-ModSourceRoot <worktree>` to snapshot completed builds from an isolated checkout.
Our four mods and automation runtime come from that root; other local mods are
copied from the installation. Build changes after creation do not affect the run.
`refresh-profile.ps1` accepts the same options and always creates a new run.

Every run layers `modsets/automation-runtime.txt`: the automation runtime,
RimBridgeServer, and `remove dubwise.dubsmintminimap` (the minimap takes keyboard
focus on load and adds nothing to mod tests). Mod set lines are `after <id>` /
`before <id>` anchors, lower-case package ids to insert, and `remove <id>`.

## Game state queries (RimBridgeServer)

`launch.ps1` gives each run its own loopback bridge port and token
(`bridge.json`, passed to the game through its environment). `bridge.ps1`
re-validates the run's owner and live game, calls one tool, and prints JSON:

```powershell
& Shared/tools/automation/bridge.ps1 -RunId $id -Tool rimworld/list_colonists
& Shared/tools/automation/bridge.ps1 -RunId $id -Tool rimworld/get_ui_layout -OutFile layout.json
```

Use it to plan and assert instead of reading pixels: colonist cells and jobs,
camera view rect, letters, alerts, the window stack and focused window,
inspect-pane text, gizmos, and control rects. Rects are logical UI pixels;
multiply by the run's uiScale (1.25) for action-file coordinates.
`rimbridge/list_capabilities` lists every tool and parameter. Scripts making
many calls can dot-source `bridge-common.ps1` and reuse one `Connect-RunBridge`
session. Send input through the pipe (action files), not through bridge input
tools. RimBridge enables the game's Ultrafast boost; speeds 1-3 are unchanged.

## In-game test runner (Pickle)

`-ModSet pickle` adds Pickle and RimLogging. Pickle's autorun waits for the
main menu, runs Gherkin features found under each mod's `Pickle/Features`,
writes `summary.json`/`junit.xml`/`report.html`, and quits the game with exit
code 0 (all passed), 1 (a failure) or 2 (framework error) in `host.json`:

```powershell
& Shared/tools/automation/launch.ps1 -RunId $id -HardCapSeconds 30 `
    -GameArguments '-pickle-run=my.feature', "-pickle-report-dir=$profile\PickleReports", '-pickle-no-http', '-pickle-scenario-timeout=25'
```

Passing `-pickle-*` arguments starts the game at the main menu (no autostart),
loads unpaused (Pickle's fixture settle waits for game ticks), and returns the
host's final state when Pickle quits. Only `-pickle-*` flags without spaces or
quotes pass the host. Fixtures are `.rws` files in the owning mod's
`Pickle/Fixtures`; loaded by Pickle after startup, the Fisso save takes about
9 s and `test-colony` about 2 s. Set `-pickle-scenario-timeout` to cover the
first fixture load. Never use Pickle's click/hover steps: they inject OS input.

## Fail-fast guard and hard cap

Runs fail fast instead of stalling. Each input command fails immediately when:

- a key or mouse press reaches a window or the root UI and nothing handles it
  (`Input not consumed: key Period reached RimWorld.MainTabWindow_Inspect ...`);
- a blocking dialog (force-pause or input-absorbing) opened since the previous
  command that the test did not cause, e.g. a letter or a mod popup. A `status`
  command acknowledges the windows open now when a test expects one;
- a click lands outside a modal that absorbs it;
- the input is never delivered within 2 s.

Keys go through the root UI (hotkeys, time controls, Escape/Enter) unless the
test clicked into the focused window or it is a dialog-layer popup.

At game-ready, `launch.ps1` starts a detached `run-guard.ps1`, which reads the
game's `game-state.json` heartbeat (4 Hz) and stops the run, recording the reason
in `stall.json`, when there is no progress (input, advancing ticks or loading)
for 10 s, when the driving script sends no pipe or bridge command for 10 s, when
the heartbeat stops, or 60 s after ready (the hard cap; `-HardCapSeconds` may
only lower it). Load time before readiness is bounded by `-TimeoutMinutes`. The
next command of a stopped run fails with the guard's reason. Wait by polling
state through `bridge.ps1`, never by sleeping.

## Ownership, discovery and cleanup

`list-runs.ps1` returns run ID, owner, purpose, state, live game PIDs, last command,
heartbeat, save and mod set. Add `-Json -Detailed` to include the complete config,
launch record and file hashes. These records are inspection data, never commands.

Ownership defaults to `CODEX_THREAD_ID`. A manual shell defaults to
`manual:<username>`; `RIMWORLD_AUTOMATION_OWNER` can establish a controller identity
before creation. Every control/stop/remove operation checks that identity. Agents
must never impersonate another owner. `-RunId` takes precedence over the optional
process-local `RIMWORLD_AUTOMATION_RUN_ID`. Omitting both fails even with one game.

Each run is single-use: restarting requires a fresh run and token. `run.json`
records its owner/purpose, source save, mod set, source paths and build hashes.
`launch.json` records actual config hashes and display settings. `host.json`
updates every five seconds while the native host owns the game; `activity.json`
updates after commands. The host holds `run.lock` for its entire lifetime and
owns a kill-on-close job. `control.lock` serializes lifecycle operations for that
run. No global lock is held by a running game.

A live PID or held lock means **in use**, regardless of timestamp age. A heartbeat
shows host liveness, not whether an agent still needs the game. Stop your run as
soon as input/capture/assertions finish. Never stop another owner's run merely
because it looks idle. Coordinate with its task or the user instead. Save relevant
manifests, logs and captures into the mod's `temp` folder, then use `remove-run.ps1`.
It rejects active/foreign runs and removes asset junctions without traversing them.
Failed preparations can be inspected and removed by their recorded owner too.
## Action files

Coordinates are physical pixels in the rendered client frame: normally 1920x1080
at UI scale 1.25. Waits are milliseconds, bounded to 60000. Examples:

```text
# Comments are allowed. A trailing # on a type line is literal text.
hover 52 60 1200
capture hovered cursor
click 90 24 400
type ^aSteel{BACKSPACE}l
rclick 580 525
drag 615 574 569 637 500
scroll 600 500 -3
type {ESC}
sleep 500
capture final
```

`click`, `rclick`, `hover`, and `drag` accept an optional final wait. A drag holds
the left button until release at the destination. `click`, `rclick` and `drag`
accept a held-modifier word right after the verb (`click shift 100 470`,
`click ctrl+shift 707 318`, `drag alt 10 10 50 50`); the modifiers ride every
event of that gesture and are released with the button. Scroll notches are signed
(positive scrolls up). `type` accepts literal Unicode text plus SendKeys-style
`^` Control, `+` Shift, `%` Alt, modifier groups such as `^(ac)`, named keys such
as `{ENTER}`, `{TAB}`, `{SPACE}`, `{BACKSPACE}`, `{LEFT 2}`, and `{F12}`. Escape
special characters with braces, e.g. `{+}`, `{^}`, `{(}`, `{{}`, `{}}`. Use literal
punctuation instead of relying on a keyboard layout to produce shifted symbols.
The complete key sequence is parsed before dispatch. There is no clipboard use.

Captures are PNGs under `AutomationProfiles/Shared/Runs/<id>/Captures`; `cursor` adds a
documentation arrow at the virtual pointer, never a capture of your real cursor.
The individual `click.ps1`, `scroll.ps1`, `capture.ps1`, `capture-hover.ps1`, and
`click-capture.ps1` entry points use the same transport.

## Isolation, failure, and supported controls

The host creates a separate Win32 desktop and owns the child process tree through
a kill-on-close job. The mod activates only with the managed run profile, fresh
launch token, and matching private desktop. Every command resolves the exact
profile/PID/token again; stored endpoint data never replaces live process validation. Pipe work is
bounded, Unity access stays on the main thread, and commands time out. An input
timeout ends the disposable session because a missing button release can leave
game-owned capture state uncertain. There is no desktop-input fallback.

The runtime adapts Unity/Verse input and the explicit own-assembly allowlist
(`EPrimeReadouts`, `WorkRoles`, `QualityJobs`, `Implanner`). It does not rewrite
third-party mod methods. Standard game widgets work through their real hit tests
and activation paths. Third-party custom controls that bypass those APIs can
remain unsupported; inspect their observable result rather than treating command
acceptance as proof. OS dialogs and Steam overlays are outside this transport.

Window input is scoped to its target and preserves disabled/used events and
control capture. Window dragging changes presentation geometry only. Sessions
are disposable local tests; this is not a multiplayer control protocol.

Inspect `AutomationProfiles/Shared/Runs/<id>/Player.log` and `Player.log.host-error.txt` for
startup failures. Keep scenario actions, logs, captures and assertions under the
tested mod's `temp` directory. `verify-background.ps1` runs an action file with a
read-only foreground audit and always stops the test process.
