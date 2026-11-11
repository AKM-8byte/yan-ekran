// 4 haneli eşleştirme ekranı. Başarılı olunca token'ı döner.

export function showPairing(root, { fetchImpl = fetch } = {}) {
  return new Promise((resolve) => {
    root.innerHTML = `
      <div class="pair">
        <div>
          <h1>YanEkran</h1>
          <p>Bilgisayarda saatin yanındaki simgeye tıkla ve görünen 4 haneli eşleştirme kodunu gir.</p>
          <div class="digits"><span></span><span></span><span></span><span></span></div>
          <div class="msg" role="status"></div>
        </div>
        <div class="pad">
          ${[1, 2, 3, 4, 5, 6, 7, 8, 9].map((n) => `<button data-d="${n}">${n}</button>`).join('')}
          <button data-act="clear" aria-label="Temizle">✕</button>
          <button data-d="0">0</button>
          <button data-act="back" aria-label="Sil">⌫</button>
        </div>
      </div>`;

    const boxes = [...root.querySelectorAll('.digits span')];
    const msg = root.querySelector('.msg');
    const digitsEl = root.querySelector('.digits');
    let code = '';
    let busy = false;

    const draw = () => boxes.forEach((b, i) => { b.textContent = code[i] ?? ''; });

    async function submit() {
      busy = true;
      msg.textContent = '';
      try {
        const res = await fetchImpl('/api/pair', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ code, deviceName: deviceName() }),
        });
        if (res.ok) {
          const { token } = await res.json();
          resolve(token);
          return;
        }
        msg.textContent = res.status === 429 ? 'Çok fazla deneme, biraz bekle'
          : res.status === 403 ? 'Eşleştirme kapalı — bilgisayarda simgeye tıklayıp kodu göster'
          : 'Kod yanlış — bilgisayardaki kodu kontrol et';
      } catch {
        msg.textContent = 'Bilgisayara ulaşılamadı';
      }
      digitsEl.classList.remove('shake');
      void digitsEl.offsetWidth;
      digitsEl.classList.add('shake');
      code = '';
      draw();
      busy = false;
    }

    root.querySelector('.pad').addEventListener('click', (e) => {
      const b = e.target.closest('button');
      if (!b || busy) return;
      if (b.dataset.act === 'back') code = code.slice(0, -1);
      else if (b.dataset.act === 'clear') code = '';
      else if (code.length < 4) code += b.dataset.d;
      draw();
      if (code.length === 4) submit();
    });
  });
}

function deviceName() {
  const m = /Android[^;)]*;\s*([^;)]+?)(?:\s+Build|\))/.exec(navigator.userAgent);
  return (m && m[1] && m[1] !== 'K' ? m[1] : 'Android telefon').slice(0, 40);
}
