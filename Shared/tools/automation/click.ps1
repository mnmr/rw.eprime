[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$X,
    [Parameter(Mandatory)][int]$Y,
    [int]$WaitMilliseconds = 750
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
    [RimWorldSharedAutomation.Win32]::mouse_event(
        0x0002, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 50
    [RimWorldSharedAutomation.Win32]::mouse_event(
        0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds $WaitMilliseconds
    Write-Host "clicked client ($X,$Y) in shared pid $($process.Id)"
}
