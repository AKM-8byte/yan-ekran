using System.Net.NetworkInformation;
using TelefonPaneli.Core.Metrics;

namespace TelefonPaneli.Windows.Sensors;

/// <summary>Arka planda düzenli ping atar; zaman aşımı veya hata → null.</summary>
public sealed class PingSource : IMetricSource, IDisposable
{
    readonly CancellationTokenSource _cts = new();
    double? _last;

    public PingSource(string host = "1.1.1.1", TimeSpan? interval = null)
    {
        var every = interval ?? TimeSpan.FromSeconds(2);
        _ = Task.Run(async () =>
        {
            using var ping = new Ping();
            using var timer = new PeriodicTimer(every);
            do
            {
                try
                {
                    var r = await ping.SendPingAsync(host, TimeSpan.FromSeconds(1), cancellationToken: _cts.Token);
                    _last = r.Status == IPStatus.Success ? r.RoundtripTime : null;
                }
                catch (OperationCanceledException) { return; }
                catch (Exception) { _last = null; }
            } while (await timer.WaitForNextTickAsync(_cts.Token).AsTask().ContinueWith(t => !t.IsCanceled && t.Result));
        });
    }

    public string Name => "ping";

    public void Collect(IDictionary<string, double?> into) => into[MetricIds.Ping] = _last;

    public void Dispose() => _cts.Cancel();
}
