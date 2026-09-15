"""Render checked-in 24 x 24 brand paths in the pearl dock style.

Requires resvg-py. No network access at build/runtime.
"""
import argparse
from pathlib import Path
from xml.etree import ElementTree as ET
import resvg_py

ROOT = Path(__file__).resolve().parents[1] / "dock" / "icons"
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("names", nargs="*")
args = parser.parse_args()
names = args.names or [p.stem for p in sorted((ROOT / "source").glob("*.svg"))]
for name in names:
    if not name.isalnum():
        raise ValueError("Icon names must be alphanumeric")
    source = ET.parse(ROOT / "source" / f"{name}.svg").getroot()
    paths = "".join(f'<path d="{p.attrib["d"]}"/>' for p in source.findall("{http://www.w3.org/2000/svg}path"))
    if not paths or source.attrib.get("viewBox") != "0 0 24 24":
        raise ValueError(f"Expected 24 x 24 paths: {name}")
    svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="192" height="192" viewBox="0 0 128 128">
  <title>{name} — nacré</title>
  <defs>
    <linearGradient id="pearl" x1="0" y1="0" x2="0.85" y2="1">
      <stop stop-color="#e2dce9"/><stop offset=".38" stop-color="#cbc3d6"/>
      <stop offset=".76" stop-color="#b1acc7"/><stop offset="1" stop-color="#a0bfc9"/>
    </linearGradient>
    <linearGradient id="edge" x1="0" y1="0" x2="1" y2="1">
      <stop stop-color="#efdfeb"/><stop offset="1" stop-color="#bad7dc"/>
    </linearGradient>
    <g id="mark">{paths}</g>
  </defs>
  <g transform="translate(22 22) scale(3.5)">
    <use href="#mark" fill="url(#pearl)"/>
    <use href="#mark" fill="none" stroke="url(#edge)" stroke-width=".1" opacity=".35"/>
  </g>
</svg>
'''
    (ROOT / "neon" / f"{name}.svg").write_text(svg, encoding="utf-8")
    (ROOT / "neon" / f"{name}.png").write_bytes(resvg_py.svg_to_bytes(svg_string=svg, skip_system_fonts=True))
    print(name)
