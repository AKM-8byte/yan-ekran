using LibreHardwareMonitor.Hardware;
using TelefonPaneli.Core.Metrics;

namespace TelefonPaneli.Windows.Sensors;

/// <summary>
/// PawnIO sürücüsü gerektiren sensörler (CPU sıcaklığı/saati, fanlar, disk sıcaklıkları).
/// Yalnızca normal modda oluşturulur; FIFA modunda bu sınıf hiç örneklenmez.
/// GPU burada okunmaz (NvmlSource okur).
/// </summary>
public sealed class LhmSource : IMetricSource, IDisposable
{
    readonly Computer _computer;
    readonly HashSet<string> _seenKeys = new(StringComparer.Ordinal);

    public LhmSource()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsMotherboardEnabled = true,
            IsStorageEnabled = true,
            IsControllerEnabled = true,
            IsGpuEnabled = false,
            IsMemoryEnabled = false,
            IsNetworkEnabled = false,
            IsBatteryEnabled = false,
            IsPsuEnabled = false,
        };
        _computer.Open();
    }

    public string Name => "lhm";

    public void Collect(IDictionary<string, double?> into)
    {
        var values = new Dictionary<string, double?>(StringComparer.Ordinal)
        {
            [MetricIds.CpuTemp] = null,
            [MetricIds.CpuClock] = null,
        };
        int fan = 0, disk = 0;
        var coreClocks = new List<float>();

        foreach (var hw in _computer.Hardware)
        {
            hw.Update();
            foreach (var sub in hw.SubHardware) sub.Update();

            switch (hw.HardwareType)
            {
                case HardwareType.Cpu:
                    var package = Find(hw, SensorType.Temperature, "Package") ?? Find(hw, SensorType.Temperature, "Tctl") ?? Find(hw, SensorType.Temperature, "Core Max");
                    values[MetricIds.CpuTemp] = package?.Value;
                    coreClocks.AddRange(hw.Sensors
                        .Where(s => s.SensorType == SensorType.Clock && s.Name.StartsWith("CPU Core", StringComparison.Ordinal) && s.Value is > 0)
                        .Select(s => s.Value!.Value));
                    break;

                case HardwareType.Storage:
                    var t = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Value is > 0);
                    if (t is not null) values[MetricIds.DiskTemp(disk++)] = t.Value;
                    break;
            }

            foreach (var s in hw.Sensors.Concat(hw.SubHardware.SelectMany(x => x.Sensors)))
                if (s.SensorType == SensorType.Fan && s.Value is > 0)
                    values[MetricIds.Fan(fan++)] = s.Value;
        }

        if (coreClocks.Count > 0) values[MetricIds.CpuClock] = coreClocks.Average();

        // Daha önce görülmüş ama artık okunamayan sensörler null olarak kalır.
        foreach (var k in _seenKeys) values.TryAdd(k, null);
        foreach (var (k, v) in values) { into[k] = v; _seenKeys.Add(k); }
    }

    static ISensor? Find(IHardware hw, SensorType type, string nameContains) =>
        hw.Sensors.FirstOrDefault(s => s.SensorType == type && s.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase) && s.Value is not null);

    public void Dispose() => _computer.Close();
}
