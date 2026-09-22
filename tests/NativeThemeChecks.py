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
    by_id={"clock":0,"weather":1,"apps":2,"music":3,"projects":4,"terminal":5,"hardware":6,"usage":7,"video":9,"audio":10,"reminders":11,"bluetooth":12,"dualsense":13,"network":14,"montagne":15,"lol":16}
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

native_rotation=ctypes.CDLL(str(output/"theme-preview.dll"))
assert native_rotation.WallpaperRotationChecks()==0, "Wallpaper timing/pause regression"
native_rotation.WallpaperPersistenceChecks.argtypes=[ctypes.c_wchar_p]
assert native_rotation.WallpaperPersistenceChecks(str(output))==0, "Wallpaper progress lost on reload"
print("PASS wallpaper progress survives reload and pause")
native_rotation.WallpaperPreview.argtypes=[ctypes.c_wchar_p,ctypes.c_wchar_p,ctypes.c_double,ctypes.POINTER(ctypes.c_uint),ctypes.POINTER(ctypes.c_float)]
rotation_frames=[]
for seconds in [299,300,301,302,600,602]:
    folder=output/("wallpaper-"+str(seconds))
    assert native_rotation.WallpaperPreview(str(root/"assets/Images"),str(folder),seconds,colors,panels)==0
    rotation_frames.append(Image.open(folder/"Battlestation/native-background-frame.png").convert("RGB"))
assert ImageChops.difference(rotation_frames[0],rotation_frames[1]).getbbox() is None, "First fade jumps"
assert ImageChops.difference(rotation_frames[1],rotation_frames[2]).getbbox(), "Mid-fade absent"
assert ImageChops.difference(rotation_frames[2],rotation_frames[3]).getbbox(), "Second illustration absent"
assert ImageChops.difference(rotation_frames[3],rotation_frames[4]).getbbox() is None, "Return fade jumps"
assert ImageChops.difference(rotation_frames[0],rotation_frames[5]).getbbox() is None, "Cycle does not return to original"
print("PASS wallpaper rotation: 300s, 2s fade, pause, full image cycle")

native_rotation.PhotoEffectsPreview.argtypes=[ctypes.c_wchar_p,ctypes.c_wchar_p,ctypes.c_int,ctypes.c_int,ctypes.POINTER(ctypes.c_uint),ctypes.POINTER(ctypes.c_float)]
for selected in [1,2]:
    versions=[]
    for enabled in [0,1]:
        folder=output/("photo-effects-"+str(selected)+"-"+str(enabled))
        assert native_rotation.PhotoEffectsPreview(str(root/"assets/Images"),str(folder),selected,enabled,colors,panels)==0
        versions.append(Image.open(folder/"Battlestation/native-background-frame.png").convert("RGB"))
    difference=ImageChops.difference(*versions)
    for box in [(0,0,2560,1440),(2560,0,5120,1440)]:
        assert difference.crop(box).getbbox(), "Photo effects missing on a screen"
    print("PASS photo effects change pixels on both screens: "+str(selected))

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
if hasattr(ctypes.CDLL(str(output/"theme-preview.dll")),"ThemePreviewCanvas"):
    def render_canvas(folder, theme, seconds, width, height, seam, left=0, top=0):
        native = ctypes.CDLL(str(output/"theme-preview.dll"))
        native.ThemePreviewCanvas.argtypes=[ctypes.c_wchar_p,ctypes.c_wchar_p,ctypes.c_int,ctypes.c_double,ctypes.POINTER(ctypes.c_uint),ctypes.POINTER(ctypes.c_float),ctypes.c_float,ctypes.c_int,ctypes.c_int,ctypes.c_int,ctypes.c_int,ctypes.c_int]
        native.ThemePreviewCanvas.restype=ctypes.c_long
        result=native.ThemePreviewCanvas(str(root/"assets/Images"),str(folder),theme,seconds,colors,panels,1.0,left,top,width,height,seam)
        assert result==0, hex(result & 0xffffffff)
        return Image.open(folder/"Battlestation/native-background-frame.png").convert("RGB")
    # A swapped secondary, an ultrawide and a laptop panel must each render at
    # their own size, with the effects still alive.
    for width, height, seam, label in [(5120,1440,2560,"paire 2 x 2560 x 1440"),(2560+1920,1440,2560,"secondaire 1920 x 1080"),(3440,1440,3440,"ultra large 3440 x 1440"),(1920,1080,1920,"portable 1920 x 1080")]:
        first=render_canvas(output/("canvas-"+str(width)+"-a"),0,15,width,height,seam)
        later=render_canvas(output/("canvas-"+str(width)+"-b"),0,22,width,height,seam)
        assert first.size==(width,height), "Taille de rendu native inattendue pour "+label+": "+str(first.size)
        assert max(hi for lo,hi in first.getextrema())>20, "Rendu natif noir sur "+label
        assert ImageChops.difference(first,later).getbbox(), "Animation absente sur "+label
        print("PASS native canvas: "+label)
if (output/"baseline-preview.dll").exists():
    old=render(output/"baseline-preview.dll",output/"baseline",0,15)
    current=Image.open(output/"vice-city.png").convert("RGB")
    difference=ImageChops.difference(old,current)
    maximum=max(hi for lo,hi in difference.getextrema())
    mean=sum(ImageStat.Stat(difference).mean)/3
    print(f"Vice City old/new: max channel difference={maximum}, mean={mean:.8f}")
    assert maximum<=1, "Vice City changed beyond one quantization step"
    (output/"comparison.json").write_text(json.dumps({"maxChannelDifference":maximum,"meanChannelDifference":mean,"size":list(current.size)}),encoding="utf-8")
