(() => {
  const version='0.4.0';
  const sourceKind=location.hostname==='www.youtube.com'?'youtube':location.hostname==='www.twitch.tv'||location.hostname==='clips.twitch.tv'?'twitch':'';
  if(!sourceKind)return;
  if(globalThis.__battlestationVideoInstalled===version){globalThis.__battlestationVideoAnnounce?.();return;}
  globalThis.__battlestationVideoDispose?.();
  globalThis.__battlestationVideoInstalled=version;
  let desired=false,epoch='',current,stream,track,worker,frame,reader,revision=0,width=1280,height=720;
  let recoveryQueued=false;
  let lastAnnouncement='';
  const events=new AbortController();
  const send=message=>{try{chrome.runtime.sendMessage({...message,kind:sourceKind,version}).catch(()=>{});}catch{}};
  const video=()=>{
    if(sourceKind==='youtube')return document.querySelector('video.html5-main-video')||document.querySelector('video');
    const videos=[...document.querySelectorAll('video')];
    return videos.filter(element=>element.readyState>=1&&element.clientWidth>0&&element.clientHeight>0)
      .sort((a,b)=>b.clientWidth*b.clientHeight-a.clientWidth*a.clientHeight)[0]||videos[0];
  };
  const announce=(force=false)=>{const element=video();const ready=!!element&&element.readyState>=2,playing=!!element&&!element.paused&&!element.ended,key=`${ready}/${playing}`;if(!force&&key===lastAnnouncement)return;lastAnnouncement=key;send({type:'state',ready,playing});};
  function stop(invalidate=true){
    if(invalidate)revision++;desired=false;reader?.cancel().catch(()=>{});reader=undefined;
    worker?.postMessage({type:'stop'});worker?.close();worker=undefined;frame?.remove();frame=undefined;
    track?.removeEventListener('ended',trackEnded);track=undefined;
    stream?.getTracks().forEach(item=>item.stop());stream=undefined;current=undefined;
  }
  function recover(){
    if(!desired||recoveryQueued)return;
    recoveryQueued=true;queueMicrotask(()=>{recoveryQueued=false;if(!desired)return;const element=video();if(!element||element.readyState<2||element.ended)return;if(element!==current||!track||track.readyState!=='live')void start(epoch);});
  }
  function trackEnded(){if(desired)recover();}
  async function start(captureId){
    const mine=++revision;stop(false);
    desired=true;epoch=captureId;current=undefined;
    const element=video();
    if(!element||element.readyState<2){send({type:'error',captureId,code:'no-video'});announce();return;}
    if(!element.captureStream||typeof MediaStreamTrackProcessor!=='function'){send({type:'error',captureId,code:'capture-unavailable',stage:'capabilities',reason:!element.captureStream?'CaptureStreamMissing':'ProcessorMissing'});return;}
    current=element;
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
      stage='capture-stream';stream=element.captureStream();stream.getAudioTracks().forEach(item=>item.stop());
      track=stream.getVideoTracks()[0];if(!track)throw new Error('no-track');
      track.addEventListener('ended',trackEnded,{once:true});
      stage='processor';reader=new MediaStreamTrackProcessor({track,maxBufferSize:1}).readable.getReader();const ownedReader=reader;
      owned.postMessage({type:'start',captureId});
      if(document.pictureInPictureElement===element)await document.exitPictureInPicture().catch(()=>{});
      let due=-Infinity;
      while(desired&&mine===revision&&reader===ownedReader){
        const {done,value:videoFrame}=await ownedReader.read();if(done){recover();break;}
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
    }catch(error){if(mine===revision){send({type:'error',captureId,code:'capture-unavailable',stage,reason:error.name});stop(false);}}
  }
  const receive=message=>{
    // Discovery predates typed browser sources and intentionally has no kind.
    if(message?.type==='state'){announce(true);return;}
    const messageKind=message?.kind||'youtube';if(messageKind!==sourceKind)return;
    if(message?.type==='configure'){width=Math.max(160,Math.min(1920,message.width||1280));height=Math.max(90,Math.min(1080,message.height||720));worker?.postMessage({type:'configure',width,height});}
    if(message?.type==='start'&&typeof message.captureId==='string'&&message.captureId.length===32){width=message.width||width;height=message.height||height;void start(message.captureId);}
    if(message?.type==='stop')stop();
    if(message?.type==='ack'&&message.captureId===epoch)worker?.postMessage(message);
    if(message?.type==='toggle'){const element=current||video();if(element){if(element.paused)element.play().catch(()=>send({type:'error',captureId:epoch,code:'play-blocked'}));else element.pause();}}
  };
  function mediaChanged(){announce();const element=video();if(element!==current||track?.readyState==='ended')recover();}
  chrome.runtime.onMessage.addListener(receive);
  // Twitch can replace its player without a full navigation. Observe the DOM
  // and recover only while an explicit mirror is still armed.
  for(const event of ['play','pause','loadstart','loadedmetadata','loadeddata','canplay','playing','emptied','ended'])document.addEventListener(event,mediaChanged,{capture:true,signal:events.signal});
  const observer=new MutationObserver(mediaChanged);observer.observe(document.documentElement,{childList:true,subtree:true});
  document.addEventListener('yt-navigate-finish',mediaChanged,{signal:events.signal});
  window.addEventListener('pagehide',()=>stop(),{signal:events.signal});
  globalThis.__battlestationVideoAnnounce=()=>announce(true);
  globalThis.__battlestationVideoDispose=()=>{stop();observer.disconnect();events.abort();chrome.runtime.onMessage.removeListener?.(receive);};
  announce();
})();
