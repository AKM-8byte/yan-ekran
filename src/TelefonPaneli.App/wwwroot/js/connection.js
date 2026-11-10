// WebSocket bağlantısı: otomatik yeniden bağlanma, bağlantı yokken gönderilenleri sıraya alma.

export const UNPAIRED_CODE = 4401;

/** Deneme sayısına göre bekleme (ms): 1, 2, 4, 5, 5… sn. */
export function nextDelay(attempt) {
  return Math.min(5000, 1000 * 2 ** Math.max(0, attempt));
}

/** Açık bağlantıda bu kadar süre hiç mesaj gelmezse (sunucu her 500 ms gönderir) bağlantı ölü sayılır. */
export const STALE_MS = 3000;

/** onStatus: 'connected' | 'reconnecting' | 'unpaired' */
export function createConnection({
  url, onMessage, onStatus,
  WebSocketImpl = globalThis.WebSocket,
  setTimeoutImpl = globalThis.setTimeout,
  setIntervalImpl = globalThis.setInterval,
  nowImpl = Date.now,
}) {
  let ws = null;
  let lastMessage = nowImpl();
  let attempt = 0;
  let closed = false;
  const pending = [];

  function connect() {
    if (closed) return;
    ws = new WebSocketImpl(url);
    ws.onopen = () => {
      attempt = 0;
      lastMessage = nowImpl();
      onStatus('connected');
      while (pending.length > 0) ws.send(pending.shift());
    };
    ws.onmessage = (e) => {
      lastMessage = nowImpl();
      let msg;
      try { msg = JSON.parse(e.data); } catch { return; }
      if (msg && typeof msg.type === 'string') onMessage(msg);
    };
    ws.onclose = (e) => {
      if (closed) return;
      if (e && e.code === UNPAIRED_CODE) {
        closed = true;
        onStatus('unpaired');
        return;
      }
      onStatus('reconnecting');
      setTimeoutImpl(connect, nextDelay(attempt));
      attempt += 1;
    };
    ws.onerror = () => { /* onclose ardından gelir */ };
  }

  connect();

  // Sessizce kopan bağlantı (PC uykuya geçti, Wi-Fi düştü) tarayıcıda "açık" görünmeye devam eder.
  setIntervalImpl(() => {
    if (!closed && ws && ws.readyState === (WebSocketImpl.OPEN ?? 1) && nowImpl() - lastMessage > STALE_MS) ws.close();
  }, 1000);

  return {
    send(obj) {
      const data = JSON.stringify(obj);
      if (ws && ws.readyState === (WebSocketImpl.OPEN ?? 1)) ws.send(data);
      else {
        if (pending.length > 20) pending.shift();
        pending.push(data);
      }
    },
    close() {
      closed = true;
      ws?.close();
    },
  };
}
