// The workers are loaded from the extension's own origin, never a page blob URL.
// Two encoders: under load one JPEG encode can exceed a frame interval, and
// encodes run in parallel across workers. Frames are forwarded in capture order;
// a frame finished after a newer one is dropped and acknowledged locally.
let port,epoch='',forwarded=0;
const encoders=[],owners=new Map();
addEventListener('message',event=>{
  const origin=chrome.runtime.getURL('').replace(/\/$/,'');
  if(port||event.source!==parent||!["https://www.youtube.com","https://www.twitch.tv","https://clips.twitch.tv",origin].includes(event.origin)||event.data?.type!=='battlestation-video-port'||event.ports.length!==1)return;
  port=event.ports[0];epoch=event.data.captureId;
  try{
    for(let index=0;index<2;index++){
      const encoder={worker:new Worker('video-worker.js'),busy:0};
      encoder.worker.onmessage=({data})=>{
        if(data.type==='consumed')encoder.busy=Math.max(0,encoder.busy-1);
        if(data.type==='frame'){
          if(data.sequence<=forwarded){encoder.worker.postMessage({type:'ack',captureId:data.captureId,sequence:data.sequence});return;}
          forwarded=data.sequence;owners.set(data.sequence,encoder.worker);
          if(owners.size>32)owners.delete(owners.keys().next().value);
        }
        port?.postMessage(data);
      };
      encoder.worker.onerror=error=>port?.postMessage({type:'error',captureId:epoch,code:'capture-unavailable',stage:'extension-worker',reason:error.error?.name||'Error'});
      encoders.push(encoder);
    }
    port.onmessage=({data})=>{
      if(data.type==='stop'){encoders.forEach(encoder=>encoder.worker.terminate());port.close();return;}
      if(data.type==='start')epoch=data.captureId;
      try{
        if(data.type==='bitmap'){const encoder=encoders.reduce((best,item)=>item.busy<best.busy?item:best);encoder.busy++;encoder.worker.postMessage(data,[data.bitmap]);}
        else if(data.type==='ack'){const worker=owners.get(data.sequence);owners.delete(data.sequence);worker?.postMessage(data);}
        else encoders.forEach(encoder=>encoder.worker.postMessage(data));
      }catch(error){port.postMessage({type:'error',captureId:epoch,code:'capture-unavailable',stage:'extension-transfer',reason:error.name});}
    };
    port.postMessage({type:'ready'});
  }catch(error){port.postMessage({type:'error',captureId:epoch,code:'capture-unavailable',stage:'extension-worker',reason:error.name});}
});
addEventListener('pagehide',()=>{encoders.forEach(encoder=>encoder.worker.terminate());port?.close();});
