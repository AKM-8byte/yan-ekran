# YanEkran

Oyun oynarken Android telefonunu bilgisayarın yanına dik: **YanEkran** onu ikinci bir ekrana çevirir.
CPU/GPU sıcaklığı ve kullanımı, RAM/VRAM, FPS, ağ ve ping değerlerini, ayrıca Spotify veya YouTube Music'te
çalan şarkıyı büyük, tek bakışta okunan kutucuklarla gösterir; müziği telefondan kontrol edersin.

- **Kaydırmalı sayfalar:** Oyun, Müzik, Detay (varsayılan). Parmakla sağa/sola geç.
- **Düzenlenebilir ekran:** Ekrana uzun bas → kutucuk ekle, sil, sürükle, S/M/L boyutlandır, renk eşiklerini değiştir, sayfa ekle/sil.
- **Müzik kontrolü:** Şarkı adı, kapak, ilerleme; ⏮ ⏯ ⏭ ve yalnızca o uygulamanın sesini değiştiren ses kaydırıcısı.
- **Uygulama kurulmaz:** Telefonda Chrome'dan açılan bir sayfa (PWA). "Ana ekrana ekle" ile tam ekran çalışır.
- **Yalnızca yerel ağ:** İnternete hiçbir şey gitmez. İlk bağlantıda 4 haneli eşleştirme kodu istenir.
- **Hafif:** Oyun sırasında hedef ortalama < %1 CPU, < 150 MB RAM.

## Nasıl çalışır

```
[Windows PC]  YanEkran.exe (sistem tepsisi, .NET 10)          [Android telefon]  Chrome / PWA
  ├─ Windows performans sayaçları  (CPU, RAM, ağ)
  ├─ NVIDIA NVML                   (GPU sıcaklık/kullanım/güç/VRAM)
  ├─ PresentMon (ETW)              (FPS, 1% low)           ◄── WebSocket, yerel ağ ──►  kutucuklar, kaydırma,
  ├─ Windows medya oturumları      (Spotify, YouTube Music)                              düzenleme modu
  └─ LibreHardwareMonitor + PawnIO (isteğe bağlı: CPU sıcaklığı, fanlar, diskler)
```

## Gereksinimler

- Windows 10/11, Android telefon (Chrome), ikisi aynı Wi‑Fi'da
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (derlemek için)
- NVIDIA ekran kartı (GPU değerleri için; yoksa bu kutucuklar "—" gösterir)
- **İsteğe bağlı:** [PresentMon](https://github.com/GameTechDev/PresentMon/releases) — FPS için. `PresentMon-<sürüm>-x64.exe` dosyasını `tools/presentmon/PresentMon.exe` olarak koy (test edilen: v2.6.0).
- **İsteğe bağlı:** [PawnIO](https://pawnio.eu/) sürücüsü — CPU sıcaklığı, fan ve disk sensörleri için. Kurulu değilse bu kutucuklar "—" gösterir, program normal çalışır.

## Kurulum

```powershell
dotnet build src/TelefonPaneli.App -c Release
powershell -ExecutionPolicy Bypass -File scripts/create-shortcuts.ps1
```

Masaüstünde **YanEkran** ve **YanEkran (FIFA modu)** kısayolları oluşur. Program Windows ile otomatik başlamaz.

1. **YanEkran**'ı aç (yönetici izni ister: sensörler, FPS ve güvenlik duvarı kuralı için).
2. Sistem tepsisindeki simgeye çift tıkla, QR kodu telefonla okut.
3. Ekranda görünen 4 haneli kodu telefona gir. Eşleştirme, program açıldıktan ve kod gösterildikten sonraki 5 dakika boyunca kabul edilir.
4. Chrome menüsünden **Ana ekrana ekle** de; ekranın kapanmaması için telefon şarjdayken Geliştirici seçenekleri → **Uyanık kal**'ı aç.

## FIFA modu (hile koruması)

Bazı hile koruma sistemleri (ör. EA AntiCheat) çekirdek sürücülerine tepki verebilir. **YanEkran (FIFA modu)**
kısayoluyla açıldığında PawnIO sürücüsü hiç yüklenmez ve durdurulur; GPU, FPS ve müzik normal çalışır, sürücü
gerektiren kutucuklarda "— 🛡" görünür. Sürücü durdurulamazsa hem tepside hem telefonda **"FIFA'yı açma"** uyarısı çıkar.
PawnIO hiç kurulu değilse bu moda gerek yoktur.

## Geliştirme

```powershell
dotnet test            # C# testleri (donanım testleri için: $env:TP_HARDWARE=1)
npm test               # telefon arayüzü testleri (Node 24)
dotnet run --project tools/DemoServer   # sahte verilerle arayüz önizleme: http://localhost:5051, kod 1234
```

| Klasör | İçerik |
|---|---|
| `src/TelefonPaneli.Core` | Windows'tan bağımsız mantık: metrikler, düzen, eşleştirme, FPS hesabı, protokol |
| `src/TelefonPaneli.Windows` | Sensörler, NVML, PresentMon, medya, PawnIO yaşam döngüsü |
| `src/TelefonPaneli.App` | Tepsi uygulaması, web sunucusu, telefon arayüzü (`wwwroot`) |
| `tools/DemoServer` | Yönetici yetkisi gerektirmeyen sahte veri sunucusu |
| `docs/superpowers` | Tasarım belgesi ve uygulama planı |
