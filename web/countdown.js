(function (root) {
  'use strict';
  // Require an explicit time zone: the count must represent the same instant
  // on every PC, including after the French daylight-saving change.
  function parseTarget(text) {
    if (typeof text !== 'string') return NaN;
    var m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(Z|[+-]\d{2}:\d{2})$/.exec(text.trim());
    if (!m) return NaN;
    var y = +m[1], month = +m[2], d = +m[3];
    if (y < 2000 || month < 1 || month > 12 || d < 1 || d > new Date(Date.UTC(y, month, 0)).getUTCDate()) return NaN;
    if (+m[4] > 23 || +m[5] > 59 || +m[6] > 59) return NaN;
    if (m[7] !== 'Z' && (+m[7].slice(1, 3) > 14 || +m[7].slice(4) > 59 || (+m[7].slice(1, 3) === 14 && +m[7].slice(4) !== 0))) return NaN;
    return Date.parse(text.trim());
  }
  function remaining(target, now) {
    var total = Math.max(0, Math.ceil((target - now) / 1000));
    return {days:Math.floor(total/86400),hours:Math.floor(total/3600)%24,minutes:Math.floor(total/60)%60,seconds:total%60,finished:now>=target};
  }
  root.ViceCountdown = {parseTarget:parseTarget,remaining:remaining};
})(typeof window !== 'undefined' ? window : globalThis);
