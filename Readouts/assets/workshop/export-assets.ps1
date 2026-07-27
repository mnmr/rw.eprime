# Exports workshop assets from SVG to PNG using Edge headless, then verifies
# geometry by measurement (never eyeballed). Outputs:
#   mod/About/Preview.png  1280x720 (SVG at 2x)
#   mod/About/ModIcon.png  256x256  (SVG at 4x)
# The preview is fully synthetic: five stylized group bands on the left 30%,
# title text filling the right 70%.
$ErrorActionPreference = "Continue"

$edge = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
if (-not (Test-Path $edge)) { $edge = "C:\Program Files\Microsoft\Edge\Application\msedge.exe" }
if (-not (Test-Path $edge)) { throw "Edge not found" }

$src   = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo  = Split-Path -Parent (Split-Path -Parent $src)
$about = Join-Path $repo "mod\About"

function Export-Svg([string]$svg, [string]$png, [int]$w, [int]$h, [int]$scale) {
    $url = "file:///" + ($svg -replace '\\', '/')
    & $edge --headless=new --disable-gpu --force-device-scale-factor=$scale `
        --default-background-color=00000000 `
        --window-size="$w,$h" --screenshot="$png" $url 2>$null | Out-Null
    if (-not (Test-Path $png)) { throw "screenshot failed: $png" }
}

Add-Type -AssemblyName System.Drawing

# --- Preview: 1280x720 ---
Export-Svg (Join-Path $src "preview.svg") (Join-Path $about "Preview.png") 640 360 2

# --- Icon: render at 4x, crop to 256x256 in case Edge enlarged the window ---
$tmpIcon = Join-Path $about "_icon_raw.png"
Export-Svg (Join-Path $src "modicon.svg") $tmpIcon 64 64 4
$img = [System.Drawing.Image]::FromFile($tmpIcon)
$side = [Math]::Min(256, [Math]::Min($img.Width, $img.Height))
$bmp = New-Object System.Drawing.Bitmap($side, $side)
$gfx = [System.Drawing.Graphics]::FromImage($bmp)
$gfx.DrawImage($img, (New-Object System.Drawing.Rectangle(0, 0, $side, $side)),
    (New-Object System.Drawing.Rectangle(0, 0, $side, $side)), [System.Drawing.GraphicsUnit]::Pixel)
$gfx.Dispose(); $img.Dispose()
$bmp.Save((Join-Path $about "ModIcon.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Remove-Item $tmpIcon

# Copy ModIcon to mod/Textures for ContentFinder
$texDir = Join-Path $repo "mod\Textures\EPrimeReadouts"
if (-not (Test-Path $texDir)) { New-Item -ItemType Directory -Path $texDir | Out-Null }
Copy-Item (Join-Path $about "ModIcon.png") (Join-Path $texDir "ModIcon.png") -Force

Get-Item (Join-Path $about "Preview.png"), (Join-Path $about "ModIcon.png"),
    (Join-Path $texDir "ModIcon.png") |
    ForEach-Object { "{0}  {1} bytes" -f $_.FullName, $_.Length }

# --- Verification: measured, never eyeballed ---
function Test-Bright([System.Drawing.Color]$c) { return ($c.A -gt 200 -and $c.R -gt 180 -and $c.G -gt 180) }

$p = New-Object System.Drawing.Bitmap((Join-Path $about "Preview.png"))
# Text block extents: scan the right 70% for bright (title) pixels.
$minX = 99999; $maxX = -1; $minY = 99999; $maxY = -1
for ($y = 0; $y -lt $p.Height; $y += 2) {
    for ($x = 360; $x -lt $p.Width; $x += 2) {
        if (Test-Bright $p.GetPixel($x, $y)) {
            if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
        }
    }
}
$textZoneLeft = 384   # 30% of 1280
$textZoneRight = 1280
"VERIFY preview {0}x{1}: title x={2}..{3} y={4}..{5}" -f $p.Width, $p.Height, $minX, $maxX, $minY, $maxY
"VERIFY fill: left-margin={0}px right-margin={1}px (want small, roughly equal); v-center offset={2}px (want ~0)" -f `
    ($minX - $textZoneLeft), ($textZoneRight - $maxX), ((($minY + $maxY) / 2) - ($p.Height / 2))
$p.Dispose()

$i = New-Object System.Drawing.Bitmap((Join-Path $about "ModIcon.png"))
$corners = @($i.GetPixel(1, 1), $i.GetPixel($i.Width - 2, 1), $i.GetPixel(1, $i.Height - 2), $i.GetPixel($i.Width - 2, $i.Height - 2))
"VERIFY icon {0}x{1}: corner alpha (expect 0): {2}" -f $i.Width, $i.Height, (($corners | ForEach-Object { $_.A }) -join ",")
if (($corners | Where-Object { $_.A -gt 8 }).Count -gt 0) { Write-Warning "icon corners not transparent!" }
$i.Dispose()
