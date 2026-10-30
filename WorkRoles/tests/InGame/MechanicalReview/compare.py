"""Pixel-compare actual current names against d47bdc4c reference draws.

Pass the preserved run directory. Outputs exact differences and review crops;
no image transforms are used in the comparisons. Pillow must be installed.
"""
import csv
import json
import math
import sys
from pathlib import Path
from PIL import Image, ImageChops

root = Path(sys.argv[1])
geometry = {}
for row in csv.reader((root / 'review-geometry.tsv').open(encoding='utf-8-sig'), delimiter='\t'):
    scene, mode, key = row[:3]
    geometry.setdefault((scene, key), {})[mode] = (list(map(float, row[3:7])), row[7])
output = root / 'Comparisons'
output.mkdir(exist_ok=True)
results = []
for (scene, key), modes in geometry.items():
    if 'current' not in modes or 'reference' not in modes:
        continue
    current = Image.open(root / 'Captures' / f'{scene}-current.png').convert('RGB')
    reference = Image.open(root / 'Captures' / f'{scene}-reference.png').convert('RGB')
    boxes = [modes[m][0] for m in ('current', 'reference')]
    # Baseline labels wrap outside the nominal 20/22px name rectangle.
    # Include the overflow and neighboring context instead of clipping the
    # comparison to the rectangle that already clips the player's long name.
    vertical_margin = math.ceil(max(b[3] for b in boxes) * 2)
    rect = (math.floor(min(b[0] for b in boxes))-2, math.floor(min(b[1] for b in boxes))-vertical_margin,
            math.ceil(max(b[0]+b[2] for b in boxes))+2, math.ceil(max(b[1]+b[3] for b in boxes))+vertical_margin)
    a, b = current.crop(rect), reference.crop(rect)
    diff = ImageChops.difference(a, b)
    changed = sum(pixel != (0, 0, 0) for pixel in diff.getdata())
    name = f'{scene}-{key.replace(":", "-")}'
    a.save(output / f'{name}-current.png')
    b.save(output / f'{name}-reference.png')
    diff.save(output / f'{name}-diff.png')
    panel = Image.new('RGB', (a.width*3, a.height), '#222222')
    panel.paste(a, (0, 0)); panel.paste(b, (a.width, 0)); panel.paste(diff, (2*a.width, 0))
    panel.save(output / f'{name}-comparison.png')
    results.append(dict(scene=scene, key=key, rectangle=rect, changed_pixels=changed, control=scene == 'reference-control',
                        current=modes['current'][1], reference=modes['reference'][1]))
(output / 'results.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
print(json.dumps(dict(comparisons=len(results), parity_unequal=sum(r['changed_pixels'] > 0 and not r['control'] for r in results),
                      positive_control_detected=any(r['changed_pixels'] > 0 and r['control'] for r in results), results=results), indent=2))
if not results:
    raise SystemExit('No paired name crops captured')
if not any(r['changed_pixels'] > 0 and r['control'] for r in results):
    raise SystemExit('Deliberate reference-control shift was not detected')
if any(r['changed_pixels'] > 0 and not r['control'] for r in results):
    raise SystemExit('Human name parity differs from pre-feature reference')
