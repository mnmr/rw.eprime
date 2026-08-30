[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$X,
    [Parameter(Mandatory)][int]$Y,
    [Parameter(Mandatory)][string]$OutName,
    [int]$WaitMilliseconds = 750,
    # Right mouse button instead of left (context menus, pin options).
    [switch]$Right
)

. (Join-Path $PSScriptRoot 'automation-common.ps1')

# Click and capture inside ONE focus session: float menus and other
# focus-sensitive UI close when the shared window helper restores the
# desktop between separate invocations, so the same session must both open
# and photograph them. The cursor stays where it clicked.
if ([IO.Path]::GetFileName($OutName) -cne $OutName) {
    throw 'OutName must be a file name, not a path'
}
if ([IO.Path]::GetExtension($OutName) -eq '') {
    $OutName += '.png'
}
if ([IO.Path]::GetExtension($OutName) -cne '.png') {
    throw 'captures must use a .png extension'
}
$captureDirectory = Join-Path $script:AutomationProfilePath 'Captures'
New-Item -ItemType Directory -Path $captureDirectory -Force | Out-Null
$outputPath = Join-Path $captureDirectory $OutName

Invoke-WithSharedGameWindow {
    param($process, $handle)
    $geometry = Get-SharedWindowGeometry $handle
    if ($X -lt 0 -or $X -ge $geometry.Width -or
            $Y -lt 0 -or $Y -ge $geometry.Height) {
        throw "client coordinate ($X,$Y) is outside $($geometry.Width)x$($geometry.Height)"
    }
    $screen = New-Object RimWorldSharedAutomation.Win32+POINT
    $screen.X = $X
    $screen.Y = $Y
    if (-not [RimWorldSharedAutomation.Win32]::ClientToScreen(
            $handle, [ref]$screen)) {
        throw 'could not translate click coordinates to the screen'
    }
    [RimWorldSharedAutomation.Win32]::SetCursorPos(
        $screen.X, $screen.Y) | Out-Null
    Start-Sleep -Milliseconds 100
    $down = if ($Right) { 0x0008 } else { 0x0002 }
    $up = if ($Right) { 0x0010 } else { 0x0004 }
    [RimWorldSharedAutomation.Win32]::mouse_event(
        $down, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 50
    [RimWorldSharedAutomation.Win32]::mouse_event(
        $up, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds $WaitMilliseconds

    $bitmap = New-Object System.Drawing.Bitmap(
        $geometry.Width, $geometry.Height)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen(
                $geometry.X, $geometry.Y, 0, 0, $bitmap.Size)
        }
        finally {
            $graphics.Dispose()
        }
        $bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
    $button = if ($Right) { 'right-clicked' } else { 'clicked' }
    Write-Host "$button client ($X,$Y) and captured $($geometry.Width)x$($geometry.Height) pid=$($process.Id) -> $outputPath"
}
