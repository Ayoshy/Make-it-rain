"""Render native themes offscreen and compare Vice City with an optional old source build."""
import ctypes
import json
import sys
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageStat
root, output = map(Path, sys.argv[1:3])
themes = json.loads((root / "src/Battlestation/Themes.json").read_text(encoding="utf-8"))
colors = (ctypes.c_uint * 18)(*[int(theme[key][1:],16) for theme in themes for key in ("Base","Light","Secondary","Glass","Rim","Edge")])
slots = 20
panels = (ctypes.c_float * (slots * 4))()
if len(sys.argv)>3 and sys.argv[3]:
    blocks=json.loads(Path(sys.argv[3]).read_text(encoding="utf-8-sig"))
    by_id={"clock":0,"weather":1,"apps":2,"music":3,"projects":4,"terminal":5,"hardware":6,"usage":7,"video":9,"audio":10,"reminders":11,"bluetooth":12,"dualsense":13,"network":14,"aquarium":15,"lol":16,"ocean":17}
    for b in blocks:
        if b["Visible"] and b["Id"] in by_id:
            i=by_id[b["Id"]]*4
            panels[i:i+4]=[b["X"],b["Y"],b["Width"],b["Height"]]
else:
    panels[:4]=[2800,200,900,400]

def render(dll, folder, theme, seconds):
    native = ctypes.CDLL(str(dll))
    native.ThemePreview.argtypes=[ctypes.c_wchar_p,ctypes.c_wchar_p,ctypes.c_int,ctypes.c_double,ctypes.POINTER(ctypes.c_uint),ctypes.POINTER(ctypes.c_float)]
    native.ThemePreview.restype=ctypes.c_long
    result=native.ThemePreview(str(root/"assets/Images"),str(folder),theme,seconds,colors,panels)
    assert result==0, hex(result & 0xffffffff)
    return Image.open(folder/"Battlestation/native-background-frame.png").convert("RGB")

def render_fade(folder, theme, seconds, panel_data, fade):
    native = ctypes.CDLL(str(output/"theme-preview.dll"))
    native.ThemePreviewFade.argtypes=[ctypes.c_wchar_p,ctypes.c_wchar_p,ctypes.c_int,ctypes.c_double,ctypes.POINTER(ctypes.c_uint),ctypes.POINTER(ctypes.c_float),ctypes.c_float]
    native.ThemePreviewFade.restype=ctypes.c_long
    result=native.ThemePreviewFade(str(root/"assets/Images"),str(folder),theme,seconds,colors,panel_data,fade)
    assert result==0, hex(result & 0xffffffff)
    return Image.open(folder/"Battlestation/native-background-frame.png").convert("RGB")

for index, theme in enumerate(themes):
    frame=render(output/"theme-preview.dll",output/theme["Id"],index,15)
    frame.save(output/(theme["Id"]+".png"))
    if index:
        later=render(output/"theme-preview.dll",output/(theme["Id"]+"-later"),index,22)
        for box in [(0,0,2560,1440),(2560,0,5120,1440)]:
            assert ImageChops.difference(frame.crop(box),later.crop(box)).getbbox(), "Animation missing on one screen"
    print("PASS native render: "+theme["Name"])
if hasattr(ctypes.CDLL(str(output/"theme-preview.dll")),"ThemePreviewFade"):
    def blank(image):
        copy=image.copy();draw=ImageDraw.Draw(copy)
        for slot in range(slots):
            x,y,w,h=panels[slot*4:slot*4+4]
            # Panels also cast a shadow: mask the glass and its halo.
            if w>0 and h>0:draw.rectangle((x-20,y-20,x+w+20,y+h+20),fill=(0,0,0))
        return copy
    empty=(ctypes.c_float * (slots * 4))()
    full=Image.open(output/"vice-city.png").convert("RGB")
    dissolved=render_fade(output/"scene-fade-out",0,15,panels,0.0)
    without=render_fade(output/"scene-fade-none",0,15,empty,1.0)
    assert ImageChops.difference(dissolved,without).getbbox() is None, "Les panneaux restent dessinés à alpha zéro"
    half=render_fade(output/"scene-fade-half",0,15,panels,0.5)
    assert ImageChops.difference(half,full).getbbox(), "Le fondu de scène ne change rien"
    assert ImageChops.difference(half,dissolved).getbbox(), "Le fondu de scène ignore son alpha"
    assert ImageChops.difference(blank(half),blank(full)).getbbox() is None, "Le fondu de scène touche autre chose que les panneaux"
    print("PASS native scene fade: panneaux seuls, alpha respecté")
if (output/"baseline-preview.dll").exists():
    old=render(output/"baseline-preview.dll",output/"baseline",0,15)
    current=Image.open(output/"vice-city.png").convert("RGB")
    difference=ImageChops.difference(old,current)
    maximum=max(hi for lo,hi in difference.getextrema())
    mean=sum(ImageStat.Stat(difference).mean)/3
    print(f"Vice City old/new: max channel difference={maximum}, mean={mean:.8f}")
    assert maximum<=1, "Vice City changed beyond one quantization step"
    (output/"comparison.json").write_text(json.dumps({"maxChannelDifference":maximum,"meanChannelDifference":mean,"size":list(current.size)}),encoding="utf-8")
