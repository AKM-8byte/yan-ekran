namespace TelefonPaneli.Core.Metrics;

/// <summary>Bir veri kaynağı. Her çağrıda kendi metriklerini yazar; okunamayan değere null yazar.</summary>
public interface IMetricSource
{
    string Name { get; }
    void Collect(IDictionary<string, double?> into);
}
