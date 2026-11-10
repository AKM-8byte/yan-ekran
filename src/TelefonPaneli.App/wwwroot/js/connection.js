// WebSocket bağlantısı: otomatik yeniden bağlanma, bağlantı yokken gönderilenleri sıraya alma.

export const UNPAIRED_CODE = 4401;

/** Deneme sayısına göre bekleme (ms): 1, 2, 4, 5, 5… sn. */
export function nextDelay(attempt) {
  return Math.min(5000, 1000 * 2 ** Math.max(0, attempt));
}

/**
 * onStatus: 'connected' | 'reconnecting' | 'unpaired'
 */
export function createConnection({ url, onMessage, onStatus, WebSocketImpl = globalThis.WebSocket, setTimeoutImpl = globalThis.setTimeout }) {
  let ws = null;
  let attempt = 0;
  let closed = false;
  const pending = [];

  function connect() {
    if (closed) return;
    ws = new WebSocketImpl(url);
    ws.onopen = () => {
      attempt = 0;
      onStatus('connected');
      while (pending.length > 0) ws.send(pending.shift());
    };
    ws.onmessage = (e) => {
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
