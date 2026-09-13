local elapsed=0
local easedX,easedY=0,0
local dots={}
local paused=nil
local function fract(n) return n-math.floor(n) end
function Initialize()
  for i=0,179 do dots[i]={x=fract(i*.61803398875+.09),y=fract(i*.754877666+.31),depth=fract(i*.414213562+.12),phase=i*2.39996,speed=.007+(i%7)*.0014} end
end
local function pos(name,x,y)
  local m=SKIN:GetMeter(name);m:SetX(x);m:SetY(y)
end
function Update()
  local isPaused=SKIN:GetMeasure('fullscreen'):GetValue()==1
  if isPaused~=paused then paused=isPaused;SKIN:Bang('!CommandMeasure','Animator',paused and 'Stop 1' or 'Execute 1') end
  if paused then return elapsed end
  elapsed=elapsed+.033
  local t=elapsed*.65
  local mx=SKIN:GetMeasure('mouseX'):GetValue()/5120*2-1
  local my=SKIN:GetMeasure('mouseY'):GetValue()/1440*2-1
  easedX=easedX+(mx-easedX)*.095;easedY=easedY+(my-easedY)*.095
  pos('Art',-77+math.sin(elapsed*.14)*20.48-easedX*15,-43+math.cos(elapsed*.11)*5.76-easedY*10)
  pos('Halo0',2500+math.sin(elapsed*.09)*70,-50+math.cos(elapsed*.11)*30)
  pos('Halo1',350+math.cos(elapsed*.08)*60,-80+math.sin(elapsed*.12)*25)
  for i=0,2 do pos('Beam'..i,5120*(.26+i*.28+math.sin(t*.075+i)*.05),-430) end
  for i=0,13 do
    local d=dots[i*7];local r=(25+d.depth*65)*1.333
    local m=SKIN:GetMeter('Bokeh'..i);m:SetW(r*2);m:SetH(r*2)
    pos('Bokeh'..i,(d.x+math.sin(t*.07+d.phase)*.045)*5120-r,(fract(d.y-t*.0025)*1.25-.125)*1440-r)
  end
  for i=0,116 do
    local d=dots[i];local r=(2.8+d.depth*8)*1.333
    local m=SKIN:GetMeter('Dust'..i);m:SetW(r*2);m:SetH(r*2)
    pos('Dust'..i,(fract(d.x+t*.0012*(d.depth+.2)+math.sin(t*.11+d.phase)*.013)*1.08-.04)*5120-r,(fract(d.y-t*d.speed)*1.12-.06)*1440-r)
  end
  for i=0,2 do
    local phase=fract((t+i*9+3)/(22+i*5))
    if phase<=.34 then local p=phase/.34;pos('Streak'..i,(-.15+p*1.4)*5120,(.86-i*.25-p*.18)*1440)
    else pos('Streak'..i,-1000,-1000) end
  end
  return elapsed
end
