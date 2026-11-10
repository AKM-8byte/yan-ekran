import { test } from 'node:test';
import assert from 'node:assert/strict';
import { swipeDecision } from '../../src/TelefonPaneli.App/wwwroot/js/pager.js';

const W = 800;

test('kısa ve yavaş sürükleme sayfa değiştirmez', () => assert.equal(swipeDecision(-60, 600, W), 0));

test('yavaş ama ekranın %15inden uzun sürükleme sayfa değiştirir', () => {
  assert.equal(swipeDecision(-130, 700, W), 1);
  assert.equal(swipeDecision(130, 700, W), -1);
});

test('hızlı kısa fiske sayfa değiştirir', () => {
  assert.equal(swipeDecision(-45, 150, W), 1);
  assert.equal(swipeDecision(45, 150, W), -1);
});

test('çok kısa dokunma fiske sayılmaz', () => assert.equal(swipeDecision(-15, 80, W), 0));
