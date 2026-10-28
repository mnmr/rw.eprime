"""Publish native-size help crops from an owned 1920x1080 / 1.0 capture run."""
import hashlib
import json
import math
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

from PIL import Image

evidence = Path(sys.argv[1]).resolve()
destination = Path(__file__).resolve().parents[3] / "mod/Help/Images"
records = []


def source(scene):
    data = ET.parse(evidence / f"display-{scene}.xml").getroot()
    assert (data.get("width"), data.get("height"), data.get("scale")) == ("1920", "1080", "1")
    path = evidence / "Captures" / f"{scene}.png"
    with Image.open(path) as image:
        assert image.size == (1920, 1080)
    return data, path


def label(data, text):
    return next(node for node in data.findall("Label") if node.get("text") == text)


def bounds(node):
    x, y, w, h = (float(node.get(key)) for key in ("x", "y", "width", "height"))
    return x, y, x + w, y + h


def publish(scene, name, box):
    _, path = source(scene)
    box = tuple(math.floor(v) if i < 2 else math.ceil(v) for i, v in enumerate(box))
    assert 0 <= box[0] < box[2] <= 1920 and 0 <= box[1] < box[3] <= 1080
    output = destination / name
    with Image.open(path) as image:
        with image.crop(box) as clipped:
            clipped.save(output, optimize=True)
    records.append(dict(image=name, source=path.name, source_sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                        source_size=[1920, 1080], ui_scale=1.0, crop=box, resampled=False))


for scene, name in (("editor", "editor-window.png"), ("defaults", "default-settings.png"),
                    ("export", "export-setup.png"), ("import", "import-selection.png"),
                    ("conflict", "default-conflict.png"), ("options", "options-window.png")):
    data, _ = source(scene)
    x, y, w, h = (float(data.get("window" + key)) for key in ("X", "Y", "Width", "Height"))
    publish(scene, name, (x, y, x + w, y + h))

editor, _ = source("editor")
buttons = [bounds(label(editor, text)) for text in ("Import", "Export", "Set defaults", "Restore defaults")]
publish("editor", "editor-buttons.png", (min(b[0] for b in buttons)-8, min(b[1] for b in buttons)-6,
                                          max(b[2] for b in buttons)+8, max(b[3] for b in buttons)+6))
options, _ = source("options")
position = bounds(label(options, "Position and Size Options"))
reset = bounds(label(options, "Reset position and size"))
publish("options", "position-size.png", (position[0]-4, position[1]-4, max(position[2], reset[2])+8, reset[3]+8))
display = bounds(label(options, "Display Options"))
font = bounds(label(options, "Render counters with tiny font"))
# The checkbox is to the right of the text rectangle, within the section width.
publish("options", "counter-font.png", (display[0]-4, display[1]-4, display[2]+8, font[3]))

(evidence / "help-media.json").write_text(json.dumps(records, indent=2) + "\n", encoding="utf-8")
print(f"Published {len(records)} native-size crops; manifest: {evidence / 'help-media.json'}")
