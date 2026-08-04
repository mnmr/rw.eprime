# Exports workshop assets from SVG to PNG using Edge headless, then verifies
# geometry by measurement (never eyeballed). Outputs:
#   mod/About/Preview.png                     1280x720 (SVG at 2x)
#   mod/About/ModIcon.png                     256x256  (SVG at 4x)
#   mod/Textures/EPrimeReadouts/ModIcon.png   256x256  (SVG at 4x)
# Icon sizing, chosen to scale acceptably across ALL user UI scales rather
# than perfectly at any one: vanilla loads About/ModIcon.png via
# Texture2D.LoadImage (auto mipmaps, BILINEAR — the GPU rounds to the
# nearest mip; magnifying a mip is what reads as blur) and draws it in
# 32-virtual-px slots (ModSummaryWindow on the loading screen and
# Dialog_Options), i.e. 32/40/48/56/64 physical px at UI scale
# 1/1.25/1.5/1.75/2. Mip spacing is octaves, so no source size avoids
# mip magnification at every scale; a power-of-two source (mips
# 256-128-64-32) is the best compromise: pixel-exact at 32 and 64 (the
# dominant 1080p@1x and 4K@2x cohorts), mild minification at 48/56, with
# magnification confined to the x.25 scales (40px draws the 32 mip).
# WorkRoles ships the same 256 About icon, keeping the mods consistent.
# The in-mod Textures copy is loaded through ModContentLoader (trilinear,
# aniso 2), which blends mips smoothly at any size, so one 256 source
# serves both.
# The preview is fully synthetic: five stylized group bands on the left 30%,
# title text filling the right 70%.
$ErrorActionPreference = "Continue"

$edge = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
if (-not (Test-Path $edge)) { $edge = "C:\Program Files\Microsoft\Edge\Application\msedge.exe" }
if (-not (Test-Path $edge)) { throw "Edge not found" }

$src   = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo  = Split-Path -Parent (Split-Path -Parent $src)
$about = Join-Path $repo "mod\About"

function Export-Svg([string]$svg, [string]$png, [int]$w, [int]$h, [double]$scale) {
    $url = "file:///" + ($svg -replace '\\', '/')
    & $edge --headless=new --disable-gpu --force-device-scale-factor=$scale `
        --default-background-color=00000000 `
        --window-size="$w,$h" --screenshot="$png" $url 2>$null | Out-Null
    if (-not (Test-Path $png)) { throw "screenshot failed: $png" }
}

Add-Type -AssemblyName System.Drawing

# --- Preview: 1280x720 ---
Export-Svg (Join-Path $src "preview.svg") (Join-Path $about "Preview.png") 640 360 2

# --- Icons: render at target size, crop in case Edge enlarged the window ---
# The SVG's intrinsic size is 64, so the window stays 64x64 and the device
# scale factor supplies the output ratio (1 -> 64px, 1.25 -> 80px).
function Export-Icon([string]$png, [int]$side, [double]$scale) {
    $tmp = Join-Path $about "_icon_raw.png"
    Export-Svg (Join-Path $src "modicon.svg") $tmp 64 64 $scale
    $img = [System.Drawing.Image]::FromFile($tmp)
    $s = [Math]::Min($side, [Math]::Min($img.Width, $img.Height))
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $gfx.DrawImage($img, (New-Object System.Drawing.Rectangle(0, 0, $s, $s)),
        (New-Object System.Drawing.Rectangle(0, 0, $s, $s)), [System.Drawing.GraphicsUnit]::Pixel)
    $gfx.Dispose(); $img.Dispose()
    $bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Remove-Item $tmp
}

$texDir = Join-Path $repo "mod\Textures\EPrimeReadouts"
if (-not (Test-Path $texDir)) { New-Item -ItemType Directory -Path $texDir | Out-Null }
Export-Icon (Join-Path $about "ModIcon.png") 256 4
Export-Icon (Join-Path $texDir "ModIcon.png") 256 4

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

foreach ($check in @(
    @{ Path = (Join-Path $about "ModIcon.png"); Side = 256 },
    @{ Path = (Join-Path $texDir "ModIcon.png"); Side = 256 })) {
    $i = New-Object System.Drawing.Bitmap($check.Path)
    # Probe the outermost corner pixels: at these small sizes the pixel one
    # step in already carries the rounded corner's antialiasing coverage.
    $corners = @($i.GetPixel(0, 0), $i.GetPixel($i.Width - 1, 0), $i.GetPixel(0, $i.Height - 1), $i.GetPixel($i.Width - 1, $i.Height - 1))
    "VERIFY icon {0}: {1}x{2} (expect {3}); corner alpha (expect 0): {4}" -f `
        $check.Path, $i.Width, $i.Height, $check.Side, (($corners | ForEach-Object { $_.A }) -join ",")
    if ($i.Width -ne $check.Side -or $i.Height -ne $check.Side) { Write-Warning "icon is not $($check.Side)px!" }
    if (($corners | Where-Object { $_.A -gt 8 }).Count -gt 0) { Write-Warning "icon corners not transparent!" }
    $i.Dispose()
}
