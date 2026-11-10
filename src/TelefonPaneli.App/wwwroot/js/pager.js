// Yatay kaydırmalı sayfalar. Eşik: ekran genişliğinin %20'si.

export function createPager(container, pageCount, onChange, startIndex = 0) {
  const track = container.querySelector('.track');
  const dots = container.querySelector('.dots');
  let index = Math.max(0, Math.min(pageCount - 1, startIndex));
  let enabled = true;
  let startX = 0, startY = 0, dx = 0, dragging = false, decided = false, horizontal = false;

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
    startX = e.clientX; startY = e.clientY; dx = 0;
  });

  container.addEventListener('pointermove', (e) => {
    if (!dragging) return;
    dx = e.clientX - startX;
    const dy = e.clientY - startY;
    if (!decided && (Math.abs(dx) > 8 || Math.abs(dy) > 8)) {
      decided = true;
      horizontal = Math.abs(dx) > Math.abs(dy);
      if (horizontal) {
        track.classList.add('dragging');
        container.setPointerCapture?.(e.pointerId);
      }
    }
    if (horizontal) {
      const w = container.clientWidth || 1;
      const edge = (index === 0 && dx > 0) || (index === pageCount - 1 && dx < 0) ? 0.3 : 1;
      track.style.transform = `translateX(${-index * 100 + (dx * edge * 100) / w}%)`;
    }
  });

  const end = () => {
    if (!dragging) return;
    dragging = false;
    track.classList.remove('dragging');
    if (horizontal) {
      const w = container.clientWidth || 1;
      if (dx < -w * 0.2) go(index + 1);
      else if (dx > w * 0.2) go(index - 1);
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
