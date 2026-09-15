import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';
import vm from 'node:vm';

const root = new URL('../', import.meta.url);
const read = path => readFile(new URL(path, root), 'utf8');

test('manifest keeps the browser surface limited to YouTube and Twitch domains', async () => {
  const manifest = JSON.parse(await read('browser/video-dock/manifest.json'));
  const allowed = ['https://www.youtube.com/*', 'https://www.twitch.tv/*', 'https://clips.twitch.tv/*'];
  assert.deepEqual(manifest.host_permissions, allowed);
  assert.deepEqual(manifest.content_scripts[0].matches, allowed);
  assert.deepEqual(manifest.web_accessible_resources[0].matches, allowed);
  assert.equal(manifest.version, '0.4.0');
});

test('background purges a Twitch tab as soon as it navigates outside an allowed host', async () => {
  const listeners = {};
  const posts = [];
  const forwarded = [];
  const event = name => ({addListener(fn) { listeners[name] = fn; }});
  const port = {postMessage(value) {posts.push(value);}, onDisconnect: event('disconnect'), onMessage: event('native')};
  const chrome = {
    runtime: {
      connectNative() {return port;}, onMessage: event('runtime'), onInstalled: event('installed'), lastError: undefined,
    },
    tabs: {
      query: async () => [], sendMessage: async (id, message) => {forwarded.push({id, message});}, update: async id => ({id, windowId: 1}),
      onRemoved: event('removed'), onActivated: event('activated'), onUpdated: event('updated'),
    },
    scripting: {executeScript: async () => {}},
    windows: {update: async () => {}},
    action: {onClicked: event('clicked')},
  };
  const context = vm.createContext({chrome, URL, Date, Number, Set, Map, Promise, console, setTimeout, clearTimeout});
  vm.runInContext(await read('browser/video-dock/background.js'), context);
  await new Promise(setImmediate);
  listeners.runtime({type: 'state', ready: true, playing: true}, {frameId: 0, tab: {id: 17, active: true}, url: 'https://www.twitch.tv/channel'});
  assert.equal(posts.at(-1).tabs[0].kind, 'twitch');
  await listeners.native({type: 'ack', tabId: 17, kind: 'twitch', captureId: 'a'.repeat(32), sequence: 1});
  assert.equal(forwarded.at(-1).message.type, 'ack', 'Twitch acknowledgements reach the encoder');
  assert.equal(forwarded.at(-1).id, 17);
  listeners.updated(17, {url: 'https://example.com/'}, {url: 'https://example.com/'});
  assert.equal(JSON.stringify(posts.at(-1)), JSON.stringify({type: 'tabs', tabs: []}));
  const count = posts.length;
  listeners.runtime({type: 'state', ready: true, playing: true}, {frameId: 0, tab: {id: 17, active: true}, url: 'https://twitch.tv/channel'});
  assert.equal(posts.length, count, 'bare twitch.tv is outside the manifest allowlist');
});

function fakeVideo() {
  const events = new Map();
  const track = {readyState: 'live', addEventListener(name, fn) {events.set(name, fn);}, removeEventListener(name) {events.delete(name);}, stop() {this.readyState = 'ended';}};
  return {
    readyState: 4, paused: false, ended: false, clientWidth: 640, clientHeight: 360, track, events,
    captureCalls: 0,
    captureStream() {this.captureCalls++; track.readyState='live'; return {getAudioTracks: () => [{stop() {}}], getVideoTracks: () => [track], getTracks: () => [track]};},
    getVideoPlaybackQuality: () => ({totalVideoFrames: 1}),
    play: async function () {this.paused = false;}, pause: function () {this.paused = true;},
  };
}

test('Twitch content script announces its kind, ignores wrong-kind controls, and resumes on replacement', async () => {
  let current = fakeVideo();
  const sent = [];
  let receive;
  let mutation;
  const document = {
    pictureInPictureElement: null,
    visibilityState: 'visible',
    documentElement: {append(node) {setTimeout(() => node.onload?.(), 0);}},
    querySelector: () => current,
    querySelectorAll: () => [current],
    createElement: () => ({hidden: false, contentWindow: {postMessage() {}}, setAttribute() {}, remove() {}}),
    addEventListener() {},
  };
  const chrome = {
    runtime: {
      getURL: value => `chrome-extension://test/${value}`,
      sendMessage: message => {sent.push(message); return Promise.resolve();},
      onMessage: {addListener(fn) {receive = fn;}, removeListener() {}},
    },
  };
  class FakeMessageChannel {constructor() {this.port1 = {postMessage() {}, close() {}}; this.port2 = {postMessage() {}, close() {}};}}
  const context = vm.createContext({
    chrome, document, location: {hostname: 'www.twitch.tv'}, window: {addEventListener() {}},
    globalThis: undefined, MessageChannel: FakeMessageChannel, MediaStreamTrackProcessor: function () {return {readable: {getReader: () => ({read: () => new Promise(() => {}), cancel: async () => {}})}};},
    MutationObserver: function (fn) {mutation = fn; this.observe = () => {}; this.disconnect = () => {};},
    AbortController, DOMException, performance: {now: () => 1}, queueMicrotask, setTimeout, clearTimeout,
  });
  context.globalThis = context;
  vm.runInContext(await read('browser/video-dock/video.js'), context);
  assert.equal(sent.at(-1).type, 'state');
  assert.equal(sent.at(-1).kind, 'twitch');
  const initialAnnouncements=sent.length;
  mutation();mutation();
  assert.equal(sent.length,initialAnnouncements,'chat DOM changes do not resend unchanged state');
  receive({type:'state'});
  assert.equal(sent.length,initialAnnouncements+1,'untyped rediscovery announces an already-installed Twitch script');
  context.__battlestationVideoAnnounce();
  assert.equal(sent.length,initialAnnouncements+2,'reinjection announces even if state is unchanged');
  assert.equal(current.captureCalls,0,'discovery does not activate capture');
  const before = current.captureCalls;
  receive({type: 'start', kind: 'youtube', captureId: 'a'.repeat(32)});
  await new Promise(resolve => setTimeout(resolve, 10));
  assert.equal(current.captureCalls, before, 'a YouTube command cannot control Twitch');
  receive({type: 'start', kind: 'twitch', captureId: 'b'.repeat(32)});
  await new Promise(resolve => setTimeout(resolve, 10));
  assert.equal(current.captureCalls, before + 1, 'capture starts only after an explicit typed command');
  const old = current;
  current = fakeVideo();
  mutation();
  await new Promise(resolve => setTimeout(resolve, 10));
  assert.equal(current.captureCalls, 1, 'SPA video replacement resumes the armed capture');
  current.ended=true;current.track.readyState='ended';
  current.events.get('ended')?.();
  await new Promise(resolve => setTimeout(resolve, 10));
  assert.equal(current.captureCalls,1,'a completed VOD does not loop capture restarts');
  current.ended=false;mutation();
  await new Promise(resolve => setTimeout(resolve, 10));
  assert.equal(current.captureCalls,2,'a new ready source resumes after track end');
  old.track.readyState = 'ended';
  old.events.get('ended')?.();
  receive({type:'stop',kind:'twitch'});
  current=fakeVideo();mutation();
  await new Promise(resolve => setTimeout(resolve, 10));
  assert.equal(current.captureCalls,0,'replacement after stop never reactivates capture');
  context.__battlestationVideoDispose();
});
