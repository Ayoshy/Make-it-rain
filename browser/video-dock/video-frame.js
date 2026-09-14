// The worker is loaded from the extension's own origin, never a page blob URL.
let port,worker,epoch='';
addEventListener('message',event=>{
  const origin=chrome.runtime.getURL('').replace(/\/$/,'');
  if(port||event.source!==parent||!['https://www.youtube.com',origin].includes(event.origin)||event.data?.type!=='battlestation-video-port'||event.ports.length!==1)return;
  port=event.ports[0];epoch=event.data.captureId;
  try{
    worker=new Worker('video-worker.js');
    worker.onmessage=({data})=>port?.postMessage(data);
    worker.onerror=error=>port?.postMessage({type:'error',captureId:epoch,code:'capture-unavailable',stage:'extension-worker',reason:error.error?.name||'Error'});
    port.onmessage=({data})=>{
      if(data.type==='stop'){worker.terminate();port.close();return;}
      if(data.type==='start')epoch=data.captureId;
      try{worker.postMessage(data,data.bitmap?[data.bitmap]:[]);}catch(error){port.postMessage({type:'error',captureId:epoch,code:'capture-unavailable',stage:'extension-transfer',reason:error.name});}
    };
    port.postMessage({type:'ready'});
  }catch(error){port.postMessage({type:'error',captureId:epoch,code:'capture-unavailable',stage:'extension-worker',reason:error.name});}
});
addEventListener('pagehide',()=>{worker?.terminate();port?.close();});
