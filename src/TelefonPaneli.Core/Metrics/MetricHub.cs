using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TelefonPaneli.Core.Metrics;

/// <summary>Tüm kaynakları toplayıp tek bir anlık görüntü üretir. Bir kaynağın hatası diğerlerini etkilemez.</summary>
public sealed class MetricHub
{
    readonly IReadOnlyList<IMetricSource> _sources;
    readonly Func<HubFlags> _flags;
    readonly ILogger _log;
    readonly Dictionary<IMetricSource, HashSet<string>> _lastKeys = new();
    readonly Dictionary<IMetricSource, DateTimeOffset> _lastErrorLog = new();
    readonly object _gate = new();

    public MetricHub(IEnumerable<IMetricSource> sources, Func<HubFlags> flags, ILogger? log = null)
    {
        _sources = sources.ToList();
        _flags = flags;
        _log = log ?? NullLogger.Instance;
    }

    public Snapshot Collect()
    {
        lock (_gate)
        {
            var all = new Dictionary<string, double?>(StringComparer.Ordinal);
            foreach (var source in _sources)
            {
                var own = new Dictionary<string, double?>(StringComparer.Ordinal);
                try
                {
                    source.Collect(own);
                    _lastKeys[source] = own.Keys.ToHashSet(StringComparer.Ordinal);
                }
                catch (Exception ex)
                {
                    LogThrottled(source, ex);
                    own.Clear();
                    if (_lastKeys.TryGetValue(source, out var keys))
                        foreach (var k in keys) own[k] = null;
                }
                foreach (var (k, v) in own) all[k] = v;
            }
            var f = _flags();
            return new Snapshot(f.Mode, f.GpuAsleep, f.FifaWarning, all);
        }
    }

    void LogThrottled(IMetricSource source, Exception ex)
    {
        var now = DateTimeOffset.UtcNow;
        if (_lastErrorLog.TryGetValue(source, out var last) && now - last < TimeSpan.FromMinutes(1)) return;
        _lastErrorLog[source] = now;
        _log.LogWarning(ex, "Kaynak okunamadı: {Source}", source.Name);
    }
}
