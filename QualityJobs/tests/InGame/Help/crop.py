"""Crop real 1920x1080 / 1.0 captures; retain originals in the run evidence."""
from pathlib import Path
import argparse
import json
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument('evidence', type=Path)
args = parser.parse_args()
destination = Path(__file__).resolve().parents[3] / 'mod/Help/Images'
destination.mkdir(parents=True, exist_ok=True)
crops = {
    'toolbar.png': ('toolbar.png', (1780, 915, 1920, 1045)),
    'options.png': ('options.png', (370, 160, 1550, 645)),
    'crafting-bill.png': ('crafting-bill.png', (420, 209, 1500, 872)),
    'finisher-options.png': ('crafting-bill.png', (1205, 276, 1489, 746)),
    'construction.png': ('construction.png', (605, 721, 1128, 1044)),
    'rebuild.png': ('rebuild.png', (1070, 659, 1592, 1044)),
}
for name, (source, bounds) in crops.items():
    with Image.open(args.evidence / 'Captures' / source) as image:
        assert image.size == (1920, 1080), (source, image.size)
        image.crop(bounds).save(destination / name, optimize=True)
(args.evidence / 'help-crops.json').write_text(json.dumps(crops, indent=2) + '\n')
print(destination)
