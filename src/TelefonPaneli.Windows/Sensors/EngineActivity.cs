namespace TelefonPaneli.Windows.Sensors;

/// <summary>
/// "GPU Engine\Utilization Percentage" Timer100Ns türünde birikimli bir sayaçtır: ham değer toplam meşgul süredir.
/// Motor ancak değeri bir önceki okumadan bu yana arttıysa meşguldür.
/// </summary>
public sealed class EngineActivity(string luidToken)
{
    Dictionary<string, long> _previous = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Bu adaptörün herhangi bir motoru son okumadan bu yana çalıştıysa true.</summary>
    public bool Update(IEnumerable<(string Instance, long Raw)> samples)
    {
        var current = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var busy = false;
        foreach (var (instance, raw) in samples)
        {
            if (!instance.Contains(luidToken, StringComparison.OrdinalIgnoreCase)) continue;
            current[instance] = raw;
            if (_previous.TryGetValue(instance, out var before) && raw > before) busy = true;
        }
        _previous = current;
        return busy;
    }
}
