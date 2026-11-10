// Düzeni DOM'a çizer ve canlı verilerle günceller (DOM'u yeniden kurmadan).

import { formatValue, metricLabel, metricUnit, levelClass, mediaText, formatDuration, requiresDriver } from './format.js';

const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

// Kutucuğun altındaki yardımcı satır: ana değere eşlik eden ikinci metrik.
const COMPANION = {
  'cpu.temp': ['cpu.load', (v) => `${formatValue('cpu.load', v)} kullanım`],
  'gpu.temp': ['gpu.load', (v) => `${formatValue('gpu.load', v)} kullanım`],
  'ram.used': ['ram.total', (v) => `/ ${formatValue('ram.total', v)} GB`],
  'gpu.vram.used': ['gpu.vram.total', (v) => `/ ${formatValue('gpu.vram.total', v)} GB`],
  'fps': ['fps.low1', (v) => `1% low: ${formatValue('fps.low1', v)}`],
};

const CHART_POINTS = 120; // 60 sn × 2/sn

export function createRenderer(root, { token, onMediaAction, onVolume }) {
  const history = new Map(); // metric → number[]
  let tiles = [];            // { tile, el, valueEl, subEl, poly? }
  let mediaEls = [];         // { kind, root }
  let lastSnapshot = null;
  let media = null;
  let mediaAt = 0;
  let artVersion = -1;

  function renderLayout(layout, pageIndexHint) {
    tiles = [];
    mediaEls = [];
    const pages = layout.pages.map((p) => {
      const strip = p.tiles.find((t) => t.kind === 'mediaStrip');
      const full = p.tiles.find((t) => t.kind === 'mediaFull');
      const gridTiles = p.tiles.filter((t) => t.kind === 'metric' || t.kind === 'chart');
      let body;
      if (full) body = mediaFullHtml(full);
      else body = `<div class="grid${gridTiles.length ? '' : ' empty'}">${gridTiles.map(tileHtml).join('')}</div>`;
      return `<section class="page" data-page="${esc(p.id)}">${body}${strip && !full ? stripHtml(strip) : ''}</section>`;
    });
    root.innerHTML = `<div class="pager"><div class="track">${pages.join('')}</div><div class="dots"></div></div><div class="rotate">Telefonu yatay çevir</div>`;

    for (const el of root.querySelectorAll('.tile')) {
      const id = el.dataset.tile;
      const tile = layout.pages.flatMap((p) => p.tiles).find((t) => t.id === id);
      tiles.push({ tile, el, valueEl: el.querySelector('.value'), subEl: el.querySelector('.sub'), poly: el.querySelector('polyline'), nowEl: el.querySelector('.chart-now') });
    }
    for (const el of root.querySelectorAll('[data-media]')) {
      mediaEls.push({ kind: el.dataset.media, root: el });
      el.addEventListener('click', (e) => {
        const b = e.target.closest('button[data-action]');
        if (b) onMediaAction(b.dataset.action);
      });
      const vol = el.querySelector('input[type=range]');
      if (vol) {
        let t;
        vol.addEventListener('input', () => {
          clearTimeout(t);
          t = setTimeout(() => onVolume(Number(vol.value)), 120);
        });
        vol.addEventListener('pointerdown', (e) => e.stopPropagation());
      }
    }
    artVersion = -1;
    if (lastSnapshot) updateSnapshot(lastSnapshot);
    updateMedia(media);
    return root.querySelector('.pager');
  }

  function tileHtml(t) {
    if (t.kind === 'chart') {
      return `<div class="tile chart ${t.size}" data-tile="${esc(t.id)}">
        <div class="chart-head"><span class="label">${esc(metricLabel(t.metric))} · 60 sn</span><span class="chart-now"></span></div>
        <svg class="spark" viewBox="0 0 ${CHART_POINTS - 1} 100" preserveAspectRatio="none"><polyline points=""/></svg>
      </div>`;
    }
    return `<div class="tile ${t.size}" data-tile="${esc(t.id)}">
      <span class="label">${esc(metricLabel(t.metric))}</span>
      <span class="value">—</span>
      <span class="sub"></span>
    </div>`;
  }

  function stripHtml() {
    return `<div class="strip" data-media="strip">
      <div class="art"></div>
      <div class="text"><b class="m-title">—</b> <span class="m-artist"></span></div>
      <div class="controls">
        <button class="ctl" data-action="prev" aria-label="Önceki">⏮</button>
        <button class="ctl" data-action="playPause" aria-label="Oynat/Duraklat"><span class="m-pp">⏯</span></button>
        <button class="ctl" data-action="next" aria-label="Sonraki">⏭</button>
      </div>
    </div>`;
  }

  function mediaFullHtml() {
    return `<div class="media-full" data-media="full">
      <div class="art"></div>
      <div class="info">
        <div class="app m-app" lang="en"></div>
        <div class="title m-title">—</div>
        <div class="artist m-artist"></div>
        <div class="progress"><i class="m-bar"></i></div>
        <div class="times"><span class="m-pos">0:00</span><span class="m-dur">0:00</span></div>
        <div class="controls">
          <button class="ctl" data-action="prev" aria-label="Önceki">⏮</button>
          <button class="ctl primary" data-action="playPause" aria-label="Oynat/Duraklat"><span class="m-pp">⏯</span></button>
          <button class="ctl" data-action="next" aria-label="Sonraki">⏭</button>
          <label class="volume" hidden><span>🔊</span><input type="range" min="0" max="1" step="0.05" aria-label="Ses"></label>
        </div>
      </div>
    </div>`;
  }

  function updateSnapshot(s) {
    lastSnapshot = s;
    const m = s.metrics || {};
    for (const [id, v] of Object.entries(m)) {
      if (v === null || v === undefined) continue;
      const h = history.get(id) || [];
      h.push(v);
      if (h.length > CHART_POINTS) h.shift();
      history.set(id, h);
    }

    for (const { tile, valueEl, subEl, poly, nowEl } of tiles) {
      const id = tile.metric;
      const v = m[id];
      if (tile.kind === 'chart') {
        nowEl.textContent = formatValue(id, v);
        poly.setAttribute('points', sparkPoints(history.get(id) || []));
        continue;
      }
      const shielded = s.mode === 'fifa' && requiresDriver(id) && (v === null || v === undefined);
      const asleep = s.gpuAsleep && id.startsWith('gpu.') && (v === null || v === undefined);
      const unit = metricUnit(id);
      let html = esc(formatValue(id, v));
      if (shielded) html += '<span class="shield" title="FIFA modu: sensör sürücüsü kapalı">🛡</span>';
      else if (asleep) html = '<span class="sleep">uyku</span>';
      else if (unit && v !== null && v !== undefined) html += `<span class="unit">${esc(unit)}</span>`;
      valueEl.innerHTML = html;
      valueEl.className = `value ${levelClass(v, tile.warn, tile.crit)}`;
      const comp = COMPANION[id];
      if (subEl) subEl.textContent = comp && m[comp[0]] !== undefined && m[comp[0]] !== null ? comp[1](m[comp[0]]) : '';
    }
  }

  function sparkPoints(values) {
    if (values.length < 2) return '';
    const max = Math.max(...values);
    const min = Math.min(...values);
    const span = max - min || 1;
    const offset = CHART_POINTS - values.length;
    return values.map((v, i) => `${offset + i},${(95 - ((v - min) / span) * 90).toFixed(1)}`).join(' ');
  }

  function updateMedia(state) {
    media = state;
    mediaAt = performance.now();
    const { title, subtitle } = mediaText(state);
    const newArt = state ? state.artVersion : -1;
    for (const { kind, root: el } of mediaEls) {
      el.querySelector('.m-title').textContent = title;
      el.querySelector('.m-artist').textContent = kind === 'strip' && subtitle ? `· ${subtitle}` : subtitle;
      const app = el.querySelector('.m-app');
      if (app) app.textContent = state ? state.app : '';
      el.querySelector('.m-pp').textContent = state && state.isPlaying ? '⏸' : '▶';
      for (const b of el.querySelectorAll('button[data-action]')) b.disabled = !state;
      const vol = el.querySelector('.volume');
      if (vol) {
        const hasVol = state && Number.isFinite(state.volume);
        vol.hidden = !hasVol;
        const input = vol.querySelector('input');
        if (hasVol && document.activeElement !== input) input.value = state.volume;
      }
      if (newArt !== artVersion) {
        const art = el.querySelector('.art');
        if (state) {
          const url = `/art?token=${encodeURIComponent(token)}&v=${state.artVersion}`;
          const img = new Image();
          img.onload = () => { art.style.backgroundImage = `url("${url}")`; art.classList.add('has-img'); };
          img.onerror = () => { art.style.backgroundImage = ''; art.classList.remove('has-img'); };
          img.src = url;
        } else {
          art.style.backgroundImage = '';
          art.classList.remove('has-img');
        }
      }
    }
    artVersion = newArt;
    tickProgress();
  }

  function tickProgress() {
    const full = mediaEls.find((x) => x.kind === 'full');
    if (!full) return;
    const bar = full.root.querySelector('.m-bar');
    const posEl = full.root.querySelector('.m-pos');
    const durEl = full.root.querySelector('.m-dur');
    if (!media || !media.durationMs) {
      bar.style.width = '0';
      posEl.textContent = '0:00';
      durEl.textContent = media ? '' : '0:00';
      return;
    }
    const elapsed = media.isPlaying ? performance.now() - mediaAt : 0;
    const pos = Math.min(media.durationMs, media.positionMs + elapsed);
    bar.style.width = `${(pos / media.durationMs) * 100}%`;
    posEl.textContent = formatDuration(pos);
    durEl.textContent = formatDuration(media.durationMs);
  }

  setInterval(tickProgress, 250);

  return { renderLayout, updateSnapshot, updateMedia, get tiles() { return tiles; } };
}
