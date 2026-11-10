# Telefon Paneli — Tasarım Belgesi

Tarih: 2026-11-10
Durum: Taslak (kullanıcı onayı bekliyor)

## 1. Amaç ve neden

Kullanıcı oyun oynarken telefonunu (Android) bilgisayarın yanına dikip **ikinci bir ekran** olarak kullanmak istiyor. Bu ekranda:
- bilgisayarın sağlığı (CPU/GPU sıcaklığı ve kullanımı, RAM, VRAM, FPS vb.),
- o an çalan şarkı (Spotify uygulaması veya tarayıcıdaki YouTube Music)

görünecek. Oyunun ortasında **göz ucuyla, tek bakışta** okunabilmeli ve şarkı telefondan kontrol edilebilmeli (durdur / geç / geri / ses).

Başarı ölçütü: Oyun açıkken telefon yanda durur, yazılar uzaktan rahat okunur, değerler canlı akar, oyun performansı fark edilir şekilde düşmez.

## 2. Hedefler

1. Bilgisayar verilerini telefonda canlı göstermek (≈ saniyede 1–2 güncelleme; FPS için en az saniyede 1).
2. Şu an çalan medyayı (başlık, sanatçı, kapak, ilerleme, kaynak uygulama) göstermek ve telefondan kontrol etmek.
3. **Sağa/sola kaydırılarak gezilen sayfalar.**
4. **Düzenlenebilir ekran:** kullanıcı kutucuk ekler, siler, sürükleyerek taşır, boyutunu (S/M/L) değiştirir; sayfa ekleyip silebilir.
5. **Büyük, okunaklı yazılar**, koyu tema, yatay kullanım.
6. FIFA 23 (EA AntiCheat) ile sorunsuz bir arada çalışabilmek.

## 3. Hedef dışı (şimdilik)

- iPhone desteği
- İnternet üzerinden uzaktan erişim (yalnızca aynı yerel ağ)
- Birden fazla bilgisayar / birden fazla telefon yönetimi
- Bilgisayarda ayrı bir ayar penceresi (ayarlar telefondan yapılır; bilgisayarda yalnızca simge menüsü)
- Native Android uygulaması (ileride olabilir, mimari buna izin vermeli)

## 4. Karşıtı hedefler (çalışsa bile başarısızlık sayılır)

- Oyunda hissedilir FPS düşüşü / takılma yaratmak.
- Hile koruma sistemine takılıp hesap riski oluşturmak.
- Küçük, okunmayan yazılar; oyun sırasında telefona eğilip bakmayı gerektirmek.
- Aynı ağdaki başka birinin ekranı görebilmesi / müziği kontrol edebilmesi.
- Windows açılışında kendiliğinden başlaması (**kullanıcı açıkça istemedi**).

## 5. Kararlaştırılanlar

### Kullanıcının kararları
- Telefon: **Android**.
- Ekran tasarımı: Görsel taslakta onaylanan 3 sayfa + düzenleme modu (bkz. §7).
- FPS ekranda olacak.
- Ekran düzenlenebilir olacak, parmak hareketiyle sayfalar arası geçilecek, yazılar büyük olacak.
- Program **Windows ile otomatik başlamayacak**; kullanıcı elle açacak.
- Hile korumalı oynanan oyun: **FIFA 23**. Diğer oyunlarda böyle bir koruma yok.

### Hedef donanım (bilgisayardan okundu)
- **Dizüstü bilgisayar**: Intel Core i7-10750H, NVIDIA GeForce GTX 1650 Ti + Intel UHD (Optimus, çift GPU).
- Kapsam: **yalnızca NVIDIA + Intel**. AMD GPU (ADLX) yolu kapsam dışı; LHM'nin sunduğu kadarıyla "en iyi çaba".
- Dizüstünde fan hızları çoğu zaman gömülü denetleyiciden (EC) okunamaz → fan kutucuğu "en iyi çaba"; okunamazsa listede görünmez.
- Optimus: GPU kutucukları GTX 1650 Ti'yi gösterir. GPU uykudayken (oyun yokken) değerler "uyku" olarak gösterilir ve okuma yüzünden GPU'nun uyandırılmamasına dikkat edilir (pil/ısı).

### Teknoloji seçimleri (kullanıcı seçimi Claude'a bıraktı — Claude'un kararı)
- **Bilgisayar tarafı:** C# / .NET 10 (LTS; .NET 8'in desteği Kasım 2026'da bittiği için plan aşamasında 10'a çekildi), tek bir Windows uygulaması (sistem tepsisinde ikon).
  - Donanım sensörleri: **LibreHardwareMonitorLib** (güncel sürüm, imzalı PawnIO sürücüsünü kullanan).
  - GPU: NVIDIA için NVML / AMD için ADLX üzerinden (LHM bunları zaten sarmalıyor; sürücüsüz okunabilen kısım bu yol).
  - CPU/RAM kullanımı: Windows performans sayaçları.
  - FPS: **PresentMon** (ETW tabanlı, kernel sürücüsü kurmaz). Ya PresentMon konsol aracı alt süreç olarak çalıştırılır ya da PresentMon servis API'si kullanılır — seçim builder'a ait.
  - Medya: Windows **GlobalSystemMediaTransportControlsSessionManager** (WinRT). Spotify uygulaması ve Chrome/Edge'de açık YouTube Music buraya kayıt oluyor. Kapak resmi de buradan alınır.
  - Sunucu: ASP.NET Core (Kestrel) — telefona web sayfasını sunar ve **WebSocket** ile canlı veri iter; telefondan gelen komutları (oynat/durdur vb.) aynı kanaldan alır.
- **Telefon tarafı:** Bilgisayarın sunduğu, Chrome'da açılan bir **PWA** (ana ekrana eklenince tam ekran açılır). Çerçeve seçimi builder'a ait; hafif bir şey tercih edilmeli.
- **Bağlantı:** Aynı Wi-Fi/yerel ağ, düz HTTP + WebSocket.

## 6. Sistem yapısı

```
[Windows PC]                                         [Android telefon]
 Tepsi uygulaması (.NET 8)                            Chrome / PWA
  ├─ Sensör toplayıcı (LHM + perf sayaçları)          ├─ Sayfa/kutucuk görünümü
  ├─ FPS toplayıcı (PresentMon)                       ├─ Kaydırma ile sayfa geçişi
  ├─ Medya izleyici + kontrol (WinRT SMTC)            ├─ Düzenleme modu
  ├─ Düzen deposu (JSON dosyası, PC'de)               └─ WebSocket istemcisi
  ├─ Eşleştirme / yetki
  └─ HTTP + WebSocket sunucusu  ◄──── yerel ağ ────►
```

- Toplayıcılar birbirinden bağımsız çalışır; biri hata verirse (ör. sürücü yüklenemedi) diğerleri devam eder ve o kutucuklarda "—" görünür.
- Sunucu tüm verileri tek bir anlık görüntü (snapshot) mesajında birleştirip ~500 ms'de bir yayınlar. Medya değişikliği (şarkı değişti, durdu) anında ayrı mesajla gönderilir.
- Kapak resmi ayrı bir HTTP adresinden sunulur (WebSocket'e gömülmez).

## 7. Telefon ekranı

Yatay kullanım, koyu tema. Onaylanan taslak `.superpowers/brainstorm/962-1794338519/content/sayfalar.html` dosyasında.

**Varsayılan sayfalar:**
1. **Oyun:** CPU sıcaklık+kullanım, GPU sıcaklık+kullanım, büyük FPS kutucuğu (1% low ile), RAM, VRAM; altta ince şarkı şeridi (kapak küçük, başlık · sanatçı, ⏮ ⏯ ⏭).
2. **Müzik:** büyük kapak, kaynak uygulama adı, başlık, sanatçı, ilerleme çubuğu ve süreler, büyük dokunmatik tuşlar (⏮ ⏯ ⏭ ses).
3. **Detay:** CPU saat hızı, GPU güç, fan RPM, disk sıcaklığı, ağ indirme hızı, ping, CPU son 60 sn mini grafik.

**Davranışlar:**
- Sayfalar arası sağa/sola kaydırma; altta sayfa noktaları.
- **Ses tuşu (Claude'un kararı):** Windows ana sesini değil, **yalnızca çalan uygulamanın sesini** değiştirir (Windows ses oturumları / Core Audio, medya oturumunun süreç adıyla eşleştirilir). Oyunun sesi asla etkilenmez. Bilinen sınır: YouTube Music tarayıcıda çalıyorsa tüm Chrome'un sesi değişir.
- Değer renkleri eşiklere göre: yeşil → turuncu → kırmızı (sıcaklıklar için varsayılan eşikler builder'ın kararı, kutucuk ayarından değiştirilebilir).
- **Düzenleme modu:** ekrana uzun basınca açılır. Kutucuklar kesik çizgili olur; sürükle-bırak ile taşınır, ✕ ile silinir, "＋ Kutucuk ekle" ile listeden yeni ölçüm eklenir, S/M/L boyut seçilir, sayfa eklenip silinebilir. "Bitti" ile çıkılır ve düzen PC'ye kaydedilir.
- Minimum yazı boyutu: değerler en küçük kutucukta bile kolay okunur olmalı (S boyutu kol mesafesinden okunmalı).
- Bağlantı koparsa ekranın üstünde belirgin bir "Bağlantı yok — yeniden deneniyor" uyarısı; otomatik yeniden bağlanma.

**Kutucuk olarak eklenebilen ölçümler (ilk sürüm):** CPU sıcaklık / kullanım / saat, GPU sıcaklık / kullanım / güç / saat, VRAM, RAM, FPS, 1% low FPS, fan RPM'leri, disk sıcaklıkları, ağ indirme/yükleme, ping (sabit bir hedefe, ör. 1.1.1.1 — builder'ın kararı), şarkı şeridi, mini grafik (seçilen bir ölçüm için).

## 8. Hile koruma (FIFA 23 / EA AntiCheat) stratejisi

Ölçümler iki gruba ayrılır:

- **Sürücüsüz (her zaman açık):** CPU/RAM kullanımı, GPU değerleri (NVML/ADLX), FPS (PresentMon/ETW), medya.
- **Sürücü gerektiren:** CPU sıcaklığı, fanlar, anakart/disk sensörleri (LHM'nin PawnIO sürücüsü). Not: disk sıcaklığı (SMART) sürücüsüz okunabiliyorsa builder onu sürücüsüz gruba alabilir.

**FIFA modu (kullanıcının kararı):** Hile koruma sistemleri sürücünün *okunmasına* değil *yüklü olmasına* tepki verebildiği ve EA AntiCheat oyunla birlikte başladığı için, oyun açıldıktan sonra kapatmak yeterli sayılmaz. Bu yüzden:

- Kullanıcı FIFA oynayacaksa programı **FIFA modunda başlatır** (ayrı bir kısayol veya başlangıç seçeneği, ör. `--fifa`). Bu modda sensör sürücüsü **hiç yüklenmez**, LHM'nin sürücü gerektiren bileşenleri hiç açılmaz.
- Program normal moddayken sürücüyü yüklemişse, **program kapanınca sürücü/servis de kaldırılır veya durdurulur.** Sistemde kalıcı bir sürücü bırakılmaz. Böylece "önce programı kapat, sonra FIFA modunda aç" akışı temiz çalışır.
- Normal moddayken FIFA 23'ün exe'si açılırsa telefonda ve tepside belirgin bir uyarı gösterilir: "FIFA açıldı — programı FIFA modunda yeniden başlat". Otomatik bir işlem yapılmaz.
- FIFA modunda sürücü gerektiren kutucuklarda "—" ve küçük bir kalkan ikonu görünür. GPU değerleri, FPS, kullanım oranları ve müzik normal çalışır.

**Erken doğrulama:** Uygulama planının ilk aşamalarında, FIFA modunda FIFA 23 açılarak sürücünün gerçekten yüklü olmadığı (servis listesi ve sürücü listesiyle) doğrulanır. PawnIO'nun nasıl kurulup kaldırılacağı builder'ın kararı; tek şart, FIFA modunda ve program kapalıyken sistemde yüklü olmaması.

## 9. Güvenlik ve eşleştirme (Claude'un kararı)

- Telefon ilk bağlandığında PC'de (tepsi bildirimi/menü) **4 haneli eşleştirme kodu** görünür; telefonda girilir.
- Başarılı eşleştirmede telefona uzun ömürlü bir belirteç (token) verilir, tarayıcıda saklanır; sonraki bağlantılarda kod sorulmaz.
- Tepsi menüsünden "eşleşmiş cihazları unut" seçeneği.
- Sunucu yalnızca yerel ağ adreslerinden gelen bağlantıları kabul eder.

## 10. Bilgisayar uygulaması davranışı

- **Elle başlatılır**, Windows ile otomatik başlamaz.
- Sensör sürücüsü ve PresentMon için **yönetici yetkisi** gerekir; uygulama açılışta yetki ister (UAC).
- İki başlatma kısayolu: **"Telefon Paneli"** (normal) ve **"Telefon Paneli (FIFA modu)"**. Tepside hangi modda çalıştığı görünür.
- Sistem tepsisinde ikon. Menü: telefon adresi (+ QR kod gösterme), eşleştirme kodu, mod bilgisi ve "FIFA modunda yeniden başlat", eşleşmiş cihazları unut, çıkış.
- Telefonun bağlanacağı adresi kolay bulmak için tepsi menüsünde QR kod gösterilir (Claude'un kararı).
- Windows Güvenlik Duvarı için ilk açılışta gerekli kural eklenir veya kullanıcıya nasıl ekleneceği gösterilir — builder'ın kararı.
- Düzen ve ayarlar PC'de bir JSON dosyasında (`%AppData%`) saklanır; telefon sıfırlansa da düzen kaybolmaz.
- Kaynak kullanımı hedefi: boşta ve oyun sırasında ortalama < %1 CPU, < 150 MB RAM (yaklaşık hedef, builder ölçer).

## 11. Telefon tarafı ek davranışlar

- Ekranın kapanmaması: HTTP üzerinden tarayıcı "Wake Lock" çalışmayabileceği için kullanıcıya Android Geliştirici Seçenekleri → **"Uyanık kal" (şarjdayken)** ayarı önerilir; ek olarak tarayıcı izin verirse Wake Lock denenir.
- Yatay yön kilidi (PWA manifest).

## 12. Builder'a bırakılanlar

- Proje/klasör yapısı, test stratejisi, telefon arayüzü için çerçeve/kütüphane seçimi.
- WebSocket mesaj formatının detayları.
- PresentMon entegrasyon yöntemi.
- Renk eşiklerinin varsayılan değerleri, tam görsel ölçüler (taslaktaki görünüme sadık kalınarak).
- Kurulum/paketleme biçimi (tek exe, zip vb.).

## 13. Riskler

- **EA AntiCheat:** sürücüye tepkisi bilinmiyor → §8 FIFA modu (sürücü hiç yüklenmez) + erken doğrulama.
- **Tarayıcıdaki YouTube Music:** Chrome medya oturumu bazen sekme başlığını eksik verebilir; kapak resmi gelmeyebilir → yer tutucu kapak gösterilir.
- **FPS:** PresentMon yönetici yetkisi ister; bazı oyunlarda (ör. tam ekran özel mod) doğru süreç seçimi gerekir → ön plandaki pencerenin süreci izlenir.
