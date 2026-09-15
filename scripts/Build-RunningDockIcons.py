"""Derive the Vice City running material from the exact current pearl SVGs.

No network or runtime generation. Requires resvg-py, like Build-DockIcons.py.
The original coloured revision was not in Git; this palette was recreated after
user approval. Geometry, gradients' direction, outlines and bounds are preserved.
"""
from copy import deepcopy
import argparse
from pathlib import Path
from xml.etree import ElementTree as ET
import resvg_py

ROOT = Path(__file__).resolve().parents[1] / "dock" / "icons"
NS = "{http://www.w3.org/2000/svg}"
ET.register_namespace("", NS[1:-1])
PALETTE = ("#ff5dbb", "#d96eff", "#987bff", "#63ddec")
(ROOT / "running").mkdir(exist_ok=True)
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("names", nargs="*")
args = parser.parse_args()
if any(not name.isalnum() for name in args.names):
    raise ValueError("Icon names must be alphanumeric")
paths = [ROOT / "neon" / f"{name}.svg" for name in args.names] if args.names else sorted((ROOT / "neon").glob("*.svg"))
for path in paths:
    svg = ET.parse(path).getroot()
    svg.find(NS + "title").text = path.stem + " — Vice City, application ouverte"
    defs = svg.find(NS + "defs")
    gradients = defs.findall(NS + "linearGradient")
    for stop, color in zip(gradients[0].findall(NS + "stop"), PALETTE, strict=True):
        stop.set("stop-color", color)
    for stop, color in zip(gradients[1].findall(NS + "stop"), ("#ffd1ef", "#a9f8ff"), strict=True):
        stop.set("stop-color", color)
    group = svg.find(NS + "g")
    # Replace the barely visible pearl halo with one consistent, contained glow.
    for child in list(group):
        if child.get("filter"):
            group.remove(child)
    halo = ET.SubElement(defs, NS + "filter", {"id": "running-halo", "x": "-40%", "y": "-40%", "width": "180%", "height": "180%"})
    ET.SubElement(halo, NS + "feGaussianBlur", {"stdDeviation": ".55"})
    glow = deepcopy(group[0])
    glow.set("filter", "url(#running-halo)")
    glow.set("opacity", ".28")
    group.insert(0, glow)
    text = ET.tostring(svg, encoding="unicode")
    (ROOT / "running" / path.name).write_text(text, encoding="utf-8")
    (ROOT / "running" / (path.stem + ".png")).write_bytes(resvg_py.svg_to_bytes(svg_string=text, skip_system_fonts=True))
    print(path.stem)
