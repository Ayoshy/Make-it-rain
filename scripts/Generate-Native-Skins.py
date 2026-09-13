"""Generate native Rainmeter meters and procedural light masks; no web renderer."""
from pathlib import Path
import math, struct, zlib, shutil
ROOT = Path(__file__).resolve().parents[1]
SKINS = ROOT / 'skins/ViceCity'
RES = SKINS / '@Resources'
IMAGES = RES / 'Images'
IMAGES.mkdir(parents=True, exist_ok=True)

def png(name, width, height, pixel):
    def chunk(kind, data):
        return struct.pack('!I', len(data)) + kind + data + struct.pack('!I', zlib.crc32(kind + data) & 0xffffffff)
    rows = bytearray()
    for y in range(height):
        rows.append(0)
        for x in range(width):
            rows.extend(max(0, min(255, int(v))) for v in pixel(x/(width-1), y/max(1,height-1)))
    (IMAGES/name).write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR',struct.pack('!2I5B',width,height,8,6,0,0,0)) + chunk(b'IDAT',zlib.compress(bytes(rows),9)) + chunk(b'IEND',b''))

colors=[(255,180,112),(255,110,191),(117,218,255)]
for i,c in enumerate(colors):
    def glow(x,y,c=c):
        r=math.hypot(x-.5,y-.5)*2
        a=max(0,1-r)**3
        return (*c,a*255)
    png(f'glow{i}.png',128,128,glow)
    png(f'bokeh{i}.png',128,128,lambda x,y,c=c:(*c,70*max(0,1-(math.hypot(x-.5,y-.5)*2)**6)))
png('art-mask.png',256,144,lambda x,y:(255,255,255,255*min(1,max(0,(1-x)/.12))))
png('vignette.png',512,144,lambda x,y:(10,5,22,110*min(1,max(0,(math.hypot((x-.5)*1.1,(y-.43)*1.1)-.25)/.5))))
png('grade.png',512,144,lambda x,y:(39+int(10*x),25,61,70+int(80*x)))
png('beam.png',128,128,lambda x,y:(199,99,240,45*max(0,1-abs(x-.5)*2)*(1-y*.5)))
for name in ['jason-lucia.jpg','logo.png']:
    shutil.copyfile(ROOT/'web/assets'/name,IMAGES/name)

class Ini:
    def __init__(self): self.s=[]
    def section(self,name,**options):
        self.s.append('['+name+']\n'+'\n'.join(k+'='+str(v) for k,v in options.items())+'\n')
    def write(self,path):
        path.parent.mkdir(parents=True,exist_ok=True)
        path.write_text('\n'.join(self.s),encoding='utf-16')

bg=Ini()
bg.section('Rainmeter',Update=1000,AccurateText=1,SkinWidth=5120,SkinHeight=1440,DynamicWindowSize=0,OnRefreshAction='[!KeepOnScreen 0][!Move 0 0][!Draggable 0][!ClickThrough 1][!CommandMeasure Host DockBackground]')
bg.section('Metadata',Name='ViceCity · Fond natif',Author='Ayo',Version='0.2.0')
bg.section('Host',Measure='Plugin',Plugin='ViceCityNative',Metric='summary',UpdateDivider=2)
for key in ['mouseX','mouseY','fullscreen']:
    bg.section(key,Measure='Plugin',Plugin='ViceCityNative',Metric=key)
bg.section('Motion',Measure='Script',ScriptFile='#@#Background.lua')
bg.section('Animator',Measure='Plugin',Plugin='ActionTimer',ActionList1='Repeat Animate,33,1000000',Animate='[!UpdateMeasure mouseX][!UpdateMeasure mouseY][!UpdateMeasure Motion][!Redraw]',UpdateDivider=-1)
bg.section('Back',Meter='Image',ImageName='#@#Images/jason-lucia.jpg',ImageCrop='2880,830,960,270',X=0,Y=0,W=5120,H=1440,ImageTint='80,62,98,255')
bg.section('Art',Meter='Image',ImageName='#@#Images/jason-lucia.jpg',MaskImageName='#@#Images/art-mask.png',X=-77,Y=-43,W=2714,H=1526,ImageTint='178,171,189,255',Group='FX')
bg.section('Grade',Meter='Image',ImageName='#@#Images/grade.png',W=5120,H=1440)
for i,(x,y,w,h) in enumerate([(2500,-50,3000,1650),(350,-80,2600,1500)]):
    bg.section('Halo'+str(i),Meter='Image',ImageName=f'#@#Images/glow{1 if i==0 else 2}.png',X=x,Y=y,W=w,H=h,ImageAlpha=70,Group='FX')
for i in range(3):
    bg.section('Beam'+str(i),Meter='Image',ImageName='#@#Images/beam.png',X=1000+i*1400,Y=-350,W=480,H=2300,ImageAlpha=65,ImageRotate=20,Group='FX')
for i in range(14):
    bg.section('Bokeh'+str(i),Meter='Image',ImageName=f'#@#Images/bokeh{i%3}.png',W=120,H=120,ImageAlpha=60,Group='FX')
for i in range(117):
    bg.section('Dust'+str(i),Meter='Image',ImageName=f'#@#Images/glow{i%3}.png',W=12,H=12,ImageAlpha=160,Group='FX')
for i in range(3):
    bg.section('Streak'+str(i),Meter='Shape',Shape=f'Line 0,0,250,-40 | StrokeWidth 2 | Stroke Color {",".join(map(str,colors[i]))},100',Group='FX')
bg.section('Vignette',Meter='Image',ImageName='#@#Images/vignette.png',W=5120,H=1440)
bg.write(ROOT/'experiments/native-meter-background/Background.ini')
bg=Ini()
bg.section('Rainmeter',Update=1000,SkinWidth=1,SkinHeight=1,OnRefreshAction='[!KeepOnScreen 0][!Move 0 0][!Draggable 0][!ClickThrough 1][!CommandMeasure Host StartBackground]')
bg.section('Metadata',Name='ViceCity · Fond Direct2D natif',Author='Ayo',Version='0.3.0')
bg.section('Host',Measure='Plugin',Plugin='ViceCityGlass',ResourcePath='#@#Images')
bg.section('Anchor',Meter='Image',W=1,H=1,SolidColor='0,0,0,1')
bg.write(SKINS/'Background/Background.ini')

ui=Ini()
ui.section('Rainmeter',Update=1000,AccurateText=1,DynamicWindowSize=1,OnRefreshAction='[!KeepOnScreen 0][!Move 3963 146][!ZPos -2][!Draggable 0][!CommandMeasure Layout "LayoutNow()"]',MouseScrollUpAction='[!CommandMeasure Layout "Scroll(-1)"]',MouseScrollDownAction='[!CommandMeasure Layout "Scroll(1)"]')
ui.section('Metadata',Name='ViceCity · Bureau natif',Author='Ayo',Version='0.2.0',Information='Logo, compte à rebours, Conrad et Codex rendus par Rainmeter.')
ui.section('Variables',TargetDate='2026-11-19T00:00:00+01:00',S=1,TotalH=1149,PanelW=779,ConradY=706,CodexY=940,CodexTop=940,ConradH=218,CodexH=209,DrawerY=706,ModelOffset=0,FanX=300,ThermalX=550,RevealY=0,ResourcePath='#@#')
ui.section('Layout',Measure='Script',ScriptFile='#@#Dashboard.lua')
ui.section('DragTimer',Measure='Plugin',Plugin='ActionTimer',ActionList1='Repeat DragFrame,33,100000',DragFrame='[!UpdateMeasure MmouseX][!UpdateMeasure MmouseDown][!UpdateMeasure Layout]',UpdateDivider=-1)
ui.section('RevealTimer',Measure='Plugin',Plugin='ActionTimer',ActionList1='Repeat RevealFrame,22,14',RevealFrame='[!CommandMeasure Layout "Reveal()"]',UpdateDivider=-1)
fields=['cpu','gpu','cpuLoad','gpuLoad','fan','watts','hottest','sensorStatus','sensorError','heatwave','remaining','quotaLabel','reset','today','total','todayCost','totalCost','credits','codexStatus','codexError','quotaDetails','controlAvailable','controlStatus','fanAuto','fanTarget','fanMin','fanMax','thermalTarget','thermalMin','thermalMax','days','hours','minutes','seconds','date','modelsCount']+['core'+str(i) for i in range(6)]
for key in fields:
    extra={}
    if key=='days':extra['TargetDate']='#TargetDate#'
    if key=='heatwave':extra=dict(IfCondition='Mheatwave = 1',IfTrueAction='[!SetOption HeatwaveButton SolidColor "132,41,113,170"][!SetOption HeatwaveButton Text "♨  CANICULE ACTIVE"][!UpdateMeter HeatwaveButton][!Redraw]',IfFalseAction='[!SetOption HeatwaveButton SolidColor "87,42,111,40"][!SetOption HeatwaveButton Text "♨  CANICULE"][!UpdateMeter HeatwaveButton][!Redraw]')
    ui.section('M'+key,Measure='Plugin',Plugin='ViceCityNative',Metric=key,UpdateDivider=20,**extra)
for key in ['mouseX','mouseDown']:
    ui.section('M'+key,Measure='Plugin',Plugin='ViceCityNative',Metric=key)
for i in range(4):
    for col in ['name','tokens','cost']:
        ui.section(f'Mmodel{i}{col}',Measure='Plugin',Plugin='ViceCityNative',Metric=f'model:{i}:{col}',UpdateDivider=20)

def text(name,value,x,y,size=10.5,color='232,219,244',group='',width=0,align='Left',font='Segoe UI Variable Text',bold=False,tracking=None,shadow=None):
    opts=dict(Meter='String',X=x,Y=y,FontFace=font,FontSize=size,FontColor=color,AntiAlias=1,StringAlign=align,Text=value,DynamicVariables=1,UpdateDivider=20)
    if width:opts.update(W=width,ClipString=2)
    if bold:opts.update(StringStyle='Bold')
    inline=[f'CharacterSpacing | 0 | ({tracking or 0}*#S#)']
    if shadow:inline.append('Shadow | 0 | 0 | (24*#S#) | '+shadow)
    for i,setting in enumerate(inline):opts['InlineSetting'+('' if i==0 else str(i+1))]=setting
    if group:opts.update(Group=group,Hidden=1)
    ui.section(name,**opts)
def box(name,x,y,w,h,group='',fill='35,17,49,230',stroke='190,115,231,75',radius=16):
    opts=dict(Meter='Shape',X=x,Y=y,Shape=f'Rectangle 0,0,({w}*#S#),({h}*#S#),{radius} | Fill Color {fill} | Stroke Color {stroke} | StrokeWidth 1',DynamicVariables=1,UpdateDivider=20)
    if group:opts.update(Group=group,Hidden=1)
    ui.section(name,**opts)
def button(name,label,x,y,w,action,group=''):
    box(name+'Box',x,y,w+18,32,group=group,fill='163,83,194,11',stroke='190,120,235,43',radius=6)
    opts=dict(Meter='String',X=x,Y=y,W=w,H=23,SolidColor='0,0,0,1',FontFace='Segoe UI Variable Text Semibold',FontSize=8.5,FontColor='224,184,236',AntiAlias=1,Padding='9,7,9,2',Text=label,LeftMouseUpAction=action,DynamicVariables=1,UpdateDivider=20,InlineSetting='CharacterSpacing | 0 | (0.45*#S#)',MouseOverAction=f'[!SetOption {name} FontColor "255,235,255"][!UpdateMeter {name}][!Redraw]',MouseLeaveAction=f'[!SetOption {name} FontColor "224,184,236"][!UpdateMeter {name}][!Redraw]')
    if group: opts.update(Group=group,Hidden=1)
    ui.section(name,**opts)

text('Eyebrow','—  VICE CITY IS CALLING  —',389,0,11,'232,203,215',align='Center',bold=True,tracking=4.44)
ui.section('Logo',Meter='Image',ImageName='#@#Images/logo.png',X=86,Y=40,W=607,H=406)
for i,key in enumerate(['days','hours','minutes','seconds']):
    text('Time'+key,'[M'+key+']',90+i*196,470,86,'239,159,218' if i==0 else '228,200,224' if i==3 else '255,240,217',align='Center',font='GTAArtDeco',bold=True,tracking=2.88,width=180)
    text('Label'+key,['JOURS','HEURES','MINUTES','SECONDES'][i],90+i*196,615,10.5,'205,177,199',align='Center',tracking=3.1)
    if i<3:box('TimeSeparator'+str(i),184+i*196,528,1,43,fill='255,222,230,55',stroke='0,0,0,0',radius=0)
text('Date','—  [Mdate]  —',389,659,13.5,'239,203,229',align='Center',bold=True,tracking=3.94)
box('ConradPanel',0,'#ConradY#',779,'#ConradH#',fill='35,17,49,38',stroke='190,115,231,45',radius=24)
text('ConradTitle','●  CONRAD SENSOR',24,'(#ConradY#+18)',8.3,bold=True,tracking=1.45,font='Arial')
text('SensorStatus','[MsensorStatus]',755,'(#ConradY#+18)',8,align='Right')
for i,(label,key,col) in enumerate([('CPU','cpu','255,111,211'),('GPU','gpu','190,129,255')]):
    x=24+i*271
    text('Label'+label,label,x,'(#ConradY#+52)',9.2,bold=True,tracking=1.7)
    text('Value'+label,'[M'+key+']',x,'(#ConradY#+70)',36,col,font='Bahnschrift',tracking=-1,shadow=col+',59')
    text('Load'+label,'[M'+key+'Load]',x+240,'(#ConradY#+52)',8,align='Right')
    box('Track'+label,x,'(#ConradY#+129)',243,3,fill='96,61,111,90',stroke='0,0,0,0',radius=0)
    ui.section('Bar'+label,Meter='Bar',MeasureName='M'+key,X=x,Y='(#ConradY#+129)',W=243,H=3,BarColor=col,BarOrientation='Horizontal',DynamicVariables=1,UpdateDivider=20)
text('FanLabel','VENTILATEUR',594,'(#ConradY#+52)',7,tracking=.94)
text('FanValue','[Mfan]',594,'(#ConradY#+72)',16,font='Bahnschrift')
text('PowerLabel','PUISSANCE',594,'(#ConradY#+107)',7,tracking=.94)
text('PowerValue','[Mwatts]',594,'(#ConradY#+123)',16,font='Bahnschrift')
button('CoresButton','6 CŒURS                         +',24,'(#ConradY#+165)',220,'[!CommandMeasure Layout "Toggle(1)"]')
button('CoolingButton','REFROIDISSEMENT              ⚙',275,'(#ConradY#+165)',220,'[!CommandMeasure Layout "Toggle(2)"]')
button('HeatwaveButton','♨  CANICULE',526,'(#ConradY#+165)',211,'[!CommandMeasure Mcpu Heatwave]')
text('CoresTitle','INTEL CORE i5-9600KF',24,'(#ConradY#+236)',10.5,bold=True,tracking=.5,group='Cores')
text('HottestCore','MAX  [Mhottest]',750,'(#ConradY#+233)',14,'228,171,250',font='Bahnschrift',align='Right',group='Cores')
for i in range(6):
    x=24+(i%3)*249;y=f'(#ConradY#+{273+(i//3)*65})'
    box('CoreBox'+str(i),x,y,232,56,group='Cores',fill='110,64,147,24',stroke='180,127,219,65',radius=10)
    text('CoreLabel'+str(i),'CŒUR '+str(i+1),x+14,f'({y[1:-1]}+20)',9,'204,177,220',bold=True,tracking=.8,group='Cores')
    text('Core'+str(i),'[Mcore'+str(i)+']',x+215,f'({y[1:-1]}+10)',22,'240,213,253',font='Bahnschrift',align='Right',group='Cores')
text('CoolingTitle','RTX 2060 SUPER',24,'(#ConradY#+232)',11,bold=True,tracking=.6,group='Cooling')
text('FanModeLabel','Mode du ventilateur',24,'(#ConradY#+261)',11,group='Cooling')
button('FanMode','AUTO',613,'(#ConradY#+253)',124,'[!CommandMeasure Layout "Manual()"]',group='Cooling')
text('FanSet','VITESSE',24,'(#ConradY#+293)',8.5,bold=True,tracking=.8,group='Cooling')
text('FanDraft','AUTO',750,'(#ConradY#+286)',15,font='Bahnschrift',group='Cooling',align='Right')
text('ThermalSet','CIBLE THERMIQUE',24,'(#ConradY#+345)',8.5,bold=True,tracking=.8,group='Cooling')
text('ThermalDraft','—',750,'(#ConradY#+338)',15,font='Bahnschrift',group='Cooling',align='Right')
for name,yy in [('FanSlider',315),('ThermalSlider',368)]:
    variable='FanX' if name=='FanSlider' else 'ThermalX'
    ui.section(name,Meter='Shape',X=24,Y=f'(#ConradY#+{yy})',Shape='Rectangle 0,0,(725*#S#),(10*#S#),5 | Fill Color 92,53,112,200 | StrokeWidth 0',Shape2=f'Ellipse (#{variable}#*#S#),(5*#S#),(8*#S#) | Fill Color 190,129,255 | StrokeWidth 0',LeftMouseDownAction=f'[!CommandMeasure Layout "Drag(\'{name}\')"]',DynamicVariables=1,Group='Cooling',Hidden=1)
button('Apply','APPLIQUER AU GPU',24,'(#ConradY#+391)',706,'[!CommandMeasure Layout "Apply()"]',group='Cooling')
text('GpuError','[MsensorError]',24,'(#ConradY#+425)',8,'255,149,193',group='Cooling',width=725)
box('CodexPanel',0,'#CodexTop#',779,'#CodexH#',fill='35,17,49,38',stroke='190,115,231,45',radius=24)
text('CodexTitle','●  CODEX METER',24,'(#CodexY#+18)',8.3,bold=True,tracking=1.45,font='Arial')
text('CodexStatus','[McodexStatus]',755,'(#CodexY#+18)',8,align='Right')
text('QuotaLabel','[MquotaLabel]',24,'(#CodexY#+51)',9)
text('Remaining','[Mremaining]',24,'(#CodexY#+70)',32,'190,129,255',font='Bahnschrift',tracking=-1,shadow='183,119,255,48')
text('RemainingLabel','RESTANT',153,'(#CodexY#+100)',7)
box('QuotaTrack',24,'(#CodexY#+127)',390,3,fill='96,61,111,90',stroke='0,0,0,0',radius=0)
ui.section('QuotaBar',Meter='Bar',MeasureName='Mremaining',X=24,Y='(#CodexY#+127)',W=390,H=3,BarColor='190,129,255',BarOrientation='Horizontal',DynamicVariables=1,UpdateDivider=20)
text('Reset','[Mreset]',24,'(#CodexY#+140)',8)
text('TodayLabel','TOKENS DU JOUR',448,'(#CodexY#+51)',8)
text('Today','[Mtoday]',448,'(#CodexY#+69)',17,font='Bahnschrift')
text('CostLabel','ÉQUIV. API ESTIMÉ',448,'(#CodexY#+108)',8)
text('Cost','[MtodayCost]',448,'(#CodexY#+125)',16,'150,231,178',font='Bahnschrift')
for name,label,x,action in [('Quotas','QUOTAS                    +',24,'[!CommandMeasure Layout "Toggle(3)"]'),('Models','MODÈLES                  +',275,'[!CommandMeasure Layout "Toggle(4)"]'),('Refresh','ACTUALISER                ↻',526,'[!CommandMeasure Mremaining Refresh]')]:
    button(name,label,x,'(#CodexY#+165)',211,action)
text('QuotaDetails','',24,'(#DrawerY#+14)',10,group='Quotas')
for i in range(3):
    y=f'(#DrawerY#+{14+i*62})'
    box(f'Q{i}Box',24,y,731,54,group='Quotas',fill='128,77,174,18',stroke='183,125,223,55',radius=10)
    text(f'Q{i}Name','',38,f'(#DrawerY#+{19+i*62})',11,bold=True,group='Quotas')
    text(f'Q{i}Reset','',38,f'(#DrawerY#+{41+i*62})',8.5,'184,166,205',group='Quotas')
    text(f'Q{i}Duration','',588,f'(#DrawerY#+{23+i*62})',9,'216,184,235',align='Right',bold=True,tracking=.5,group='Quotas')
    text(f'Q{i}Remaining','',737,f'(#DrawerY#+{18+i*62})',20,'205,156,255',font='Bahnschrift',align='Right',group='Quotas')
    box(f'Q{i}Track',38,f'(#DrawerY#+{62+i*62})',699,2,group='Quotas',fill='146,98,177,35',stroke='0,0,0,0',radius=0)
    box(f'Q{i}Bar',38,f'(#DrawerY#+{62+i*62})',0,2,group='Quotas',fill='192,131,247,190',stroke='0,0,0,0',radius=0)
text('Credits','[Mcredits]',24,'(#DrawerY#+211)',8.5,'181,161,204',group='Quotas')
text('TotalsLabel','COMPTE · TOKENS CUMULÉS',24,'(#DrawerY#+12)',7.5,'184,164,205',tracking=.7,bold=True,group='Models')
text('Totals','[Mtotal]',24,'(#DrawerY#+27)',17,font='Bahnschrift',group='Models')
text('TotalCostLabel','ÉQUIVALENT API LOCAL',750,'(#DrawerY#+12)',7.5,'184,164,205',tracking=.7,bold=True,group='Models',align='Right')
text('TotalCost','[MtotalCost]',750,'(#DrawerY#+27)',17,'150,231,178',font='Bahnschrift',group='Models',align='Right')
for i,(label,x,align) in enumerate([('MODÈLE',38,'Left'),('EFFORT',422,'Center'),('TOKENS',565,'Right'),('ESTIMÉ',735,'Right')]):
    text('TableHeader'+str(i),label,x,'(#DrawerY#+61)',7.5,'167,146,189',bold=True,tracking=.8,align=align,group='Models')
for i in range(4):
    y=81+i*30
    box(f'ModelRow{i}',24,f'(#DrawerY#+{y})',731,28,group='Models',fill='180,131,216,'+('14' if i%2==0 else '4'),stroke='0,0,0,0',radius=6)
    text(f'Model{i}name','',38,f'(#DrawerY#+{y+4})',10.5,bold=True,group='Models',width=322)
    text(f'Model{i}effort','',422,f'(#DrawerY#+{y+7})',8,'199,159,226',align='Center',bold=True,tracking=.6,group='Models')
    text(f'Model{i}tokens',f'[Mmodel{i}tokens]',565,f'(#DrawerY#+{y+3})',12,'230,219,242',font='Bahnschrift',align='Right',group='Models')
    text(f'Model{i}cost',f'[Mmodel{i}cost]',735,f'(#DrawerY#+{y+3})',12,'150,231,178',font='Bahnschrift',align='Right',group='Models')
text('ModelNote','Historique local · estimation partielle, pas une facture · molette pour parcourir',24,'(#DrawerY#+211)',7.5,'167,146,189',group='Models')
for group in ['Cores','Cooling','Quotas','Models']:
    top='(#ConradY#+218)' if group in ['Cores','Cooling'] else '#CodexTop#'
    height='Max(0,#ConradH#-218)' if group in ['Cores','Cooling'] else 'Max(0,#CodexH#-209)'
    ui.section(group+'Mask',Meter='Shape',Shape=f'Rectangle 0,({top}*#S#),(779*#S#),({height}*#S#) | Fill Color 255,255,255,255 | StrokeWidth 0',DynamicVariables=1)
ui.section('Bounds',Meter='Shape',Shape='Rectangle 0,0,(779*#S#),(#TotalH#*#S#) | Fill Color 255,255,255,255 | StrokeWidth 0',DynamicVariables=1)
# Scale dimensions and text as a group while keeping all coordinates in one readable layout.
import re
main=None
for i,s in enumerate(ui.s):
    if s.startswith('[ConradPanel]'):main='MainConrad'
    if s.startswith('[CoresTitle]'):main=None
    if s.startswith('[CodexPanel]'):main='MainCodex'
    if s.startswith('[QuotaDetails]'):main=None
    if main and 'Meter=' in s:ui.s[i]=s+'Group='+main+'\n'
manifest={g:[] for g in ['Cores','Cooling','Quotas','Models']}
for s in ui.s:
    group=re.search(r'^Group=(.+)$',s,re.M)
    if group and group[1] in manifest and 'Meter=String' in s:
        name=re.search(r'^\[(.+)\]',s)[1];color=re.search(r'^FontColor=(.+)$',s,re.M)[1]
        manifest[group[1]].append((name,color))
(RES/'RevealMeters.lua').write_text('return {\n'+',\n'.join(g+'={'+','.join('{'+repr(n)+','+repr(c)+'}' for n,c in entries)+'}' for g,entries in manifest.items())+'\n}',encoding='utf-8')
ui.s=[re.sub(r'^Y=(.+)$',lambda m:'Y=('+m[1]+'+#RevealY#)',s,flags=re.M) if re.search(r'^Group=(Cores|Cooling|Quotas|Models)$',s,re.M) else s for s in ui.s]
ui.s=[re.sub(r'^(X|Y|W|H|FontSize)=(.+)$',lambda m:m[1]+'=('+m[2]+'*#S#)',s,flags=re.M) if 'Meter=' in s else s for s in ui.s]
ui.s=[s+'Container='+(re.search(r'^Group=(Cores|Cooling|Quotas|Models)$',s,re.M)[1]+'Mask' if re.search(r'^Group=(Cores|Cooling|Quotas|Models)$',s,re.M) else 'Bounds')+'\n' if 'Meter=' in s and not re.match(r'^\[(Bounds|CoresMask|CoolingMask|QuotasMask|ModelsMask)\]',s) else s for s in ui.s]
ui.s=[s.replace('UpdateDivider=20','UpdateDivider=1') for s in ui.s]
ui.write(SKINS/'Dashboard/Dashboard.ini')
print('Native Background and Dashboard generated')
