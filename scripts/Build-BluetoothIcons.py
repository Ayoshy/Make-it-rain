"""Render Bluetooth artwork with the exact existing app pearl/Vice City materials.

Only writes dock/icons/bluetooth/{pearl,connected}; never rewrites app assets.
Requires the same resvg-py used by the dock icon generators. No network/runtime work.
"""
from copy import deepcopy
from pathlib import Path
from xml.etree import ElementTree as ET
import resvg_py

ROOT = Path(__file__).resolve().parents[1] / "dock" / "icons"
NS = "{http://www.w3.org/2000/svg}"
ET.register_namespace("", NS[1:-1])
for state, template_name in (("pearl", "neon"), ("connected", "running")):
    template = ET.parse(ROOT / template_name / "discord.svg").getroot()
    material, edge = template.find(NS + "defs").findall(NS + "linearGradient")[:2]
    output = ROOT / "bluetooth" / state
    output.mkdir(parents=True, exist_ok=True)
    for source in sorted((ROOT / "bluetooth" / "source").glob("*.svg")):
        art = ET.parse(source).getroot()
        if art.get("viewBox") != "0 0 24 24":
            raise ValueError(f"Expected 24 x 24 vector geometry: {source}")
        svg = ET.Element(NS + "svg", {"width": "192", "height": "192", "viewBox": "0 0 128 128"})
        ET.SubElement(svg, NS + "title").text = f"{source.stem} — {state}"
        defs = ET.SubElement(svg, NS + "defs")
        for original, name in ((material, "material"), (edge, "edge")):
            gradient = deepcopy(original)
            gradient.set("id", name)
            defs.append(gradient)
        mesh = ET.SubElement(defs, NS + "pattern", {"id": "mesh", "width": ".6", "height": ".6", "patternUnits": "userSpaceOnUse"})
        ET.SubElement(mesh, NS + "circle", {"cx": ".3", "cy": ".3", "r": ".075", "fill": "#241e2e", "opacity": ".55"})
        group = ET.SubElement(svg, NS + "g", {"transform": "translate(22 22) scale(3.5)"})
        if state == "connected":
            halo = deepcopy(template.find(NS + "defs").find(f"{NS}filter[@id='running-halo']"))
            if halo is None:
                raise ValueError("Existing app running halo unavailable")
            defs.append(halo)
            glow = deepcopy(art.find(f"{NS}g[@id='body']"))
            glow.attrib.pop("id")
            glow.set("filter", "url(#running-halo)")
            glow.set("opacity", ".28")
            group.append(glow)
        for child in art:
            group.append(deepcopy(child))
        text = ET.tostring(svg, encoding="unicode")
        (output / source.name).write_text(text, encoding="utf-8")
        (output / f"{source.stem}.png").write_bytes(resvg_py.svg_to_bytes(svg_string=text, skip_system_fonts=True))
        print(f"{state}/{source.stem}")
