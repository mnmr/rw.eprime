# Follow-up: retire the buffered-renderer self-checks and recovery ladder

Status: open. Act when the Linux reporters confirm the minimize/restore fix
(GitHub mnmr/rw.eprime#6, shipped after 2026-09-20). Until then everything below
stays in place and must keep passing its tests.

## Why these exist and why they can go

Both Linux reports ("readout gone after suspend/resume", 2026-09-09; "readout
gone after minimize/restore", 2026-09-20) share one mechanism, demonstrated on
Windows/D3D11 on 2026-09-20: screen presentation went through `Sprites/Default`,
the only depth-tested draw in the UI, and RimWorld never resets the backbuffer
depth at GUI time, so one near-depth write (a window-surface round trip) hid the
panel for the rest of the process. Every self-check draws into offscreen targets
that have no depth buffer, so all of them reported healthy while the screen was
blank. Presentation now uses Unity's built-in GUI blit material (`ZTest Always`),
which removes the failure class; the checks were built for a misdiagnosed cause
("textures evicted from GPU memory") that was never reproduced on real hardware.
They are not a feature. Render performance is the priority.

## Remove

- `src/EPrimeReadouts/UI/PanelTextureHealth.cs` (periodic 4x1 presentation-path
  sample, async readback) and `TextureHealthResult`.
- `src/EPrimeReadouts.Core/PanelHealthSchedule.cs` and
  `src/EPrimeReadouts.Core.Tests/PanelHealthScheduleTests.cs`.
- `src/EPrimeReadouts.Core/PanelBufferRecovery.cs` (`BufferRepair` ladder).
- In `src/EPrimeReadouts/UI/ReadoutPanel.cs`: `healthSchedule`, `bufferRecovery`,
  `textureHealth`, `replacementBuffers`, `lastBufferWorkTick`, `recoveryNeeded`,
  `recoveryWarningLogged`, `recoveryReason`, `RequestBufferRecovery`,
  `RecoverBufferedRenderer`, the health/recovery branches of
  `ProcessPendingGraphics`, and the `presentFailures`-triggers-recovery branch in
  `Draw` (keep the per-frame "present failed, draw direct" fallback itself).
- Lost-render-target restoration: `PanelSurfaceChannel.HasLostTarget`,
  `RestoreAfterTargetLoss` and their callers in `PanelFrameBuffers`,
  `PanelHeaderSurface`, `ReadoutPanel` (built for the 2026-09-09 report).
- `PanelTextureSample` and the `out PanelTextureSample` overloads of
  `PublishFromReadback`/`PublishSync` in `PanelBufferBackend` (only the health
  check reads them), plus `PanelSurfaceChannel.frontSample/backSample`.
- `GameRenderData.TryGetLastCountRefreshTick` if the schedule was its only caller.
- `AGENTS.md` (this mod): the "Published texture sample" and "Texture health
  result" matrix rows, the canonical-refresh bullet about texture health checks,
  the "Recovery first re-uploads..." and "Runtime verification must cover..."
  paragraphs, and the buffered-surfaces paragraph about lost targets.

## Keep (deliberately)

- `PanelBufferBackend.TryInitialize`/`ValidateRoundTrip`: it is not a check, it
  calibrates `readbackFlipsRows` (async readback row order differs by platform)
  and disables buffering where the pipeline cannot work. Required.
- The screen-presentation rule in `AGENTS.md` and `PanelBufferBackend.Present`
  drawing through the material-less `Graphics.DrawTexture` with the neutral
  (0.5, 0.5, 0.5, 0.5) tint. This is the actual fix. Never present through a
  custom material without `ZTest Always`.
- Coverage validation inside `PublishFromReadback`/`PublishSync`
  (`requiresCoverage`): CPU-side, runs inside the existing pixel conversion, no
  extra GPU work. Optional to keep; removing it is fine if the tests go with it.
- `Patch_ResourceReadout` fault ladder (exceptions -> vanilla for the frame ->
  direct -> vanilla for the session). Exception safety, not a GPU check.
- The per-repaint "Present returned false -> draw directly this frame" path.
- `MaxConsecutivePublishFailures` in `PanelFrameBuffers` (a failed readback is a
  real transient; three in a row disable buffering). Cheap, keep.

## How to verify the removal

1. `dotnet build -c Release src\EPrimeReadouts.slnx --no-restore` (0 warnings)
   and `dotnet test src\EPrimeReadouts.slnx --no-restore`.
2. Rerun the depth-persistence reproduction on a managed run: compile a temporary
   probe (via `-p:CustomAfterMicrosoftCommonTargets=<Probe.targets>`) whose
   OnGUI prefix draws a fullscreen quad through `Hidden/Internal-Colored` with
   `_ZWrite=1`, `_ZTest=Always`, blend Zero/One, at z=+0.999 under
   `GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0)` (+z is toward the
   viewer; the GUI plane is z=0). Toggle it on for ~1.5 s, then off; capture at
   0 s, 4 s and 10 s after; the panel must stay pixel-identical to the baseline,
   and hover/gear captures must match theirs. Probe hotkeys: use F9, F10, F12,
   F13, F14, F15 only. F11 is RimWorld's screenshot-mode toggle (hides all UI,
   filters Repaint, passes KeyDown) and fakes a "whole UI vanished" result.
   `KeyCode` stops at F15.
3. Restore the clean build and confirm the shipped assembly contains no probe.

Reference evidence (local only, `temp/` is git-ignored): `temp/linux-minimize/`
(investigation.txt, DepthProbe.cs, run3.ps1, verify3.py, captures). Earlier
check-era evidence: `temp/render-health*`, `temp/periodic-texture-health`,
`temp/suspend-recovery`.

## Not needed

- Hooking sleep/minimize/focus/resolution events. The failure is not an event
  to react to; the draw itself is now immune, and no event handler can observe
  the screen without a stalling readback.
- A user-facing "disable buffering" setting (retired earlier; direct rendering
  is far slower).
- A Linux/OpenGL periodic check: `SystemInfo.supportsAsyncGPUReadback` is false
  on OpenGL Core (RimWorld's Linux default), so it can only exist as a stall.
