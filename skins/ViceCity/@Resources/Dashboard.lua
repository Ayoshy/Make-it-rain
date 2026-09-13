local drawer=0
local scale=1
local offset=0
local manual=false
local fan,thermal=50,83
local pending=false
local dragging=nil
local groups={'Cores','Cooling','Quotas','Models'}
local revealMeters={}
local visualCh,visualMh=218,209
local fromCh,fromMh,toCh,toMh=218,209,218,209
local revealStep=14
local revealing=0
local closing=false
local quotaOffset=0
local quotaRows={}
local lastQuota=nil
local function val(name) return tonumber(SKIN:GetMeasure('M'..name):GetStringValue()) end
local function cmd(command) SKIN:Bang('!CommandMeasure','Mcpu',command) end
local function variable(name,value) SKIN:Bang('!SetVariable',name,tostring(value)) end
local function layout()
  local ch,mh=visualCh,visualMh
  scale=1
  variable('S',1);variable('TotalH',1149);variable('ConradH',ch);variable('CodexY',940);variable('CodexTop',1149-mh);variable('CodexH',mh)
  local mode=closing and revealing or drawer
  local showConrad=mode==0 or mode<=2
  local showCodex=mode==0 or mode>=3
  SKIN:Bang(showConrad and '!ShowMeterGroup' or '!HideMeterGroup','MainConrad')
  SKIN:Bang(showCodex and '!ShowMeterGroup' or '!HideMeterGroup','MainCodex')
  SKIN:Bang('!CommandMeasure','Host',string.format('Glass:%.3f:%.3f:%.3f:%.3f:%.3f:%.3f',3962,145+706,145+1149-mh,779,showConrad and ch or 0,showCodex and mh or 0),'ViceCity\\Background')
  SKIN:Bang('!UpdateMeter','*');SKIN:Bang('!Redraw')
end
function Initialize() revealMeters=dofile(SKIN:GetVariable('ResourcePath')..'RevealMeters.lua');SKIN:Bang('!Move',3962,145);layout() end
function LayoutNow() SKIN:Bang('!Move',3962,145);layout() end
local function setText(name,value) SKIN:Bang('!SetOption',name,'Text',value) end
local function modelRows()
  for i=0,3 do
    local combined=SKIN:GetMeasure('Mmodel'..i..'name'):GetStringValue()
    local name,effort=combined:match('^(.-)%s%s+(.+)$')
    name=name or combined;effort=effort or ''
    if name=='unknown' then name='Mod'..string.char(232)..'le inconnu' end
    if effort=='unspecified' then effort='' end
    setText('Model'..i..'name',name)
    setText('Model'..i..'effort',string.upper(effort))
  end
end
local function quotas(force)
  local raw=SKIN:GetMeasure('MquotaDetails'):GetStringValue()
  if raw~=lastQuota then
    lastQuota=raw;quotaRows={}
    local name=nil
    for line in (raw..'\n'):gmatch('(.-)\n') do
      if line=='' then name=nil
      elseif not name then name=line
      else
        local windows=line:gsub('\194\183','|'):gsub(string.char(183),'|')
        for window in windows:gmatch('[^|]+') do
          local remaining,duration,reset=window:match('^%s*(.-)%s+/%s+(.-)%s+/%s+(.-)%s*$')
          if duration then quotaRows[#quotaRows+1]={name=name,remaining=tonumber(remaining:match('(%d+)%%')),duration=duration,reset=reset} end
        end
      end
    end
    quotaOffset=math.min(quotaOffset,math.max(0,#quotaRows-3));force=true
  end
  if not force then return end
  for i=0,2 do
    local row=quotaRows[i+quotaOffset+1]
    local visible=row and (drawer==3 or closing and revealing==3)
    for _,suffix in ipairs({'Box','Name','Reset','Duration','Remaining','Track','Bar'}) do SKIN:Bang(visible and '!ShowMeter' or '!HideMeter','Q'..i..suffix) end
    if row then
      setText('Q'..i..'Name',row.name=='codex' and 'Codex' or row.name)
      setText('Q'..i..'Reset','Reset  '..row.reset)
      setText('Q'..i..'Duration',string.upper(row.duration))
      setText('Q'..i..'Remaining',row.remaining and row.remaining..'%' or 'N/D')
      SKIN:Bang('!SetOption','Q'..i..'Bar','Shape',string.format('Rectangle 0,0,%.2f,2 | Fill Color 192,131,247,190 | StrokeWidth 0',(row.remaining or 0)*6.99))
    end
  end
  setText('QuotaDetails',#quotaRows==0 and 'Quotas indisponibles' or '')
end
function Inspect()
  local rows={}
  for _,name in ipairs({'Logo','Timedays','Timehours','Timeminutes','Timeseconds','ConradPanel','CodexPanel'}) do
    local m=SKIN:GetMeter(name)
    rows[#rows+1]=string.format('"%s":{"x":%d,"y":%d,"w":%d,"h":%d}',name,m:GetX(),m:GetY(),m:GetW(),m:GetH())
  end
  local file=io.open(SKIN:GetVariable('ResourcePath')..'layout-state.json','w')
  if file then file:write('{"drawer":'..drawer..',"meters":{'..table.concat(rows,',')..'}}');file:close() end
end
local function revealStyle(amount)
  variable('RevealY',(revealing<=2 and -14 or 14)*(1-amount))
  local group=groups[revealing]
  if not group then return end
  local alpha=math.floor(255*amount*amount)
  local blur=12*(1-amount)*scale
  for _,item in ipairs(revealMeters[group] or {}) do
    SKIN:Bang('!SetOption',item[1],'FontColor',item[2]..','..alpha)
    SKIN:Bang('!SetOption',item[1],'InlineSetting2',string.format('Shadow | 0 | 0 | %.2f | %s,%d',blur,item[2],math.floor(180*(1-amount))))
  end
end
function Toggle(n)
  local old=drawer
  drawer=drawer==n and 0 or n
  closing=drawer==0;revealing=closing and old or drawer
  for i,group in ipairs(groups) do SKIN:Bang(i==revealing and '!ShowMeterGroup' or '!HideMeterGroup',group) end
  if drawer==2 then pending=true;cmd('GpuRead') end
  if drawer==3 then quotas(true) end
  if drawer==4 then modelRows() end
  fromCh,fromMh=visualCh,visualMh
  if drawer==1 or drawer==2 then toCh=443 else toCh=218 end
  if drawer==3 or drawer==4 then toMh=443 else toMh=209 end
  revealStep=0
  revealStyle(closing and 1 or 0)
  layout()
  SKIN:Bang('!CommandMeasure','RevealTimer','Stop 1')
  SKIN:Bang('!CommandMeasure','RevealTimer','Execute 1')
end
function Reveal()
  revealStep=math.min(14,revealStep+1)
  local p=revealStep/14
  local eased=1-(1-p)^3
  visualCh=fromCh+(toCh-fromCh)*eased;visualMh=fromMh+(toMh-fromMh)*eased
  local content=math.max(0,(p-.1)/.9)
  content=content*content*(3-2*content)
  revealStyle(closing and 1-content or content)
  layout()
  if revealStep==14 then
    if closing then for _,group in ipairs(groups) do SKIN:Bang('!HideMeterGroup',group) end;revealing=0;closing=false;layout() end
    variable('RevealY',0)
    SKIN:Bang('!CommandMeasure','RevealTimer','Stop 1')
    SKIN:Bang('!UpdateMeter','*');SKIN:Bang('!Redraw')
  end
end
local function drafts()
  local fmin,fmax,tmin,tmax=val('fanMin'),val('fanMax'),val('thermalMin'),val('thermalMax')
  if not fmin or not fmax or not tmin or not tmax then return end
  variable('FanX',(fan-fmin)/math.max(1,fmax-fmin)*725)
  variable('ThermalX',(thermal-tmin)/math.max(1,tmax-tmin)*725)
  SKIN:Bang('!SetOption','FanDraft','Text',manual and tostring(fan)..' %' or 'AUTO')
  SKIN:Bang('!SetOption','ThermalDraft','Text',thermal..string.char(176))
  SKIN:Bang('!SetOption','FanMode','Text',manual and 'MANUEL' or 'AUTO')
  SKIN:Bang('!UpdateMeterGroup','Cooling');SKIN:Bang('!Redraw')
end
function Manual() if val('controlAvailable')==1 then manual=not manual;drafts() end end
function Drag(name) if val('controlAvailable')==1 and (name~='FanSlider' or manual) then dragging=name;SKIN:Bang('!CommandMeasure','DragTimer','Execute 1') end end
function Apply() if val('controlAvailable')==1 then cmd('Apply:'..(manual and '1' or '0')..':'..fan..':'..thermal) end end
function Scroll(delta)
  if drawer==3 then quotaOffset=math.max(0,math.min(math.max(0,#quotaRows-3),quotaOffset+delta));quotas(true);SKIN:Bang('!UpdateMeterGroup','Quotas');SKIN:Bang('!Redraw');return end
  if drawer~=4 then return end
  offset=math.max(0,math.min(math.max(0,(val('modelsCount') or 0)-4),offset+delta))
  for i=0,3 do for _,col in ipairs({'name','tokens','cost'}) do SKIN:Bang('!SetOption','Mmodel'..i..col,'Metric','model:'..(i+offset)..':'..col) end end
  SKIN:Bang('!UpdateMeasure','*');modelRows();SKIN:Bang('!UpdateMeterGroup','Models');SKIN:Bang('!Redraw')
end
function Update()
  modelRows();quotas(false)
  if pending and val('controlAvailable')==1 then pending=false;manual=val('fanAuto')==0;fan=val('fanTarget');thermal=val('thermalTarget');drafts() end
  if dragging then
    if val('mouseDown')~=1 then dragging=nil;SKIN:Bang('!CommandMeasure','DragTimer','Stop 1');return 0 end
    local origin=4352-779*scale/2
    local amount=math.max(0,math.min(1,((val('mouseX')-origin)/scale-24)/725))
    if dragging=='FanSlider' then fan=math.floor(val('fanMin')+amount*(val('fanMax')-val('fanMin'))+.5)
    else thermal=math.floor(val('thermalMin')+amount*(val('thermalMax')-val('thermalMin'))+.5) end
    drafts()
  end
  return drawer
end
