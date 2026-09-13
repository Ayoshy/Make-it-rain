(function () {
  'use strict';
  var defaults = {targetdate:'2026-11-19T00:00:00+01:00',theme:'sunset',screenlayout:'auto',positionx:80,positiony:50,scale:100,darkness:14,showseconds:true,showlogo:true,showdate:true,motion:true,parallax:true,particles:true,bokeh:true,lightbeams:true,lightstreaks:true,fxintensity:70,particledensity:65,fxspeed:65};
  var settings = Object.assign({}, defaults);
  var target = ViceCountdown.parseTarget(defaults.targetdate);
  var preview = new URLSearchParams(location.search).has('settings');
  var hostPaused = false, stopped = false, frameId = null, timerId = null, fps = 30, lastFrame = 0, elapsed = 0, fxelapsed = 0, quietRect = null;
  var mouse = {x:0,y:0}, eased = {x:0,y:0}, width = innerWidth, height = innerHeight;
  var composition = document.getElementById('composition'), artwork = document.getElementById('artwork');
  var canvas = document.getElementById('particles'), overlays = new ViceOverlays(canvas);
  var pink = document.getElementById('pink-light'), gold = document.getElementById('gold-light'), glow = document.getElementById('brand-glow');
  var form = document.getElementById('settings-form'), panel = document.getElementById('settings-panel');
  var months = ['JANVIER','FÉVRIER','MARS','AVRIL','MAI','JUIN','JUILLET','AOÛT','SEPTEMBRE','OCTOBRE','NOVEMBRE','DÉCEMBRE'];
  var numberNodes = {};
  ['days','hours','minutes','seconds'].forEach(function(k){numberNodes[k]=document.getElementById(k);});
  function clamp(v,a,b){return Math.max(a,Math.min(b,v));}
  function apply(values) {
    Object.keys(defaults).forEach(function(k){
      if (!(k in values)) return;
      var v = values[k];
      if(k==='targetdate'){
        var parsed=ViceCountdown.parseTarget(v);
        document.getElementById('date-error').hidden=Number.isFinite(parsed);
        if(Number.isFinite(parsed)){settings[k]=v.trim();target=parsed;}
      }else if(k==='theme'){
        if(v==='sunset'||v==='night')settings[k]=v;
      }else if(k==='screenlayout'){
        if(v==='auto'||v==='single'||v==='dual')settings[k]=v;
      }else if(typeof defaults[k]==='boolean'){
        if(typeof v==='boolean')settings[k]=v;
      }else{
        var limits={positionx:[15,85],positiony:[25,75],scale:[65,125],darkness:[0,70],fxintensity:[0,100],particledensity:[0,100],fxspeed:[10,150]};
        if(Number.isFinite(Number(v)))settings[k]=clamp(Number(v),limits[k][0],limits[k][1]);
      }
    });
    document.body.classList.toggle('night',settings.theme==='night');
    document.body.classList.toggle('no-seconds',!settings.showseconds);
    document.body.classList.toggle('no-logo',!settings.showlogo);
    document.body.classList.toggle('no-date',!settings.showdate);
    document.documentElement.style.setProperty('--darkness',settings.darkness/100);
    canvas.style.display=ViceOverlays.enabled(settings)?'block':'none';
    canvas.style.opacity=settings.fxintensity/100;
    pink.style.display=gold.style.display=glow.style.display=settings.motion?'block':'none';
    var parts=settings.targetdate.slice(0,10).split('-');
    document.getElementById('release-date').textContent=Number(parts[2])+' '+months[Number(parts[1])-1]+' '+parts[0];
    document.getElementById('release-date').dateTime=settings.targetdate;
    layout();tick();drawFrame(0);syncForm();ensureFrame();
  }
  function layout(){
    width=innerWidth;height=innerHeight;
    var dual=settings.screenlayout==='dual'||(settings.screenlayout==='auto'&&width/height>2.7);
    document.body.classList.toggle('dual',dual);
    var cw=Math.min(width*.275,height*.52)*settings.scale/100;
    if(width<height)cw=Math.min(width*.8,height*.4)*settings.scale/100;
    cw=Math.min(cw,width*.88);
    composition.style.setProperty('--composition-width',cw+'px');
    var x=width*settings.positionx/100;
    var y=height*settings.positiony/100;
    if(width<height){x=width/2;y=height*.70+(settings.positiony-50)*height/150;}
    var minX=dual?width*.5+cw/2+width*.025:cw/2+width*.035;
    composition.style.left=clamp(x,minX,width-cw/2-width*.035)+'px';
    var ch=composition.getBoundingClientRect().height;
    // Keep the whole timer + Conrad stack inside the timer's monitor.
    for(var attempt=0;attempt<3&&ch>height*.92;attempt++){
      cw*=height*.90/ch;
      composition.style.setProperty('--composition-width',cw+'px');
      ch=composition.getBoundingClientRect().height;
    }
    composition.style.top=clamp(y,ch/2+height*.04,height-ch/2-height*.04)+'px';
    quietRect=composition.getBoundingClientRect();
    overlays.resize(width,height);
  }
  function tick(){
    var t=ViceCountdown.remaining(target,Date.now());
    ['days','hours','minutes','seconds'].forEach(function(k){
      var text=String(t[k]).padStart(2,'0');
      if(numberNodes[k].textContent!==text)numberNodes[k].textContent=text;
    });
    document.getElementById('countdown').hidden=t.finished;
    document.getElementById('arrival').hidden=!t.finished;
  }
  function scheduleTick(){
    clearTimeout(timerId);timerId=null;
    if(stopped)return;
    tick();
    timerId=setTimeout(scheduleTick,1000-(Date.now()%1000)+15);
  }
  function animated(){return settings.motion||settings.parallax||ViceOverlays.enabled(settings);}
  function ensureFrame(){if(!stopped&&animated()&&frameId===null)frameId=requestAnimationFrame(frame);}
  function frame(now){
    frameId=null;if(stopped)return;
    var dt=now-lastFrame;
    if(dt>=1000/fps){lastFrame=now;dt=Math.min(dt,100);elapsed+=dt/1000;fxelapsed+=dt/1000*settings.fxspeed/100;drawFrame(dt/1000);}
    ensureFrame();
  }
  function drawFrame(dt){
    var a=1-Math.exp(-dt*3);
    eased.x+=(mouse.x-eased.x)*a;eased.y+=(mouse.y-eased.y)*a;
    var driftX=settings.motion?Math.sin(elapsed*.14)*width*.004:0;
    var driftY=settings.motion?Math.cos(elapsed*.11)*height*.004:0;
    var px=settings.parallax?eased.x*width*.006:0;
    var py=settings.parallax?eased.y*height*.006:0;
    artwork.style.transform='translate3d('+(driftX+px).toFixed(2)+'px,'+(driftY+py).toFixed(2)+'px,0)';
    if(settings.motion){
      pink.style.transform='translate3d('+(Math.sin(elapsed*.21)*30).toFixed(2)+'px,'+(Math.cos(elapsed*.16)*20).toFixed(2)+'px,0)';
      pink.style.opacity=(.68+.22*Math.sin(elapsed*.33)).toFixed(3);
      gold.style.opacity=(.58+.25*Math.sin(elapsed*.22+1)).toFixed(3);
      glow.style.opacity=(.6+.3*Math.sin(elapsed*.48)).toFixed(3);
    }
    overlays.render(fxelapsed,settings,quietRect);
  }
  function pause(){
    stopped=hostPaused||document.hidden;
    if(stopped){cancelAnimationFrame(frameId);frameId=null;clearTimeout(timerId);timerId=null;}
    else{lastFrame=performance.now();scheduleTick();ensureFrame();}
  }
  // Assigned globally at script evaluation time, as required by Wallpaper Engine.
  window.wallpaperPropertyListener={
    applyUserProperties:function(props){
      var values={};Object.keys(props).forEach(function(k){if(props[k]&&Object.prototype.hasOwnProperty.call(props[k],'value'))values[k]=props[k].value;});
      apply(values);document.getElementById('settings-button').hidden=true;panel.hidden=true;
    },
    applyGeneralProperties:function(props){if(props.fps&&Number.isFinite(Number(props.fps)))fps=clamp(Number(props.fps),1,30);},
    setPaused:function(value){hostPaused=!!value;pause();window.dispatchEvent(new CustomEvent('conradpause',{detail:hostPaused}));}
  };
  function syncForm(){
    Object.keys(defaults).forEach(function(k){var e=form.elements.namedItem(k);if(!e)return;if(e.type==='checkbox')e.checked=settings[k];else if(document.activeElement!==e)e.value=settings[k];});
  }
  form.addEventListener('submit',function(e){e.preventDefault();});
  form.addEventListener('input',function(e){var el=e.target;if(!el.name)return;var v={};v[el.name]=el.type==='checkbox'?el.checked:el.value;apply(v);});
  document.getElementById('settings-button').addEventListener('click',function(){panel.hidden=!panel.hidden;});
  document.getElementById('close-settings').addEventListener('click',function(){panel.hidden=true;});
  document.getElementById('reset-settings').addEventListener('click',function(){apply(defaults);});
  document.getElementById('export-settings').addEventListener('click',function(){
    var text='// Réglages du fond GTA VI.\nwindow.GTA_VI_SETTINGS = '+JSON.stringify(settings,null,2)+';\n';
    var blob=new Blob([text],{type:'text/javascript;charset=utf-8'}),url=URL.createObjectURL(blob),link=document.createElement('a');
    link.href=url;link.download='config.js';document.body.appendChild(link);link.click();link.remove();setTimeout(function(){URL.revokeObjectURL(url);},3000);
  });
  document.addEventListener('keydown',function(e){if(preview&&e.key.toLowerCase()==='c'&&!['INPUT','SELECT','TEXTAREA'].includes(document.activeElement.tagName)){panel.hidden=!panel.hidden;}if(e.key==='Escape')panel.hidden=true;});
  window.addEventListener('mousemove',function(e){mouse.x=e.clientX/width*2-1;mouse.y=e.clientY/height*2-1;});
  document.addEventListener('mouseleave',function(){mouse.x=mouse.y=0;});
  window.addEventListener('resize',layout);
  window.addEventListener('conradlayout',layout);
  document.addEventListener('visibilitychange',pause);
  if(document.fonts)document.fonts.ready.then(layout);
  apply(window.GTA_VI_SETTINGS||{});
  if(preview)document.getElementById('settings-button').hidden=false;
  pause();
})();
