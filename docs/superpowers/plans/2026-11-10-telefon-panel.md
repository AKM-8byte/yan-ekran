# Telefon Paneli Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bilgisayarın sensör/FPS/medya verilerini yerel ağ üzerinden Android telefondaki düzenlenebilir, kaydırmalı bir PWA ekranında canlı göstermek ve müziği telefondan kontrol etmek.

**Architecture:** Tek bir Windows tepsi uygulaması (.NET 10, WinForms `NotifyIcon`) aynı süreçte Kestrel çalıştırır: veri kaynakları (`IMetricSource`) `MetricHub`'a yazar, sunucu 500 ms'de bir anlık görüntüyü WebSocket ile yayınlar ve telefon sayfasını (`wwwroot`, derleme adımı olmayan vanilla JS ES modülleri) sunar. Saf mantık (`TelefonPaneli.Core`) Windows'tan bağımsızdır ve xUnit ile; web tarafındaki saf modüller `node --test` ile test edilir. Donanım/Windows entegrasyonu (`TelefonPaneli.Windows`) gerçek makinede elle doğrulanır.

**Tech Stack:** .NET 10 SDK, ASP.NET Core (Kestrel, WebSockets), WinForms, LibreHardwareMonitorLib (PawnIO kullanan sürüm), PresentMon CLI, Windows.Media.Control (WinRT), NAudio.Wasapi, QRCoder, xUnit, Microsoft.AspNetCore.Mvc.Testing, Node 24 (`node --test`).

**Spec:** `docs/superpowers/specs/2026-11-10-telefon-panel-design.md`

## Global Constraints

- Hedef makine: Windows 11, Intel i7-10750H, NVIDIA GTX 1650 Ti + Intel UHD (Optimus). AMD yolları kapsam dışı.
- .NET 10 (spec'teki .NET 8 yerine; 8'in desteği Kasım 2026'da bitti). App/Windows projeleri `net10.0-windows10.0.19041.0`, Core `net10.0`.
- Program **Windows ile otomatik başlamaz**; kayıt defteri/Startup klasörüne hiçbir şey yazılmaz.
- Program yönetici olarak çalışır (`app.manifest` → `requireAdministrator`).
- Sabit port: **5050** (HTTP + WS). Yalnızca yerel ağ adresleri kabul edilir.
- Yayın aralığı: anlık görüntü **500 ms**; medya değişikliği anında ayrı mesaj.
- Kaynak bütçesi: ortalama **< %1 CPU, < 150 MB RAM**.
- FIFA modunda (`--fifa`) PawnIO sürücüsü **yüklü olmaz**; program kapalıyken de yüklü kalmaz (servis durdurulur, başlangıç türü "manual").
- Ses tuşu yalnızca medya kaynağı uygulamanın sesini değiştirir, ana sese dokunmaz.
- Ayarlar: `%AppData%\TelefonPaneli\settings.json`.
- Tüm kullanıcıya görünen metinler Türkçe. Telefon arayüzü koyu tema, yatay.
- Okunamayan değer `"—"` gösterilir, asla `0` gösterilmez.

## Review Focus

1. **Telefon Wi‑Fi'ı kaybeder veya PC programı yeniden başlar** → telefon kendiliğinden yeniden bağlanır, eşleştirme kodu tekrar sorulmaz, düzen aynı kalır. (Task 6: yeniden başlatılan host eski token'ı kabul eder; Task 12: yeniden bağlanma gecikmesi.)
2. **Sensör yok/okunamıyor** (dizüstü fanı, uykudaki dGPU, FIFA modu, kaynak exception) → ilgili kutucuk `"—"`, uygulama çökmez, diğer değerler akmaya devam eder. (Task 2, Task 12.)
3. **Çalan medya yok / medya uygulaması şarkı ortasında kapanır** → medya kutucukları "Çalan bir şey yok" der, komut tuşları hata vermez. (Task 6, Task 12.)
4. **"Eşleşmiş cihazları unut" sonrası eski telefon bağlanmaya çalışır** → 401/kapatma, telefon eşleştirme ekranına döner. (Task 6.)
5. **Bozuk veya eski sürüm `settings.json`** → varsayılanlarla açılır, bozuk dosya `.bak` olarak saklanır. (Task 3.)

---

## Dosya Yapısı

```
TelefonPaneli.sln
.gitignore                                  (bin/ obj/ .superpowers/ tools/presentmon/*.exe)
src/TelefonPaneli.Core/
  Metrics/MetricIds.cs          metrik kimlikleri + DriverMetrics kümesi
  Metrics/IMetricSource.cs      kaynak arayüzü
  Metrics/MetricHub.cs          kaynakları toplar → Snapshot
  Metrics/Snapshot.cs
  Metrics/FpsCalculator.cs      frametime → fps / %1 low
  Metrics/PresentMonCsv.cs      CSV satır ayrıştırıcı
  Media/MediaState.cs           medya durumu + komut enum'u
  Layout/LayoutModel.cs         Layout/Page/Tile kayıtları + DefaultLayout
  Layout/LayoutValidator.cs
  Settings/AppSettings.cs
  Settings/SettingsStore.cs     JSON yükle/kaydet, bozuk dosya kurtarma
  Security/PairingService.cs    4 haneli kod + token
  Security/NetworkGuard.cs      IP yerel mi?
  Protocol/Messages.cs          WS mesajları (System.Text.Json)
src/TelefonPaneli.Windows/
  Sensors/PerfCounterSource.cs  cpu.load, ram.*, net.*
  Sensors/PingSource.cs
  Sensors/LhmSource.cs          LHM; mod'a göre donanım grupları
  Sensors/GpuActivityGate.cs    dGPU uyanık mı (perf sayacı)
  Driver/IServiceControl.cs, PawnIoDriver.cs
  Games/GameWatcher.cs          FIFA süreci + ön plan süreci
  Fps/PresentMonSource.cs
  Media/SmtcMediaSource.cs
  Media/AppVolume.cs
src/TelefonPaneli.App/
  Program.cs                    argümanlar, mod, host + tray başlatma
  app.manifest
  Server/WebServer.cs           Kestrel, endpoint'ler, WS yayını
  Tray/TrayApp.cs, Tray/QrForm.cs
  Setup/Firewall.cs
  wwwroot/index.html, styles.css, manifest.webmanifest, icon-512.png
  wwwroot/js/format.js layout.js connection.js pager.js render.js editor.js pairing.js main.js
scripts/create-shortcuts.ps1
tools/presentmon/README.md      PresentMon exe'nin nereye konacağı
tests/TelefonPaneli.Core.Tests/
tests/TelefonPaneli.App.Tests/
tests/web/*.test.mjs
```

## Ortak sözleşmeler (tüm görevler bunları kullanır)

**Metrik kimlikleri** (`MetricIds`, `string` sabitleri; web tarafı aynı dizeleri kullanır):
`cpu.temp, cpu.load, cpu.clock, gpu.temp, gpu.load, gpu.power, gpu.clock, gpu.vram.used, gpu.vram.total, ram.used, ram.total, fps, fps.low1, net.down, net.up, ping` + dinamik `fan.<n>`, `disk.<n>.temp`.
Birimler: °C, %, MHz, W, GB, MB/s, ms.
`DriverMetrics` = `cpu.temp, cpu.clock` + `fan.*` + `disk.*` (önek eşleşmesi). `MetricIds.RequiresDriver(string id) -> bool`.

**WebSocket mesajları** (JSON, `type` alanı ile):
- Sunucu → telefon:
  - `{"type":"snapshot","mode":"normal"|"fifa","gpuAsleep":bool,"fifaWarning":bool,"metrics":{"cpu.temp":62.1,"fan.0":null,...}}`
  - `{"type":"media","state":null | {"app":"Spotify","title":"...","artist":"...","isPlaying":true,"positionMs":94000,"durationMs":200000,"artVersion":3,"volume":0.6|null}}`
  - `{"type":"layout","layout":{...}}` (bağlanınca ve her kayıttan sonra)
- Telefon → sunucu:
  - `{"type":"media","action":"playPause"|"next"|"prev"}`
  - `{"type":"volume","value":0.0-1.0}`
  - `{"type":"saveLayout","layout":{...}}`

**HTTP:** `POST /api/pair {"code":"1234","deviceName":"..."}` → `200 {"token":"..."}` | `401` | `429`; `GET /ws?token=` (geçersiz token → `401`); `GET /art?token=&v=` → kapak `image/*` veya `404`; statik dosyalar token gerektirmez.

**Düzen** (`Layout`, JSON camelCase):
```json
{"version":1,"pages":[{"id":"p1","name":"Oyun","tiles":[{"id":"t1","kind":"metric","metric":"cpu.temp","size":"M","warn":70,"crit":85}]}]}
```
`kind`: `"metric"` | `"chart"` (metric zorunlu, son 60 sn) | `"mediaStrip"` | `"mediaFull"`. `size`: `"S"` (1×1) | `"M"` (2×1) | `"L"` (2×2), 4 sütunlu CSS grid, `grid-auto-flow: dense`; `mediaStrip` her zaman tam satır, `mediaFull` tam sayfa. Kutucuk sırası = dizi sırası. `warn`/`crit` isteğe bağlı.

---

### Task 1: Ortam ve iskelet

**Files:**
- Create: `TelefonPaneli.sln`, `.gitignore`, `src/TelefonPaneli.Core/TelefonPaneli.Core.csproj`, `src/TelefonPaneli.Windows/TelefonPaneli.Windows.csproj`, `src/TelefonPaneli.App/TelefonPaneli.App.csproj`, `tests/TelefonPaneli.Core.Tests/…csproj`, `tests/TelefonPaneli.App.Tests/…csproj`, `tests/web/smoke.test.mjs`

**Interfaces:**
- Produces: derlenen boş solution; `dotnet test` ve `node --test tests/web` çalışır.

- [ ] **Step 1:** .NET 10 SDK kur (makinede SDK yok): `winget install Microsoft.DotNet.SDK.10`. Doğrula: `dotnet --list-sdks` → `10.x` satırı.
- [ ] **Step 2:** `git init`; `.gitignore` (`bin/ obj/ .vs/ .superpowers/ tools/presentmon/*.exe`).
- [ ] **Step 3:** Projeleri oluştur (Core: `classlib net10.0`; Windows: `classlib net10.0-windows10.0.19041.0`; App: `Microsoft.NET.Sdk.Web`, `OutputType WinExe`, `UseWindowsForms true`, aynı TFM; testler xUnit). Referanslar: Windows→Core, App→Core+Windows, Core.Tests→Core, App.Tests→App.
- [ ] **Step 4:** `tests/web/smoke.test.mjs` içinde `assert.equal(1, 1)`.
- [ ] **Step 5:** Çalıştır: `dotnet build` (0 hata), `dotnet test` (PASS), `node --test tests/web` (PASS).
- [ ] **Step 6:** Commit: `chore: solution iskeleti`.

---

### Task 2: Metrik kataloğu ve MetricHub

**Files:**
- Create: `src/TelefonPaneli.Core/Metrics/MetricIds.cs`, `IMetricSource.cs`, `Snapshot.cs`, `MetricHub.cs`
- Test: `tests/TelefonPaneli.Core.Tests/MetricHubTests.cs`

**Interfaces:**
- Produces:
  - `static class MetricIds { const string CpuTemp = "cpu.temp"; …; static bool RequiresDriver(string id); }`
  - `interface IMetricSource { string Name { get; } void Collect(IDictionary<string, double?> into); }` (her çağrıda kendi metriklerini yazar; okunamayan değere `null`)
  - `sealed record Snapshot(string Mode, bool GpuAsleep, bool FifaWarning, IReadOnlyDictionary<string, double?> Metrics)`
  - `sealed class MetricHub(IEnumerable<IMetricSource> sources, Func<HubFlags> flags)` → `Snapshot Collect()`; `record HubFlags(string Mode, bool GpuAsleep, bool FifaWarning)`

- [ ] **Step 1: Failing tests**

```csharp
[Fact] public void Collect_merges_all_sources() // iki sahte kaynak → her iki metrik de var
[Fact] public void Throwing_source_does_not_break_others()
{
    var hub = new MetricHub(new IMetricSource[] { new Throwing(), new Fixed("cpu.load", 34) }, () => new("normal", false, false));
    var s = hub.Collect();
    Assert.Equal(34, s.Metrics["cpu.load"]);
}
[Fact] public void Throwing_source_keeps_its_last_keys_as_null() // önce "fan.0"=1450 yazan kaynak sonra throw → "fan.0" null
[Theory, InlineData("cpu.temp", true), InlineData("fan.3", true), InlineData("disk.1.temp", true), InlineData("gpu.temp", false), InlineData("fps", false)]
public void RequiresDriver(string id, bool expected)
```

- [ ] **Step 2:** `dotnet test --filter MetricHubTests` → FAIL (tipler yok).
- [ ] **Step 3:** Uygula. `MetricHub` her kaynağın son yazdığı anahtarları hatırlar; exception'da o anahtarları `null` yapar ve hatayı kaynak başına en fazla dakikada bir loglar (`ILogger`).
- [ ] **Step 4:** Testler PASS.
- [ ] **Step 5:** Commit: `feat(core): metrik kataloğu ve MetricHub`.

---

### Task 3: Düzen modeli, doğrulama ve ayar deposu

**Files:**
- Create: `src/TelefonPaneli.Core/Layout/LayoutModel.cs`, `LayoutValidator.cs`, `Settings/AppSettings.cs`, `Settings/SettingsStore.cs`
- Test: `tests/TelefonPaneli.Core.Tests/LayoutTests.cs`, `SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  - `record Layout(int Version, List<Page> Pages)`, `record Page(string Id, string Name, List<Tile> Tiles)`, `record Tile(string Id, string Kind, string? Metric, string Size, double? Warn, double? Crit)`
  - `static class DefaultLayout { static Layout Create(); }` — spec §7'deki 3 sayfa: **Oyun** (`cpu.temp` M, `gpu.temp` M, `fps` L, `ram.used` S, `gpu.vram.used` S, `mediaStrip`), **Müzik** (`mediaFull`), **Detay** (`cpu.clock`, `gpu.power`, `fan.0`, `disk.0.temp`, `net.down`, `ping` hepsi S, `chart` `cpu.load` M). Sıcaklıklarda `warn 70, crit 85`; `ping` için `warn 60, crit 120`.
  - `static class LayoutValidator { static Layout Sanitize(Layout? input); }`
  - `record AppSettings(Layout Layout, List<DeviceToken> Devices, List<string> FifaProcessNames)`; `record DeviceToken(string Id, string Name, string TokenHash, DateTimeOffset Created)`; varsayılan `FifaProcessNames = ["FIFA23.exe", "FIFA23_Trial.exe"]`
  - `sealed class SettingsStore(string path)` → `AppSettings Load()`, `void Save(AppSettings)` (geçici dosyaya yaz + `File.Move(overwrite)`), thread-safe.

- [ ] **Step 1: Failing tests**

```csharp
[Fact] public void Sanitize_null_returns_default() => Assert.Equal(3, LayoutValidator.Sanitize(null).Pages.Count);
[Fact] public void Sanitize_drops_tiles_with_unknown_kind()
[Fact] public void Sanitize_metric_tile_without_metric_is_dropped()
[Fact] public void Sanitize_invalid_size_becomes_M()
[Fact] public void Sanitize_zero_pages_gets_one_empty_page()   // Pages.Count == 1, Name "Sayfa 1"
[Fact] public void Sanitize_duplicate_tile_ids_are_regenerated()
[Fact] public void Sanitize_caps_at_10_pages_and_24_tiles_per_page()

[Fact] public void Load_missing_file_returns_defaults()
[Fact] public void Save_then_Load_roundtrips()
[Fact] public void Load_corrupt_json_returns_defaults_and_keeps_backup()
{
    File.WriteAllText(path, "{ bozuk");
    var s = new SettingsStore(path).Load();
    Assert.Equal(3, s.Layout.Pages.Count);
    Assert.True(File.Exists(path + ".bak"));
}
[Fact] public void Load_sanitizes_layout_from_disk()
```

- [ ] **Step 2:** Testler FAIL.
- [ ] **Step 3:** Uygula (System.Text.Json, camelCase, `JsonSerializerDefaults.Web`).
- [ ] **Step 4:** Testler PASS.
- [ ] **Step 5:** Commit: `feat(core): düzen modeli ve ayar deposu`.

---

### Task 4: Eşleştirme ve yerel ağ koruması

**Files:**
- Create: `src/TelefonPaneli.Core/Security/PairingService.cs`, `NetworkGuard.cs`
- Test: `tests/TelefonPaneli.Core.Tests/PairingTests.cs`, `NetworkGuardTests.cs`

**Interfaces:**
- Consumes: `SettingsStore`, `DeviceToken` (Task 3)
- Produces:
  - `sealed class PairingService(SettingsStore store, Func<int> rng)`:
    `string CurrentCode { get; }` (4 hane, `"0000"`–`"9999"`, baştaki sıfırlar korunur), `event Action<string>? CodeChanged`,
    `PairResult TryPair(string code, string deviceName)` → `record PairResult(PairStatus Status, string? Token)`, `enum PairStatus { Ok, WrongCode, Locked }`,
    `bool IsValid(string? token)`, `void ForgetAll()`.
    Kurallar: başarılı eşleşmeden sonra kod yenilenir; 5 yanlış denemede kod yenilenir ve 30 sn `Locked`; token = 32 rastgele bayt base64url, saklanan = SHA-256 hex.
  - `static class NetworkGuard { static bool IsLocal(IPAddress ip); }` — loopback, 10/8, 172.16/12, 192.168/16, 169.254/16, IPv6 `fe80::/10`, `fc00::/7`, IPv4-mapped IPv6 bunların karşılığı.

- [ ] **Step 1: Failing tests**

```csharp
[Fact] public void Correct_code_returns_token_and_rotates_code()
[Fact] public void Token_is_valid_and_survives_new_service_instance() // aynı settings dosyasıyla yeni PairingService → IsValid true
[Fact] public void Stored_token_is_hashed() // settings.json içinde düz token geçmez
[Fact] public void Five_wrong_codes_lock_and_rotate()
[Fact] public void ForgetAll_invalidates_existing_tokens()
[Fact] public void Code_keeps_leading_zeros() // rng → 42 → "0042"
[Theory, InlineData("192.168.1.20", true), InlineData("10.0.0.5", true), InlineData("172.20.1.1", true),
 InlineData("172.32.0.1", false), InlineData("8.8.8.8", false), InlineData("::1", true),
 InlineData("fe80::1", true), InlineData("::ffff:192.168.1.5", true), InlineData("2001:4860::1", false)]
public void IsLocal(string ip, bool expected)
```

- [ ] **Step 2:** FAIL. **Step 3:** Uygula (kilit süresi için `TimeProvider` enjekte et). **Step 4:** PASS.
- [ ] **Step 5:** Commit: `feat(core): eşleştirme ve yerel ağ koruması`.

---

### Task 5: FPS hesabı ve PresentMon CSV ayrıştırıcı

**Files:**
- Create: `src/TelefonPaneli.Core/Metrics/FpsCalculator.cs`, `PresentMonCsv.cs`
- Test: `tests/TelefonPaneli.Core.Tests/FpsTests.cs`, `tests/TelefonPaneli.Core.Tests/Fixtures/presentmon-sample.csv` (gerçek PresentMon `--v1_metrics` başlığıyla 20 satırlık örnek)

**Interfaces:**
- Produces:
  - `sealed class FpsCalculator(TimeProvider time)` → `void AddFrame(double msBetweenPresents)`, `double? Fps` (son 1 sn: kare sayısı / toplam süre × 1000), `double? Low1` (son 10 sn frametime'larının 99. yüzdeliği → `1000 / p99`), `void Reset()`. 2 sn hiç kare gelmezse ikisi de `null`.
  - `sealed class PresentMonCsv` → `bool TryReadHeader(string line)` (sütunu **ada göre** bulur: `MsBetweenPresents`), `bool TryParse(string line, out string processName, out double ms)`.

- [ ] **Step 1: Failing tests**

```csharp
[Fact] public void Steady_144fps() // 1 sn boyunca 6.944 ms kareler → Fps ≈ 144 (±1)
[Fact] public void Low1_reflects_spikes() // 990 kare 7 ms + 10 kare 20 ms → Low1 ≈ 50 (±2)
[Fact] public void No_frames_for_2s_gives_null()
[Fact] public void Parses_fixture_by_header_name() // fixture'daki ilk satır: processName ve ms beklenen değerler
[Fact] public void Malformed_line_returns_false()
```

- [ ] **Step 2:** FAIL. **Step 3:** Uygula (çerçeve kuyruğu zaman damgalı; `FakeTimeProvider` testlerde). **Step 4:** PASS.
- [ ] **Step 5:** Commit: `feat(core): FPS hesabı ve PresentMon ayrıştırıcı`.

---

### Task 6: Web sunucusu ve WebSocket protokolü

**Files:**
- Create: `src/TelefonPaneli.Core/Protocol/Messages.cs`, `src/TelefonPaneli.Core/Media/MediaState.cs`, `src/TelefonPaneli.App/Server/WebServer.cs`
- Test: `tests/TelefonPaneli.App.Tests/WebServerTests.cs`

**Interfaces:**
- Consumes: `MetricHub`, `Snapshot` (T2), `SettingsStore`, `LayoutValidator` (T3), `PairingService`, `NetworkGuard` (T4)
- Produces:
  - `record MediaState(string App, string Title, string Artist, bool IsPlaying, long PositionMs, long DurationMs, int ArtVersion, double? Volume)`; `enum MediaAction { PlayPause, Next, Prev }`
  - `interface IMediaController { MediaState? Current { get; } event Action? Changed; Task<byte[]?> GetArtAsync(); Task SendAsync(MediaAction a); Task SetVolumeAsync(double v); }` (Task 10 uygular; testte sahte)
  - `static class WebServer { static WebApplication Build(WebServerOptions o); }`; `record WebServerOptions(int Port, MetricHub Hub, SettingsStore Store, PairingService Pairing, IMediaController Media, TimeSpan Interval, bool EnforceLocalNetwork)`
  - Protokol "Ortak sözleşmeler" bölümündeki gibi. Telefon mesajı geçersiz JSON / bilinmeyen `type` ise yok sayılır, bağlantı kapanmaz. `volume.value` [0,1] aralığına sıkıştırılır. `ForgetAll` çağrılınca açık soketler `4401` koduyla kapatılır.

- [ ] **Step 1: Failing tests** (`WebApplicationFactory`/`TestServer` + `WebSocketClient`, sahte kaynak ve `FakeMedia`, `Interval = 50ms`)

```csharp
[Fact] public async Task Pair_with_wrong_code_is_401()
[Fact] public async Task Ws_without_token_is_401()
[Fact] public async Task Ws_receives_layout_then_snapshots()  // ilk mesaj type=layout, sonra type=snapshot ve metrics["cpu.load"]==34
[Fact] public async Task Missing_metric_is_serialized_as_null() // "fan.0": null, 0 değil
[Fact] public async Task Media_null_is_sent_as_state_null_and_commands_do_not_throw()
[Fact] public async Task Media_action_reaches_controller()
[Fact] public async Task Volume_is_clamped() // value 1.7 → SetVolumeAsync(1.0)
[Fact] public async Task SaveLayout_is_sanitized_persisted_and_broadcast_to_other_clients()
[Fact] public async Task Garbage_message_keeps_socket_open()
[Fact] public async Task Token_still_valid_after_host_restart() // aynı settings dosyası, yeni host
[Fact] public async Task ForgetAll_closes_open_socket_with_4401()
[Fact] public async Task Non_local_ip_is_403_when_enforced() // RemoteIpAddress 8.8.8.8 middleware'i
[Fact] public async Task Art_returns_404_when_no_media()
```

- [ ] **Step 2:** FAIL. **Step 3:** Uygula: tek bir yayın döngüsü (`PeriodicTimer`) tüm istemcilere aynı JSON'u gönderir; istemci başına gönderim kuyruğu yavaş telefonu diğerlerini bekletmesin diye sınırlı (son mesaj kazanır). Statik dosyalar `UseStaticFiles` + `index.html` varsayılan. **Step 4:** PASS.
- [ ] **Step 5:** Commit: `feat(app): web sunucusu ve WebSocket protokolü`.

---

### Task 7: Windows sensör kaynakları

**Files:**
- Create: `src/TelefonPaneli.Windows/Sensors/PerfCounterSource.cs`, `PingSource.cs`, `LhmSource.cs`, `GpuActivityGate.cs`
- Test: `tests/TelefonPaneli.App.Tests/HardwareSmokeTests.cs` (`[Trait("Category","Hardware")]`, CI'da atlanır)

**Interfaces:**
- Consumes: `IMetricSource`, `MetricIds` (T2)
- Produces:
  - `PerfCounterSource : IMetricSource` → `cpu.load`, `ram.used`, `ram.total` (GB), `net.down`, `net.up` (MB/s, tüm fiziksel adaptörlerin toplamı)
  - `PingSource : IMetricSource, IDisposable` → `ping` (1.1.1.1, 2 sn'de bir arka planda; zaman aşımı 1 sn → `null`)
  - `LhmSource(bool driverAllowed, GpuActivityGate gate) : IMetricSource, IDisposable` → `driverAllowed=false` iken `Computer { IsGpuEnabled = true, IsCpuEnabled = false, IsMotherboardEnabled = false, IsControllerEnabled = false, IsStorageEnabled = false, IsMemoryEnabled = false }`; `true` iken hepsi açık. NVIDIA GPU donanımını `gate.IsAwake` false iken **güncellemez** ve `gpu.*` için `null` yazar. Fan sensörleri `fan.0..n`, disk sıcaklıkları `disk.0..n.temp` (bulunamazsa hiç yazmaz).
  - `GpuActivityGate` → `bool IsAwake` : "GPU Engine" performans sayacında NVIDIA adaptörünün LUID'li örneklerinden herhangi biri son 5 sn içinde > %0 ise `true`. LUID, DXGI adaptör listesinden `VendorId == 0x10DE` ile bulunur.

- [ ] **Step 1:** Smoke testi yaz: her kaynak bir kez `Collect` → `cpu.load` 0–100 arası, `ram.total` > 0; `LhmSource(false, …)` sonrası `sc query PawnIO` çıktısında `RUNNING` yok.
- [ ] **Step 2:** `dotnet test --filter Category=Hardware` (yönetici terminalde) → FAIL.
- [ ] **Step 3:** Uygula. LHM NuGet sürümünü PawnIO kullanan sürüm olarak sabitle (sürüm notunda "PawnIO" geçen en yeni kararlı; csproj'a tam sürüm yaz).
- [ ] **Step 4:** Smoke testi PASS. Elle doğrula: oyun yokken `gpuAsleep` true ve Aygıt Yöneticisi'nde/`nvidia-smi` çağırmadan GTX 1650 Ti uykuda kalıyor (Görev Yöneticisi → GPU 1 "Askıya alındı/0%").
- [ ] **Step 5:** Commit: `feat(windows): sensör kaynakları`.

---

### Task 8: PawnIO sürücü yaşam döngüsü, modlar ve FIFA uyarısı

**Files:**
- Create: `src/TelefonPaneli.Windows/Driver/IServiceControl.cs`, `PawnIoDriver.cs`, `src/TelefonPaneli.Windows/Games/GameWatcher.cs`
- Test: `tests/TelefonPaneli.App.Tests/PawnIoDriverTests.cs`, `GameWatcherTests.cs` (sahte `IServiceControl` ile)

**Interfaces:**
- Produces:
  - `interface IServiceControl { ServiceState? Query(string name); void Start(string name); void Stop(string name); void SetManualStart(string name); }`, `enum ServiceState { Stopped, Running, Other }` — gerçek uygulaması `ServiceController` + `sc config PawnIO start= demand`.
  - `sealed class PawnIoDriver(IServiceControl sc)` → `DriverStatus Prepare(bool fifaMode)`; `void Shutdown()`; `enum DriverStatus { Ready, NotInstalled, DisabledForFifa }`.
    Kurallar: Servis yoksa `NotInstalled` (driver metrikleri `null`, tepside "CPU sıcaklığı için PawnIO kurulmalı" bilgisi). Normal modda: `SetManualStart` + `Start`. FIFA modunda: `SetManualStart` + çalışıyorsa `Stop`. `Shutdown` her zaman `Stop`.
  - `sealed class GameWatcher(Func<IEnumerable<string>> processNames, IReadOnlyList<string> fifaNames)` → `bool FifaRunning`, `int? ForegroundPid` (`GetForegroundWindow` + `GetWindowThreadProcessId`), `void Poll()` (2 sn'de bir çağrılır). Ad karşılaştırması büyük/küçük harf duyarsız.
  - Snapshot bayrağı: `FifaWarning = mode == "normal" && watcher.FifaRunning`.

- [ ] **Step 1: Failing tests**

```csharp
[Fact] public void Normal_mode_sets_manual_and_starts()
[Fact] public void Fifa_mode_stops_running_service_and_never_starts()
[Fact] public void Missing_service_returns_NotInstalled_without_throwing()
[Fact] public void Shutdown_stops_service()
[Fact] public void Fifa_detected_case_insensitive() // "fifa23.EXE" listede → FifaRunning true
```

- [ ] **Step 2:** FAIL. **Step 3:** Uygula. **Step 4:** PASS.
- [ ] **Step 5:** Elle: PawnIO'yu kullanıcı kendisi resmi sitesinden kurar (indirme izni kullanıcıdan). Uygulamayı normal modda aç → `sc query PawnIO` = RUNNING; kapat → STOPPED; `--fifa` ile aç → STOPPED ve `driverquery | findstr /i pawnio` boş.
- [ ] **Step 6:** Commit: `feat(windows): PawnIO yaşam döngüsü ve FIFA algılama`.

---

### Task 9: PresentMon FPS kaynağı

**Files:**
- Create: `src/TelefonPaneli.Windows/Fps/PresentMonSource.cs`, `tools/presentmon/README.md`

**Interfaces:**
- Consumes: `FpsCalculator`, `PresentMonCsv` (T5), `GameWatcher.ForegroundPid` (T8)
- Produces: `PresentMonSource(string exePath, GameWatcher watcher) : IMetricSource, IDisposable` → `fps`, `fps.low1`.
  Çalıştırma: `PresentMon.exe --output_stdout --no_console_stats --stop_existing_session --session_name TelefonPaneli --v1_metrics` (tüm süreçler), stdout satırları okunur; yalnızca `ForegroundPid`'in süreç adıyla eşleşen satırlar `FpsCalculator`'a gider; ön plan süreci değişince `Reset()`. Exe yoksa veya süreç çökerse metrikler `null`, 10 sn sonra yeniden dener. Uygulama kapanınca süreç öldürülür.
  `tools/presentmon/README.md`: exe'nin GitHub `GameTechDev/PresentMon` sürümlerinden indirilip `tools/presentmon/PresentMon.exe` olarak konacağı; build bu dosyayı çıktıya kopyalar (`CopyToOutputDirectory`, dosya yoksa uyarı).

- [ ] **Step 1:** README ve csproj kopyalama kuralını ekle; PresentMon'u kullanıcı onayıyla indir (dosya adı/boyut sorulur).
- [ ] **Step 2:** Uygula.
- [ ] **Step 3:** Elle doğrula: bir oyun (FIFA olmayan) açıkken telefonda FPS oyun içi sayaçla ±%5 uyumlu; oyundan masaüstüne alt-tab yapınca FPS 2 sn içinde `—`; Görev Yöneticisi'nde uygulama CPU < %1.
- [ ] **Step 4:** Commit: `feat(windows): PresentMon FPS kaynağı`.

---

### Task 10: Medya (SMTC) ve uygulama sesi

**Files:**
- Create: `src/TelefonPaneli.Windows/Media/SmtcMediaSource.cs`, `AppVolume.cs`

**Interfaces:**
- Consumes: `IMediaController`, `MediaState`, `MediaAction` (T6)
- Produces: `SmtcMediaSource(AppVolume volume) : IMediaController, IDisposable`.
  - Oturum seçimi: `GetCurrentSession()`; yoksa ilk çalan oturum; hiçbiri yoksa `Current = null`.
  - `App` adı `SourceAppUserModelId`'den: `Spotify` içeriyorsa `"Spotify"`, `chrome`/`msedge` ise `"YouTube Music"` eğer başlık/albüm boşsa bile tarayıcı adı yerine bu ad (spec: tarayıcıdaki YT Music), aksi halde AUMID'in son parçası.
  - Kapak değişince `ArtVersion++`; kapak alınamazsa `GetArtAsync` → `null` (telefon yer tutucu gösterir).
  - `Changed` olayı medya/oynatma/zaman çizelgesi değişikliklerinde tetiklenir; konum telefonda yerel olarak ilerletilir (sunucu her saniye göndermez).
- `AppVolume` (NAudio `AudioSessionManager`): `double? Get(string aumid)`, `void Set(string aumid, double v)` — AUMID → süreç adı (`Spotify.exe`, `chrome.exe`, `msedge.exe`) → o sürecin tüm ses oturumları. Eşleşme yoksa `Volume = null` ve telefonda ses tuşu gizlenir. **Ana ses (endpoint volume) asla değiştirilmez.**

- [ ] **Step 1:** Uygula.
- [ ] **Step 2:** Elle doğrula (telefonsuz, tarayıcıdan `ws` istemcisi veya Task 13 sonrası telefondan): Spotify'da şarkı → başlık/sanatçı/kapak doğru; ⏯ ⏭ ⏮ çalışıyor; ses tuşu yalnızca Spotify'ın sesini değiştiriyor (Ses Karıştırıcı'da kontrol); Chrome'da YouTube Music → aynı kontroller; Spotify'ı şarkı ortasında kapat → telefonda "Çalan bir şey yok", hata yok.
- [ ] **Step 3:** Commit: `feat(windows): medya kontrolü ve uygulama sesi`.

---

### Task 11: Tepsi uygulaması, modlar, güvenlik duvarı, kısayollar

**Files:**
- Create: `src/TelefonPaneli.App/Program.cs`, `app.manifest`, `Tray/TrayApp.cs`, `Tray/QrForm.cs`, `Setup/Firewall.cs`, `scripts/create-shortcuts.ps1`

**Interfaces:**
- Consumes: hepsi (T2–T10)
- Produces:
  - `Program.Main(string[] args)`: `--fifa` → mod `"fifa"`. Tek örnek kilidi (`Mutex "TelefonPaneli"`; ikinci örnek mevcut olanın QR penceresini açtırır ve çıkar). Sıra: ayarları yükle → `PawnIoDriver.Prepare` → kaynaklar → `MetricHub` → `WebServer.Build(... Port 5050 ...)` → tepsi. Çıkışta: sunucuyu durdur, kaynakları dispose et, `PawnIoDriver.Shutdown()`.
  - Tepsi menüsü (Türkçe): "Telefon adresi: http://<LAN-IP>:5050" (tıklayınca QR penceresi), "Eşleştirme kodu: 1234", "Mod: Normal / FIFA", "FIFA modunda yeniden başlat" / "Normal modda yeniden başlat", "Eşleşmiş cihazları unut", "Çıkış". FIFA uyarısı aktifse tepside balon bildirimi: "FIFA açıldı — programı FIFA modunda yeniden başlat".
  - LAN IP: ağ geçidi olan, `Up` durumdaki ilk IPv4 adaptörü.
  - `Firewall.EnsureRule()`: `netsh advfirewall firewall show rule name="TelefonPaneli"` yoksa `add rule ... dir=in action=allow protocol=TCP localport=5050 profile=private`.
  - `create-shortcuts.ps1`: masaüstüne "Telefon Paneli" ve "Telefon Paneli (FIFA modu)" (`--fifa`) kısayolları. Başlangıç klasörüne **yazmaz**.

- [ ] **Step 1:** Uygula.
- [ ] **Step 2:** Elle doğrula: UAC istemi çıkar; tepsi ikonu ve menü görünür; QR telefondan okununca sayfa açılır; "FIFA modunda yeniden başlat" → mod değişir ve `sc query PawnIO` STOPPED; Windows'u yeniden başlat → program kendiliğinden **açılmaz**; ikinci kez çift tıklama yeni örnek açmaz.
- [ ] **Step 3:** Commit: `feat(app): tepsi uygulaması ve modlar`.

---

### Task 12: Web saf modülleri (biçimlendirme, düzen işlemleri, yeniden bağlanma)

**Files:**
- Create: `src/TelefonPaneli.App/wwwroot/js/format.js`, `layout.js`, `connection.js` (saf kısım)
- Test: `tests/web/format.test.mjs`, `layout.test.mjs`, `connection.test.mjs`

**Interfaces:**
- Produces:
  - `format.js`: `formatValue(metricId, value) -> string` (`null`/`undefined` → `"—"`; sıcaklık `"62°"`, yüzde `"%34"`, GB bir ondalık, MB/s bir ondalık, ms tam sayı, fps tam sayı); `levelClass(value, warn, crit) -> "ok"|"warn"|"hot"|""`; `metricLabel(metricId) -> string` (Türkçe: `"CPU"`, `"GPU"`, `"FPS"`, `"1% low"`, `"Fan 1"`, `"Disk 1"`, `"Ping"` …); `formatDuration(ms) -> "1:34"`; `mediaText(state) -> {title, subtitle}` (`state === null` → `{title: "Çalan bir şey yok", subtitle: ""}`); `AVAILABLE_TILES` (ekleme listesi).
  - `layout.js` (tüm fonksiyonlar yeni nesne döner, girdiyi değiştirmez): `addTile(layout, pageId, tile)`, `removeTile(layout, pageId, tileId)`, `moveTile(layout, pageId, fromIndex, toIndex)`, `resizeTile(layout, pageId, tileId, size)`, `setThresholds(layout, pageId, tileId, warn, crit)`, `addPage(layout, name)`, `removePage(layout, pageId)` (son sayfa silinemez), `newId()`.
  - `connection.js`: `nextDelay(attempt) -> ms` (1000, 2000, 4000, 5000, 5000…); `createConnection({url, onMessage, onStatus, WebSocketImpl, setTimeoutImpl})` → `{send(obj), close()}`; kapanış kodu `4401` veya HTTP 401 → `onStatus("unpaired")`, diğerlerinde `onStatus("reconnecting")` ve `nextDelay` ile yeniden dener.

- [ ] **Step 1: Failing tests**

```js
test('null değer tire olur', () => assert.equal(formatValue('cpu.temp', null), '—'));
test('sıfır tire değildir', () => assert.equal(formatValue('cpu.load', 0), '%0'));
test('eşikler', () => { assert.equal(levelClass(69, 70, 85), 'ok'); assert.equal(levelClass(70, 70, 85), 'warn'); assert.equal(levelClass(85, 70, 85), 'hot'); assert.equal(levelClass(50, undefined, undefined), ''); });
test('medya yoksa', () => assert.equal(mediaText(null).title, 'Çalan bir şey yok'));
test('moveTile sırayı değiştirir ve girdiyi bozmaz', ...);
test('son sayfa silinemez', ...);
test('yeniden bağlanma gecikmesi 5 snde tavan yapar', () => assert.deepEqual([0,1,2,3,7].map(nextDelay), [1000,2000,4000,5000,5000]));
test('4401 ile kapanınca unpaired', ...);   // sahte WebSocketImpl
test('ağ kopunca reconnecting ve tekrar dener', ...);
```

- [ ] **Step 2:** `node --test tests/web` → FAIL. **Step 3:** Uygula. **Step 4:** PASS.
- [ ] **Step 5:** Commit: `feat(web): saf modüller`.

---

### Task 13: Telefon ekranı — eşleştirme, sayfalar, kaydırma, müzik

**Files:**
- Create: `wwwroot/index.html`, `styles.css`, `manifest.webmanifest`, `icon-512.png`, `js/pairing.js`, `js/pager.js`, `js/render.js`, `js/main.js`

**Interfaces:**
- Consumes: `format.js`, `layout.js`, `connection.js` (T12); WS protokolü (T6)
- Produces: çalışan PWA. Görsel referans: `.superpowers/brainstorm/962-1794338519/content/sayfalar.html` (renkler, kutucuk stili, sayfa noktaları).
  - `pairing.js`: token `localStorage`'da yoksa 4 haneli büyük rakam tuş takımı; `POST /api/pair`; `429` → "Çok fazla deneme, 30 sn bekle".
  - `pager.js`: `createPager(container, pageCount, onChange)` — yatay kaydırma (pointer olayları, eşik ekran genişliğinin %20'si), altta sayfa noktaları; son açık sayfa `localStorage`'da.
  - `render.js`: `renderLayout(root, layout)`, `updateSnapshot(root, snapshot)` (yalnızca metin/sınıf günceller, DOM'u yeniden kurmaz), `updateMedia(root, state)`. Kutucuk değer yazısı: S ≥ 34px, M ≥ 44px, L ≥ 72px. `mediaFull`: kapak 150px+, ⏮ ⏯ ⏭ tuşları ≥ 56px, ses kaydırıcısı (`volume === null` ise gizli), ilerleme çubuğu `requestAnimationFrame` ile yerel ilerler. `chart` kutucuğu: metriğin son 60 sn değerlerini (120 nokta) bellekte tutar ve SVG `polyline` çizer; `null` noktalar çizgide boşluk bırakır. FIFA modunda `RequiresDriver` metriklerinde `—` yanında 🛡. `gpuAsleep` → GPU kutucuklarında "uyku". `fifaWarning` → üstte turuncu şerit "FIFA açıldı — programı FIFA modunda yeniden başlat".
  - Bağlantı durumu: `reconnecting` → üstte kırmızı şerit "Bağlantı yok — yeniden deneniyor"; `unpaired` → token silinir, eşleştirme ekranı.
  - Ekran açık kalsın: `navigator.wakeLock` varsa iste; yoksa/reddedilirse bir kerelik ipucu: "Telefonu şarja tak ve Geliştirici seçenekleri → Uyanık kal'ı aç".
  - `manifest.webmanifest`: `display: fullscreen`, `orientation: landscape`, `background_color: #0b0d10`, ad "Telefon Paneli".

- [ ] **Step 1:** Uygula.
- [ ] **Step 2:** Elle doğrula telefonda: QR → eşleştirme → üç sayfa taslaktaki gibi; kaydırma akıcı; değerler 0.5 sn'de bir değişiyor; Wi‑Fi'ı kapat/aç → şerit çıkar ve kod sorulmadan geri bağlanır; PC uygulamasını yeniden başlat → aynısı; "Eşleşmiş cihazları unut" → telefon eşleştirme ekranına döner; "Ana ekrana ekle" → tam ekran yatay açılır.
- [ ] **Step 3:** Commit: `feat(web): telefon ekranı`.

---

### Task 14: Düzenleme modu

**Files:**
- Create: `wwwroot/js/editor.js`; Modify: `js/main.js`, `js/render.js`, `styles.css`

**Interfaces:**
- Consumes: `layout.js` işlemleri (T12), `saveLayout` mesajı (T6)
- Produces: `enterEditMode(root, layout, onSave)`:
  - Herhangi bir kutucuğa **600 ms** uzun basınca açılır; kaydırma (pager) devre dışı kalır.
  - Kutucuklar kesik mavi çerçeve; ✕ sil; sürükle-bırak ile sıralama (`moveTile`); kutucuğa dokununca alt panel: boyut S/M/L, eşik (warn/crit sayı girişleri, yalnızca metric kutucukları).
  - "＋ Kutucuk ekle" → `AVAILABLE_TILES` listesi (bu makinede son snapshot'ta hiç görülmemiş metrikler gri ve "bu bilgisayarda yok" etiketiyle).
  - Üst çubuk: "＋ Sayfa", "Sayfayı sil" (onay sorar; son sayfada pasif), sayfa adını düzenle, **"Bitti ✓"** → `onSave(layout)` → `saveLayout` gönderilir; sunucudan dönen `layout` mesajı ekranı yeniden kurar.
  - Bağlantı yokken "Bitti" → değişiklik bekletilir ve yeniden bağlanınca gönderilir.

- [ ] **Step 1:** Uygula.
- [ ] **Step 2:** Elle doğrula: kutucuk ekle/sil/taşı/boyutlandır, eşik değiştir → renk değişimi; sayfa ekle/sil; "Bitti" sonrası PC uygulamasını yeniden başlat ve telefon tarayıcı verisini sil → yeniden eşleştirince düzen aynı (PC'de saklandığı için).
- [ ] **Step 3:** Commit: `feat(web): düzenleme modu`.

---

### Task 15: Uçtan uca doğrulama

- [ ] **Step 1:** `dotnet test` ve `node --test tests/web` → tümü PASS.
- [ ] **Step 2:** Normal modda bir oyun (FIFA değil) 15 dk oyna: Görev Yöneticisi'nde TelefonPaneli ortalama CPU < %1, RAM < 150 MB; oyun içi FPS'te uygulama açık/kapalı farkı ölçülebilir değil (aynı sahnede 2 ölçüm).
- [ ] **Step 3:** FIFA modu: masaüstü kısayolu "Telefon Paneli (FIFA modu)" → `sc query PawnIO` STOPPED, `driverquery | findstr /i pawnio` boş → FIFA 23'ü aç, bir maç oyna: telefonda GPU, FPS, müzik çalışıyor; CPU sıcaklığı `— 🛡`; EA AntiCheat uyarısı yok.
- [ ] **Step 4:** Normal moddayken FIFA 23'ü aç → telefonda ve tepside FIFA uyarısı görünür.
- [ ] **Step 5:** Sonuçları `docs/superpowers/plans/2026-11-10-telefon-panel.md` sonuna "Doğrulama notları" olarak ekle; commit: `docs: uçtan uca doğrulama notları`.
