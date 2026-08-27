[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$X,
    [Parameter(Mandatory)][int]$Y,
    [Parameter(Mandatory)][string]$OutName,
    [int]$WaitMilliseconds = 750
)

. (Join-Path $PSScriptRoot 'automation-common.ps1')

# Hover and capture inside one focus session: the shared window helper
# restores the desktop cursor when a command exits, so hover-dependent UI
# states can only be captured by the same invocation that positions the
# cursor.
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
        throw 'could not translate hover coordinates to the screen'
    }
    [RimWorldSharedAutomation.Win32]::SetCursorPos(
        $screen.X, $screen.Y) | Out-Null
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
    Write-Host "captured hover ($X,$Y) $($geometry.Width)x$($geometry.Height) pid=$($process.Id) -> $outputPath"
}
