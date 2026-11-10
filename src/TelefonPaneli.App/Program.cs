using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using TelefonPaneli.App.Server;
using TelefonPaneli.App.Setup;
using TelefonPaneli.App.Tray;
using TelefonPaneli.Core.Metrics;
using TelefonPaneli.Core.Security;
using TelefonPaneli.Core.Settings;
using TelefonPaneli.Windows.Driver;
using TelefonPaneli.Windows.Fps;
using TelefonPaneli.Windows.Games;
using TelefonPaneli.Windows.Media;
using TelefonPaneli.Windows.Sensors;

namespace TelefonPaneli.App;

public static class Program
{
    public const int Port = 5050;
    const string MutexName = "Global\\TelefonPaneli";
    const string ShowEventName = "Global\\TelefonPaneli.Show";

    [STAThread]
    public static void Main(string[] rawArgs)
    {
        var args = AppArgs.Parse(rawArgs);
        WaitForPreviousInstance(args.AfterPid);

        using var mutex = new Mutex(true, MutexName, out var first);
        if (!first)
        {
            // Zaten çalışıyor: mevcut örneğe QR penceresini açtır.
            try { using var ev = EventWaitHandle.OpenExisting(ShowEventName); ev.Set(); } catch (Exception) { }
            return;
        }

        ApplicationConfiguration.Initialize();
        var logPath = Path.Combine(Path.GetDirectoryName(SettingsStore.DefaultPath())!, "hata.log");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => TryLog(logPath, e.ExceptionObject?.ToString());
        Application.ThreadException += (_, e) => TryLog(logPath, e.Exception.ToString());

        var mode = args.Fifa ? "fifa" : "normal";
        var store = new SettingsStore(SettingsStore.DefaultPath());
        var settings = store.Load();
        var pairing = new PairingService(store);

        Firewall.EnsureRule(Port);
        var driver = new PawnIoDriver(new WindowsServiceControl());
        var driverStatus = driver.Prepare(args.Fifa);

        var watcher = GameWatcher.ForWindows(settings.FifaProcessNames);
        watcher.Poll();
        using var watchTimer = new System.Threading.Timer(_ => watcher.Poll(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

        using var gate = new GpuActivityGate();
        var disposables = new List<IDisposable>();
        var sources = new List<IMetricSource>();
        T Add<T>(T s) where T : IMetricSource { sources.Add(s); if (s is IDisposable d) disposables.Add(d); return s; }

        Add(new PerfCounterSource());
        Add(new NvmlSource(gate));
        Add(new PingSource());
        var presentMon = Add(new PresentMonSource(Path.Combine(AppContext.BaseDirectory, "tools", "presentmon", "PresentMon.exe"), watcher));
        if (!args.Fifa && driverStatus == DriverStatus.Ready)
        {
            // FIFA modunda bu dal hiç çalışmaz: LibreHardwareMonitor ve PawnIO hiç açılmaz.
            try { Add(new LhmSource()); }
            catch (Exception ex) { TryLog(logPath, "LHM açılamadı: " + ex); }
        }

        var hub = new MetricHub(sources, () => new HubFlags(
            mode,
            GpuAsleep: gate.HasNvidia && !gate.IsAwake,
            FifaWarning: !args.Fifa && watcher.FifaRunning,
            DriverStopFailed: driverStatus == DriverStatus.FifaStopFailed));

        var media = SmtcMediaSource.CreateAsync(new AppVolume()).GetAwaiter().GetResult();
        disposables.Add(media);

        var web = WebServer.Build(new WebServerOptions(Port, hub, store, pairing, media, TimeSpan.FromMilliseconds(500), EnforceLocalNetwork: true));
        try
        {
            web.StartAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            TryLog(logPath, ex.ToString());
            MessageBox.Show($"Sunucu başlatılamadı (port {Port} kullanımda olabilir).\n\n{ex.Message}", "Telefon Paneli",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Cleanup();
            return;
        }

        var url = $"http://{LanAddress.Current()}:{Port}";
        var restart = false;
        TrayApp? tray = null;
        tray = new TrayApp(new TrayOptions(url, args.Fifa, driverStatus, presentMon.ExeFound, pairing,
            () => watcher.FifaRunning,
            () => { restart = true; tray!.ExitThread(); }));

        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var showWait = ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) => tray.ShowQrFromAnyThread(), null, -1, false);

        Application.Run(tray);

        showWait.Unregister(null);
        web.StopAsync().GetAwaiter().GetResult();
        Cleanup();

        if (restart)
        {
            mutex.ReleaseMutex();
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!, AppArgs.RestartArguments(!args.Fifa, Environment.ProcessId))
            {
                UseShellExecute = true,
            });
        }

        void Cleanup()
        {
            foreach (var d in disposables) { try { d.Dispose(); } catch (Exception) { } }
            driver.Shutdown();
        }
    }

    static void WaitForPreviousInstance(int? pid)
    {
        if (pid is not { } p) return;
        try { using var old = Process.GetProcessById(p); old.WaitForExit(15000); }
        catch (Exception) { }
    }

    static void TryLog(string path, string? text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"[{DateTimeOffset.Now:u}] {text}{Environment.NewLine}");
        }
        catch (Exception) { }
    }
}
