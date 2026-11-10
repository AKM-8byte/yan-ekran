import { test } from 'node:test';
import assert from 'node:assert/strict';
import { addTile, removeTile, moveTile, resizeTile, setThresholds, addPage, removePage, renamePage, newId } from '../../src/TelefonPaneli.App/wwwroot/js/layout.js';

const base = () => ({
  version: 1,
  pages: [
    {
      id: 'p1', name: 'Oyun', tiles: [
        { id: 'a', kind: 'metric', metric: 'cpu.temp', size: 'M' },
        { id: 'b', kind: 'metric', metric: 'gpu.temp', size: 'M' },
        { id: 'c', kind: 'metric', metric: 'fps', size: 'L' },
      ],
    },
    { id: 'p2', name: 'Müzik', tiles: [] },
  ],
});
const clone = (o) => JSON.parse(JSON.stringify(o));

test('moveTile sırayı değiştirir ve girdiyi bozmaz', () => {
  const l = base();
  const before = clone(l);
  const r = moveTile(l, 'p1', 0, 2);
  assert.deepEqual(r.pages[0].tiles.map((t) => t.id), ['b', 'c', 'a']);
  assert.deepEqual(l, before);
});

test('moveTile sınır dışı indeksleri sıkıştırır', () => {
  assert.deepEqual(moveTile(base(), 'p1', 2, 99).pages[0].tiles.map((t) => t.id), ['a', 'b', 'c']);
  assert.deepEqual(moveTile(base(), 'p1', 2, -5).pages[0].tiles.map((t) => t.id), ['c', 'a', 'b']);
});

test('addTile sona ekler ve id verir', () => {
  const r = addTile(base(), 'p2', { kind: 'mediaFull', size: 'L' });
  assert.equal(r.pages[1].tiles.length, 1);
  assert.ok(r.pages[1].tiles[0].id);
});

test('addTile 24 kutucukta durur', () => {
  let l = base();
  for (let i = 0; i < 30; i++) l = addTile(l, 'p2', { kind: 'metric', metric: 'fps', size: 'S' });
  assert.equal(l.pages[1].tiles.length, 24);
});

test('removeTile', () => assert.deepEqual(removeTile(base(), 'p1', 'b').pages[0].tiles.map((t) => t.id), ['a', 'c']));

test('resizeTile yalnızca S/M/L kabul eder', () => {
  assert.equal(resizeTile(base(), 'p1', 'a', 'S').pages[0].tiles[0].size, 'S');
  assert.equal(resizeTile(base(), 'p1', 'a', 'XL').pages[0].tiles[0].size, 'M');
});

test('setThresholds sayı olmayanları temizler', () => {
  const t = setThresholds(base(), 'p1', 'a', 60, '').pages[0].tiles[0];
  assert.equal(t.warn, 60);
  assert.equal(t.crit, null);
});

test('addPage ve renamePage', () => {
  const r = renamePage(addPage(base(), 'Yeni'), 'p1', 'Maç');
  assert.equal(r.pages.length, 3);
  assert.equal(r.pages[2].name, 'Yeni');
  assert.equal(r.pages[0].name, 'Maç');
});

test('addPage 10 sayfada durur', () => {
  let l = base();
  for (let i = 0; i < 20; i++) l = addPage(l, 'x');
  assert.equal(l.pages.length, 10);
});

test('removePage', () => assert.deepEqual(removePage(base(), 'p1').pages.map((p) => p.id), ['p2']));

test('son sayfa silinemez', () => {
  const one = removePage(base(), 'p1');
  assert.equal(removePage(one, 'p2').pages.length, 1);
});

test('newId benzersiz', () => assert.notEqual(newId(), newId()));
