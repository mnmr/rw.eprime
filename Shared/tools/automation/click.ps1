[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$X,
    [Parameter(Mandatory)][int]$Y,
    [int]$WaitMilliseconds = 750,
    # Right mouse button instead of left (context menus, pin options).
    [switch]$Right
)

. (Join-Path $PSScriptRoot 'automation-common.ps1')

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
    $button = if ($Right) { 'right-clicked' } else { 'clicked' }
    Write-Host "$button client ($X,$Y) in shared pid $($process.Id)"
}
