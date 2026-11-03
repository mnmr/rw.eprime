"""Help compatibility verdict markers, 16x16 RGBA glyphs drawn at 16x and downscaled.

verdict-green.png   green check    fully compatible, works out of the box
verdict-blue.png    blue check     fully compatible through custom support
verdict-yellow.png  yellow "!"     partially compatible
verdict-red.png     red cross      incompatible

Run:  python make_verdict_icons.py [Mod ...]
Writes the PNGs beside this script and copies them into each named mod's
mod/Help/Images folder (e.g. Readouts WorkRoles QualityJobs Implanner).
"""
import shutil
import sys
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).parent
ROOT = HERE.parents[2]   # D:\Code\RimWorld
SIZE = 16
S = 256

ICONS = {
    'verdict-green': ((63, 163, 77, 255), 'check'),
    'verdict-blue': ((78, 140, 214, 255), 'check'),
    'verdict-yellow': ((230, 160, 40, 255), 'bang'),
    'verdict-red': ((205, 60, 60, 255), 'cross'),
}


def draw(ink, glyph):
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    w = 44
    if glyph == 'check':
        d.line([(66, 132), (112, 178), (194, 84)], fill=ink, width=w, joint='curve')
    elif glyph == 'cross':
        d.line([(84, 84), (172, 172)], fill=ink, width=w)
        d.line([(172, 84), (84, 172)], fill=ink, width=w)
    else:
        d.rounded_rectangle((106, 44, 150, 160), radius=22, fill=ink)
        d.ellipse((104, 176, 152, 224), fill=ink)
    return img.resize((SIZE, SIZE), Image.LANCZOS)


for name, (ink, glyph) in ICONS.items():
    draw(ink, glyph).save(HERE / f'{name}.png')
for mod in sys.argv[1:]:
    target = ROOT / mod / 'mod' / 'Help' / 'Images'
    target.mkdir(parents=True, exist_ok=True)
    for name in ICONS:
        shutil.copyfile(HERE / f'{name}.png', target / f'{name}.png')
print('wrote', ', '.join(ICONS), 'to', HERE, 'and', sys.argv[1:] or 'no mods')
