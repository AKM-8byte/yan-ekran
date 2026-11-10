import { test } from 'node:test';
import assert from 'node:assert/strict';
import { nextDelay, createConnection } from '../../src/TelefonPaneli.App/wwwroot/js/connection.js';

class FakeWs {
  static all = [];
  static OPEN = 1;
  constructor(url) { this.url = url; this.sent = []; this.readyState = 0; FakeWs.all.push(this); }
  send(d) { this.sent.push(d); }
  close() { this.readyState = 3; this.onclose?.({ code: 1000 }); }
  open() { this.readyState = 1; this.onopen?.(); }
  message(obj) { this.onmessage?.({ data: JSON.stringify(obj) }); }
  drop(code = 1006) { this.readyState = 3; this.onclose?.({ code }); }
}

function setup() {
  FakeWs.all = [];
  const timers = [];
  const statuses = [];
  const messages = [];
  const conn = createConnection({
    url: 'ws://pc/ws?token=t',
    onMessage: (m) => messages.push(m),
    onStatus: (s) => statuses.push(s),
    WebSocketImpl: FakeWs,
    setTimeoutImpl: (fn, ms) => timers.push({ fn, ms }),
    setIntervalImpl: () => {},
  });
  return { conn, timers, statuses, messages };
}

test('yeniden bağlanma gecikmesi 5 snde tavan yapar', () =>
  assert.deepEqual([0, 1, 2, 3, 7].map(nextDelay), [1000, 2000, 4000, 5000, 5000]));

test('açılınca connected ve mesajlar ayrıştırılır', () => {
  const { statuses, messages } = setup();
  FakeWs.all[0].open();
  FakeWs.all[0].message({ type: 'snapshot', metrics: {} });
  assert.equal(statuses.at(-1), 'connected');
  assert.equal(messages[0].type, 'snapshot');
});

test('bozuk mesaj yok sayılır', () => {
  const { messages } = setup();
  FakeWs.all[0].open();
  FakeWs.all[0].onmessage({ data: '{ bozuk' });
  assert.equal(messages.length, 0);
});

test('4401 ile kapanınca unpaired ve tekrar denemez', () => {
  const { statuses, timers } = setup();
  FakeWs.all[0].open();
  FakeWs.all[0].drop(4401);
  assert.equal(statuses.at(-1), 'unpaired');
  assert.equal(timers.length, 0);
});

test('ağ kopunca reconnecting ve gecikmeyle tekrar dener', () => {
  const { statuses, timers } = setup();
  FakeWs.all[0].drop();
  assert.equal(statuses.at(-1), 'reconnecting');
  assert.equal(timers[0].ms, 1000);
  timers[0].fn();
  assert.equal(FakeWs.all.length, 2);
  FakeWs.all[1].drop();
  assert.equal(timers[1].ms, 2000);
  timers[1].fn();
  FakeWs.all[2].open();
  FakeWs.all[2].drop();
  assert.equal(timers[2].ms, 1000, 'başarılı bağlantıdan sonra gecikme sıfırlanır');
});

test('bağlantı yokken gönderilenler sıraya alınır ve açılınca gider', () => {
  const { conn } = setup();
  conn.send({ type: 'media', action: 'next' });
  assert.equal(FakeWs.all[0].sent.length, 0);
  FakeWs.all[0].open();
  assert.deepEqual(FakeWs.all[0].sent.map((s) => JSON.parse(s).action), ['next']);
});

test('close sonrası yeniden bağlanmaz', () => {
  const { conn, timers } = setup();
  conn.close();
  assert.equal(timers.length, 0);
});

test('mesaj gelmeyen açık bağlantı 3 sn sonra kapatılıp yeniden denenir', () => {
  FakeWs.all = [];
  let now = 0;
  const ticks = [];
  const statuses = [];
  createConnection({
    url: 'ws://pc/ws',
    onMessage: () => {},
    onStatus: (s) => statuses.push(s),
    WebSocketImpl: FakeWs,
    setTimeoutImpl: () => {},
    setIntervalImpl: (fn) => ticks.push(fn),
    nowImpl: () => now,
  });
  FakeWs.all[0].open();
  now = 2000; ticks.forEach((f) => f());
  assert.equal(statuses.at(-1), 'connected');
  FakeWs.all[0].message({ type: 'snapshot', metrics: {} });
  now = 4500; ticks.forEach((f) => f());
  assert.equal(statuses.at(-1), 'connected', 'mesaj geldiği için süre sıfırlandı');
  now = 5500; ticks.forEach((f) => f());
  assert.equal(statuses.at(-1), 'reconnecting');
});
