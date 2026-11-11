using Microsoft.AspNetCore.Builder;
using TelefonPaneli.App.Server;
using TelefonPaneli.Core.Media;
using TelefonPaneli.Core.Metrics;
using TelefonPaneli.Core.Security;
using TelefonPaneli.Core.Settings;

// Sahte verilerle sunucu: dotnet run --project tools/DemoServer [-- port] [--fifa]
var port = args.Select(a => int.TryParse(a, out var p) ? p : 0).FirstOrDefault(p => p > 0);
if (port == 0) port = 5051;
var fifa = args.Contains("--fifa");

var dir = Path.Combine(Path.GetTempPath(), "TelefonPaneliDemo");
var store = new SettingsStore(Path.Combine(dir, "settings.json"));
var pairing = new PairingService(store, () => 1234);
var media = new DemoMedia();
var hub = new MetricHub([new DemoSource(fifa)], () => new HubFlags(fifa ? "fifa" : "normal", false, false));

var app = WebServer.Build(new WebServerOptions(port, hub, store, pairing, media, TimeSpan.FromMilliseconds(500), EnforceLocalNetwork: true));
Console.WriteLine($"Demo: http://localhost:{port}  (eşleştirme kodu: 1234)");
await app.RunAsync();

sealed class DemoSource(bool fifa) : IMetricSource
{
    readonly Random _r = new(1);
    double _cpu = 62, _gpu = 74, _fps = 140, _load = 34;
    public string Name => "demo";
    public void Collect(IDictionary<string, double?> into)
    {
        double Walk(ref double v, double step, double min, double max) => v = Math.Clamp(v + (_r.NextDouble() - 0.5) * step, min, max);
        into["cpu.temp"] = fifa ? null : Walk(ref _cpu, 3, 45, 95);
        into["cpu.load"] = Walk(ref _load, 8, 5, 100);
        into["cpu.clock"] = fifa ? null : 4200 + _r.Next(700);
        into["gpu.temp"] = Walk(ref _gpu, 2, 40, 90);
        into["gpu.load"] = 90 + _r.Next(10);
        into["gpu.power"] = 48 + _r.Next(10);
        into["gpu.clock"] = 1590;
        into["gpu.vram.used"] = 3.1;
        into["gpu.vram.total"] = 4;
        into["ram.used"] = 11.2;
        into["ram.total"] = 16;
        into["fps"] = Math.Round(Walk(ref _fps, 10, 90, 165));
        into["fps.low1"] = Math.Round(_fps * 0.7);
        into["fan.0"] = fifa ? null : 3200 + _r.Next(300);
        into["disk.0.temp"] = fifa ? null : 41;
        into["net.down"] = _r.NextDouble() * 15;
        into["net.up"] = _r.NextDouble() * 2;
        into["ping"] = 15 + _r.Next(10);
    }
}

sealed class DemoMedia : IMediaController
{
    bool _playing = true;
    int _track;
    double _volume = 0.6;
    DateTime _started = DateTime.UtcNow;
    static readonly (string T, string A, long D)[] Tracks = [("Gece Yolu", "Demo Sanatçı", 200_000), ("Uzun Bir Parça Adı Burada Uzuyor Da Uzuyor", "Başka Biri", 245_000)];
    public event Action? Changed;
    public MediaState? Current => new("Spotify", Tracks[_track].T, Tracks[_track].A, _playing,
        (long)(DateTime.UtcNow - _started).TotalMilliseconds % Tracks[_track].D, Tracks[_track].D, _track, _volume);
    public Task<byte[]?> GetArtAsync() => Task.FromResult<byte[]?>(DemoArt.Png(_track));
    public Task SendAsync(MediaAction a)
    {
        if (a == MediaAction.PlayPause) _playing = !_playing;
        else { _track = (_track + 1) % Tracks.Length; _started = DateTime.UtcNow; }
        Changed?.Invoke();
        return Task.CompletedTask;
    }
    public Task SetVolumeAsync(double v) { _volume = v; Changed?.Invoke(); return Task.CompletedTask; }
}

static class DemoArt
{
    // Demo için çizilmiş kapak: degrade zemin ve nota işareti.
    public static byte[] Png(int track)
    {
        using var bmp = new System.Drawing.Bitmap(300, 300);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var (a, b) = track == 0
                ? (System.Drawing.Color.FromArgb(29, 185, 84), System.Drawing.Color.FromArgb(10, 60, 40))
                : (System.Drawing.Color.FromArgb(124, 58, 237), System.Drawing.Color.FromArgb(30, 20, 70));
            using var bg = new System.Drawing.Drawing2D.LinearGradientBrush(new System.Drawing.Rectangle(0, 0, 300, 300), a, b, 45f);
            g.FillRectangle(bg, 0, 0, 300, 300);
            using var font = new System.Drawing.Font("Segoe UI Symbol", 120, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            using var fg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(220, 255, 255, 255));
            var size = g.MeasureString("♪", font);
            g.DrawString("♪", font, fg, (300 - size.Width) / 2, (300 - size.Height) / 2);
        }
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }
}
