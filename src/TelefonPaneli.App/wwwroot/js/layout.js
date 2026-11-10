// Düzen üzerinde saf işlemler. Hiçbiri girdiyi değiştirmez; hep yeni nesne döner.
// Sınırlar C# LayoutValidator ile aynıdır.

export const MAX_PAGES = 10;
export const MAX_TILES = 24;
const SIZES = new Set(['S', 'M', 'L']);

let counter = 0;
export function newId() {
  counter += 1;
  return `t-${Date.now().toString(36)}-${counter.toString(36)}-${Math.random().toString(36).slice(2, 6)}`;
}

const clone = (o) => JSON.parse(JSON.stringify(o));

function mapPage(layout, pageId, fn) {
  const next = clone(layout);
  next.pages = next.pages.map((p) => (p.id === pageId ? fn(p) : p));
  return next;
}

function mapTile(layout, pageId, tileId, fn) {
  return mapPage(layout, pageId, (p) => ({ ...p, tiles: p.tiles.map((t) => (t.id === tileId ? fn(t) : t)) }));
}

export function addTile(layout, pageId, tile) {
  return mapPage(layout, pageId, (p) => {
    if (p.tiles.length >= MAX_TILES) return p;
    const { label, ...rest } = tile;
    return { ...p, tiles: [...p.tiles, { ...rest, id: newId(), size: SIZES.has(rest.size) ? rest.size : 'M' }] };
  });
}

export function removeTile(layout, pageId, tileId) {
  return mapPage(layout, pageId, (p) => ({ ...p, tiles: p.tiles.filter((t) => t.id !== tileId) }));
}

export function moveTile(layout, pageId, fromIndex, toIndex) {
  return mapPage(layout, pageId, (p) => {
    const tiles = [...p.tiles];
    if (fromIndex < 0 || fromIndex >= tiles.length) return p;
    const [moved] = tiles.splice(fromIndex, 1);
    const to = Math.max(0, Math.min(tiles.length, toIndex));
    tiles.splice(to, 0, moved);
    return { ...p, tiles };
  });
}

export function resizeTile(layout, pageId, tileId, size) {
  return mapTile(layout, pageId, tileId, (t) => ({ ...t, size: SIZES.has(size) ? size : 'M' }));
}

const num = (v) => {
  if (v === '' || v === null || v === undefined) return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
};

export function setThresholds(layout, pageId, tileId, warn, crit) {
  return mapTile(layout, pageId, tileId, (t) => ({ ...t, warn: num(warn), crit: num(crit) }));
}

export function addPage(layout, name) {
  const next = clone(layout);
  if (next.pages.length >= MAX_PAGES) return next;
  next.pages.push({ id: newId().replace(/^t-/, 'p-'), name: (name || `Sayfa ${next.pages.length + 1}`).slice(0, 30), tiles: [] });
  return next;
}

export function renamePage(layout, pageId, name) {
  return mapPage(layout, pageId, (p) => ({ ...p, name: (name || p.name).trim().slice(0, 30) || p.name }));
}

export function removePage(layout, pageId) {
  const next = clone(layout);
  if (next.pages.length <= 1) return next;
  next.pages = next.pages.filter((p) => p.id !== pageId);
  return next;
}
