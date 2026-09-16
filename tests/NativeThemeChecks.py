"""Render native themes offscreen and compare Vice City with an optional old source build."""
import ctypes
import json
import sys
from pathlib import Path
from PIL import Image, ImageChops, ImageStat
root, output = map(Path, sys.argv[1:3])
themes = json.loads((root / "src/Battlestation/Themes.json").read_text(encoding="utf-8"))
colors = (ctypes.c_uint * 18)(*[int(theme[key][1:],16) for theme in themes for key in ("Base","Light","Secondary","Glass","Rim","Edge")])
panels = (ctypes.c_float * 64)()
if len(sys.argv)>3 and sys.argv[3]:
    blocks=json.loads(Path(sys.argv[3]).read_text(encoding="utf-8-sig"))
    slots={"clock":0,"weather":1,"apps":2,"music":3,"projects":4,"terminal":5,"hardware":6,"usage":7,"video":9,"audio":10,"reminders":11,"bluetooth":12}
    for b in blocks:
        if b["Visible"] and b["Id"] in slots:
            i=slots[b["Id"]]*4
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

for index, theme in enumerate(themes):
    frame=render(output/"theme-preview.dll",output/theme["Id"],index,15)
    frame.save(output/(theme["Id"]+".png"))
    if index:
        later=render(output/"theme-preview.dll",output/(theme["Id"]+"-later"),index,22)
        for box in [(0,0,2560,1440),(2560,0,5120,1440)]:
            assert ImageChops.difference(frame.crop(box),later.crop(box)).getbbox(), "Animation missing on one screen"
    print("PASS native render: "+theme["Name"])
if (output/"baseline-preview.dll").exists():
    old=render(output/"baseline-preview.dll",output/"baseline",0,15)
    current=Image.open(output/"vice-city.png").convert("RGB")
    difference=ImageChops.difference(old,current)
    maximum=max(hi for lo,hi in difference.getextrema())
    mean=sum(ImageStat.Stat(difference).mean)/3
    print(f"Vice City old/new: max channel difference={maximum}, mean={mean:.8f}")
    assert maximum<=1, "Vice City changed beyond one quantization step"
    (output/"comparison.json").write_text(json.dumps({"maxChannelDifference":maximum,"meanChannelDifference":mean,"size":list(current.size)}),encoding="utf-8")
