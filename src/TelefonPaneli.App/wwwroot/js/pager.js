// Yatay kaydırmalı sayfalar.

/**
 * Sürüklemenin sonunda sayfa değişsin mi? 1 = sonraki, -1 = önceki, 0 = kal.
 * Ekranın %15'inden uzun sürükleme ya da hızlı bir fiske (≥40 px, ≥0.3 px/ms) sayfayı değiştirir.
 */
export function swipeDecision(dx, dtMs, width) {
  const dist = Math.abs(dx);
  const fast = dist >= 40 && dtMs > 0 && dist / dtMs >= 0.3;
  if (dist < width * 0.15 && !fast) return 0;
  return dx < 0 ? 1 : -1;
}

export function createPager(container, pageCount, onChange, startIndex = 0) {
  const track = container.querySelector('.track');
  const dots = container.querySelector('.dots');
  let index = Math.max(0, Math.min(pageCount - 1, startIndex));
  let enabled = true;
  let startX = 0, startY = 0, startT = 0, dx = 0, dragging = false, decided = false, horizontal = false;

  function apply() {
    track.style.transform = `translateX(${-index * 100}%)`;
    dots.innerHTML = Array.from({ length: pageCount }, (_, i) => (i === index ? '<b>●</b>' : '●')).join(' ');
    dots.hidden = pageCount < 2;
  }

  function go(i) {
    const next = Math.max(0, Math.min(pageCount - 1, i));
    const changed = next !== index;
    index = next;
    apply();
    if (changed) onChange(index);
  }

  container.addEventListener('pointerdown', (e) => {
    if (!enabled || e.button > 0) return;
    dragging = true; decided = false; horizontal = false;
    startX = e.clientX; startY = e.clientY; startT = e.timeStamp; dx = 0;
  });

  container.addEventListener('pointermove', (e) => {
    if (!dragging) return;
    dx = e.clientX - startX;
    const dy = e.clientY - startY;
    if (!decided && (Math.abs(dx) > 6 || Math.abs(dy) > 6)) {
      decided = true;
      // Yatay hareket hafif çapraz olsa da kaydırma sayılsın.
      horizontal = Math.abs(dx) >= Math.abs(dy) * 0.7;
      if (horizontal) {
        track.classList.add('dragging');
        try { container.setPointerCapture(e.pointerId); } catch { /* takip olmadan da çalışır */ }
      }
    }
    if (horizontal) {
      const w = container.clientWidth || 1;
      const edge = (index === 0 && dx > 0) || (index === pageCount - 1 && dx < 0) ? 0.3 : 1;
      track.style.transform = `translateX(${-index * 100 + (dx * edge * 100) / w}%)`;
    }
  });

  const end = (e) => {
    if (!dragging) return;
    dragging = false;
    track.classList.remove('dragging');
    if (horizontal) {
      const step = swipeDecision(dx, (e?.timeStamp ?? startT) - startT, container.clientWidth || 1);
      if (step !== 0) go(index + step);
      else apply();
    }
  };
  container.addEventListener('pointerup', end);
  container.addEventListener('pointercancel', end);

  apply();

  return {
    get index() { return index; },
    go,
    setEnabled(v) { enabled = v; if (!v) { dragging = false; apply(); } },
  };
}
