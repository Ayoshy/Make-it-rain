const {chromium}=require('playwright-core');
const fs=require('node:fs');const os=require('node:os');const path=require('node:path');const assert=require('node:assert/strict');
(async()=>{
  const temp=fs.mkdtempSync(path.join(os.tmpdir(),'Battlestation-extension-'));const extension=path.join(temp,'extension');fs.mkdirSync(extension);
  for(const name of ['video.js','video-worker.js','video-frame.js','video-frame.html','background.js'])fs.copyFileSync(path.join(__dirname,'../browser/video-dock',name),path.join(extension,name));
  const manifest=JSON.parse(fs.readFileSync(path.join(__dirname,'../browser/video-dock/manifest.json'),'utf8'));delete manifest.key;
  fs.writeFileSync(path.join(extension,'manifest.json'),JSON.stringify(manifest));
  // Synthetic in-memory native port. The real tabs/content-script boundary is
  // exercised, but this isolated profile can never connect to the user's dock.
  const port=`const dockListeners=[];globalThis.dockStats={frames:0,changes:0,width:0,height:0,tabs:[],version:null};let dockLast='';
  globalThis.dockEmit=m=>dockListeners.forEach(f=>f(m));
  chrome.runtime.connectNative=()=>({onDisconnect:{addListener(){}},onMessage:{addListener:f=>dockListeners.push(f)},postMessage:m=>{
    if(m.type==='tabs')dockStats.tabs=m.tabs;
    if(m.type==='frame'){dockStats.frames++;if(m.jpeg!==dockLast)dockStats.changes++;dockLast=m.jpeg;dockStats.width=m.width;dockStats.height=m.height;dockStats.version=m.version;dockStats.encodeMs=m.encodeMs;dockStats.inputFrames=m.inputFrames;dockStats.skippedPacing=m.skippedPacing;dockStats.skippedPending=m.skippedPending;dockStats.decoded=m.decodedFrames;
      if(!globalThis.dockDropAck)queueMicrotask(()=>dockEmit({type:'ack',tabId:m.tabId,captureId:m.captureId,sequence:m.sequence}));}
    if(m.type==='error'){dockStats.error=m.code;dockStats.stage=m.stage;dockStats.reason=m.reason;}
  }});
`;
  fs.writeFileSync(path.join(extension,'background.js'),port+fs.readFileSync(path.join(extension,'background.js'),'utf8'));
  let context;
  try{
    context=await chromium.launchPersistentContext(path.join(temp,'profile'),{executablePath:process.env.BATTLESTATION_TEST_BROWSER||'C:/Program Files/BraveSoftware/Brave-Browser/Application/brave.exe',headless:false,args:[`--disable-extensions-except=${extension}`,`--load-extension=${extension}`,'--window-position=-10000,-10000'],ignoreDefaultArgs:['--disable-extensions','--disable-background-timer-throttling','--disable-backgrounding-occluded-windows','--disable-renderer-backgrounding']});
    await context.route('https://www.youtube.com/**',route=>route.fulfill({contentType:'text/html',headers:{'Content-Security-Policy':"require-trusted-types-for 'script'; trusted-types default youtube; worker-src 'self'; frame-src 'self'"},body:'<video class="html5-main-video" muted playsinline loop></video>'}));
    const page=await context.newPage();page.on('console',message=>{if(message.type()==='error')console.log('Fixture console: '+message.text());});await page.goto('https://www.youtube.com/watch?v=battlestation-fixture');
    const worker=context.serviceWorkers()[0]||await context.waitForEvent('serviceworker',{timeout:15000});
    await page.evaluate(async()=>{
      const canvas=document.createElement('canvas');canvas.width=1280;canvas.height=720;const draw=canvas.getContext('2d');let n=0;
      const clock=setInterval(()=>{draw.fillStyle=`hsl(${++n*9%360},100%,50%)`;draw.fillRect(0,0,1280,720);},25);
      const stream=canvas.captureStream(30),parts=[];const recorder=new MediaRecorder(stream,{mimeType:'video/webm;codecs=vp8'});recorder.ondataavailable=e=>parts.push(e.data);const stopped=new Promise(r=>recorder.onstop=r);recorder.start();await new Promise(r=>setTimeout(r,1600));recorder.stop();await stopped;clearInterval(clock);stream.getTracks().forEach(t=>t.stop());
      const video=document.querySelector('video');video.src=URL.createObjectURL(new Blob(parts,{type:'video/webm'}));await video.play();
    });
    async function waitFor(fn,message){const until=Date.now()+12000;while(Date.now()<until){if(await worker.evaluate(fn))return;await new Promise(r=>setTimeout(r,100));}throw new Error(message+JSON.stringify(await worker.evaluate(()=>dockStats)));}
    await waitFor(()=>dockStats.tabs.some(t=>t.ready),'Discovery after loadeddata ');
    await worker.evaluate(()=>{const tab=dockStats.tabs.find(t=>t.ready);dockEmit({type:'start',tabId:tab.id,captureId:'a'.repeat(32),width:1280,height:720});});
    await waitFor(()=>dockStats.frames>=10,'Worker through actual extension ');
    assert.equal((await worker.evaluate(()=>dockStats)).width,1280);
    await worker.evaluate(()=>dockEmit({type:'toggle',tabId:dockStats.tabs.find(t=>t.ready).id}));await page.waitForFunction(()=>document.querySelector('video').paused);
    await worker.evaluate(()=>dockEmit({type:'toggle',tabId:dockStats.tabs.find(t=>t.ready).id}));await page.waitForFunction(()=>!document.querySelector('video').paused);

    const cdp=await context.newCDPSession(page);const {targetInfo}=await cdp.send('Target.getTargetInfo');const {windowId}=await cdp.send('Browser.getWindowForTarget',{targetId:targetInfo.targetId});await cdp.send('Browser.setWindowBounds',{windowId,bounds:{windowState:'minimized'}});
    await new Promise(r=>setTimeout(r,500));const state=await cdp.send('Browser.getWindowBounds',{windowId});
    const before=await worker.evaluate(()=>({...dockStats}));await new Promise(r=>setTimeout(r,5000));const after=await worker.evaluate(()=>({...dockStats}));
    const fps=(after.changes-before.changes)/5;console.log(JSON.stringify({windowState:state.bounds.windowState,visibility:await page.evaluate(()=>document.visibilityState),fps,width:after.width,height:after.height,version:after.version,before,after}));
    assert.equal(state.bounds.windowState,'minimized');assert(fps>=20,'Minimized extension must deliver 20 changing frames/s from 30 fps HD source');
    await worker.evaluate(()=>{tabs.clear();dockStats.tabs=[];dockEmit({type:'list'});});await waitFor(()=>dockStats.tabs.some(t=>t.ready),'Rediscovery after lost registry ');
    await worker.evaluate(()=>{const tab=dockStats.tabs.find(t=>t.ready);dockEmit({type:'configure',tabId:tab.id,width:480,height:270});});await waitFor(()=>dockStats.width===480,'Dynamic output resolution ');
    await worker.evaluate(()=>{const tab=dockStats.tabs.find(t=>t.ready);dockEmit({type:'stop',tabId:tab.id});});await new Promise(r=>setTimeout(r,200));const stopped=await worker.evaluate(()=>dockStats.frames);await new Promise(r=>setTimeout(r,500));assert.equal(await worker.evaluate(()=>dockStats.frames),stopped);assert.equal(await page.evaluate(()=>document.querySelector('video').paused),false);
    console.log('PASS: actual extension content world + worker, readiness, HD, minimized window, registry recovery, dynamic size and stop; synthetic video and mock native port only.');
  }finally{await context?.close();fs.rmSync(temp,{recursive:true,force:true});}
})().catch(e=>{console.error(e);process.exitCode=1;});
