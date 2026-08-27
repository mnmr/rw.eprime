param(
    [Parameter(Mandatory)][string]$Png,
    [Parameter(Mandatory)][string]$Out,
    [int]$X, [int]$Y, [int]$W, [int]$H, [int]$Scale = 4
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$src = [System.Drawing.Bitmap]::FromFile($Png)
try {
    $dst = New-Object System.Drawing.Bitmap ($W * $Scale), ($H * $Scale)
    $g = [System.Drawing.Graphics]::FromImage($dst)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.DrawImage($src,
        (New-Object System.Drawing.Rectangle 0, 0, ($W * $Scale), ($H * $Scale)),
        (New-Object System.Drawing.Rectangle $X, $Y, $W, $H),
        [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    $dst.Save($Out)
    $dst.Dispose()
    Write-Host "saved $Out"
}
finally { $src.Dispose() }
