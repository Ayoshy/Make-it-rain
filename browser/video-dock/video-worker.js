// Packaged extension worker: no page CSP, remote code, video URLs or audio.
let running=false,epoch='',sequence=0,busy=false;
const pending=new Map(),canvas=new OffscreenCanvas(1,1),context=canvas.getContext('2d',{alpha:false});
onmessage=async ({data})=>{
  if(data.type==='ack'){if(data.captureId===epoch)pending.delete(data.sequence);return;}
  if(data.type==='stop'){running=false;pending.clear();return;}
  if(data.type==='start'){epoch=data.captureId;running=true;return;}
  if(data.type!=='bitmap')return;
  const bitmap=data.bitmap,captureId=data.captureId;
  const consumed=()=>postMessage({type:'consumed',captureId});
  const now=performance.now();for(const [id,at] of pending)if(now-at>2000)pending.delete(id);
  if(!running||epoch!==captureId||busy||pending.size>=2){bitmap.close();consumed();return;}
  busy=true;
  try{
    const width=bitmap.width,height=bitmap.height;if(width<1||height<1||width>1920||height>1080)return;
    if(canvas.width!==width||canvas.height!==height){canvas.width=width;canvas.height=height;}
    context.drawImage(bitmap,0,0);const began=performance.now();
    const blob=await canvas.convertToBlob({type:'image/jpeg',quality:.9});if(!running||epoch!==captureId)return;
    const bytes=new Uint8Array(await blob.arrayBuffer());if(bytes.length>2*1024*1024)return;
    let binary='';for(let i=0;i<bytes.length;i+=8192)binary+=String.fromCharCode(...bytes.subarray(i,i+8192));
    const id=Number.isInteger(data.sequence)?data.sequence:++sequence;pending.set(id,performance.now());
    postMessage({type:'frame',captureId,sequence:id,width,height,jpeg:btoa(binary),encodeMs:performance.now()-began,sourceTimestamp:data.sourceTimestamp});
  }catch(error){postMessage({type:'error',captureId,code:'capture-unavailable',stage:'encoding',reason:error.name});}
  finally{bitmap.close();busy=false;consumed();}
};
