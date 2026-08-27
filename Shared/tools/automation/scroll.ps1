[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$X,
    [Parameter(Mandatory)][int]$Y,
    [Parameter(Mandatory)][int]$Notches,
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
        throw 'could not translate scroll coordinates to the screen'
    }
    [RimWorldSharedAutomation.Win32]::SetCursorPos(
        $screen.X, $screen.Y) | Out-Null
    Start-Sleep -Milliseconds 100
    # MOUSEEVENTF_WHEEL = 0x0800; positive data scrolls up, negative down.
    # The data parameter is unsigned, so a downward notch is the two's
    # complement of -120.
    $data = if ($Notches -ge 0) { [uint32]120 } else { [uint32]4294967176 }
    for ($i = 0; $i -lt [Math]::Abs($Notches); $i++) {
        [RimWorldSharedAutomation.Win32]::mouse_event(
            0x0800, 0, 0, $data, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 80
    }
    Start-Sleep -Milliseconds $WaitMilliseconds
    Write-Host "scrolled $Notches notch(es) at client ($X,$Y) in shared pid $($process.Id)"
}
