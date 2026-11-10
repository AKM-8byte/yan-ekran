import { test } from 'node:test';
import assert from 'node:assert/strict';
import { formatValue, levelClass, metricLabel, formatDuration, mediaText, AVAILABLE_TILES, requiresDriver } from '../../src/TelefonPaneli.App/wwwroot/js/format.js';

test('null değer tire olur', () => {
  assert.equal(formatValue('cpu.temp', null), '—');
  assert.equal(formatValue('fps', undefined), '—');
});

test('sıfır tire değildir', () => assert.equal(formatValue('cpu.load', 0), '%0'));

test('birimler', () => {
  assert.equal(formatValue('cpu.temp', 62.4), '62°');
  assert.equal(formatValue('disk.0.temp', 41), '41°');
  assert.equal(formatValue('gpu.load', 97.2), '%97');
  assert.equal(formatValue('ram.used', 11.234), '11.2');
  assert.equal(formatValue('gpu.vram.used', 7.06), '7.1');
  assert.equal(formatValue('net.down', 12.44), '12.4');
  assert.equal(formatValue('ping', 18.7), '19');
  assert.equal(formatValue('fps', 143.6), '144');
  assert.equal(formatValue('fps.low1', 98), '98');
  assert.equal(formatValue('cpu.clock', 4890), '4.9');
  assert.equal(formatValue('gpu.clock', 1590), '1590');
  assert.equal(formatValue('gpu.power', 48.6), '49');
  assert.equal(formatValue('fan.0', 1450.4), '1450');
});

test('etiketler', () => {
  assert.equal(metricLabel('cpu.temp'), 'CPU');
  assert.equal(metricLabel('gpu.temp'), 'GPU');
  assert.equal(metricLabel('fps'), 'FPS');
  assert.equal(metricLabel('fps.low1'), '1% low');
  assert.equal(metricLabel('fan.0'), 'Fan 1');
  assert.equal(metricLabel('disk.1.temp'), 'Disk 2');
  assert.equal(metricLabel('ping'), 'Ping');
  assert.equal(metricLabel('bilinmeyen.x'), 'bilinmeyen.x');
});

test('eşikler', () => {
  assert.equal(levelClass(69, 70, 85), 'ok');
  assert.equal(levelClass(70, 70, 85), 'warn');
  assert.equal(levelClass(85, 70, 85), 'hot');
  assert.equal(levelClass(50, undefined, undefined), '');
  assert.equal(levelClass(null, 70, 85), '');
  assert.equal(levelClass(90, null, 85), 'hot');
});

test('süre biçimi', () => {
  assert.equal(formatDuration(94000), '1:34');
  assert.equal(formatDuration(5000), '0:05');
  assert.equal(formatDuration(3723000), '1:02:03');
  assert.equal(formatDuration(null), '0:00');
  assert.equal(formatDuration(-5), '0:00');
});

test('medya yoksa', () => {
  assert.deepEqual(mediaText(null), { title: 'Çalan bir şey yok', subtitle: '' });
  assert.deepEqual(mediaText({ title: 'Blinding Lights', artist: 'The Weeknd', app: 'Spotify' }), { title: 'Blinding Lights', subtitle: 'The Weeknd' });
  assert.equal(mediaText({ title: '', artist: '', app: 'Spotify' }).title, 'Bilinmeyen parça');
});

test('eklenebilir kutucuk listesi', () => {
  const kinds = new Set(AVAILABLE_TILES.map((t) => t.kind));
  assert.ok(kinds.has('metric') && kinds.has('chart') && kinds.has('mediaStrip') && kinds.has('mediaFull'));
  assert.ok(AVAILABLE_TILES.some((t) => t.metric === 'fps'));
  assert.ok(AVAILABLE_TILES.every((t) => t.label));
});

test('sürücü gerektiren metrikler C# ile aynı', () => {
  for (const id of ['cpu.temp', 'cpu.clock', 'fan.3', 'disk.1.temp']) assert.equal(requiresDriver(id), true, id);
  for (const id of ['gpu.temp', 'fps', 'cpu.load']) assert.equal(requiresDriver(id), false, id);
});
