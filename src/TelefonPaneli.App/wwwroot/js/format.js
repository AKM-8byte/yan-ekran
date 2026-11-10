// Değer biçimlendirme, etiketler ve eşik renkleri. Metrik kimlikleri C# MetricIds ile aynıdır.

const DASH = '—';

const LABELS = {
  'cpu.temp': 'CPU', 'cpu.load': 'CPU kullanım', 'cpu.clock': 'CPU saat',
  'gpu.temp': 'GPU', 'gpu.load': 'GPU kullanım', 'gpu.power': 'GPU güç', 'gpu.clock': 'GPU saat',
  'gpu.vram.used': 'VRAM', 'gpu.vram.total': 'VRAM toplam',
  'ram.used': 'RAM', 'ram.total': 'RAM toplam',
  'fps': 'FPS', 'fps.low1': '1% low',
  'net.down': 'İndirme', 'net.up': 'Yükleme', 'ping': 'Ping',
};

const UNITS = {
  'cpu.load': '%', 'gpu.load': '%',
  'cpu.clock': 'GHz', 'gpu.clock': 'MHz', 'gpu.power': 'W',
  'gpu.vram.used': 'GB', 'ram.used': 'GB',
  'net.down': 'MB/s', 'net.up': 'MB/s', 'ping': 'ms',
  'fps': '', 'fps.low1': 'fps',
};

export function requiresDriver(id) {
  return id === 'cpu.temp' || id === 'cpu.clock' || id.startsWith('fan.') || id.startsWith('disk.');
}

const isTemp = (id) => id.endsWith('.temp');

export function formatValue(id, value) {
  if (value === null || value === undefined || !Number.isFinite(value)) return DASH;
  if (isTemp(id)) return `${Math.round(value)}°`;
  switch (id) {
    case 'cpu.load':
    case 'gpu.load':
      return `%${Math.round(value)}`;
    case 'cpu.clock':
      return (value / 1000).toFixed(1);
    case 'ram.used':
    case 'ram.total':
    case 'gpu.vram.used':
    case 'gpu.vram.total':
    case 'net.down':
    case 'net.up':
      return value.toFixed(1);
    default:
      return String(Math.round(value));
  }
}

export function metricUnit(id) {
  if (isTemp(id)) return '';
  if (id.startsWith('fan.')) return 'RPM';
  return UNITS[id] ?? '';
}

export function metricLabel(id) {
  if (LABELS[id]) return LABELS[id];
  let m = /^fan\.(\d+)$/.exec(id);
  if (m) return `Fan ${Number(m[1]) + 1}`;
  m = /^disk\.(\d+)\.temp$/.exec(id);
  if (m) return `Disk ${Number(m[1]) + 1}`;
  return id;
}

/** Değer eşiğe göre: 'ok' | 'warn' | 'hot'; eşik yoksa ''. */
export function levelClass(value, warn, crit) {
  if (value === null || value === undefined || !Number.isFinite(value)) return '';
  const hasWarn = Number.isFinite(warn);
  const hasCrit = Number.isFinite(crit);
  if (!hasWarn && !hasCrit) return '';
  if (hasCrit && value >= crit) return 'hot';
  if (hasWarn && value >= warn) return 'warn';
  return 'ok';
}

export function formatDuration(ms) {
  if (!Number.isFinite(ms) || ms <= 0) return '0:00';
  const total = Math.floor(ms / 1000);
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = String(total % 60).padStart(2, '0');
  return h > 0 ? `${h}:${String(m).padStart(2, '0')}:${s}` : `${m}:${s}`;
}

export function mediaText(state) {
  if (!state) return { title: 'Çalan bir şey yok', subtitle: '' };
  return { title: state.title || 'Bilinmeyen parça', subtitle: state.artist || '' };
}

const temp = (metric, label) => ({ kind: 'metric', metric, label, size: 'M', warn: 70, crit: 85 });
const plain = (metric, label, size = 'S') => ({ kind: 'metric', metric, label, size });

/** "Kutucuk ekle" listesi. */
export const AVAILABLE_TILES = [
  temp('cpu.temp', 'CPU sıcaklığı'),
  temp('gpu.temp', 'GPU sıcaklığı'),
  { kind: 'metric', metric: 'fps', label: 'FPS', size: 'L' },
  plain('fps.low1', '1% low FPS'),
  plain('cpu.load', 'CPU kullanımı'),
  plain('gpu.load', 'GPU kullanımı'),
  plain('cpu.clock', 'CPU saat hızı'),
  plain('gpu.clock', 'GPU saat hızı'),
  plain('gpu.power', 'GPU gücü'),
  plain('ram.used', 'RAM'),
  plain('gpu.vram.used', 'VRAM'),
  plain('fan.0', 'Fan 1'),
  plain('fan.1', 'Fan 2'),
  { kind: 'metric', metric: 'disk.0.temp', label: 'Disk 1 sıcaklığı', size: 'S', warn: 60, crit: 70 },
  plain('net.down', 'İndirme hızı'),
  plain('net.up', 'Yükleme hızı'),
  { kind: 'metric', metric: 'ping', label: 'Ping', size: 'S', warn: 60, crit: 120 },
  { kind: 'chart', metric: 'cpu.load', label: 'CPU grafiği (60 sn)', size: 'M' },
  { kind: 'chart', metric: 'gpu.temp', label: 'GPU sıcaklık grafiği', size: 'M' },
  { kind: 'chart', metric: 'fps', label: 'FPS grafiği', size: 'M' },
  { kind: 'mediaStrip', label: 'Şarkı şeridi', size: 'M' },
  { kind: 'mediaFull', label: 'Müzik (tam sayfa)', size: 'L' },
];
