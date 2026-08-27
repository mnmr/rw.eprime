param(
    [Parameter(Mandatory)][string]$Png,
    [int]$Y0 = 34,
    [int]$Y1 = 56,
    [int]$X0 = 0,
    [int]$X1 = 1300
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile($Png)
try {
    $w = [Math]::Min($X1, $bmp.Width - 1)
    $lum = New-Object double[] ($w + 1)
    for ($x = $X0; $x -le $w; $x++) {
        $sum = 0.0
        for ($y = $Y0; $y -le $Y1; $y++) {
            $c = $bmp.GetPixel($x, $y)
            $sum += 0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B
        }
        $lum[$x] = $sum / ($Y1 - $Y0 + 1)
    }
    # A seam column is a sharp local minimum: much darker than BOTH neighbors.
    for ($x = $X0 + 1; $x -lt $w; $x++) {
        $l = $lum[$x - 1]; $c = $lum[$x]; $r = $lum[$x + 1]
        if (($l - $c) -gt 12 -and ($r - $c) -gt 12) {
            '{0},{1:F1},{2:F1},{3:F1}' -f $x, $l, $c, $r
        }
    }
}
finally { $bmp.Dispose() }
