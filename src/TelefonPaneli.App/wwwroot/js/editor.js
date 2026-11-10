// Düzenleme modu: ekrana 600 ms uzun basınca açılır. Kutucuk ekle/sil/taşı/boyutlandır, eşik, sayfa işlemleri.
// Çalışma kopyası üzerinde çalışır; "Bitti" ile kaydeder, "Vazgeç" ile atar.

import { addTile, removeTile, moveTile, resizeTile, setThresholds, addPage, removePage, renamePage } from './layout.js';
import { AVAILABLE_TILES, metricLabel } from './format.js';

const LONG_PRESS_MS = 600;
const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

function tileTitle(t) {
  if (t.kind === 'mediaStrip') return 'Şarkı şeridi';
  if (t.kind === 'mediaFull') return 'Müzik (tam sayfa)';
  if (t.kind === 'chart') return `${metricLabel(t.metric)} grafiği`;
  return metricLabel(t.metric);
}

export function installEditor({ root, getLayout, getPager, getSeen, save, toast }) {
  const state = { active: false, work: null, pageIndex: 0, selected: null, overlay: null };

  // ---- Uzun basma algılama ----
  let timer = null, sx = 0, sy = 0;
  root.addEventListener('pointerdown', (e) => {
    if (state.active || !getLayout() || e.target.closest('button, input, .pair')) return;
    if (!e.target.closest('.tile, .strip, .media-full, .grid')) return;
    sx = e.clientX; sy = e.clientY;
    clearTimeout(timer);
    timer = setTimeout(() => { timer = null; open(); }, LONG_PRESS_MS);
  });
  const cancel = () => { clearTimeout(timer); timer = null; };
  root.addEventListener('pointermove', (e) => { if (timer && Math.hypot(e.clientX - sx, e.clientY - sy) > 10) cancel(); });
  root.addEventListener('pointerup', cancel);
  root.addEventListener('pointercancel', cancel);
  root.addEventListener('contextmenu', (e) => e.preventDefault());

  function open() {
    state.active = true;
    state.work = JSON.parse(JSON.stringify(getLayout()));
    state.pageIndex = Math.min(getPager()?.index ?? 0, state.work.pages.length - 1);
    state.selected = null;
    getPager()?.setEnabled(false);
    navigator.vibrate?.(30);
    state.overlay = document.createElement('div');
    state.overlay.className = 'editor';
    document.body.appendChild(state.overlay);
    draw();
  }

  function close(commit) {
    if (commit) save(state.work);
    state.overlay?.remove();
    state.overlay = null;
    state.active = false;
    getPager()?.setEnabled(true);
    if (commit) {
      getPager()?.go(state.pageIndex);
      toast('Düzen kaydedildi');
    }
  }

  const page = () => state.work.pages[state.pageIndex];

  function update(next) {
    state.work = next;
    state.pageIndex = Math.min(state.pageIndex, state.work.pages.length - 1);
    draw();
  }

  function draw() {
    const p = page();
    const sel = p.tiles.find((t) => t.id === state.selected);
    state.overlay.innerHTML = `
      <div class="ed-bar">
        <button class="ed-btn" data-act="prevPage" aria-label="Önceki sayfa" ${state.pageIndex === 0 ? 'disabled' : ''}>◀</button>
        <input class="ed-name" value="${esc(p.name)}" maxlength="30" aria-label="Sayfa adı">
        <span class="ed-count">${state.pageIndex + 1}/${state.work.pages.length}</span>
        <button class="ed-btn" data-act="nextPage" aria-label="Sonraki sayfa" ${state.pageIndex === state.work.pages.length - 1 ? 'disabled' : ''}>▶</button>
        <button class="ed-btn" data-act="addPage" ${state.work.pages.length >= 10 ? 'disabled' : ''}>＋ Sayfa</button>
        <button class="ed-btn danger" data-act="removePage" ${state.work.pages.length <= 1 ? 'disabled' : ''}>Sayfayı sil</button>
        <span class="ed-spacer"></span>
        <button class="ed-btn" data-act="cancel">Vazgeç</button>
        <button class="ed-btn primary" data-act="done">Bitti ✓</button>
      </div>
      <div class="ed-grid">
        ${p.tiles.map((t, i) => `
          <div class="ed-tile ${t.kind === 'mediaStrip' ? 'M' : t.kind === 'mediaFull' ? 'L' : t.size} ${t.id === state.selected ? 'sel' : ''}" data-id="${esc(t.id)}" data-i="${i}">
            <button class="ed-x" data-act="remove" data-id="${esc(t.id)}" aria-label="Sil">✕</button>
            <span class="ed-grip">⠿</span>
            <span class="ed-title">${esc(tileTitle(t))}</span>
            <span class="ed-size">${t.kind === 'metric' || t.kind === 'chart' ? t.size : ''}</span>
          </div>`).join('')}
        <button class="ed-add" data-act="openAdd" ${p.tiles.length >= 24 ? 'disabled' : ''}>＋<span>Kutucuk ekle</span></button>
      </div>
      ${sel ? panelHtml(sel) : '<div class="ed-hint">Kutucuğa dokun: boyut ve eşik · Sürükle: yerini değiştir</div>'}`;
    wire();
  }

  function panelHtml(t) {
    const sized = t.kind === 'metric' || t.kind === 'chart';
    const thresholds = t.kind === 'metric';
    return `<div class="ed-panel">
      <b>${esc(tileTitle(t))}</b>
      ${sized ? `<span class="ed-seg">${['S', 'M', 'L'].map((s) => `<button class="ed-btn ${t.size === s ? 'on' : ''}" data-act="size" data-size="${s}">${s}</button>`).join('')}</span>` : ''}
      ${thresholds ? `
        <label>Turuncu ≥ <input class="ed-num" data-th="warn" inputmode="decimal" value="${t.warn ?? ''}"></label>
        <label>Kırmızı ≥ <input class="ed-num" data-th="crit" inputmode="decimal" value="${t.crit ?? ''}"></label>` : ''}
      <span class="ed-spacer"></span>
      <button class="ed-btn" data-act="deselect">Tamam</button>
    </div>`;
  }

  function addListHtml() {
    const seen = getSeen();
    return `<div class="ed-sheet">
      <div class="ed-sheet-head"><b>Kutucuk ekle</b><button class="ed-btn" data-act="closeAdd">Kapat</button></div>
      <div class="ed-list">
        ${AVAILABLE_TILES.map((t, i) => {
          const missing = t.metric && !seen.has(t.metric);
          return `<button class="ed-item ${missing ? 'missing' : ''}" data-act="add" data-i="${i}">${esc(t.label)}${missing ? '<small>bu bilgisayarda yok</small>' : ''}</button>`;
        }).join('')}
      </div>
    </div>`;
  }

  function wire() {
    const o = state.overlay;
    o.querySelector('.ed-name').addEventListener('change', (e) => update(renamePage(state.work, page().id, e.target.value)));
    for (const input of o.querySelectorAll('.ed-num')) {
      input.addEventListener('change', () => {
        const t = page().tiles.find((x) => x.id === state.selected);
        const warn = o.querySelector('[data-th=warn]').value;
        const crit = o.querySelector('[data-th=crit]').value;
        update(setThresholds(state.work, page().id, t.id, warn, crit));
      });
    }
    o.onclick = (e) => {
      const b = e.target.closest('[data-act]');
      if (b && !b.disabled) act(b.dataset.act, b);
    };
    wireDrag(o.querySelector('.ed-grid'));
  }

  function act(name, b) {
    const p = page();
    switch (name) {
      case 'prevPage': state.pageIndex -= 1; state.selected = null; draw(); break;
      case 'nextPage': state.pageIndex += 1; state.selected = null; draw(); break;
      case 'addPage': update(addPage(state.work, `Sayfa ${state.work.pages.length + 1}`)); state.pageIndex = state.work.pages.length - 1; draw(); break;
      case 'removePage':
        if (confirm(`"${p.name}" sayfası silinsin mi?`)) update(removePage(state.work, p.id));
        break;
      case 'remove':
        if (state.selected === b.dataset.id) state.selected = null;
        update(removeTile(state.work, p.id, b.dataset.id));
        break;
      case 'size': update(resizeTile(state.work, p.id, state.selected, b.dataset.size)); break;
      case 'deselect': state.selected = null; draw(); break;
      case 'openAdd': state.overlay.insertAdjacentHTML('beforeend', addListHtml()); break;
      case 'closeAdd': state.overlay.querySelector('.ed-sheet')?.remove(); break;
      case 'add': update(addTile(state.work, p.id, AVAILABLE_TILES[Number(b.dataset.i)])); break;
      case 'cancel': close(false); break;
      case 'done': close(true); break;
    }
  }

  // ---- Dokun = seç, sürükle = yerini değiştir ----
  // Parmak takibi (pointer capture) yalnızca sürükleme başlayınca açılır; aksi halde
  // tıklama olayı kutucuk yerine ızgaraya gider ve seçim çalışmaz.
  function wireDrag(grid) {
    let from = -1, fromId = null, startX = 0, startY = 0, ghost = null, moved = false;
    grid.onpointerdown = (e) => {
      const tile = e.target.closest('.ed-tile');
      if (!tile || e.target.closest('.ed-x')) return;
      from = Number(tile.dataset.i); fromId = tile.dataset.id;
      startX = e.clientX; startY = e.clientY; moved = false;
    };
    grid.onpointermove = (e) => {
      if (from < 0) return;
      if (!moved && Math.hypot(e.clientX - startX, e.clientY - startY) < 10) return;
      if (!moved) {
        moved = true;
        try { grid.setPointerCapture(e.pointerId); } catch { /* takip olmadan da çalışır */ }
        ghost = grid.querySelector(`.ed-tile[data-i="${from}"]`);
        ghost.classList.add('dragging');
      }
      ghost.style.transform = `translate(${e.clientX - startX}px, ${e.clientY - startY}px)`;
    };
    grid.onpointerup = (e) => {
      if (from < 0) return;
      if (!moved) {
        state.selected = fromId === state.selected ? null : fromId;
        from = -1;
        draw();
        return;
      }
      ghost.style.visibility = 'hidden';
      const under = document.elementFromPoint(e.clientX, e.clientY)?.closest('.ed-tile, .ed-add');
      const to = !under ? from : under.classList.contains('ed-add') ? page().tiles.length - 1 : Number(under.dataset.i);
      from === to ? draw() : update(moveTile(state.work, page().id, from, to));
      from = -1; ghost = null;
    };
    grid.onpointercancel = () => { if (from >= 0) { from = -1; ghost = null; draw(); } };
  }

  return { get active() { return state.active; } };
}
