import { test } from 'node:test';
import assert from 'node:assert/strict';
import { isIOS, deviceName, keepAwakeHint } from '../../src/TelefonPaneli.App/wwwroot/js/platform.js';

const IPHONE = 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Mobile/15E148 Safari/604.1';
// iPadOS 13+ kendini masaüstü Mac gibi tanıtır; dokunmatik nokta sayısıyla ayırt edilir.
const IPAD = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Safari/605.1.15';
const ANDROID = 'Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Mobile Safari/537.36';
const ANDROID_REDUCED = 'Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Mobile Safari/537.36';

test('iPhone ve iPad iOS sayılır, Mac ve Android sayılmaz', () => {
  assert.equal(isIOS(IPHONE, 5), true);
  assert.equal(isIOS(IPAD, 5), true);
  assert.equal(isIOS(IPAD, 0), false, 'dokunmatiksiz Mac');
  assert.equal(isIOS(ANDROID, 5), false);
});

test('cihaz adı', () => {
  assert.equal(deviceName(IPHONE, 5), 'iPhone');
  assert.equal(deviceName(IPAD, 5), 'iPad');
  assert.equal(deviceName(ANDROID, 5), 'SM-S918B');
  assert.equal(deviceName(ANDROID_REDUCED, 5), 'Android telefon');
  assert.equal(deviceName('bilinmeyen', 0), 'Telefon');
});

test('ekranı açık tutma ipucu platforma göre', () => {
  assert.match(keepAwakeHint(IPHONE, 5), /Otomatik Kilit/);
  assert.match(keepAwakeHint(ANDROID, 5), /Uyanık kal/);
});
