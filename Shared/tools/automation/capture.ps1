[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutName,
    [switch]$WaitForRenderedMap,
    # Keep the cursor where it is so hover-dependent UI states survive the
    # capture; without it the cursor parks at the window corner first.
    [switch]$KeepCursor
)

. (Join-Path $PSScriptRoot 'automation-common.ps1')

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
    if ($WaitForRenderedMap) {
        Wait-ForSharedMapPixels $process $handle
    }
    $geometry = Get-SharedWindowGeometry $handle
    if (-not $KeepCursor) {
        [RimWorldSharedAutomation.Win32]::SetCursorPos(
            $geometry.X + 1, $geometry.Y + 1) | Out-Null
        Start-Sleep -Milliseconds 300
    }
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
    Write-Host "captured $($geometry.Width)x$($geometry.Height) pid=$($process.Id) -> $outputPath"
}
