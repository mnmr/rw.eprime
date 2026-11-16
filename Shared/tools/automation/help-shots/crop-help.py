"""Cut a mod's help images from a capture run.

    crop-help.py <mod folder> <run folder>...         cut every image in crops.json
    crop-help.py <mod folder> <run folder>... --check report only, write nothing
    crop-help.py <mod folder> <run folder>... --preview <folder>
                                              write the cuts there instead
    crop-help.py <mod folder> <run folder> --list <capture> [prefix]
                                              print the regions of a capture
    crop-help.py <mod folder> <run folder> --overlay <capture> <out.png> [prefix]
                                              draw the regions on the frame

The mod folder (e.g. D:/Code/RimWorld/WorkRoles) holds
tests/InGame/HelpShots/crops.json and mod/Help/Images.

The run folder holds one full frame per scene (<capture>.png) and
help-shots-manifest.jsonl, which names the pixel rectangle of every element
drawn in each frame. crops.json maps a help image to a capture and four edge
expressions, each "<region id>.<l|t|r|b>" with an optional "+n" or "-n" pixel
offset (or a plain number); "box": "<region id>" gives all four edges of one
region at once. A crop is therefore tied to the elements it shows, not to
coordinates. An image identical to the one already in mod/Help/Images
is left untouched, so unchanged rendering changes no file ("jitter": n on a
crop accepts up to n differing pixels as unchanged).
"""
import json, os, re, sys
from PIL import Image, ImageChops, ImageDraw

EDGE = {"l": 0, "t": 1, "r": 2, "b": 3}
EXPR = re.compile(r"^(.*)\.([ltrb])\s*([+-]\s*\d+)?$")


def manifest(run):
    captures = {}
    with open(os.path.join(run, "help-shots-manifest.jsonl"), encoding="utf-8") as lines:
        for line in lines:
            if line.strip():
                entry = json.loads(line)
                captures[entry["capture"]] = entry
    return captures


def edge(expression, regions, capture):
    if isinstance(expression, (int, float)):
        return int(expression)
    match = EXPR.match(expression.strip())
    if not match:
        raise ValueError("bad edge expression: " + expression)
    region, side, offset = match.group(1), match.group(2), match.group(3)
    return find(region, regions, capture)[EDGE[side]] + (int(offset.replace(" ", "")) if offset else 0)


def find(spec, regions, capture):
    """"id" is an exact region; "id@container" is the first region of that
    name (any ordinal) whose centre lies inside the container region."""
    at = spec.find("@")  # containers nest: "label:enabled@tip@table"
    if at <= 0:
        if spec not in regions:
            raise KeyError("capture '%s' has no region '%s'" % (capture, spec))
        return regions[spec]
    name, box = spec[:at], find(spec[at + 1:], regions, capture)
    for key, rect in regions.items():
        if key == name or key.startswith(name + "#"):
            x, y = (rect[0] + rect[2]) / 2, (rect[1] + rect[3]) / 2
            if box[0] <= x < box[2] and box[1] <= y < box[3]:
                return rect
    raise KeyError("capture '%s' has no region '%s'" % (capture, spec))


def same_as_old(status):
    return status.startswith("unchanged")


def main():
    arguments = sys.argv[1:]
    mod = arguments[0]
    # One run folder per mod set the mod's images need; a crop names its
    # run with "variant" (the mod set) and otherwise uses the first one.
    folders = []
    while len(arguments) > 1 + len(folders) and not arguments[1 + len(folders)].startswith("--"):
        folders.append(arguments[1 + len(folders)])
    run = folders[0]
    images = os.path.join(mod, "mod", "Help", "Images")
    captures = manifest(run)
    default = (run, captures)
    variants = {}
    for folder in folders:
        name = os.path.join(folder, "help-shots-variant.txt")
        if os.path.exists(name):
            with open(name, encoding="utf-8") as source:
                variants[source.read().strip()] = (folder, manifest(folder))

    if "--list" in arguments:
        at = arguments.index("--list")
        prefix = arguments[at + 2] if len(arguments) > at + 2 else ""
        for name, box in captures[arguments[at + 1]]["regions"].items():
            if name.startswith(prefix):
                print(name, box)
        return 0

    if "--overlay" in arguments:
        at = arguments.index("--overlay")
        capture, out = arguments[at + 1], arguments[at + 2]
        prefix = arguments[at + 3] if len(arguments) > at + 3 else ""
        frame = Image.open(os.path.join(run, capture + ".png")).convert("RGB")
        draw = ImageDraw.Draw(frame)
        for name, box in captures[capture]["regions"].items():
            if name.startswith(prefix):
                draw.rectangle([box[0], box[1], box[2] - 1, box[3] - 1], outline=(255, 0, 255))
        frame.save(out)
        return 0

    check = "--check" in arguments
    # --preview <folder>: write every cut there (to look at them) and leave
    # the mod's images alone.
    preview = arguments[arguments.index("--preview") + 1] if "--preview" in arguments else None
    if preview:
        os.makedirs(preview, exist_ok=True)
    with open(os.path.join(mod, "tests", "InGame", "HelpShots", "crops.json"), encoding="utf-8") as source:
        crops = json.load(source)
    failed = 0
    for crop in crops:
        image, capture = crop["image"], crop["capture"]
        try:
            run, captures = variants[crop["variant"]] if "variant" in crop else default
            entry = captures[capture]
            # "box": "<region>" stands for that region's four edges; a
            # side given as well replaces its edge.
            box = tuple(edge(crop.get(side, crop.get("box", "") + "." + side), entry["regions"], capture)
                        for side in ("l", "t", "r", "b"))
            width, height = entry["screen"]
            if not (0 <= box[0] < box[2] <= width and 0 <= box[1] < box[3] <= height):
                raise ValueError("box %s is outside the %dx%d frame" % (box, width, height))
            frame = Image.open(os.path.join(run, capture + ".png")).convert("RGB")
            if "trimRight" in crop:
                # Pull the right edge in to the last column that differs from
                # the background (the box's top-left pixel), plus a margin.
                pixels, background = frame.load(), frame.getpixel((box[0], box[1]))
                right = box[2]
                while right > box[0] + 1 and all(
                        pixels[right - 1, y] == background for y in range(box[1], box[3])):
                    right -= 1
                box = (box[0], box[1], min(box[2], right + crop["trimRight"]), box[3])
            if "trimBottom" in crop:
                # The same for the bottom edge and the last differing row.
                pixels, background = frame.load(), frame.getpixel((box[0], box[1]))
                bottom = box[3]
                while bottom > box[1] + 1 and all(
                        pixels[x, bottom - 1] == background for x in range(box[0], box[2])):
                    bottom -= 1
                box = (box[0], box[1], box[2], min(box[3], bottom + crop["trimBottom"]))
            cut = frame.crop(box)
            if "frame" in crop:
                # A highlight frame around one element inside the image.
                inner = [edge(crop["frame"][side], entry["regions"], capture) - box[i % 2]
                         for i, side in enumerate(("l", "t", "r", "b"))]
                ImageDraw.Draw(cut).rectangle(
                    [inner[0], inner[1], inner[2] - 1, inner[3] - 1], outline=(255, 221, 51), width=2)
            target = os.path.join(images, image + ".png")
            status = "new"
            if os.path.exists(target):
                old = Image.open(target).convert("RGB")
                differing = (sum(ImageChops.difference(old, cut).convert("L").histogram()[1:])
                             if old.size == cut.size else -1)
                # "jitter": n lets up to n pixels differ: rotated text is not
                # sampled identically in every run.
                same = 0 <= differing <= crop.get("jitter", 0)
                status = ("unchanged" + (" (%d px jitter)" % differing if differing else "") if same
                          else "changed (was %dx%d)" % old.size)
            if preview:
                cut.save(os.path.join(preview, image + ".png"))
            elif not same_as_old(status) and not check:
                cut.save(target)
            print("%-24s %-14s %4dx%-4d %s" % (image, capture, cut.width, cut.height, status))
        except Exception as problem:  # report every image, then fail
            failed += 1
            print("%-24s %-14s FAILED: %s" % (image, capture, problem))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
