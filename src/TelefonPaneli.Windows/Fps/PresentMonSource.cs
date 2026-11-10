using System.Diagnostics;
using TelefonPaneli.Core.Metrics;
using TelefonPaneli.Windows.Games;

namespace TelefonPaneli.Windows.Fps;

/// <summary>
/// PresentMon CLI'ı alt süreç olarak çalıştırır (ETW tabanlı, sürücü kurmaz) ve ön plandaki
/// pencerenin sürecine ait kare sürelerinden FPS hesaplar.
/// </summary>
public sealed class PresentMonSource : IMetricSource, IDisposable
{
    const string SessionName = "TelefonPaneli";
    static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    readonly string _exePath;
    readonly GameWatcher _watcher;
    readonly FpsCalculator _calc = new(TimeProvider.System);
    readonly CancellationTokenSource _cts = new();
    readonly Dictionary<int, string> _nameCache = new();
    volatile string? _target;
    Process? _process;

    public PresentMonSource(string exePath, GameWatcher watcher)
    {
        _exePath = exePath;
        _watcher = watcher;
        _ = Task.Run(RunAsync);
    }

    public string Name => "presentmon";

    public bool ExeFound => File.Exists(_exePath);

    public void Collect(IDictionary<string, double?> into)
    {
        var target = TargetName(_watcher.ForegroundPid);
        if (!string.Equals(target, _target, StringComparison.OrdinalIgnoreCase))
        {
            _target = target;
            _calc.Reset();
        }
        into[MetricIds.Fps] = _calc.Fps is { } f ? Math.Round(f) : null;
        into[MetricIds.FpsLow1] = _calc.Low1 is { } l ? Math.Round(l) : null;
    }

    string? TargetName(int? pid)
    {
        if (pid is not { } p) return null;
        if (_nameCache.TryGetValue(p, out var cached)) return cached;
        try
        {
            using var proc = Process.GetProcessById(p);
            var name = proc.ProcessName + ".exe";
            if (_nameCache.Count > 256) _nameCache.Clear();
            _nameCache[p] = name;
            return name;
        }
        catch (Exception) { return null; }
    }

    async Task RunAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            if (!File.Exists(_exePath))
            {
                await Delay(RetryDelay);
                continue;
            }
            try
            {
                var psi = new ProcessStartInfo(_exePath,
                    $"--output_stdout --no_console_stats --stop_existing_session --session_name {SessionName} --v1_metrics")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var proc = Process.Start(psi)!;
                _process = proc;
                _ = proc.StandardError.ReadToEndAsync();
                var csv = new PresentMonCsv();
                var headerRead = false;
                string? line;
                while ((line = await proc.StandardOutput.ReadLineAsync(_cts.Token)) is not null)
                {
                    if (!headerRead) { headerRead = csv.TryReadHeader(line); continue; }
                    var target = _target;
                    if (target is null) continue;
                    if (csv.TryParse(line, out var name, out var ms) && name.Equals(target, StringComparison.OrdinalIgnoreCase))
                        _calc.AddFrame(ms);
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception) { }
            finally { Kill(); }
            await Delay(RetryDelay);
        }
    }

    Task Delay(TimeSpan t) => Task.Delay(t, _cts.Token).ContinueWith(_ => { });

    void Kill()
    {
        try { if (_process is { HasExited: false } p) p.Kill(entireProcessTree: true); }
        catch (Exception) { }
        _process = null;
    }

    public void Dispose()
    {
        _cts.Cancel();
        Kill();
    }
}
