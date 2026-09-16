"""Render the two additional themes from the existing SVG geometry and shared palettes.
Vice City sources and rasters remain byte-identical. Requires resvg-py.
"""
import json
from pathlib import Path
from xml.etree import ElementTree as ET
import resvg_py
ROOT = Path(__file__).resolve().parents[1]
ICONS = ROOT / "dock/icons"
NS = "{http://www.w3.org/2000/svg}"
ET.register_namespace("", NS[1:-1])
themes = json.loads((ROOT / "src/Battlestation/Themes.json").read_text(encoding="utf-8"))
for theme in themes[1:]:
    for folder in ("neon", "running", "bluetooth/pearl", "bluetooth/connected"):
        active = folder in ("running", "bluetooth/connected")
        output = ICONS / "themes" / theme["Id"] / folder
        output.mkdir(parents=True, exist_ok=True)
        for path in sorted((ICONS / folder).glob("*.svg")):
            svg = ET.parse(path).getroot()
            gradients = svg.find(NS + "defs").findall(NS + "linearGradient")
            for stop, color in zip(gradients[0].findall(NS + "stop"), theme["Active" if active else "Pearl"], strict=True):
                stop.set("stop-color", color)
            for stop, color in zip(gradients[1].findall(NS + "stop"), (theme["Edge"], theme["Rim"]), strict=True):
                stop.set("stop-color", color)
            for element in svg.iter():
                for attribute in ("fill", "stroke"):
                    color = element.get(attribute, "")
                    if color.startswith("#") and color.upper() in theme["Colors"]:
                        element.set(attribute, theme["Colors"][color.upper()])
            text = ET.tostring(svg, encoding="unicode")
            (output / path.name).write_text(text, encoding="utf-8")
            (output / (path.stem + ".png")).write_bytes(resvg_py.svg_to_bytes(svg_string=text, skip_system_fonts=True))
    print(theme["Name"])
