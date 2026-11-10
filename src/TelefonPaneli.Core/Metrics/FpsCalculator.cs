namespace TelefonPaneli.Core.Metrics;

/// <summary>
/// Kare süresi (ms) akışından FPS ve "1% low" hesaplar.
/// Fps: son 1 sn içindeki kareler / toplam süre. Low1: son 10 sn'deki en yavaş %1 karenin ortalamasından.
/// 2 sn hiç kare gelmezse ikisi de null.
/// </summary>
public sealed class FpsCalculator(TimeProvider time)
{
    static readonly TimeSpan FpsWindow = TimeSpan.FromSeconds(1);
    static readonly TimeSpan LowWindow = TimeSpan.FromSeconds(10);
    static readonly TimeSpan Stale = TimeSpan.FromSeconds(2);

    readonly Queue<(DateTimeOffset At, double Ms)> _frames = new();
    readonly object _gate = new();

    public void AddFrame(double msBetweenPresents)
    {
        if (!double.IsFinite(msBetweenPresents) || msBetweenPresents <= 0) return;
        lock (_gate)
        {
            var now = time.GetUtcNow();
            _frames.Enqueue((now, msBetweenPresents));
            Trim(now);
        }
    }

    public void Reset()
    {
        lock (_gate) _frames.Clear();
    }

    public double? Fps
    {
        get
        {
            lock (_gate)
            {
                if (!Fresh(out var now)) return null;
                var cutoff = now - FpsWindow;
                int count = 0; double sum = 0;
                foreach (var f in _frames)
                    if (f.At > cutoff) { count++; sum += f.Ms; }
                return count == 0 || sum <= 0 ? null : count * 1000.0 / sum;
            }
        }
    }

    public double? Low1
    {
        get
        {
            lock (_gate)
            {
                if (!Fresh(out var now)) return null;
                Trim(now);
                var worst = _frames.Select(f => f.Ms).OrderByDescending(x => x).ToList();
                if (worst.Count == 0) return null;
                var n = Math.Max(1, (int)Math.Ceiling(worst.Count * 0.01));
                return 1000.0 / worst.Take(n).Average();
            }
        }
    }

    bool Fresh(out DateTimeOffset now)
    {
        now = time.GetUtcNow();
        return _frames.Count > 0 && now - _frames.Last().At <= Stale;
    }

    void Trim(DateTimeOffset now)
    {
        while (_frames.Count > 0 && now - _frames.Peek().At > LowWindow) _frames.Dequeue();
    }
}
