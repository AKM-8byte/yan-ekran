// Uygulama akışı: eşleştirme → bağlantı → çizim → canlı güncelleme.

import { createConnection } from './connection.js';
import { showPairing } from './pairing.js';
import { createPager } from './pager.js';
import { createRenderer } from './render.js';
import { keepAwakeHint } from './platform.js';

const TOKEN_KEY = 'tp.token';
const PAGE_KEY = 'tp.page';
const HINT_KEY = 'tp.wakeHint';

const app = document.getElementById('app');
const banner = document.getElementById('banner');
const toastEl = document.getElementById('toast');

const store = {
  get(k) { try { return localStorage.getItem(k); } catch { return null; } },
  set(k, v) { try { localStorage.setItem(k, v); } catch { /* yok say */ } },
  del(k) { try { localStorage.removeItem(k); } catch { /* yok say */ } },
};

let toastTimer;
export function toast(text, ms = 3500) {
  toastEl.textContent = text;
  toastEl.hidden = false;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { toastEl.hidden = true; }, ms);
}

const banners = { connection: null, fifa: null };
function setBanner(kind, text) {
  banners[kind] = text;
  const conn = banners.connection;
  const shown = conn ?? banners.fifa;
  banner.hidden = !shown;
  banner.textContent = shown ?? '';
  banner.className = `banner ${conn ? 'error' : 'warn'}`;
}

async function start() {
  let token = store.get(TOKEN_KEY);
  if (!token) {
    token = await showPairing(app);
    store.set(TOKEN_KEY, token);
  }
  run(token);
}

function run(token) {
  let layout = null;
  let pager = null;
  let editor = null;
  const seen = new Set();

  const proto = location.protocol === 'https:' ? 'wss' : 'ws';
  const conn = createConnection({
    url: `${proto}://${location.host}/ws?token=${encodeURIComponent(token)}`,
    onMessage,
    onStatus,
  });

  const renderer = createRenderer(app, {
    token,
    onMediaAction: (action) => conn.send({ type: 'media', action }),
    onVolume: (value) => conn.send({ type: 'volume', value }),
  });

  function draw() {
    const startPage = Math.min(Number(store.get(PAGE_KEY)) || 0, layout.pages.length - 1);
    const pagerEl = renderer.renderLayout(layout);
    pager = createPager(pagerEl, layout.pages.length, (i) => store.set(PAGE_KEY, String(i)), startPage);
    window.dispatchEvent(new CustomEvent('tp:layout', { detail: { layout, pager, pagerEl, conn, renderer } }));
  }

  function onMessage(msg) {
    switch (msg.type) {
      case 'layout':
        layout = msg.layout;
        if (!editor?.active) draw();
        break;
      case 'snapshot':
        renderer.updateSnapshot(msg);
        for (const [k, v] of Object.entries(msg.metrics || {})) if (v !== null && v !== undefined) seen.add(k);
        setBanner('fifa', msg.driverStopFailed
          ? 'Sensör sürücüsü durdurulamadı — FIFA’yı AÇMA (bilgisayardaki uyarıya bak)'
          : msg.fifaWarning ? 'FIFA açıldı — programı FIFA modunda yeniden başlat' : null);
        break;
      case 'media':
        renderer.updateMedia(msg.state);
        break;
    }
  }

  function onStatus(status) {
    document.body.classList.toggle('connected', status === 'connected');
    if (status === 'connected') setBanner('connection', null);
    else if (status === 'reconnecting') setBanner('connection', 'Bağlantı yok — yeniden deneniyor');
    else if (status === 'unpaired') {
      setBanner('connection', null);
      setBanner('fifa', null);
      store.del(TOKEN_KEY);
      // Eski oturumun dinleyicileri/zamanlayıcıları kalmasın diye sayfayı baştan yükle.
      location.reload();
    }
  }

  // Düzenleme modu (Task 14) kendi modülünde; yoksa sessizce atlanır.
  import('./editor.js')
    .then((m) => { editor = m.installEditor({
        root: app,
        getLayout: () => layout,
        getPager: () => pager,
        getSeen: () => seen,
        save: (next) => { layout = next; conn.send({ type: 'saveLayout', layout: next }); draw(); },
        redraw: () => { if (layout) draw(); },
        toast,
      });; })
    .catch(() => {});

  keepAwake();
}

async function keepAwake() {
  let lock = null;
  async function request() {
    try {
      if ('wakeLock' in navigator) {
        lock = await navigator.wakeLock.request('screen');
        return true;
      }
    } catch { /* HTTP'de veya izin yoksa çalışmaz */ }
    return false;
  }
  const ok = await request();
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible' && lock?.released !== false) request();
  });
  if (!ok && !store.get(HINT_KEY)) {
    store.set(HINT_KEY, '1');
    toast(keepAwakeHint(), 8000);
  }
  try { await screen.orientation?.lock?.('landscape'); } catch { /* tam ekran değilse olmaz */ }
}

start();
