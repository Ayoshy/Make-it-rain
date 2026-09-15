// No remote service, cookies, history, titles or URLs are sent to Battlestation.
const tabs = new Map();
let nativePort;
let retry;
let discovering;
const SOURCE_URLS=['https://www.youtube.com/*','https://www.twitch.tv/*','https://clips.twitch.tv/*'];
function sourceKind(url){
  try{
    const parsed=new URL(url);
    if(parsed.protocol!=='https:')return;
    if(parsed.hostname==='www.youtube.com')return 'youtube';
    if(parsed.hostname==='www.twitch.tv'||parsed.hostname==='clips.twitch.tv')return 'twitch';
  }catch{}
}
function sourceKindForTab(tab){return sourceKind(tab?.url||'');}
async function discover(){
  if(discovering)return discovering;
  discovering=(async()=>{const open=await chrome.tabs.query({url:SOURCE_URLS});const ids=new Set(open.map(t=>t.id));for(const id of tabs.keys())if(!ids.has(id))tabs.delete(id);
    for(const tab of open)if(tab.id){try{await chrome.tabs.sendMessage(tab.id,{type:'state'});}catch{await chrome.scripting.executeScript({target:{tabId:tab.id},files:['video.js']}).catch(()=>{});}}
    state();})();
  try{await discovering;}finally{discovering=undefined;}
}
function publish(message) {
  try { nativePort?.postMessage(message); } catch { /* reconnect handles this */ }
}
function state() {
  publish({type: 'tabs', tabs: [...tabs].slice(-32).map(([id, value]) => ({id, ...value}))});
}
function connect() {
  clearTimeout(retry);
  if (nativePort) return;
  try {
    const port = chrome.runtime.connectNative('com.battlestation.video');
    nativePort = port;
    port.onDisconnect.addListener(() => {
      void chrome.runtime.lastError;
      if (nativePort === port) nativePort = undefined;
      retry = setTimeout(connect, 2000);
    });
    port.onMessage.addListener(async message => {
      if (message?.type === 'list') {
        await discover();
        return;
      }
      const id = message?.tabId;
      if (!Number.isInteger(id) || !tabs.has(id)) return;
      const commandKind=message.kind||'youtube';
      if(commandKind!==tabs.get(id).kind)return;
      if (message.type === 'focus') {
        try { const tab = await chrome.tabs.update(id, {active: true}); await chrome.windows.update(tab.windowId, {focused: true}); } catch {}
      } else if (['start', 'stop', 'toggle', 'ack', 'configure'].includes(message.type)) {
        chrome.tabs.sendMessage(id, message).catch(() => { tabs.delete(id); state(); });
      }
    });
    state();void discover();
  } catch { retry = setTimeout(connect, 2000); }
}
chrome.runtime.onMessage.addListener((message, sender) => {
  const kind=sourceKind(sender.url||'');
  if (sender.frameId !== 0 || !sender.tab?.id || !kind) return;
  const id = sender.tab.id;
  if (message?.type === 'state') {
    const previous = tabs.get(id);
    tabs.set(id, {kind, ready: message.ready === true, playing: message.playing === true, active: sender.tab.active === true, used: previous?.used ?? Date.now()});
    state();
  } else if (['frame', 'ended', 'error'].includes(message?.type)) {
    if (message.type === 'frame' && (typeof message.jpeg !== 'string' || message.jpeg.length > 2800000)) return;
    publish({...message, tabId: id, kind});
  }
});
chrome.tabs.onRemoved.addListener(id => { if (tabs.delete(id)) state(); });
chrome.tabs.onActivated.addListener(({tabId}) => {
  for (const [id, tab] of tabs) { tab.active = id === tabId; if (id === tabId) tab.used = Date.now(); }
  state();
});
chrome.tabs.onUpdated.addListener((id,change,tab)=>{
  if(change.url!==undefined&&!sourceKind(change.url)){if(tabs.delete(id))state();return;}
  if(change.url!==undefined){if(tabs.delete(id))state();if(change.status==='complete')void discover();return;}
  if(change.status==='complete'&&sourceKindForTab(tab))void discover();
});
chrome.action.onClicked.addListener(async tab => {
  connect();
  if (tab.id && sourceKindForTab(tab)) {
    await chrome.scripting.executeScript({target: {tabId: tab.id}, files: ['video.js']}).catch(() => {});
    chrome.tabs.sendMessage(tab.id, {type: 'state'}).catch(() => {});
  }
});
chrome.runtime.onInstalled.addListener(async () => {
  const open = await chrome.tabs.query({url: SOURCE_URLS});
  for (const tab of open) if (tab.id) chrome.scripting.executeScript({target: {tabId: tab.id}, files: ['video.js']}).catch(() => {});
});
connect();
