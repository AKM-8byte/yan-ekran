// Telefonun platformuna göre küçük farklar (iPhone/iPad deneysel destek).

/** iPhone, iPod veya iPad mi? iPadOS 13+ kendini Mac gibi tanıtır; dokunmatik ekranıyla ayırt edilir. */
export function isIOS(ua = navigator.userAgent, maxTouchPoints = navigator.maxTouchPoints) {
  if (/iPhone|iPad|iPod/.test(ua)) return true;
  return /Macintosh/.test(ua) && maxTouchPoints > 1;
}

/** Eşleşen cihazın bilgisayarda görünecek adı. */
export function deviceName(ua = navigator.userAgent, maxTouchPoints = navigator.maxTouchPoints) {
  if (isIOS(ua, maxTouchPoints)) return /iPhone|iPod/.test(ua) ? 'iPhone' : 'iPad';
  const m = /Android[^;)]*;\s*([^;)]+?)(?:\s+Build|\))/.exec(ua);
  if (m) return (m[1] && m[1] !== 'K' ? m[1] : 'Android telefon').slice(0, 40);
  return 'Telefon';
}

/** Tarayıcı ekranı açık tutamazsa gösterilecek ipucu. */
export function keepAwakeHint(ua = navigator.userAgent, maxTouchPoints = navigator.maxTouchPoints) {
  return isIOS(ua, maxTouchPoints)
    ? 'Ekranın kapanmaması için: Ayarlar → Ekran ve Parlaklık → Otomatik Kilit → "Asla"'
    : 'Ekranın kapanmaması için: telefonu şarja tak ve Geliştirici seçenekleri → "Uyanık kal"ı aç';
}
