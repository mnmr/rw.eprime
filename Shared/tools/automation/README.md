# Background game automation

The shared commands run RimWorld on a private Windows desktop. They never switch
desktops, focus the game on your desktop, move the hardware cursor, or send global
keyboard input. Keep using your normal applications while a run is in progress.
Screenshots come directly from the game renderer, including when it is invisible.

## Prepare and run

Use PowerShell 7 from `D:\Code\RimWorld`:

```powershell
pwsh Shared/tools/automation/build-runtime.ps1
pwsh Shared/tools/automation/deploy-runtime.ps1
pwsh Shared/tools/automation/refresh-profile.ps1
try {
    pwsh Shared/tools/automation/launch.ps1
    if ($LASTEXITCODE) { throw 'Launch failed' }
    pwsh Shared/tools/automation/run-sequence.ps1 -File Readouts/temp/check.actions
} finally {
    pwsh Shared/tools/automation/stop.ps1
}
```

Build and deploy the mod under test separately, before launch. Building never
updates the installed assembly. Close the disposable game as soon as verification
ends. Launch refuses to start alongside any existing RimWorld process.

The only supported profile is `AutomationProfiles/Shared`. Refresh reads the
newest player `Fisso-NAM*.rws` without modifying it, verifies the copied save and
ordered mod list, then enables `eprime.sharedautomation` in the disposable copy.
Use `refresh-profile.ps1 -ModSet readouts-modern-dev-tools` for that compatibility
scenario. Plain refresh restores the baseline plus the automation runtime.

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

Captures are PNGs under `AutomationProfiles/Shared/Captures`; `cursor` adds a
documentation arrow at the virtual pointer, never a capture of your real cursor.
The individual `click.ps1`, `scroll.ps1`, `capture.ps1`, `capture-hover.ps1`, and
`click-capture.ps1` entry points use the same transport.

## Isolation, failure, and supported controls

The host creates a separate Win32 desktop and owns the child process tree through
a kill-on-close job. The mod activates only with the canonical profile, fresh
launch token, and matching private desktop. Every command resolves the exact
profile/PID/token again; there are no reusable endpoint files. Pipe work is
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

Inspect `AutomationProfiles/Shared/Player.log` and `Player.log.host-error.txt` for
startup failures. Keep scenario actions, logs, captures and assertions under the
tested mod's `temp` directory. `verify-background.ps1` runs an action file with a
read-only foreground audit and always stops the test process.
