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
verifies the save/hash and ordered mod list, and enables `eprime.sharedautomation`.
Use `-ModSet readouts-modern-dev-tools` for that compatibility scenario. Use
`-ModSourceRoot <worktree>` to snapshot completed builds from an isolated checkout.
Our four mods and automation runtime come from that root; other local mods are
copied from the installation. Build changes after creation do not affect the run.
`refresh-profile.ps1` accepts the same options and always creates a new run.

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
the left button until release at the destination. Scroll notches are signed
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
