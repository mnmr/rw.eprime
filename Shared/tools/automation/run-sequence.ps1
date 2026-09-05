[CmdletBinding()]
param(
    # Path to an action script: one action per line, '#' comments allowed.
    #   click X Y [waitMs]     left click at client coords (default 500ms)
    #   rclick X Y [waitMs]    right click
    #   hover X Y [waitMs]     move cursor only (default 900ms, for tooltips)
    #   capture NAME [cursor]  screenshot the window; 'cursor' composites the
    #                          visible mouse cursor into the image (help
    #                          images), otherwise the pixels are cursor-free
    #   scroll X Y NOTCHES     wheel notches at client coords (+up / -down)
    #   type TEXT              send keystrokes to the focused control (SendKeys
#                          syntax; the rest of the line is the text)
#   sleep MS               plain wait
    [Parameter(Mandatory)][string]$File
)

. (Join-Path $PSScriptRoot 'automation-common.ps1')
Add-Type -AssemblyName System.Windows.Forms

# The whole sequence runs inside ONE focus session: the player's desktop is
# taken exactly once, the actions run back to back with no per-command
# focus/restore churn, and analysis of the captures happens offline
# afterwards. Never interleave inspection with a running sequence.
if (-not (Test-Path -LiteralPath $File -PathType Leaf)) {
    throw "action file not found: $File"
}
$actions = @(Get-Content -LiteralPath $File | ForEach-Object {
    $line = ($_ -split '#')[0].Trim()
    if ($line) { $line }
})
$captureDirectory = Join-Path $script:AutomationProfilePath 'Captures'
New-Item -ItemType Directory -Path $captureDirectory -Force | Out-Null

Invoke-WithSharedGameWindow {
    param($process, $handle)
    $geometry = Get-SharedWindowGeometry $handle

    function Set-CursorClient([int]$x, [int]$y) {
        if ($x -lt 0 -or $x -ge $geometry.Width -or
                $y -lt 0 -or $y -ge $geometry.Height) {
            throw "client coordinate ($x,$y) is outside $($geometry.Width)x$($geometry.Height)"
        }
        $point = New-Object RimWorldSharedAutomation.Win32+POINT
        $point.X = $x
        $point.Y = $y
        if (-not [RimWorldSharedAutomation.Win32]::ClientToScreen(
                $handle, [ref]$point)) {
            throw 'could not translate coordinates to the screen'
        }
        [RimWorldSharedAutomation.Win32]::SetCursorPos(
            $point.X, $point.Y) | Out-Null
        Start-Sleep -Milliseconds 80
    }

    # CopyFromScreen never includes the hardware cursor. Draw the cursor the
    # game currently shows at its hotspot-corrected client position; the
    # process is DPI aware, so cursor and window coordinates share pixels.
    function Add-CursorToCapture($graphics, $geometry) {
        $info = New-Object RimWorldSharedAutomation.Win32+CURSORINFO
        $info.Size = [Runtime.InteropServices.Marshal]::SizeOf($info)
        if (-not [RimWorldSharedAutomation.Win32]::GetCursorInfo([ref]$info)) {
            return
        }
        if (($info.Flags -band 1) -eq 0 -or $info.Cursor -eq [IntPtr]::Zero) {
            return
        }
        $icon = New-Object RimWorldSharedAutomation.Win32+ICONINFO
        if (-not [RimWorldSharedAutomation.Win32]::GetIconInfo(
                $info.Cursor, [ref]$icon)) {
            return
        }
        try {
            $hdc = $graphics.GetHdc()
            try {
                [RimWorldSharedAutomation.Win32]::DrawIconEx(
                    $hdc,
                    $info.ScreenPos.X - $geometry.X - $icon.HotspotX,
                    $info.ScreenPos.Y - $geometry.Y - $icon.HotspotY,
                    $info.Cursor, 0, 0, 0, [IntPtr]::Zero, 3) | Out-Null
            }
            finally { $graphics.ReleaseHdc($hdc) }
        }
        finally {
            if ($icon.Mask -ne [IntPtr]::Zero) {
                [RimWorldSharedAutomation.Win32]::DeleteObject($icon.Mask) | Out-Null
            }
            if ($icon.Color -ne [IntPtr]::Zero) {
                [RimWorldSharedAutomation.Win32]::DeleteObject($icon.Color) | Out-Null
            }
        }
    }

    function Send-Button([int]$down, [int]$up) {
        [RimWorldSharedAutomation.Win32]::mouse_event(
            $down, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 50
        [RimWorldSharedAutomation.Win32]::mouse_event(
            $up, 0, 0, 0, [UIntPtr]::Zero)
    }

    foreach ($action in $actions) {
        $parts = -split $action
        switch ($parts[0]) {
            'click' {
                Set-CursorClient ([int]$parts[1]) ([int]$parts[2])
                Send-Button 0x0002 0x0004
                $wait = if ($parts.Count -ge 4) { [int]$parts[3] } else { 500 }
                Start-Sleep -Milliseconds $wait
            }
            'rclick' {
                Set-CursorClient ([int]$parts[1]) ([int]$parts[2])
                Send-Button 0x0008 0x0010
                $wait = if ($parts.Count -ge 4) { [int]$parts[3] } else { 500 }
                Start-Sleep -Milliseconds $wait
            }
            'hover' {
                Set-CursorClient ([int]$parts[1]) ([int]$parts[2])
                $wait = if ($parts.Count -ge 4) { [int]$parts[3] } else { 900 }
                Start-Sleep -Milliseconds $wait
            }
            'capture' {
                $name = $parts[1]
                if ([IO.Path]::GetExtension($name) -eq '') { $name += '.png' }
                $outputPath = Join-Path $captureDirectory $name
                $bitmap = New-Object System.Drawing.Bitmap(
                    $geometry.Width, $geometry.Height)
                try {
                    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
                    try {
                        $graphics.CopyFromScreen(
                            $geometry.X, $geometry.Y, 0, 0, $bitmap.Size)
                        if ($parts.Count -ge 3 -and $parts[2] -eq 'cursor') {
                            Add-CursorToCapture $graphics $geometry
                        }
                    }
                    finally { $graphics.Dispose() }
                    $bitmap.Save($outputPath,
                        [System.Drawing.Imaging.ImageFormat]::Png)
                }
                finally { $bitmap.Dispose() }
                Write-Host "captured $name"
            }
            'drag' {
                # drag X1 Y1 X2 Y2 [waitMs]: press at A, glide, release at B.
                Set-CursorClient ([int]$parts[1]) ([int]$parts[2])
                [RimWorldSharedAutomation.Win32]::mouse_event(
                    0x0002, 0, 0, 0, [UIntPtr]::Zero)
                Start-Sleep -Milliseconds 120
                for ($step = 1; $step -le 8; $step++) {
                    $x = [int]([int]$parts[1] +
                        ([int]$parts[3] - [int]$parts[1]) * $step / 8)
                    $y = [int]([int]$parts[2] +
                        ([int]$parts[4] - [int]$parts[2]) * $step / 8)
                    Set-CursorClient $x $y
                    Start-Sleep -Milliseconds 40
                }
                Start-Sleep -Milliseconds 150
                [RimWorldSharedAutomation.Win32]::mouse_event(
                    0x0004, 0, 0, 0, [UIntPtr]::Zero)
                $wait = if ($parts.Count -ge 6) { [int]$parts[5] } else { 500 }
                Start-Sleep -Milliseconds $wait
            }
            'scroll' {
                Set-CursorClient ([int]$parts[1]) ([int]$parts[2])
                $notches = [int]$parts[3]
                # mouse_event takes the wheel delta as an unsigned DWORD; a
                # negative notch travels as its two's-complement bit pattern.
                $step = if ($notches -ge 0) { [uint32]120 } else { [uint32](0x100000000 - 120) }
                for ($i = 0; $i -lt [Math]::Abs($notches); $i++) {
                    [RimWorldSharedAutomation.Win32]::mouse_event(
                        0x0800, 0, 0, $step, [UIntPtr]::Zero)
                    Start-Sleep -Milliseconds 60
                }
                Start-Sleep -Milliseconds 300
            }
            'type' {
                $text = ($action -split '\s+', 2)[1]
                [System.Windows.Forms.SendKeys]::SendWait($text)
                Start-Sleep -Milliseconds 400
            }
            'sleep' {
                Start-Sleep -Milliseconds ([int]$parts[1])
            }
            default {
                throw "unknown action: $action"
            }
        }
    }
    Write-Host "sequence complete: $($actions.Count) actions in pid $($process.Id)"
}
