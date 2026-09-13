(function () {
  'use strict';
  var pending = new Map(), sequence = 0;
  window.ViceCity = {
    available: !!(window.chrome && window.chrome.webview),
    request: function (channel, path, body) {
      return new Promise(function (resolve, reject) {
        if (!window.ViceCity.available) { reject(new Error('Plugin Rainmeter indisponible.')); return; }
        var id = ++sequence;
        var timeout = setTimeout(function () { pending.delete(id); reject(new Error('Délai dépassé.')); }, 90000);
        pending.set(id, {resolve: resolve, reject: reject, timeout: timeout});
        window.chrome.webview.postMessage({id: id, channel: channel, path: path, body: body || null});
      });
    }
  };
  if (window.ViceCity.available) window.chrome.webview.addEventListener('message', function (event) {
    var message = event.data, request = pending.get(message.id);
    if (!request) return;
    clearTimeout(request.timeout); pending.delete(message.id);
    if (message.error) request.reject(new Error(message.error)); else request.resolve(message.result);
  });
})();
