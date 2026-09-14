(() => {
  const version='0.3.2';
  if(globalThis.__battlestationVideoInstalled===version){globalThis.__battlestationVideoAnnounce?.();return;}
  globalThis.__battlestationVideoDispose?.();
  globalThis.__battlestationVideoInstalled=version;
  let desired=false,epoch='',current,stream,worker,frame,reader,revision=0,width=1280,height=720;
  const events=new AbortController();
  const send=message=>{try{chrome.runtime.sendMessage({...message,version}).catch(()=>{});}catch{}};
  const video=()=>document.querySelector('video.html5-main-video')||document.querySelector('video');
  const announce=()=>{const element=video();send({type:'state',ready:!!element&&element.readyState>=2,playing:!!element&&!element.paused&&!element.ended,version});};
  function stop(invalidate=true){
    if(invalidate)revision++;desired=false;reader?.cancel().catch(()=>{});reader=undefined;
    worker?.postMessage({type:'stop'});worker?.close();worker=undefined;frame?.remove();frame=undefined;
    stream?.getTracks().forEach(track=>track.stop());stream=undefined;current=undefined;
  }
  async function start(captureId){
    const mine=++revision;stop(false);
    const element=video();
    if(!element||element.readyState<2){send({type:'error',captureId,code:'no-video'});announce();return;}
    if(!element.captureStream||typeof MediaStreamTrackProcessor!=='function'){send({type:'error',captureId,code:'capture-unavailable',stage:'capabilities',reason:!element.captureStream?'CaptureStreamMissing':'ProcessorMissing'});return;}
    desired=true;epoch=captureId;current=element;
    let stage='extension-frame';
    try{
      // Load packaged code in its own extension frame, respecting the page CSP.
      const elementFrame=document.createElement('iframe');elementFrame.hidden=true;elementFrame.setAttribute('aria-hidden','true');elementFrame.src=chrome.runtime.getURL('video-frame.html');frame=elementFrame;
      await new Promise((resolve,reject)=>{const timeout=setTimeout(()=>reject(new DOMException('Capture frame unavailable','TimeoutError')),4000);elementFrame.onload=()=>{clearTimeout(timeout);resolve();};elementFrame.onerror=()=>{clearTimeout(timeout);reject(new DOMException('Capture frame unavailable','NetworkError'));};document.documentElement.append(elementFrame);});
      if(mine!==revision)return;
      const channel=new MessageChannel();worker=channel.port1;const owned=worker;
      let inFlight=0;
      owned.onmessage=({data})=>{if(owned!==worker||!desired||data.captureId!==epoch)return;if(data.type==='consumed'){inFlight=Math.max(0,inFlight-1);return;}send({...data,playing:!element.paused,decodedFrames:element.getVideoPlaybackQuality().totalVideoFrames,visibility:document.visibilityState,version});};
      elementFrame.contentWindow.postMessage({type:'battlestation-video-port',captureId},chrome.runtime.getURL('').replace(/\/$/,''),[channel.port2]);
      stage='capture-stream';stream=element.captureStream();stream.getAudioTracks().forEach(track=>track.stop());
      const track=stream.getVideoTracks()[0];if(!track)throw new Error('no-track');
      stage='processor';reader=new MediaStreamTrackProcessor({track,maxBufferSize:1}).readable.getReader();const ownedReader=reader;
      owned.postMessage({type:'start',captureId});
      if(document.pictureInPictureElement===element)await document.exitPictureInPicture().catch(()=>{});
      let due=-Infinity;
      while(desired&&mine===revision&&reader===ownedReader){
        const {done,value:videoFrame}=await ownedReader.read();if(done)break;
        try{
          const now=performance.now();if(now+4<due||inFlight>=2)continue;
          if(videoFrame.displayWidth>8192||videoFrame.displayHeight>8192||videoFrame.displayWidth*videoFrame.displayHeight>32*1024*1024)continue;
          const scale=Math.min(1,width/videoFrame.displayWidth,height/videoFrame.displayHeight);
          const w=Math.max(1,Math.round(videoFrame.displayWidth*scale)),h=Math.max(1,Math.round(videoFrame.displayHeight*scale));
          stage='image-transfer';const bitmap=await createImageBitmap(videoFrame,{resizeWidth:w,resizeHeight:h,resizeQuality:'high'});
          if(!desired||mine!==revision){bitmap.close();break;}
          try{owned.postMessage({type:'bitmap',captureId,bitmap,sourceTimestamp:videoFrame.timestamp},[bitmap]);inFlight++;due=Math.max(now,(Number.isFinite(due)?due:now)+1000/30);}
          catch(error){bitmap.close();throw error;}
        }finally{videoFrame.close();}
      }
    }catch(error){if(mine===revision){send({type:'error',captureId,code:'capture-unavailable',stage,reason:error.name,version});stop(false);}}
  }
  const receive=message=>{
    if(message?.type==='state')announce();
    if(message?.type==='configure'){width=Math.max(160,Math.min(1920,message.width||1280));height=Math.max(90,Math.min(1080,message.height||720));worker?.postMessage({type:'configure',width,height});}
    if(message?.type==='start'&&typeof message.captureId==='string'&&message.captureId.length===32){width=message.width||width;height=message.height||height;void start(message.captureId);}
    if(message?.type==='stop')stop();
    if(message?.type==='ack'&&message.captureId===epoch)worker?.postMessage(message);
    if(message?.type==='toggle'){const element=current||video();if(element){if(element.paused)element.play().catch(()=>send({type:'error',captureId:epoch,code:'play-blocked'}));else element.pause();}}
  };
  chrome.runtime.onMessage.addListener(receive);
  // canplay/loadeddata matter: loadedmetadata may announce ready=false and no
  // later play event occurs when an already-playing page receives the extension.
  for(const event of ['play','pause','loadedmetadata','loadeddata','canplay','emptied','ended'])document.addEventListener(event,announce,{capture:true,signal:events.signal});
  document.addEventListener('yt-navigate-finish',()=>{announce();if(desired&&video()!==current)void start(epoch);},{signal:events.signal});
  window.addEventListener('pagehide',()=>stop(),{signal:events.signal});
  globalThis.__battlestationVideoAnnounce=announce;
  globalThis.__battlestationVideoDispose=()=>{stop();events.abort();chrome.runtime.onMessage.removeListener?.(receive);};
  announce();
})();
